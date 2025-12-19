using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// ReceiveWithPool의 각 단계별 오버헤드를 정밀 측정하는 마이크로 벤치마크
/// 목적: new Message() Recv vs ReceiveWithPool의 성능 차이가 어디서 발생하는지 파악
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class ReceivePoolProfilingTest
{
    [Params(64, 1024, 65536)] // 64B, 1KB, 64KB
    public int MessageSize { get; set; }

    [Params(10000)]
    public int MessageCount { get; set; }

    private Context _ctx = null!;
    private Socket _router1 = null!, _router2 = null!;
    private byte[] _router2Id = null!;
    private byte[] _testData = null!;
    private byte[] _identityBuffer = null!;
    private byte[] _recvBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _testData = new byte[MessageSize];
        _identityBuffer = new byte[64];
        _recvBuffer = new byte[MessageSize];
        Array.Fill(_testData, (byte)'T');

        _ctx = new Context();
        _router1 = CreateSocket(SocketType.Router);
        _router2 = CreateSocket(SocketType.Router);
        _router2Id = "r2"u8.ToArray();
        _router1.SetOption(SocketOption.Routing_Id, "r1"u8.ToArray());
        _router2.SetOption(SocketOption.Routing_Id, _router2Id);
        _router1.Bind("tcp://127.0.0.1:0");
        _router2.Connect(_router1.GetOptionString(SocketOption.Last_Endpoint));

        Thread.Sleep(100);

        // Handshake
        _router2.Send("r1"u8.ToArray(), SendFlags.SendMore);
        _router2.Send("hi"u8.ToArray());
        _router1.Recv(_identityBuffer);
        _router1.Recv(_identityBuffer);

        // MessagePool prewarm - 실제 필요 개수 테스트
        var msgSize = MessageSize switch
        {
            64 => Net.Zmq.MessageSize.B64,
            1024 => Net.Zmq.MessageSize.K1,
            65536 => Net.Zmq.MessageSize.K64,
            _ => Net.Zmq.MessageSize.B64
        };
        // 100개로 줄여서 테스트 (실제로 몇 개가 필요한지 확인)
        MessagePool.Shared.SetMaxBuffers(msgSize, 100);
        MessagePool.Shared.Prewarm(msgSize, 100);

        Console.WriteLine($"Setup complete: MessageSize={MessageSize}, MessageCount={MessageCount}");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        var stats = MessagePool.Shared.GetStatistics();
        Console.WriteLine($"MessagePool Statistics: {stats}");

        // 실제 풀에 있는 버퍼 개수 확인
        var poolCounts = MessagePool.Shared.GetPoolCounts();
        Console.WriteLine($"Pool Counts: {string.Join(", ", poolCounts.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}bytes: {kv.Value}"))}");

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
    /// Baseline: new Message() + Recv (가장 기본적인 방식)
    /// 이 속도가 기준 (1.00x)
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Baseline_NewMessage_Recv()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < MessageCount; i++)
            {
                _router2.Recv(_identityBuffer);
                using var msg = new Message();
                _router2.Recv(msg);
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// ReceiveWithPool() - 전체 경로
    /// = Recv(_recvBufferPtr) + Rent(actualSize) + CopyFromNative + Dispose
    /// </summary>
    [Benchmark]
    public void ReceiveWithPool_Full()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < MessageCount; i++)
            {
                _router2.Recv(_identityBuffer);
                using var msg = _router2.ReceiveWithPool();
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Step 1: Recv into byte[] buffer만
    /// ReceiveWithPool의 첫 단계 오버헤드 측정
    /// </summary>
    [Benchmark]
    public void Step1_RecvToByteArray_Only()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < MessageCount; i++)
            {
                _router2.Recv(_identityBuffer);
                int size = _router2.Recv(_recvBuffer);
                // 받기만 하고 아무것도 안 함
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Step 2: Recv + Rent (복사 제외)
    /// 풀에서 대여하는 오버헤드 추가 측정
    /// </summary>
    [Benchmark]
    public void Step2_RecvToByteArray_Plus_Rent()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < MessageCount; i++)
            {
                _router2.Recv(_identityBuffer);
                int size = _router2.Recv(_recvBuffer);

                // Rent + 즉시 Dispose (복사 없음)
                var msg = MessagePool.Shared.Rent(size);
                msg.Dispose();
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Step 3: Recv + Rent + 데이터 복사 (Span을 통한 복사)
    /// ReceiveWithPool과 유사한 경로 (공개 API만 사용)
    /// </summary>
    [Benchmark]
    public void Step3_RecvToByteArray_Plus_Rent_Plus_Copy()
    {
        var countdown = new CountdownEvent(1);
        var thread = new Thread(() =>
        {
            for (int i = 0; i < MessageCount; i++)
            {
                _router2.Recv(_identityBuffer);
                int size = _router2.Recv(_recvBuffer);

                // Rent하고 데이터 복사 (공개 API 사용)
                var msg = MessagePool.Shared.Rent(_recvBuffer.AsSpan(0, size));
                msg.Dispose();
            }
            countdown.Signal();
        });
        thread.Start();

        for (int i = 0; i < MessageCount; i++)
        {
            _router1.Send(_router2Id, SendFlags.SendMore);
            _router1.Send(_testData);
        }

        countdown.Wait(TimeSpan.FromSeconds(30));
    }

    // =====================================
    // 순수 오버헤드 측정 (I/O 없이)
    // =====================================

    /// <summary>
    /// 순수 풀 오버헤드: Rent + Dispose만 반복
    /// I/O 없이 풀 관리 비용만 측정
    /// </summary>
    [Benchmark]
    public void Overhead_RentDispose_Only()
    {
        for (int i = 0; i < MessageCount; i++)
        {
            var msg = MessagePool.Shared.Rent(MessageSize);
            msg.Dispose();
        }
    }

    /// <summary>
    /// 순수 복사 오버헤드: Span.CopyTo만 반복
    /// I/O 없이 메모리 복사 비용만 측정
    /// </summary>
    [Benchmark]
    public void Overhead_SpanCopy_Only()
    {
        var sourceSpan = _recvBuffer.AsSpan();
        var msg = MessagePool.Shared.Rent(MessageSize);
        var destSpan = msg.Data;

        for (int i = 0; i < MessageCount; i++)
        {
            sourceSpan.CopyTo(destSpan);
        }

        msg.Dispose();
    }

    /// <summary>
    /// 순수 할당 오버헤드: new Message()만 반복
    /// 비교 기준: 풀 없이 매번 할당하는 비용
    /// </summary>
    [Benchmark]
    public void Overhead_NewMessage_Only()
    {
        for (int i = 0; i < MessageCount; i++)
        {
            using var msg = new Message(MessageSize);
        }
    }

    /// <summary>
    /// 순수 복사 오버헤드: new Message(data)만 반복
    /// 비교 기준: 할당 + 데이터 복사 비용
    /// </summary>
    [Benchmark]
    public void Overhead_NewMessage_WithData()
    {
        for (int i = 0; i < MessageCount; i++)
        {
            using var msg = new Message(_testData.AsSpan());
        }
    }
}
