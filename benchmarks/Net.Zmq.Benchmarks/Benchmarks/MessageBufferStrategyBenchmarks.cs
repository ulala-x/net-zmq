using BenchmarkDotNet.Attributes;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Compares six message buffer management approaches for ZeroMQ send/recv operations:
/// 1. ByteArray (Baseline): Allocate new byte[] for each message (max GC pressure)
/// 2. ArrayPool: Reuse byte[] from ArrayPool.Shared (min GC pressure)
/// 3. Message: Use Message objects backed by native memory (allocate native)
/// 4. MessageZeroCopy: Use Message with zmq_msg_init_data (true zero-copy)
/// 5. MessagePooled: Send with MessagePool.Shared, receive with new Message
/// 6. MessagePooled_WithReceivePool: Both send and receive use MessagePool
///
/// Scenario: ROUTER-to-ROUTER multipart messaging (identity + body)
/// - Sender creates buffers and sends data
/// - Receiver processes messages and creates output buffers for external delivery
///
/// This benchmark helps determine the optimal message buffer strategy based on:
/// - Performance (throughput/latency)
/// - GC pressure (Gen0/Gen1/Gen2 collections)
/// - Memory efficiency (total allocations)
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class MessageBufferStrategyBenchmarks
{
    [Params(64, 512, 1024, 65536, 131072, 262144)]
    public int MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    private byte[] _recvBuffer = null!; // Fixed buffer for direct recv
    private byte[] _identityBuffer = null!; // Buffer for identity frame

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize buffers
        _recvBuffer = new byte[(int)MessageSize];
        _identityBuffer = new byte[64];

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

        // Prewarm the message pool for benchmarks
        MessagePool.Shared.Prewarm((Net.Zmq.MessageSize)MessageSize, 100);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ctx.Shutdown();
        _router1?.Dispose();
        _router2?.Dispose();
        _ctx.Dispose();
        MessagePool.Shared.Clear();
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
            for (int n = 0; n < MessageCount; n++)
            {
                // Receive identity frame (blocking)
                _router2.Recv(_identityBuffer);
                // Receive data frame (blocking)
                int size = _router2.Recv(_recvBuffer);

                // Simulate external delivery: create new output buffer (GC pressure!)
                var outputBuffer = new byte[size];
                _recvBuffer.AsSpan(0, size).CopyTo(outputBuffer);
                // External consumer would use outputBuffer here
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: create new buffer for each message (GC pressure!)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            var sendBuffer = new byte[(int)MessageSize];
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
            for (int n = 0; n < MessageCount; n++)
            {
                // Receive identity frame (blocking)
                _router2.Recv(_identityBuffer);
                // Receive data frame (blocking)
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
                _router1.Send(sendBuffer.AsSpan(0, (int)MessageSize), SendFlags.DontWait);
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
            for (int n = 0; n < MessageCount; n++)
            {
                // Receive identity frame (blocking)
                _router2.Recv(_identityBuffer);
                // Receive data frame into Message (blocking)
                using var msg = new Message();
                _router2.Recv(msg);
                // Use msg.Data directly (no copy to managed memory)
                // External consumer would use msg.Data here
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: create Message + send (native allocation)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            using var msg = new Message((int)MessageSize);
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
            for (int n = 0; n < MessageCount; n++)
            {
                // Receive identity frame (blocking)
                _router2.Recv(_identityBuffer);
                // Receive data frame into Message (blocking)
                using var msg = new Message();
                _router2.Recv(msg);
                // Use msg.Data directly (no copy to managed memory)
                // External consumer would use msg.Data here
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
    // MessagePooled approach: Reuse native memory from pool
    // ========================================
    /// <summary>
    /// MessagePooled approach: Use MessagePool.Shared for send buffers.
    /// Sender rents Message from pool, receiver uses new Message.
    /// Expected: Low GC pressure, good performance with native memory reuse.
    /// </summary>
    [Benchmark]
    public void MessagePooled_SendRecv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int n = 0; n < MessageCount; n++)
            {
                // Receive identity frame (blocking)
                _router2.Recv(_identityBuffer);
                // Receive data frame into Message (blocking)
                using var msg = new Message();
                _router2.Recv(msg);
                // Use msg.Data directly (no copy to managed memory)
                // External consumer would use msg.Data here
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: rent from MessagePool + send (automatic return via ZMQ callback)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            var msg = MessagePool.Shared.Rent((int)MessageSize);
            _router1.Send(msg, SendFlags.DontWait);
            // Message automatically returns to pool after ZMQ sends it
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    // ========================================
    // MessagePooled with receive pool: Full pooling
    // ========================================
    /// <summary>
    /// MessagePooled with receive pool: Both sender and receiver use MessagePool.
    /// Sender rents Message from pool, receiver also receives into pooled Message.
    /// Expected: Minimal GC pressure, best native memory reuse.
    /// </summary>
    [Benchmark]
    public void MessagePooled_SendRecv_WithReceivePool()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int n = 0; n < MessageCount; n++)
            {
                // Receive identity frame (blocking)
                _router2.Recv(_identityBuffer);
                // Receive data frame into pooled Message (blocking)
                var msg = MessagePool.Shared.Rent((int)MessageSize);
                _router2.Recv(msg, (int)MessageSize);
                // Use msg.Data directly (no copy to managed memory)
                // External consumer would use msg.Data here
                msg.Dispose(); // Returns to pool
            }
            countdown.Signal();
        });
        thread.Start();

        // Sender: rent from MessagePool + send (automatic return via ZMQ callback)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);

            var msg = MessagePool.Shared.Rent((int)MessageSize);
            _router1.Send(msg, SendFlags.DontWait);
            // Message automatically returns to pool after ZMQ sends it
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

}
