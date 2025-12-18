using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;

namespace Net.Zmq;

/// <summary>
/// Predefined message sizes for MessagePool operations.
/// All sizes are powers of 2 from 16 bytes to 4 MB.
/// </summary>
public enum MessageSize
{
    /// <summary>16 bytes</summary>
    B16 = 16,
    /// <summary>32 bytes</summary>
    B32 = 32,
    /// <summary>64 bytes</summary>
    B64 = 64,
    /// <summary>128 bytes</summary>
    B128 = 128,
    /// <summary>256 bytes</summary>
    B256 = 256,
    /// <summary>512 bytes</summary>
    B512 = 512,
    /// <summary>1 KB (1024 bytes)</summary>
    K1 = 1024,
    /// <summary>2 KB (2048 bytes)</summary>
    K2 = 2048,
    /// <summary>4 KB (4096 bytes)</summary>
    K4 = 4096,
    /// <summary>8 KB (8192 bytes)</summary>
    K8 = 8192,
    /// <summary>16 KB (16384 bytes)</summary>
    K16 = 16384,
    /// <summary>32 KB (32768 bytes)</summary>
    K32 = 32768,
    /// <summary>64 KB (65536 bytes)</summary>
    K64 = 65536,
    /// <summary>128 KB (131072 bytes)</summary>
    K128 = 131072,
    /// <summary>256 KB (262144 bytes)</summary>
    K256 = 262144,
    /// <summary>512 KB (524288 bytes)</summary>
    K512 = 524288,
    /// <summary>1 MB (1048576 bytes)</summary>
    M1 = 1048576,
    /// <summary>2 MB (2097152 bytes)</summary>
    M2 = 2097152,
    /// <summary>4 MB (4194304 bytes)</summary>
    M4 = 4194304
}

/// <summary>
/// Pool of native memory buffers for zero-copy Message creation.
/// Reduces allocation/deallocation overhead by reusing native memory buffers.
/// Thread-safe for concurrent use.
/// </summary>
public sealed class MessagePool
{
    // Bucket sizes: powers of 2 from 16 bytes to 4 MB
    // IMPORTANT: Must be declared before Shared to ensure proper initialization order
    private static readonly int[] BucketSizes =
    [
        16,       // 16 B
        32,       // 32 B
        64,       // 64 B
        128,      // 128 B
        256,      // 256 B
        512,      // 512 B
        1024,     // 1 KB
        2048,     // 2 KB
        4096,     // 4 KB
        8192,     // 8 KB
        16384,    // 16 KB
        32768,    // 32 KB
        65536,    // 64 KB
        131072,   // 128 KB
        262144,   // 256 KB
        524288,   // 512 KB
        1048576,  // 1 MB
        2097152,  // 2 MB
        4194304   // 4 MB
    ];

    // Max buffers per bucket: smaller buffers = more count, larger buffers = fewer count
    // Rationale: Small buffers are cheap (16B-512B), large buffers are expensive (1MB-4MB)
    private static readonly int[] MaxBuffersPerBucket =
    [
        1000,  // 16 B   - very cheap, high count
        1000,  // 32 B   - very cheap, high count
        1000,  // 64 B   - very cheap, high count
        1000,  // 128 B  - very cheap, high count
        1000,  // 256 B  - cheap, high count
        1000,  // 512 B  - cheap, high count
        500,   // 1 KB   - moderate, medium count
        500,   // 2 KB   - moderate, medium count
        500,   // 4 KB   - moderate, medium count
        250,   // 8 KB   - medium cost
        250,   // 16 KB  - medium cost
        250,   // 32 KB  - medium cost
        250,   // 64 KB  - medium cost
        100,   // 128 KB - expensive, low count
        100,   // 256 KB - expensive, low count
        100,   // 512 KB - expensive, low count
        50,    // 1 MB   - very expensive, very low count
        50,    // 2 MB   - very expensive, very low count
        50     // 4 MB   - very expensive, very low count
    ];

    private const int MaxPoolableSize = 4194304; // 4MB

