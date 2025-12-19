using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// 마이크로 벤치마크: Message 생성/재사용의 각 단계별 성능 측정
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class MessageCreationMicroBenchmark
{
    private const int Iterations = 1000;
    private const int MessageSize = 64;

    // 재사용을 위한 필드들
    private Message? _reusableMessage;
    private nint _nativePointer;

    [GlobalSetup]
    public void Setup()
    {
        // 재사용할 Message 미리 준비
        _reusableMessage = MessagePool.Shared.Rent(MessageSize);
        _nativePointer = Marshal.AllocHGlobal(MessageSize);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reusableMessage?.Dispose();
        if (_nativePointer != nint.Zero)
            Marshal.FreeHGlobal(_nativePointer);
    }

    /// <summary>
    /// Baseline: 매번 새 Message 생성 (네이티브 메모리 할당 + Message 객체 생성)
    /// </summary>
    [Benchmark(Baseline = true)]
    public void NewMessage_Full()
    {
        for (int i = 0; i < Iterations; i++)
        {
            using var msg = new Message(MessageSize);
            // Dispose 자동 호출
        }
    }

    /// <summary>
    /// Message 객체만 생성 (네이티브 메모리는 재사용)
    /// </summary>
    [Benchmark]
    public void NewMessage_ObjectOnly()
    {
        for (int i = 0; i < Iterations; i++)
        {
            // 네이티브 포인터는 재사용, Message 객체만 새로 생성
            var msg = new Message(_nativePointer, MessageSize, freeCallback: null);
            msg.Dispose();
        }
    }

    /// <summary>
    /// Pool에서 Rent/Dispose 반복 (실제 사용 패턴)
    /// </summary>
    [Benchmark]
    public void PoolRent_ReusePattern()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var msg = MessagePool.Shared.Rent(MessageSize);
            msg.Dispose();
        }
    }

    /// <summary>
    /// Pool에서 Rent만 (Dispose 없음, 메모리 누수)
    /// </summary>
    [Benchmark]
    public void PoolRent_OnlyRent()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var msg = MessagePool.Shared.Rent(MessageSize);
            // Dispose 안 함 - Rent 비용만 측정
        }
    }

    /// <summary>
    /// ConcurrentStack에서 Pop/Push 오버헤드만 측정
    /// </summary>
    [Benchmark]
    public void StackPopPush()
    {
        var stack = new System.Collections.Concurrent.ConcurrentStack<Message>();
        var msg = new Message(MessageSize);
        stack.Push(msg);

        for (int i = 0; i < Iterations; i++)
        {
            stack.TryPop(out var m);
            stack.Push(m!);
        }

        msg.Dispose();
    }
}
