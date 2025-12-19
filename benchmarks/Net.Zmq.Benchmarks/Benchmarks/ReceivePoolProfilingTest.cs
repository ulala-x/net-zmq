using BenchmarkDotNet.Attributes;
using System.Diagnostics;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// ReceiveWithPool의 각 단계별 오버헤드를 측정하는 마이크로 벤치마크
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class ReceivePoolProfilingTest
{
    [Params(10000)]
    public int Iterations { get; set; }

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;
    private byte[] _testData = null!;
    private byte[] _identityBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _testData = new byte[64];
        _identityBuffer = new byte[64];
        Array.Fill(_testData, (byte)'A');

        _ctx = new Context();
        _router1 = CreateSocket(SocketType.Router);
        _router2 = CreateSocket(SocketType.Router);
        _router2Id = "r2"u8.ToArray();
        _router1.SetOption(SocketOption.Routing_Id, "r1"u8.ToArray());
        _router2.SetOption(SocketOption.Routing_Id, _router2Id);
        _router1.Bind("tcp://127.0.0.1:0");
        _router2.Connect(_router1.GetOptionString(SocketOption.Last_Endpoint));

        Thread.Sleep(100);

        _router2.Send("r1"u8.ToArray(), SendFlags.SendMore);
        _router2.Send("hi"u8.ToArray());
        _router1.Recv(_identityBuffer);
        _router1.Recv(_identityBuffer);

        MessagePool.Shared.SetMaxBuffers(MessageSize.B64, 800);
        MessagePool.Shared.Prewarm(MessageSize.B64, 800);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
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

    /// <summary>
    /// Baseline: new Message() + Recv (정상 속도)
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Baseline_NewMessage_Recv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                _router2.Recv(_identityBuffer);
                using var msg = new Message();
                _router2.Recv(msg);
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < Iterations; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait();
    }

    /// <summary>
    /// ReceiveWithPool() 사용 (느림)
    /// </summary>
    [Benchmark]
    public void Test_ReceiveWithPool()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                _router2.Recv(_identityBuffer);
                using var msg = _router2.ReceiveWithPool();
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < Iterations; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait();
    }

    /// <summary>
    /// MessagePool.Rent만 반복 호출 (풀 오버헤드 측정)
    /// </summary>
    [Benchmark]
    public void Overhead_PoolRentReturn()
    {
        for (int i = 0; i < Iterations; i++)
        {
            using var msg = MessagePool.Shared.Rent(_testData.AsSpan());
            // 즉시 Dispose
        }
    }

    /// <summary>
    /// new Message() + 데이터 복사만 반복 (비교용)
    /// </summary>
    [Benchmark]
    public void Overhead_NewMessageWithData()
    {
        for (int i = 0; i < Iterations; i++)
        {
            using var msg = new Message(_testData.AsSpan());
            // 즉시 Dispose
        }
    }
}
