using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Net.Zmq;

/// <summary>
/// Pool of native memory buffers for zero-copy Message creation.
/// Reduces allocation/deallocation overhead by reusing native memory buffers.
/// Thread-safe for concurrent use.
/// </summary>
public sealed class MessagePool
{
    // Bucket sizes: powers of 2 + common sizes like MTU (1500)
    // IMPORTANT: Must be declared before Shared to ensure proper initialization order
    private static readonly int[] BucketSizes = new[]
    {
        16, 32, 64, 128, 256, 512, 1024,
        1500, // MTU size
        2048, 4096, 8192, 16384, 32768, 65536,
        131072, 262144, 524288, 1048576
    };

    private const int MaxBuffersPerBucket = 500;
    private const int MaxPoolableSize = 1048576; // 1MB

    /// <summary>
    /// Shared singleton instance of MessagePool.
    /// </summary>
    public static MessagePool Shared { get; } = new MessagePool();

    // Native memory buffers organized by size bucket
    private readonly ConcurrentBag<nint>[] _buffers;
    private readonly int[] _bucketCounts;

    // Statistics
    private long _totalRents;
    private long _totalReturns;
    private long _poolHits;
    private long _poolMisses;

    /// <summary>
    /// Initializes a new instance of MessagePool.
    /// </summary>
    public MessagePool()
    {
        _buffers = new ConcurrentBag<nint>[BucketSizes.Length];
        _bucketCounts = new int[BucketSizes.Length];

        for (int i = 0; i < _buffers.Length; i++)
        {
            _buffers[i] = new ConcurrentBag<nint>();
        }
    }

    /// <summary>
    /// Rents a Message backed by pooled native memory and copies the provided data into it.
    /// The returned Message will automatically return the buffer to the pool when disposed.
    /// </summary>
    /// <param name="data">The data to copy into the message.</param>
    /// <returns>A Message instance that will automatically return the buffer to the pool on disposal.</returns>
    public Message Rent(ReadOnlySpan<byte> data)
    {
        int size = data.Length;
        int bucketIndex = SelectBucket(size);

        nint nativePtr;
        int actualSize;

        if (bucketIndex == -1 || !TryRentFromBucket(bucketIndex, out nativePtr))
        {
            // Pool miss: allocate new native memory
            actualSize = bucketIndex >= 0 ? BucketSizes[bucketIndex] : size;
            nativePtr = Marshal.AllocHGlobal(actualSize);
            Interlocked.Increment(ref _poolMisses);
        }
        else
        {
            // Pool hit: reuse existing buffer
            actualSize = BucketSizes[bucketIndex];
            Interlocked.Increment(ref _poolHits);
        }

        Interlocked.Increment(ref _totalRents);

        // Copy data to native memory
        unsafe
        {
            var span = new Span<byte>((void*)nativePtr, size);
            data.CopyTo(span);
        }

        // Create zero-copy Message with free callback
        var capturedSize = actualSize;
        var capturedBucketIndex = bucketIndex;

        return new Message(nativePtr, size, ptr =>
        {
            // ZMQ calls this when done with the message
            Return(ptr, capturedSize, capturedBucketIndex);
        });
    }

