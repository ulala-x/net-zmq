using BenchmarkDotNet.Attributes;
using Net.Zmq;

namespace Net.Zmq.Benchmarks.Benchmarks;

public enum ReceiveMode { Blocking, NonBlocking, Poller }

[MemoryDiagnoser]
public class SocketPatternBenchmarks
{
    [Params(64, 1024, 65536)]
    public int MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    [Params(ReceiveMode.Blocking, ReceiveMode.NonBlocking, ReceiveMode.Poller)]
    public ReceiveMode Mode { get; set; }

    private byte[] _sendData = null!;
    private byte[] _recvBuffer = null!;
    private byte[] _identityBuffer = null!;

    private Context _ctx = null!;
    private Socket _push = null!, _pull = null!;
    private Socket _pub = null!, _sub = null!;
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

        // PUSH/PULL
        _pull = CreateSocket(SocketType.Pull);
        _push = CreateSocket(SocketType.Push);
        _pull.Bind("tcp://127.0.0.1:0");
        _push.Connect(_pull.GetOptionString(SocketOption.Last_Endpoint));

        // PUB/SUB
        _pub = CreateSocket(SocketType.Pub);
        _sub = CreateSocket(SocketType.Sub);
        _pub.Bind("tcp://127.0.0.1:0");
        _sub.Subscribe("");
        _sub.Connect(_pub.GetOptionString(SocketOption.Last_Endpoint));

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
        _push?.Dispose(); _pull?.Dispose();
        _pub?.Dispose(); _sub?.Dispose();
        _router1?.Dispose(); _router2?.Dispose();
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

    private void Run(Socket recvSocket, Action send, Action blockingRecv, Func<bool> tryRecv)
    {
        var thread = Mode switch
        {
            ReceiveMode.Blocking => new Thread(() =>
            {
                for (int i = 0; i < MessageCount; i++)
                    blockingRecv();
            }),
            ReceiveMode.NonBlocking => new Thread(() =>
            {
                int n = 0;
                while (n < MessageCount)
                {
                    if (tryRecv()) { n++; while (n < MessageCount && tryRecv()) n++; }
                    else Thread.Sleep(10);
                }
            }),
            ReceiveMode.Poller => new Thread(() =>
            {
                using var poller = new Poller(1);
                int idx = poller.Add(recvSocket, PollEvents.In);
                int n = 0;
                while (n < MessageCount)
                {
                    poller.Poll(-1);
                    while (n < MessageCount && tryRecv()) n++;
                }
            }),
            _ => throw new ArgumentException()
        };

        thread.Start();
        for (int i = 0; i < MessageCount; i++) send();
        thread.Join();
    }

    [Benchmark(Baseline = true)]
    public void PushPull_Throughput() => Run(
        _pull,
        () => _push.Send(_sendData,SendFlags.SendMore),
        () => _pull.Recv(_recvBuffer),
        () => _pull.TryRecv(_recvBuffer, out _)
    );

    [Benchmark]
    public void PubSub_Throughput() => Run(
        _sub,
        () => _pub.Send(_sendData, SendFlags.SendMore),
        () => _sub.Recv(_recvBuffer),
        () => _sub.TryRecv(_recvBuffer, out _)
    );

    [Benchmark]
    public void RouterRouter_Throughput() => Run(
        _router2,
        () => { _router1.Send(_router2Id, SendFlags.SendMore); _router1.Send(_sendData); },
        () => { _router2.Recv(_identityBuffer); _router2.Recv(_recvBuffer); },
        () => { if (!_router2.TryRecv(_identityBuffer, out _)) return false; _router2.TryRecv(_recvBuffer, out _); return true; }
    );
}
