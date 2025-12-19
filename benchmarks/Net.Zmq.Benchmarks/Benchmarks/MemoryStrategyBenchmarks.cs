using BenchmarkDotNet.Attributes;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Compares five memory management approaches for ZeroMQ send/recv operations:
/// 1. ByteArray (Baseline): Allocate new byte[] for each message (max GC pressure)
/// 2. ArrayPool: Reuse byte[] from ArrayPool.Shared (min GC pressure)
/// 3. Message: Use Message objects backed by native memory (copy to native)
/// 4. MessageZeroCopy: Use Message with zmq_msg_init_data (true zero-copy)
/// 5. MessagePooled: Use MessagePool to reuse native memory buffers (pooled zero-copy)
///
/// Scenario: ROUTER-to-ROUTER multipart messaging (identity + body)
/// - Sender creates buffers and sends data
/// - Receiver processes messages and creates output buffers for external delivery
///
/// This benchmark helps determine the optimal memory strategy based on:
/// - Performance (throughput/latency)
/// - GC pressure (Gen0/Gen1/Gen2 collections)
/// - Memory efficiency (total allocations)
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class MemoryStrategyBenchmarks
{
    [Params(MessageSize.B64, MessageSize.B512, MessageSize.K1, MessageSize.K64)]
    public  MessageSize MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    private byte[] _sourceData = null!; // Simulates external input data
    private nint _sourceNativeData  ; // Simulates external input data
    private byte[] _recvBuffer = null!; // Fixed buffer for direct recv
    private byte[] _identityBuffer = null!; // Buffer for identity frame

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize test data
        _sourceNativeData = Marshal.AllocHGlobal((int)MessageSize);
        unsafe
        {
            var sourceSpan = new Span<byte>((void*)_sourceNativeData, (int)MessageSize);
            sourceSpan.Fill((byte)'A');
        }



        _sourceData = new byte[(int)MessageSize];
        _recvBuffer = new byte[(int)MessageSize];
        _identityBuffer = new byte[64];
        Array.Fill(_sourceData, (byte)'A');

        // Create ZeroMQ context
        _ctx = new Context();

        // Setup ROUTER/ROUTER socket pair
        _router1 = CreateSocket(SocketType.Router);
        _router2 = CreateSocket(SocketType.Router);
        _router2Id = "r2"u8.ToArray();
        _router1.SetOption(SocketOption.Routing_Id, "r1"u8.ToArray());
        _router2.SetOption(SocketOption.Routing_Id, _router2Id);
        _router1.Bind("tcp://127.0.0.1:0");
        _router2.Connect(_router1.GetOptionString(SocketOption.Last_Endpoint));

        // Allow connections to establish
        Thread.Sleep(100);

        // Perform initial handshake so routers know each other's identities
        _router2.Send("r1"u8.ToArray(), SendFlags.SendMore);
        _router2.Send("hi"u8.ToArray());
        _router1.Recv(_identityBuffer);
        _router1.Recv(_identityBuffer);

        // Pre-warm MessagePool with buffers for this message size
        // Allocate 400 buffers to ensure 100% hit rate during benchmark
        MessagePool.Shared.SetMaxBuffers(MessageSize,800);
        MessagePool.Shared.Prewarm(MessageSize, 800);
        Console.WriteLine($"Pre-warmed MessagePool with 400 buffers of size {MessageSize}");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Print MessagePool statistics
        var stats = MessagePool.Shared.GetStatistics();
        Console.WriteLine($"MessagePool Statistics: {stats}");

        // Clear the pool to avoid state carryover between benchmark runs
        MessagePool.Shared.Clear();

        _ctx.Shutdown();
        _router1?.Dispose();
        _router2?.Dispose();
        _ctx.Dispose();
    }

    private Socket CreateSocket(SocketType type)
    {
        var socket = new Socket(_ctx, type);
        socket.SetOption(SocketOption.Sndhwm, 0);
        socket.SetOption(SocketOption.Rcvhwm, 0);
        socket.SetOption(SocketOption.Linger, 0);
        return socket;
    }

    // ========================================
    // Baseline: Allocate new byte[] every time
    // ========================================
    /// <summary>
    /// Baseline approach: Create new byte[] for both send and receive buffers.
    /// Maximum GC pressure due to frequent allocations.
    /// Expected: Worst GC stats, moderate performance.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ByteArray_SendRecv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            int n = 0;

            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBuffer);
                int size = _router2.Recv(_recvBuffer);

                // Simulate external delivery: create new output buffer (GC pressure!)
                var outputBuffer = new byte[size];
                _recvBuffer.AsSpan(0, size).CopyTo(outputBuffer);
                // External consumer would use outputBuffer here

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    // Receive into fixed buffer
                    size = _router2.Recv(_recvBuffer);

                    // Simulate external delivery: create new output buffer (GC pressure!)
                    outputBuffer = new byte[size];
                    _recvBuffer.AsSpan(0, size).CopyTo(outputBuffer);
                    // External consumer would use outputBuffer here

                    n++;
                }
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: create new buffer for each message (GC pressure!)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            var sendBuffer = new byte[(int)MessageSize];
            _sourceData.AsSpan(0, (int)MessageSize).CopyTo(sendBuffer);
            _router1.Send(sendBuffer, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    // ========================================
    // ArrayPool approach: Reuse managed memory
    // ========================================
    /// <summary>
    /// ArrayPool approach: Rent and return byte[] from ArrayPool.Shared.
    /// Minimal GC pressure by reusing managed memory buffers.
    /// Expected: Best GC stats, best performance due to reduced allocations.
    /// </summary>
    [Benchmark]
    public void ArrayPool_SendRecv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            int n = 0;

            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBuffer);
                int size = _router2.Recv(_recvBuffer);

                // Simulate external delivery: rent from pool (minimal GC!)
                var outputBuffer = ArrayPool<byte>.Shared.Rent(size);
                try
                {
                    _recvBuffer.AsSpan(0, size).CopyTo(outputBuffer);
                    // External consumer would use outputBuffer[0..size] here
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(outputBuffer);
                }

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    // Receive into fixed buffer
                    size = _router2.Recv(_recvBuffer);

                    // Simulate external delivery: rent from pool (minimal GC!)
                    outputBuffer = ArrayPool<byte>.Shared.Rent(size);
                    try
                    {
                        _recvBuffer.AsSpan(0, size).CopyTo(outputBuffer);
                        // External consumer would use outputBuffer[0..size] here
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(outputBuffer);
                    }

                    n++;
                }
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: rent from pool + send + return (minimal GC!)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            var sendBuffer = ArrayPool<byte>.Shared.Rent((int)MessageSize);
            try
            {
                _sourceData.AsSpan(0, (int) MessageSize).CopyTo(sendBuffer);
                _router1.Send(sendBuffer.AsSpan(0,(int) MessageSize), SendFlags.DontWait);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sendBuffer);
            }
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    // ========================================
    // Message approach: Use native memory
    // ========================================
    /// <summary>
    /// Message approach: Use Message objects backed by libzmq's native memory.
    /// Medium GC pressure (Message wrapper objects) but data in native memory.
    /// Expected: Medium GC stats, potentially good performance with native integration.
    /// </summary>
    [Benchmark]
    public void Message_SendRecv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            int n = 0;

            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBuffer);
                using (var msg = new Message())
                {
                    _router2.Recv(msg);
                    // Use msg.Data directly (no copy to managed memory)
                    // External consumer would use msg.Data here
                }

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    // Receive into Message (native memory allocation)
                    using var msg = new Message();
                    _router2.Recv(msg);
                    // Use msg.Data directly (no copy to managed memory)
                    // External consumer would use msg.Data here

                    n++;
                }
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: create Message + copy data + send
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            using var msg = new Message(_sourceData.AsSpan(0, (int)MessageSize));
            _router1.Send(msg, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    // ========================================
    // MessageZeroCopy approach: True zero-copy with native memory
    // ========================================
    /// <summary>
    /// MessageZeroCopy approach: Use Message with zmq_msg_init_data for true zero-copy.
    /// Allocate native memory, pass pointer to Message, let ZMQ manage it.
    /// Expected: Similar GC stats to Message, potentially better performance by avoiding one copy.
    /// </summary>
    [Benchmark]
    public void MessageZeroCopy_SendRecv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            int n = 0;

            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBuffer);
                using (var msg = new Message())
                {
                    _router2.Recv(msg);
                    // Use msg.Data directly (no copy to managed memory)
                    // External consumer would use msg.Data here
                }

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    // Receive into Message (already zero-copy from ZMQ side)
                    using var msg = new Message();
                    _router2.Recv(msg);
                    // Use msg.Data directly (no copy to managed memory)
                    // External consumer would use msg.Data here

                    n++;
                }
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: allocate native memory + zero-copy Message + send
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            // Allocate native memory
            nint nativePtr = Marshal.AllocHGlobal((int)MessageSize);

            // Copy source data to native memory
            unsafe
            {
                Buffer.MemoryCopy(
                    (void*)_sourceNativeData,
                    (void*)nativePtr,
                    (int)MessageSize,
                    (int)MessageSize
                );
            }

            // Create Message with zero-copy (ZMQ will own this memory)
            using var msg = new Message(nativePtr, (int)MessageSize, ptr =>
            {
                // Free callback - called when ZMQ is done with the message
                Marshal.FreeHGlobal(ptr);
            });

            _router1.Send(msg, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    // ========================================
    // MessagePooled approach: Pooled zero-copy native memory
    // ========================================
    /// <summary>
    /// MessagePooled approach: Use MessagePool to reuse native memory buffers with zero-copy.
    /// Reduces allocation/deallocation overhead by pooling native memory buffers.
    /// Expected: Similar GC stats to MessageZeroCopy, potentially better performance by eliminating repeated alloc/free.
    /// </summary>
    [Benchmark]
    public void MessagePooled_SendRecv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            int n = 0;

            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBuffer);
                using (var msg = new Message())
                {
                    _router2.Recv(msg);
                    // Use msg.Data directly (no copy to managed memory)
                    // External consumer would use msg.Data here
                }

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    // Receive into Message (already zero-copy from ZMQ side)
                    using var msg = new Message();
                    _router2.Recv(msg);
                    // Use msg.Data directly (no copy to managed memory)
                    // External consumer would use msg.Data here

                    n++;
                }
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: use MessagePool to rent pooled native memory + zero-copy Message
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            // Rent from MessagePool (automatically returned via ZMQ free callback after transmission)
            using var msg = MessagePool.Shared.Rent(_sourceData.AsSpan(0, (int)MessageSize));
            _router1.Send(msg, SendFlags.DontWait);

        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    // ========================================
    // MessagePooled with ReceivePool approach: Full pooling for both send and receive
    // ========================================
    /// <summary>
    /// MessagePooled with ReceivePool approach: Use MessagePool for both sending and receiving.
    /// Send: Rent from MessagePool (automatically returned via ZMQ free callback after transmission).
    /// Receive: Use ReceiveWithPool (must manually return to pool after processing).
    /// Expected: Minimal native memory allocations, best performance for both send/recv paths.
    /// </summary>
    [Benchmark]
    public void MessagePooled_SendRecv_WithReceivePool()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            int n = 0;

            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBuffer);

                // Receive: ReceiveWithPool 사용 (Dispose() 시 자동 반환)
                using var msg = _router2.ReceiveWithPool();
                // Use msg.Data here
                // Dispose() 시 자동 반환

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    // Receive into pooled Message
                    using var msg2 = _router2.ReceiveWithPool();
                    // Use msg2.Data here
                    // Dispose() 시 자동 반환

                    n++;
                }
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: use MessagePool to rent pooled native memory + zero-copy Message
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            // Rent from MessagePool (automatically returned via ZMQ free callback after transmission)
            using var msg = MessagePool.Shared.Rent(_sourceData.AsSpan(0, (int)MessageSize));
            _router1.Send(msg, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }
}