    /// <summary>
    /// Shared singleton instance of MessagePool.
    /// </summary>
    public static MessagePool Shared { get; } = new();

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
    ///
    /// <para>
    /// <strong>IMPORTANT - Automatic Return Behavior:</strong>
    /// <list type="bullet">
    /// <item>If you call socket.Send(message), the buffer is returned when ZMQ finishes transmission via free callback</item>
    /// <item>If you DON'T send the message, the buffer is returned when you Dispose() the message via zmq_msg_close()</item>
    /// <item>You should always use 'using var msg = ...' pattern for automatic disposal</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Unlike ArrayPool, you do NOT need to call Return() manually. The pool uses ZMQ's
    /// free callback mechanism to automatically return buffers at the correct time, ensuring
    /// buffers aren't returned while ZMQ is still using them.
    /// </para>
    ///
    /// <example>
    /// <code>
    /// using var msg = MessagePool.Shared.Rent(data);
    /// socket.Send(msg);
    /// // Buffer automatically returned to pool after ZMQ transmission completes
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="data">The data to copy into the message.</param>
    /// <returns>A Message instance that will automatically return the buffer to the pool.</returns>
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

        // Thread-safe callback wrapper to ensure single execution
        int callbackExecuted = 0;
        return new Message(nativePtr, size, ptr =>
        {
            // Ensure callback executes only once (handles concurrent invocation)
            if (Interlocked.CompareExchange(ref callbackExecuted, 1, 0) == 0)
            {
                Return(ptr, capturedSize, capturedBucketIndex);
            }
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
        if (currentCount >= MaxBuffersPerBucket[bucketIndex])
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
    /// Pre-warms the pool by allocating buffers for a specific message size.
    /// </summary>
    /// <param name="size">Message size to pre-warm.</param>
    /// <param name="count">Number of buffers to allocate.</param>
    public void Prewarm(MessageSize size, int count)
    {
        Prewarm(new[] { (int)size }, count);
    }

    /// <summary>
    /// Pre-warms the pool by allocating buffers for multiple message sizes.
    /// </summary>
    /// <param name="sizes">Message sizes to pre-warm.</param>
    /// <param name="count">Number of buffers to allocate per size.</param>
    public void Prewarm(MessageSize[] sizes, int count)
    {
        Prewarm(sizes.Select(s => (int)s).ToArray(), count);
    }

    /// <summary>
    /// Pre-warms the pool by allocating buffers for specific message sizes.
    /// This helps avoid allocation overhead during benchmarks.
    /// </summary>
    /// <param name="messageSizes">Array of message sizes to pre-warm.</param>
    /// <param name="countPerSize">Number of buffers to allocate per size.</param>
    private void Prewarm(int[] messageSizes, int countPerSize)
    {
        foreach (int size in messageSizes)
        {
            int bucketIndex = SelectBucket(size);
            if (bucketIndex == -1)
                continue;

            int bucketSize = BucketSizes[bucketIndex];
            int currentCount = Volatile.Read(ref _bucketCounts[bucketIndex]);
            int toAllocate = Math.Min(countPerSize, MaxBuffersPerBucket[bucketIndex] - currentCount);

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

    /// <summary>
    /// Sets the maximum number of buffers for a specific bucket size.
    /// This allows runtime configuration of pool limits per size.
    /// </summary>
    /// <param name="size">The message size to configure.</param>
    /// <param name="maxBuffers">Maximum number of buffers to pool for this size. Must be positive.</param>
    /// <exception cref="ArgumentException">If size is not poolable or maxBuffers is not positive.</exception>
    /// <example>
    /// <code>
    /// // Increase buffer count for 1KB messages to 1000
    /// MessagePool.Shared.SetMaxBuffers(MessageSize.K1, 1000);
    ///
    /// // Reduce buffer count for 4MB messages to 25
    /// MessagePool.Shared.SetMaxBuffers(MessageSize.M4, 25);
    /// </code>
    /// </example>
    public void SetMaxBuffers(MessageSize size, int maxBuffers)
    {
        if (maxBuffers <= 0)
            throw new ArgumentException("maxBuffers must be positive", nameof(maxBuffers));

        int bucketIndex = SelectBucket((int)size);
        if (bucketIndex == -1)
            throw new ArgumentException($"Size {size} ({(int)size} bytes) is not poolable (max: {MaxPoolableSize} bytes)", nameof(size));

        MaxBuffersPerBucket[bucketIndex] = maxBuffers;
    }

    /// <summary>
    /// Gets the current maximum number of buffers for a specific bucket size.
    /// </summary>
    /// <param name="size">The message size to query.</param>
    /// <returns>Maximum number of buffers for this size.</returns>
    /// <exception cref="ArgumentException">If size is not poolable.</exception>
    public int GetMaxBuffers(MessageSize size)
    {
        int bucketIndex = SelectBucket((int)size);
        if (bucketIndex == -1)
            throw new ArgumentException($"Size {size} ({(int)size} bytes) is not poolable (max: {MaxPoolableSize} bytes)", nameof(size));

        return MaxBuffersPerBucket[bucketIndex];
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