    /// <summary>
    /// Selects the appropriate bucket index for the given size.
    /// Returns -1 if the size is too large to pool.
    /// </summary>
    private static int SelectBucket(int size)
    {
        if (size > MaxPoolableSize)
            return -1;

        // Find the smallest bucket that can fit the requested size
        for (int i = 0; i < BucketSizes.Length; i++)
        {
            if (BucketSizes[i] >= size)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Attempts to rent a buffer from the specified bucket.
    /// </summary>
    private bool TryRentFromBucket(int bucketIndex, out nint pointer)
    {
        if (_buffers[bucketIndex].TryTake(out pointer))
        {
            Interlocked.Decrement(ref _bucketCounts[bucketIndex]);
            return true;
        }

        pointer = nint.Zero;
        return false;
    }

    /// <summary>
    /// Returns a native memory buffer to the pool or frees it if the pool is full.
    /// Called by the Message free callback when ZMQ is done with the buffer.
    /// </summary>
    private void Return(nint pointer, int size, int bucketIndex)
    {
        if (pointer == nint.Zero)
            return;

        Interlocked.Increment(ref _totalReturns);

        if (bucketIndex == -1)
        {
            // Not poolable: free immediately
            Marshal.FreeHGlobal(pointer);
            return;
        }

        // Check if bucket is full
        int currentCount = Volatile.Read(ref _bucketCounts[bucketIndex]);
        if (currentCount >= MaxBuffersPerBucket)
        {
            // Pool is full: free the buffer
            Marshal.FreeHGlobal(pointer);
            return;
        }

        // Return to pool
        Interlocked.Increment(ref _bucketCounts[bucketIndex]);
        _buffers[bucketIndex].Add(pointer);
    }

    /// <summary>
    /// Gets current pool statistics.
    /// Useful for detecting memory leaks and monitoring pool efficiency.
    /// </summary>
    /// <returns>Current pool statistics.</returns>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            TotalRents = Volatile.Read(ref _totalRents),
            TotalReturns = Volatile.Read(ref _totalReturns),
            PoolHits = Volatile.Read(ref _poolHits),
            PoolMisses = Volatile.Read(ref _poolMisses),
            OutstandingBuffers = Volatile.Read(ref _totalRents) - Volatile.Read(ref _totalReturns)
        };
    }

    /// <summary>
    /// Pre-warms the pool by allocating buffers for specific message sizes.
    /// This helps avoid allocation overhead during benchmarks.
    /// </summary>
    /// <param name="messageSizes">Array of message sizes to pre-warm.</param>
    /// <param name="countPerSize">Number of buffers to allocate per size.</param>
    public void Prewarm(int[] messageSizes, int countPerSize)
    {
        foreach (int size in messageSizes)
        {
            int bucketIndex = SelectBucket(size);
            if (bucketIndex == -1)
                continue;

            int bucketSize = BucketSizes[bucketIndex];
            int currentCount = Volatile.Read(ref _bucketCounts[bucketIndex]);
            int toAllocate = Math.Min(countPerSize, MaxBuffersPerBucket - currentCount);

            for (int i = 0; i < toAllocate; i++)
            {
                nint pointer = Marshal.AllocHGlobal(bucketSize);
                _buffers[bucketIndex].Add(pointer);
                Interlocked.Increment(ref _bucketCounts[bucketIndex]);
            }
        }
    }

    /// <summary>
    /// Clears all pooled buffers and releases their memory.
    /// Use with caution - only call when no Messages from this pool are in use.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _buffers.Length; i++)
        {
            while (_buffers[i].TryTake(out nint pointer))
            {
                Marshal.FreeHGlobal(pointer);
                Interlocked.Decrement(ref _bucketCounts[i]);
            }
        }
    }
}

/// <summary>
/// Statistics about MessagePool usage.
/// </summary>
public struct PoolStatistics
{
    /// <summary>
    /// Total number of Rent() calls.
    /// </summary>
    public long TotalRents { get; init; }

    /// <summary>
    /// Total number of buffers returned to the pool.
    /// </summary>
    public long TotalReturns { get; init; }

    /// <summary>
    /// Number of times a buffer was reused from the pool.
    /// </summary>
    public long PoolHits { get; init; }

    /// <summary>
    /// Number of times a new buffer had to be allocated.
    /// </summary>
    public long PoolMisses { get; init; }

    /// <summary>
    /// Number of buffers currently in use (not yet returned).
    /// Should be 0 at the end of benchmarks to ensure no leaks.
    /// </summary>
    public long OutstandingBuffers { get; init; }

    /// <summary>
    /// Pool hit rate (0.0 to 1.0).
    /// Higher is better - indicates efficient buffer reuse.
    /// </summary>
    public double HitRate => (PoolHits + PoolMisses) > 0
        ? (double)PoolHits / (PoolHits + PoolMisses)
        : 0.0;

    public override string ToString()
    {
        return $"Rents: {TotalRents}, Returns: {TotalReturns}, " +
               $"Hits: {PoolHits}, Misses: {PoolMisses}, " +
               $"Outstanding: {OutstandingBuffers}, HitRate: {HitRate:P2}";
    }
}
