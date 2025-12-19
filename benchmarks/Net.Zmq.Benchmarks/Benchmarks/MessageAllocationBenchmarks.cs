using BenchmarkDotNet.Attributes;

namespace Net.Zmq.Benchmarks.Benchmarks;

/// <summary>
/// Compares the performance of MessagePool.Rent() vs new Message().
/// This benchmark measures the pure allocation/deallocation overhead without any I/O.
///
/// Purpose: Understand the theoretical performance difference between:
/// 1. MessagePool.Rent() - Reuse native memory from pool
/// 2. new Message(size) - Allocate new native memory each time
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
public class MessageAllocationBenchmarks
{
    [Params(MessageSize.B64, MessageSize.B512, MessageSize.K1, MessageSize.K64,Zmq.MessageSize.M1)]
    public MessageSize MessageSize { get; set; }

    private byte[] _sourceArray = null!;

    [Params(1000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Pre-warm MessagePool with sufficient buffers to ensure 100% hit rate
        MessagePool.Shared.SetMaxBuffers(MessageSize, 1000);
        MessagePool.Shared.Prewarm(MessageSize, 1000);
        Console.WriteLine($"Pre-warmed MessagePool with 1000 buffers of size {MessageSize}");
        _sourceArray = new byte[(int)MessageSize];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Print MessagePool statistics
        var stats = MessagePool.Shared.GetStatistics();
        Console.WriteLine($"MessagePool Statistics: {stats}");

        // Clear the pool to avoid state carryover between benchmark runs
        MessagePool.Shared.Clear();
    }

    /// <summary>
    /// Baseline: Create new Message objects.
    /// Each iteration allocates native memory via Marshal.AllocHGlobal.
    /// Expected: Slower due to allocation overhead, high GC pressure from wrapper objects.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void NewMessage()
    {
        List<Message> msgList = new List<Message>();
        for (int i = 0; i < MessageCount; i++)
        {
            using var msg = new Message((int)MessageSize);
            msgList.Add(msg);
            // Message is disposed and native memory is freed
        }

        msgList.Clear();
    }

    /// <summary>
    /// MessagePool.Rent (size only).
    /// Reuses native memory from pool, buffer is automatically returned on dispose.
    /// Expected: Faster due to memory reuse, minimal allocations.
    /// </summary>
    [Benchmark]
    public void PoolRent_SizeOnly()
    {
        List<Message> msgList = new List<Message>();
        for (int i = 0; i < MessageCount; i++)
        {
            using var msg = MessagePool.Shared.Rent((int)MessageSize);
            msgList.Add(msg);
            // Message is disposed and buffer is automatically returned to pool
        }
    }

    /// <summary>
    /// MessagePool.Rent with data (ReadOnlySpan).
    /// Reuses native memory from pool and copies data, buffer is automatically returned on dispose.
    /// Expected: Faster than NewMessage due to pooling, but includes copy overhead.
    /// </summary>
    [Benchmark]
    public void PoolRent_WithData()
    {
        List<Message> msgList = new List<Message>();
        for (int i = 0; i < MessageCount; i++)
        {
            using var msg = MessagePool.Shared.Rent(_sourceArray);
            msgList.Add(msg);
            // Message is disposed and buffer is automatically returned to pool
        }
    }
}
