using BenchmarkDotNet.Attributes;
using Net.Zmq;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Compares three receive strategies for ROUTER-to-ROUTER multipart messaging:
///
/// 1. Blocking: Thread blocks on Recv() until message available
///    - Highest throughput (baseline)
///    - Simplest implementation
///    - Thread dedicated to single socket
///
/// 2. NonBlocking: TryRecv() with Thread.Sleep() fallback
///    - No blocking, but Thread.Sleep(10ms) adds overhead
///    - 5-6x slower than Poller
///    - Not recommended for production
///
/// 3. Poller: Event-driven with zmq_poll()
///    - 98-99% of Blocking performance
///    - Multi-socket support
///    - Recommended for production use
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class ReceiveModeBenchmarks
{
    [Params(64, 1500, 65536)]
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
    /// Receiver thread blocks on Recv() until messages are available.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Blocking_RouterToRouter()
    {
        var recvThread = new Thread(() =>
        {
            for (int i = 0; i < MessageCount; i++)
            {
                _router2.Recv(_identityBuffer);  // Identity frame
                _router2.Recv(_recvBuffer);       // Body frame
            }
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        recvThread.Join();
    }

    /// <summary>
    /// Non-blocking receive mode - uses TryRecv() with Thread.Sleep() fallback.
    /// Thread.Sleep(10ms) overhead makes this 5-6x slower than Poller.
    /// Not recommended for production use.
    /// </summary>
    [Benchmark]
    public void NonBlocking_RouterToRouter()
    {
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                if (_router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                    // Batch receive without sleep
                    while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                    {
                        _router2.TryRecv(_recvBuffer, out _);
                        n++;
                    }
                }
                else
                {
                    Thread.Sleep(10);  // Wait before retry
                }
            }
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        recvThread.Join();
    }

    /// <summary>
    /// Non-blocking receive mode with Thread.Sleep(1ms) fallback.
    /// Tests whether reducing sleep time from 10ms to 1ms improves performance.
    /// </summary>
    [Benchmark]
    public void NonBlocking_Sleep1_RouterToRouter()
    {
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                if (_router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                    // Batch receive without sleep
                    while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                    {
                        _router2.TryRecv(_recvBuffer, out _);
                        n++;
                    }
                }
                else
                {
                    Thread.Sleep(1);  // Wait before retry
                }
            }
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        recvThread.Join();
    }

    /// <summary>
    /// Non-blocking receive mode with Thread.Sleep(5ms) fallback.
    /// Tests whether reducing sleep time from 10ms to 5ms improves performance.
    /// </summary>
    [Benchmark]
    public void NonBlocking_Sleep5_RouterToRouter()
    {
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                if (_router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                    // Batch receive without sleep
                    while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                    {
                        _router2.TryRecv(_recvBuffer, out _);
                        n++;
                    }
                }
                else
                {
                    Thread.Sleep(5);  // Wait before retry
                }
            }
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        recvThread.Join();
    }

    /// <summary>
    /// Non-blocking receive mode with Thread.Yield() fallback.
    /// Tests whether Thread.Yield() (cooperative yield to other threads) performs better than Thread.Sleep().
    /// Yield gives up the CPU but allows immediate re-scheduling, unlike Sleep which waits for a minimum time.
    /// </summary>
    [Benchmark]
    public void NonBlocking_Yield_RouterToRouter()
    {
        var recvThread = new Thread(() =>
        {
            int n = 0;
            while (n < MessageCount)
            {
                if (_router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                    // Batch receive without sleep
                    while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                    {
                        _router2.TryRecv(_recvBuffer, out _);
                        n++;
                    }
                }
                else
                {
                    Thread.Yield();  // Wait before retry
                }
            }
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        recvThread.Join();
    }

    /// <summary>
    /// Poller-based receive mode - event-driven approach using zmq_poll().
    /// Achieves 98-99% of Blocking performance with multi-socket support.
    /// Recommended for production use.
    /// </summary>
    [Benchmark]
    public void Poller_RouterToRouter()
    {
        var recvThread = new Thread(() =>
        {
            using var poller = new Poller(1);
            poller.Add(_router2, PollEvents.In);

            int n = 0;
            while (n < MessageCount)
            {
                poller.Poll(-1);  // Wait for events

                // Batch receive all available messages
                while (n < MessageCount && _router2.TryRecv(_identityBuffer, out _))
                {
                    _router2.TryRecv(_recvBuffer, out _);
                    n++;
                }
            }
        });
        recvThread.Start();

        // Sender
        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_sendData, SendFlags.DontWait);
        }

        recvThread.Join();
    }
}
