using BenchmarkDotNet.Attributes;
using Net.Zmq;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Compares receive strategies for ROUTER-to-ROUTER multipart messaging:
///
/// 1. Blocking: Thread blocks on Recv() until message available
///    - Highest throughput (baseline)
///    - Simplest implementation
///    - Thread dedicated to single socket
///
/// 2. NonBlocking (Sleep 1ms): TryRecv() with Thread.Sleep(1ms) fallback
///    - No blocking, but Thread.Sleep() adds overhead
///    - Slower than Blocking/Poller
///    - Not recommended for production
///
/// 3. Poller: Event-driven with zmq_poll()
///    - Similar to Blocking performance
///    - Multi-socket support
///    - Recommended for production use
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class ReceiveModeBenchmarks
{
    [Params(64, 512, 1024, 65536)]
    public int MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    private byte[] _sendData = null!;
    private byte[] _recvBuffer = null!;
    private byte[] _identityBuffer = null!;

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sendData = new byte[MessageSize];
        _recvBuffer = new byte[MessageSize];
        _identityBuffer = new byte[64];
        Array.Fill(_sendData, (byte)'A');

        _ctx = new Context();

        // ROUTER/ROUTER
        _router1 = CreateSocket(SocketType.Router);
        _router2 = CreateSocket(SocketType.Router);
        _router2Id = "r2"u8.ToArray();
        _router1.SetOption(SocketOption.Routing_Id, "r1"u8.ToArray());
        _router2.SetOption(SocketOption.Routing_Id, _router2Id);
        _router1.Bind("tcp://127.0.0.1:0");
        _router2.Connect(_router1.GetOptionString(SocketOption.Last_Endpoint));

        Thread.Sleep(100);

        // Router handshake
        _router2.Send("r1"u8.ToArray(), SendFlags.SendMore);
        _router2.Send("hi"u8.ToArray());
        _router1.Recv(_identityBuffer);
        _router1.Recv(_identityBuffer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
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
    /// Blocking receive mode - highest performance, simplest implementation.
    /// Uses blocking Recv() for first message, then batch-processes available messages
    /// with TryRecv() to minimize syscall overhead.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Blocking_RouterToRouter()
    {
        var countdown = new CountdownEvent(1);
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                // First message: blocking wait (maintains Blocking semantics)
                _router2.Recv(_identityBuffer);
                _router2.Recv(_recvBuffer);
                n++;

                // Batch receive available messages (reduces syscalls)
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    _router2.Recv(_recvBuffer, RecvFlags.DontWait);
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
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }


    /// <summary>
    /// Non-blocking receive mode with Thread.Sleep(1ms) fallback.
    /// Slower than Blocking/Poller due to polling overhead.
    /// Not recommended for production use.
    /// </summary>
    [Benchmark]
    public void NonBlocking_RouterToRouter()
    {
        var countdown = new CountdownEvent(1);
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                if (_router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    _router2.Recv(_recvBuffer, RecvFlags.DontWait);
                    n++;
                    // Batch receive without sleep
                    while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                    {
                        _router2.Recv(_recvBuffer, RecvFlags.DontWait);
                        n++;
                    }
                }
                else
                {
                    Thread.Sleep(1);  // Wait before retry
                }
            }
            countdown.Signal();  // Signal completion
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }



    /// <summary>
    /// Poller-based receive mode - event-driven approach using zmq_poll().
    /// Achieves 98-99% of Blocking performance with multi-socket support.
    /// Recommended for production use.
    /// </summary>
    [Benchmark]
    public void Poller_RouterToRouter()
    {
        var countdown = new CountdownEvent(1);
        var recvThread = new Thread(() =>
        {
            using var poller = new Poller(1);
            poller.Add(_router2, PollEvents.In);

            int n = 0;
            while (n < MessageCount)
            {
                poller.Poll(-1);  // Wait for events

                // Batch receive all available messages
                while (n < MessageCount && _router2.Recv(_identityBuffer, RecvFlags.DontWait) != -1)
                {
                    _router2.Recv(_recvBuffer, RecvFlags.DontWait);
                    n++;
                }
            }
            countdown.Signal();  // Signal completion
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        if (!countdown.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Benchmark timeout after 30s - receiver may be hung");
        }
    }
}
