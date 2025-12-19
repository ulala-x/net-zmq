using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
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

    // 재사용 가능한 Message 객체 풀
    private readonly ConcurrentStack<Message>[] _pooledMessages;

    // Per-bucket Interlocked counters for O(1) thread-safe maxBuffer enforcement
    private long[] _pooledMessageCounts = new long[19];  // Per-bucket counters
    private long _poolRejects = 0;  // Messages rejected due to maxBuffer

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
        _pooledMessages = new ConcurrentStack<Message>[BucketSizes.Length];

        for (int i = 0; i < _pooledMessages.Length; i++)
        {
            _pooledMessages[i] = new ConcurrentStack<Message>();
        }
    }

    /// <summary>
    /// Unmanaged callback function pointer for zmq_msg_init_data.
    /// Uses UnmanagedCallersOnly for modern .NET P/Invoke.
    /// </summary>
    private static readonly unsafe delegate* unmanaged[Cdecl]<nint, nint, void> FreeCallbackFunctionPointer = &FreeCallbackImpl;

    /// <summary>
    /// ZeroMQ free callback 구현.
    /// data 파라미터는 사용하지 않고, hint에 저장된 GCHandle을 통해 콜백을 실행합니다.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void FreeCallbackImpl(nint data, nint hint)
    {
        if (hint == nint.Zero)
            return;

        try
        {
            var handle = GCHandle.FromIntPtr(hint);
            var callback = handle.Target as Action<nint>;
            callback?.Invoke(nint.Zero);
            // 주의: GCHandle은 여기서 해제하지 않음 (Message가 재사용되므로)
        }
        catch
        {
            // Swallow exceptions in unmanaged callback
        }
    }

    /// <summary>
    /// 풀에서 재사용할 수 있는 Message를 생성합니다.
    /// zmq_msg_t를 버킷 크기로 한 번만 초기화하고, 이후 재사용합니다.
    /// </summary>
    private Message CreatePooledMessage(int bucketSize, int bucketIndex)
    {
        var dataPtr = Marshal.AllocHGlobal(bucketSize);
        var msg = new Message();

        // 콜백 설정
        msg._reusableCallback = (ptr) => ReturnMessageToPool(msg);
        msg._callbackHandle = GCHandle.Alloc(msg._reusableCallback);

        // zmq_msg_t를 버킷 크기로 한 번만 초기화!
        nint ffnPtr;
        unsafe
        {
            ffnPtr = (nint)FreeCallbackFunctionPointer;
        }

        var result = Core.Native.LibZmq.MsgInitDataPtr(
            msg._msgPtr,
            dataPtr,
            (nuint)bucketSize,
            ffnPtr,
            GCHandle.ToIntPtr(msg._callbackHandle));

        if (result != 0)
        {
            msg._callbackHandle.Free();
            Marshal.FreeHGlobal(dataPtr);
            throw new ZmqException();
        }

        msg._isFromPool = true;
        msg._poolBucketIndex = bucketIndex;
        msg._initialized = true;
        msg._poolDataPtr = dataPtr;
        msg._poolActualSize = bucketSize;

        // 새로운 필드들 직접 초기화 (internal 필드이므로 직접 접근 가능)
        // SetActualDataSize를 호출하기 전에 _bufferSize를 먼저 설정해야 함
        msg._bufferSize = bucketSize;
        msg._actualDataSize = bucketSize;  // 초기에는 전체 버킷 크기

        return msg;
    }

    /// <summary>
    /// Message를 풀에 반환합니다.
    /// maxBuffer 제한을 초과하는 경우 실제로 dispose합니다.
    /// </summary>
    private void ReturnMessageToPool(Message msg)
    {
        if (Interlocked.CompareExchange(ref msg._callbackExecuted, 1, 0) == 0)
        {
            int bucketIndex = msg._poolBucketIndex;

            // Check if pool has room using Interlocked counter (O(1), thread-safe)
            if (Interlocked.Read(ref _pooledMessageCounts[bucketIndex]) < MaxBuffersPerBucket[bucketIndex])
            {
                // Pool has room - return message for reuse
                msg.ReturnToPool();
                _pooledMessages[bucketIndex].Push(msg);
                Interlocked.Increment(ref _pooledMessageCounts[bucketIndex]);  // Update counter
                Interlocked.Increment(ref _totalReturns);
            }
            else
            {
                // Pool is full - actually dispose the message
                msg.DisposePooledMessage();
                Interlocked.Increment(ref _totalReturns);
                Interlocked.Increment(ref _poolRejects);
            }
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
        int actualSize = data.Length;

        // 1. 풀에서 재사용 가능한 메시지 가져오기
        var msg = RentReusable(actualSize);

        // 2. 데이터 복사 (버킷 크기 버퍼에 실제 데이터만 복사)
        unsafe
        {
            fixed (byte* srcPtr = data)
            {
                Buffer.MemoryCopy(srcPtr, (void*)msg.DataPtr, actualSize, actualSize);
            }
        }

        // 3. 실제 데이터 크기 설정
        if (msg._isFromPool)
        {
            msg.SetActualDataSize(actualSize);
        }

        return msg;
    }

    /// <summary>
    /// Message를 대여합니다.
    /// Message 객체를 재사용하므로 매우 빠릅니다.
    /// </summary>
    /// <param name="size">요청 크기</param>
    /// <returns>Message</returns>
    public Message Rent(int size)
    {
        return RentReusable(size);
    }

    /// <summary>
    /// 재사용 가능한 Message를 대여합니다 (새로운 방식).
    /// Message 객체를 재사용하므로 매우 빠릅니다.
    /// </summary>
    private Message RentReusable(int size)
    {
        var bucketIndex = GetBucketIndex(size);

        if (bucketIndex == -1)
        {
            // 크기가 너무 큼 - 풀링하지 않고 일회용 Message 생성
            var dataPtr = Marshal.AllocHGlobal(size);
            var msg = new Message(dataPtr, size, ptr => Marshal.FreeHGlobal(ptr));
            Interlocked.Increment(ref _poolMisses);
            Interlocked.Increment(ref _totalRents);
            return msg;
        }

        if (_pooledMessages[bucketIndex].TryPop(out var pooledMsg))
        {
            Interlocked.Decrement(ref _pooledMessageCounts[bucketIndex]);
            pooledMsg.PrepareForReuse();
            Interlocked.Increment(ref _poolHits);
            Interlocked.Increment(ref _totalRents);
            return pooledMsg;
        }

        // 풀에 없으면 새로 생성
        var bucketSize = GetBucketSize(bucketIndex);
        var newMsg = CreatePooledMessage(bucketSize, bucketIndex);
        Interlocked.Increment(ref _poolMisses);
        Interlocked.Increment(ref _totalRents);
        return newMsg;
    }


    /// <summary>
    /// 버킷 인덱스를 반환합니다.
    /// </summary>
    private static int GetBucketIndex(int size)
    {
        return SelectBucket(size);
    }

    /// <summary>
    /// 버킷 크기를 반환합니다.
    /// </summary>
    private static int GetBucketSize(int bucketIndex)
    {
        return BucketSizes[bucketIndex];
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
            PoolRejects = Volatile.Read(ref _poolRejects),
            OutstandingBuffers = Volatile.Read(ref _totalRents) - Volatile.Read(ref _totalReturns)
        };
    }

    /// <summary>
    /// Gets the current number of buffers in each pool bucket.
    /// This shows how many Message objects are actually pooled and ready for reuse.
    /// </summary>
    /// <returns>Dictionary mapping bucket sizes to their current pool counts.</returns>
    public Dictionary<int, int> GetPoolCounts()
    {
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < BucketSizes.Length; i++)
        {
            counts[BucketSizes[i]] = _pooledMessages[i].Count;
        }
        return counts;
    }

    /// <summary>
    /// Pre-warms the pool by allocating buffers for a specific message size.
    /// </summary>
    /// <param name="size">Message size to pre-warm.</param>
    /// <param name="count">Number of buffers to allocate.</param>
    public void Prewarm(MessageSize size, int count)
    {
        Prewarm([(int)size], count);
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
    /// Pre-warms the pool by allocating different numbers of buffers for different message sizes.
    /// This allows fine-grained control over pre-allocation per size.
    /// </summary>
    /// <param name="configuration">Dictionary mapping message sizes to the number of buffers to allocate for each size.</param>
    /// <exception cref="ArgumentNullException">If configuration is null.</exception>
    /// <exception cref="ArgumentException">If any count value is negative.</exception>
    /// <example>
    /// <code>
    /// // Pre-warm with different counts per size
    /// MessagePool.Shared.Prewarm(new Dictionary&lt;MessageSize, int&gt;
    /// {
    ///     { MessageSize.B64, 1000 },    // 1000 buffers for 64-byte messages
    ///     { MessageSize.K1, 500 },      // 500 buffers for 1KB messages
    ///     { MessageSize.K64, 100 }      // 100 buffers for 64KB messages
    /// });
    /// </code>
    /// </example>
    public void Prewarm(Dictionary<MessageSize, int> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Validate all entries first
        foreach (var kvp in configuration)
        {
            if (kvp.Value < 0)
                throw new ArgumentException($"Buffer count must be non-negative for size {kvp.Key}", nameof(configuration));
        }

        // Apply pre-warming for each size
        foreach (var kvp in configuration)
        {
            if (kvp.Value > 0)
            {
                Prewarm([(int)kvp.Key], kvp.Value);
            }
        }
    }

    /// <summary>
    /// Pre-warms the pool by allocating buffers for specific message sizes.
    /// This helps avoid allocation overhead during benchmarks.
    /// </summary>
    /// <param name="messageSizes">Array of message sizes to pre-warm.</param>
    /// <param name="countPerSize">Number of buffers to allocate per size.
    /// Actual allocation will not exceed the bucket's maximum buffer limit.</param>
    private void Prewarm(int[] messageSizes, int countPerSize)
    {
        foreach (int size in messageSizes)
        {
            int bucketIndex = SelectBucket(size);
            if (bucketIndex == -1)
                continue;

            int bucketSize = BucketSizes[bucketIndex];
            long currentCount = Interlocked.Read(ref _pooledMessageCounts[bucketIndex]);
            int toAllocate = Math.Min(countPerSize, MaxBuffersPerBucket[bucketIndex] - (int)currentCount);

            for (int i = 0; i < toAllocate; i++)
            {
                var msg = CreatePooledMessage(bucketSize, bucketIndex);
                _pooledMessages[bucketIndex].Push(msg);
                Interlocked.Increment(ref _pooledMessageCounts[bucketIndex]);
            }
        }
    }

    /// <summary>
    /// Clears all pooled buffers and releases their memory.
    /// Use with caution - only call when no Messages from this pool are in use.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _pooledMessages.Length; i++)
        {
            while (_pooledMessages[i].TryPop(out var msg))
            {
                // GCHandle 해제
                if (msg._callbackHandle.IsAllocated)
                {
                    msg._callbackHandle.Free();
                }

                // 네이티브 메모리 해제
                if (msg._poolDataPtr != nint.Zero)
                {
                    Marshal.FreeHGlobal(msg._poolDataPtr);
                    msg._poolDataPtr = nint.Zero;
                }

                // zmq_msg_t 종료
                if (msg._initialized)
                {
                    Core.Native.LibZmq.MsgClosePtr(msg._msgPtr);
                    msg._initialized = false;
                }
            }

            // Reset counter for this bucket
            Interlocked.Exchange(ref _pooledMessageCounts[i], 0);
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
    /// Sets the maximum number of buffers for multiple bucket sizes at once.
    /// This allows batch configuration of pool limits.
    /// </summary>
    /// <param name="configuration">Dictionary mapping message sizes to their maximum buffer counts.</param>
    /// <exception cref="ArgumentNullException">If configuration is null.</exception>
    /// <exception cref="ArgumentException">If any size is not poolable or any maxBuffers value is not positive.</exception>
    /// <example>
    /// <code>
    /// // Configure multiple sizes at once
    /// MessagePool.Shared.SetMaxBuffers(new Dictionary&lt;MessageSize, int&gt;
    /// {
    ///     { MessageSize.K1, 1000 },
    ///     { MessageSize.K64, 200 },
    ///     { MessageSize.M1, 100 }
    /// });
    /// </code>
    /// </example>
    public void SetMaxBuffers(Dictionary<MessageSize, int> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Validate all entries first before applying any changes
        foreach (var kvp in configuration)
        {
            if (kvp.Value <= 0)
                throw new ArgumentException($"maxBuffers must be positive for size {kvp.Key}", nameof(configuration));

            int bucketIndex = SelectBucket((int)kvp.Key);
            if (bucketIndex == -1)
                throw new ArgumentException($"Size {kvp.Key} ({(int)kvp.Key} bytes) is not poolable (max: {MaxPoolableSize} bytes)", nameof(configuration));
        }

        // Apply changes after validation
        foreach (var kvp in configuration)
        {
            int bucketIndex = SelectBucket((int)kvp.Key);
            MaxBuffersPerBucket[bucketIndex] = kvp.Value;
        }
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
    /// Number of messages rejected due to maxBuffer limit being exceeded.
    /// These messages are disposed instead of being returned to the pool.
    /// </summary>
    public long PoolRejects { get; init; }

    /// <summary>
    /// Number of buffers currently in use (not yet returned).
    /// Should be 0 at the end of benchmarks to ensure no leaks.
    /// </summary>
    public long OutstandingBuffers { get; init; }

    // 하위 호환성을 위한 별칭 속성들
    /// <summary>
    /// Alias for TotalRents (for backward compatibility).
    /// </summary>
    public long Rents => TotalRents;

    /// <summary>
    /// Alias for TotalReturns (for backward compatibility).
    /// </summary>
    public long Returns => TotalReturns;

    /// <summary>
    /// Alias for OutstandingBuffers (for backward compatibility).
    /// </summary>
    public long OutstandingMessages => OutstandingBuffers;

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
               $"Hits: {PoolHits}, Misses: {PoolMisses}, Rejects: {PoolRejects}, " +
               $"Outstanding: {OutstandingBuffers}, HitRate: {HitRate:P2}";
    }
}
