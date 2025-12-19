using BenchmarkDotNet.Attributes;
using Net.Zmq;
using System.Runtime.InteropServices;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Compares Send and Receive methods for ROUTER-to-ROUTER multipart messaging:
///
/// Receive Method Comparison:
/// 1. Recv(Span&lt;byte&gt; buffer) - Reuse native memory buffer allocated once in GlobalSetup
///    - Zero allocations for receive buffer
///    - Minimal GC pressure
///    - Direct copy to native memory
///
/// 2. Recv(Message message) - Create new Message for each receive
///    - Message object allocation (wrapper)
///    - Native memory managed by libzmq
///    - Zero-copy access to data
///
/// Send Method Comparison:
/// 1. Send(ReadOnlySpan&lt;byte&gt;) - Managed memory with pinning overhead
///    - Requires pinning for GC safety
///    - Slight overhead from pin/unpin operations
///
/// 2. Send(nint, int) - Native memory without pinning overhead
///    - Zero pinning overhead
///    - Direct native memory access
///    - Best performance expected
///
/// Scenario: ROUTER-to-ROUTER multipart messaging (identity + body)
/// - Sender sends MessageCount messages of MessageSize bytes
/// - Receiver processes messages using native memory buffer
/// - Blocking receive for first message, then batch receive with DontWait
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class RecvMethodComparisonBenchmarks
{
    [Params(64, 512, 1024, 65536)] public int MessageSize { get; set; }

    [Params(10000)] public int MessageCount { get; set; }

    // Reusable buffers (allocated once in GlobalSetup)
    private nint _sendDataPtr; // Native memory for send data
    private nint _recvBufferPtr; // Native memory for Span receive
    private nint _identityBufferPtr; // Native memory for identity frame

    // Socket infrastructure
    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Allocate native memory for send and receive buffers
        _sendDataPtr = Marshal.AllocHGlobal(MessageSize);
        _recvBufferPtr = Marshal.AllocHGlobal(MessageSize);
        _identityBufferPtr = Marshal.AllocHGlobal(64);

        // Fill send buffer with test data
        unsafe
        {
            var sendSpan = new Span<byte>((void*)_sendDataPtr, MessageSize);
            sendSpan.Fill((byte)'A');
        }

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

        unsafe
        {
            var identitySpan = new Span<byte>((void*)_identityBufferPtr, 64);
            _router1.Recv(identitySpan);
            _router1.Recv(identitySpan);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Free native memory
        if (_sendDataPtr != nint.Zero)
            Marshal.FreeHGlobal(_sendDataPtr);
        if (_recvBufferPtr != nint.Zero)
            Marshal.FreeHGlobal(_recvBufferPtr);
        if (_identityBufferPtr != nint.Zero)
            Marshal.FreeHGlobal(_identityBufferPtr);

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

    /// <summary>
    /// Baseline: Recv(Span&lt;byte&gt;) - reuse native memory buffer allocated in GlobalSetup.
    /// Zero allocations for receive buffer, minimal GC pressure.
    /// Expected: Best performance for small to medium messages due to zero allocations.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void SpanRecv_RouterToRouter()
    {
        var countdown = new CountdownEvent(1);
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBufferPtr, 64);
                _router2.Recv(_recvBufferPtr, MessageSize);
                n++;

                // Batch receive available messages (reduces syscalls)
                while (n < MessageCount && _router2.Recv(_identityBufferPtr, 64, RecvFlags.DontWait) != -1)
                {
                    _router2.Recv(_recvBufferPtr, MessageSize, RecvFlags.DontWait);
                    n++;
                }
            }

            countdown.Signal();
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendDataPtr, MessageSize, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }

    /// <summary>
    /// Recv(Message) - create new Message for each receive, dispose immediately.
    /// Message object allocation + native memory managed by libzmq.
    /// Expected: Competitive performance for large messages due to zero-copy native memory access.
    /// </summary>
    [Benchmark]
    public void MessageRecv_RouterToRouter()
    {
        var countdown = new CountdownEvent(1);
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                // First message: blocking wait
                _router2.Recv(_identityBufferPtr, 64);

                using (var dataMsg = new Message())
                {
                    _router2.Recv(dataMsg);
                    // Use dataMsg.Data here if needed
                }

                n++;

                // Batch receive available messages
                while (n < MessageCount && _router2.Recv(_identityBufferPtr, 64, RecvFlags.DontWait) != -1)
                {
                    using var dataMsg = new Message();
                    _router2.Recv(dataMsg);
                    // Use dataMsg.Data here if needed

                    n++;
                }
            }

            countdown.Signal();
        });
        recvThread.Start();

        // Sender (same as SpanRecv)
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendDataPtr, MessageSize, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }
}
