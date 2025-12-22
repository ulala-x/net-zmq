[![English](https://img.shields.io/badge/lang:en-red.svg)](benchmarks.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](benchmarks.ko.md)

# Net.Zmq Performance Benchmarks

This document contains comprehensive performance benchmark results for Net.Zmq, focusing on receive mode comparisons and memory strategy evaluations.

## Executive Summary

Net.Zmq provides multiple receive modes and memory strategies to accommodate different performance requirements and architectural patterns. This benchmark suite evaluates:

- **Receive Modes**: Blocking, NonBlocking, and Poller-based message reception
- **Message Buffer Strategies**: ByteArray, ArrayPool, Message, and MessageZeroCopy approaches
- **Message Sizes**: 64 bytes (small), 512 bytes, 1024 bytes, and 65KB (large)

### Test Environment

| Component | Specification |
|-----------|--------------|
| **CPU** | Intel Core Ultra 7 265K (20 cores) |
| **OS** | Ubuntu 24.04.3 LTS (Noble Numbat) |
| **Runtime** | .NET 8.0.22 (8.0.2225.52707) |
| **JIT** | X64 RyuJIT AVX2 |
| **Benchmark Tool** | BenchmarkDotNet v0.14.0 |

### Benchmark Configuration

- **Job**: ShortRun
- **Platform**: X64
- **Iteration Count**: 3
- **Warmup Count**: 3
- **Launch Count**: 1
- **Message Count**: 10,000 messages per test
- **Transport**: tcp://127.0.0.1 (localhost loopback)
- **Pattern**: ROUTER-to-ROUTER (for receive mode tests)

## Receive Mode Benchmarks

### How Each Mode Works

The benchmark compares four different receive strategies:

#### 1. PureBlocking: Pure Blocking Recv() Pattern

**Implementation**:
```csharp
for (int n = 0; n < MessageCount; n++)
{
    socket.Recv(buffer);  // Blocking wait for each message
}
```

**Characteristics**:
- Every message requires a blocking `recv()` syscall
- Simplest implementation with deterministic waiting
- **CPU usage: 0% while waiting** (thread is asleep in kernel)
- One thread per socket required

#### 2. BlockingWithBatch: Blocking + Batch Hybrid Pattern

**Implementation**:
```csharp
while (n < MessageCount)
{
    // First message: blocking wait
    socket.Recv(buffer);
    n++;

    // Batch receive available messages
    while (n < MessageCount && socket.Recv(buffer, RecvFlags.DontWait) != -1)
    {
        n++;
    }
}
```

**Characteristics**:
- Blocks for the first message, then drains all available messages
- Reduces syscall overhead by batching when multiple messages are queued
- **CPU usage: 0% while waiting** for the first message
- Optimal for high-throughput scenarios with bursty traffic

#### 3. NonBlocking: DontWait + Sleep(1ms) + Batch Pattern

**Implementation**:
```csharp
while (n < MessageCount)
{
    while (n < MessageCount && socket.Recv(buffer, RecvFlags.DontWait) != -1)
    {
        n++;
    }
    Thread.Sleep(1);  // Sleep 1ms to reduce CPU usage
}
```

**Characteristics**:
- **No kernel-level blocking** - all polling happens in user space
- `Thread.Sleep(1ms)` reduces CPU usage but adds latency overhead
- Batches messages when available, but Sleep() adds 1ms minimum latency
- **Relatively lower performance** (1.3-1.7x slower than blocking)

#### 4. Poller: Poll(-1) + Batch Pattern (I/O Multiplexing)

**Implementation**:
```csharp
while (n < MessageCount)
{
    poller.Poll(-1);  // Blocking wait for any socket event

    // Batch receive all available messages
    while (n < MessageCount && socket.Recv(buffer, RecvFlags.DontWait) != -1)
    {
        n++;
    }
}
```

**Characteristics**:
- Uses OS multiplexing APIs (epoll/kqueue/IOCP) to wait for socket events
- **CPU usage: 0% while waiting** (kernel-level blocking)
- Batches all available messages after wake-up
- Ideal for monitoring multiple sockets with a single thread

#### Why Blocking and Poller Are Efficient

| Mode | Waiting Location | Wake Mechanism | CPU (Idle) | Efficiency |
|------|-----------------|----------------|------------|------------|
| **Blocking** | Kernel space | Kernel interrupt | 0% | ✓ Optimal for single socket |
| **Poller** | Kernel space | Kernel (epoll/kqueue) | 0% | ✓ Optimal for multiple sockets |
| **NonBlocking** | User space | None (continuous polling) | Low (Sleep 1ms) | ✗ Poor performance |

**Key Insight**: Blocking and Poller delegate waiting to the kernel, which:
- Uses hardware interrupts to detect data arrival instantly
- Keeps threads asleep (0% CPU) until events occur
- Wakes threads at the exact moment needed

NonBlocking lacks this kernel support, forcing continuous checking in user space with Thread.Sleep() adding latency overhead.

### Understanding Benchmark Metrics

The benchmark results include the following columns:

| Column | Description |
|--------|-------------|
| **Mean** | Average execution time to send and receive all messages (lower is better) |
| **Error** | Standard error of the mean (statistical margin of error) |
| **StdDev** | Standard deviation showing measurement variability |
| **Ratio** | Performance ratio compared to baseline (1.00x = baseline, higher = slower) |
| **Latency** | Per-message latency calculated as `Mean / MessageCount` |
| **Messages/sec** | Message throughput - how many messages processed per second |
| **Data Throughput** | Actual network bandwidth (Gbps for small messages, GB/s for large messages) |
| **Allocated** | Total memory allocated during the benchmark |
| **Gen0/Gen1** | Number of garbage collection cycles (lower is better) |

**How to read the results**: Lower Mean times and higher Messages/sec indicate better performance. Ratio shows relative performance where 1.00x is the baseline (typically the slowest method in each category).

### Performance Results

All tests use ROUTER-to-ROUTER pattern with concurrent sender and receiver.

#### 64-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 2.187 ms | 218.7 ns | 4.57M | 2.34 Gbps | 336 B | 1.00x |
| **Poller** | 2.311 ms | 231.1 ns | 4.33M | 2.22 Gbps | 456 B | 1.06x |
| NonBlocking (Sleep 1ms) | 3.783 ms | 378.3 ns | 2.64M | 1.35 Gbps | 336 B | 1.73x |

#### 512-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 4.902 ms | 490.2 ns | 2.04M | 8.36 Gbps | 336 B | 1.00x |
| **Poller** | 4.718 ms | 471.8 ns | 2.12M | 8.68 Gbps | 456 B | 0.96x |
| NonBlocking (Sleep 1ms) | 6.137 ms | 613.7 ns | 1.63M | 6.67 Gbps | 336 B | 1.25x |

#### 1024-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 7.541 ms | 754.1 ns | 1.33M | 10.82 Gbps | 336 B | 1.00x |
| **Poller** | 7.737 ms | 773.7 ns | 1.29M | 10.53 Gbps | 456 B | 1.03x |
| NonBlocking (Sleep 1ms) | 9.661 ms | 966.1 ns | 1.04M | 8.44 Gbps | 336 B | 1.28x |

#### 65KB Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 139.915 ms | 13.99 μs | 71.47K | 4.37 GB/s | 664 B | 1.00x |
| **Poller** | 141.733 ms | 14.17 μs | 70.56K | 4.31 GB/s | 640 B | 1.01x |
| NonBlocking (Sleep 1ms) | 260.014 ms | 26.00 μs | 38.46K | 2.35 GB/s | 1736 B | 1.86x |

### Performance Analysis

**Blocking vs Poller**: Performance is nearly identical across all message sizes (96-106% relative performance). For small messages (64B-1KB), Poller is 0-4% faster than Blocking. For large messages (65KB), Blocking is 1% faster than Poller. Both modes use kernel-level waiting mechanisms that efficiently wake threads when messages arrive. Poller allocates slightly more memory (456-640 bytes vs 336-664 bytes for 10K messages) due to polling infrastructure, but the difference is negligible in practice.

**NonBlocking Performance**: NonBlocking mode with `Thread.Sleep(1ms)` is consistently slower than Blocking and Poller modes (1.25-1.86x slower) due to:
1. User-space polling with `TryRecv()` has overhead compared to kernel-level blocking
2. Thread.Sleep() adds latency even with minimal 1ms sleep interval
3. Blocking and Poller modes use efficient kernel mechanisms (`recv()` syscall and `zmq_poll()`) that wake threads immediately when messages arrive

**Message Size Impact**: The Sleep overhead is most pronounced with small messages (64B) where NonBlocking is 1.73x slower, while for large messages (65KB) it's 1.86x slower.

**Recommendation**: NonBlocking mode has relatively lower performance (25-86% slower), so there's no need to use it. Use Poller for most scenarios (simplest API with best overall performance) or Blocking for single-socket applications.

### Receive Mode Selection Considerations

When choosing a receive mode, consider:

**Recommended Approaches**:
- **Single Socket**: Use **PureBlocking** mode for simplicity and deterministic behavior
  - Simplest implementation with minimal code complexity
  - Predictable behavior: one syscall per message
  - Ideal for scenarios where message arrival is unpredictable or sparse

- **Multiple Sockets**: Use **Poller** mode to monitor multiple sockets with a single thread
  - Event-driven architecture scales to many sockets
  - Kernel efficiently wakes thread when any socket has data
  - Batches available messages for throughput efficiency

**Performance Characteristics by Strategy**:

1. **PureBlocking**: Best for single-socket, low-to-medium throughput scenarios
   - One blocking syscall per message
   - No batching overhead or complexity
   - Predictable, deterministic latency

2. **BlockingWithBatch**: Best for high-throughput, bursty traffic scenarios
   - Combines blocking wait with opportunistic batching
   - Reduces syscall overhead when messages arrive in bursts
   - Optimal when sender produces messages faster than receiver can process

3. **NonBlocking**: **Not recommended** - relatively lower performance
   - 1.3-1.7x slower than blocking strategies
   - Thread.Sleep(1ms) adds minimum latency per batch
   - Only use if you must integrate with an existing polling loop

4. **Poller**: Best for multi-socket scenarios
   - Single thread can efficiently monitor multiple sockets
   - Kernel-level event notification with zero idle CPU usage
   - Batches messages after wake-up for throughput efficiency

**General Recommendations**:
- **Single Socket + Predictable Traffic**: Use **PureBlocking**
- **Single Socket + Bursty Traffic**: Use **BlockingWithBatch** (if implemented)
- **Multiple Sockets**: Always use **Poller**
- **Avoid**: NonBlocking mode (1.3-1.7x slower with Sleep overhead)

## Message Buffer Strategy Benchmarks

### How Each Strategy Works

**ByteArray (`new byte[]`)**: Allocates a new byte array for each message. Simple and straightforward, but creates garbage collection pressure proportional to message size and frequency.

**ArrayPool (`ArrayPool<byte>.Shared`)**: Rents buffers from a shared pool and returns them after use. Reduces GC allocations by reusing memory, though requires manual return management.

**Message (`zmq_msg_t`)**: Uses libzmq's native message structure, which manages memory internally. The .NET wrapper marshals data between native and managed memory as needed.

**MessageZeroCopy (`Marshal.AllocHGlobal`)**: Allocates unmanaged memory directly and transfers ownership to libzmq via a free callback. Provides zero-copy semantics but requires careful lifecycle management.

### Understanding Message Buffer Benchmark Metrics

In addition to the [standard benchmark metrics](#understanding-benchmark-metrics), message buffer strategy benchmarks include:

| Column | Description |
|--------|-------------|
| **Gen0** | Number of Generation 0 garbage collections during the benchmark (lower is better) |
| **Gen1** | Number of Generation 1 garbage collections (only appears for large allocations) |

**GC Impact**: Higher Gen0/Gen1 values indicate more GC pressure, which can cause performance degradation and unpredictable latency spikes. A dash (-) means zero collections occurred.

### Performance Results

All tests use PureBlocking mode for reception.

#### 64-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 2.382 ms | 238.17 ns | 4.20M | 2.15 Gbps | 3.91 | 1719.08 KB | 1.00x |
| **ArrayPool** | 2.410 ms | 240.96 ns | 4.15M | 2.13 Gbps | - | 1.08 KB | 1.01x |
| **Message** | 4.275 ms | 427.47 ns | 2.34M | 1.20 Gbps | - | 625.34 KB | 1.79x |
| **MessageZeroCopy** | 5.897 ms | 589.71 ns | 1.70M | 0.87 Gbps | - | 625.34 KB | 2.48x |

#### 512-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 6.584 ms | 658.41 ns | 1.52M | 6.23 Gbps | 23.44 | 10469.08 KB | 1.00x |
| **ArrayPool** | 6.214 ms | 621.44 ns | 1.61M | 6.60 Gbps | - | 1.53 KB | 0.94x |
| **Message** | 7.930 ms | 793.02 ns | 1.26M | 5.16 Gbps | - | 625.34 KB | 1.20x |
| **MessageZeroCopy** | 12.263 ms | 1.23 μs | 815.46K | 3.34 Gbps | - | 625.34 KB | 1.86x |

#### 1024-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 8.731 ms | 873.14 ns | 1.15M | 9.39 Gbps | 46.88 | 20469.09 KB | 1.00x |
| **ArrayPool** | 8.267 ms | 826.69 ns | 1.21M | 9.88 Gbps | - | 2.04 KB | 0.95x |
| **Message** | 9.309 ms | 930.94 ns | 1.07M | 8.75 Gbps | - | 625.34 KB | 1.07x |
| **MessageZeroCopy** | 13.296 ms | 1.33 μs | 752.13K | 6.15 Gbps | - | 625.34 KB | 1.52x |

#### 65KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **ByteArray** | 140.630 ms | 14.06 μs | 71.11K | 4.35 GB/s | 3333.33 | - | 1280469.54 KB | 1.00x |
| **ArrayPool** | 138.018 ms | 13.80 μs | 72.45K | 4.43 GB/s | - | - | 65.38 KB | 0.98x |
| **Message** | 111.554 ms | 11.16 μs | 89.64K | 5.48 GB/s | - | - | 625.53 KB | 0.79x |
| **MessageZeroCopy** | 123.824 ms | 12.38 μs | 80.76K | 4.94 GB/s | - | - | 625.58 KB | 0.88x |

#### 128KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Gen2 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|------|-----------|-------|
| **ByteArray** | 1,259 ms | 125.9 μs | 7.94K | 1.02 GB/s | 57000 | 57000 | 57000 | 2.5 GB | 1.00x |
| **ArrayPool** | 251.62 ms | 25.16 μs | 39.74K | 5.09 GB/s | - | - | - | 129 KB | 0.20x |
| **Message** | 203.68 ms | 20.37 μs | 49.10K | 6.28 GB/s | - | - | - | 625 KB | 0.16x |
| **MessageZeroCopy** | 226.45 ms | 22.65 μs | 44.16K | 5.65 GB/s | - | - | - | 625 KB | 0.18x |

#### 256KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Gen2 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|------|-----------|-------|
| **ByteArray** | 2,495 ms | 249.5 μs | 4.01K | 1.03 GB/s | 105000 | 105000 | 105000 | 5 GB | 1.00x |
| **ArrayPool** | 413.04 ms | 41.30 μs | 24.21K | 6.20 GB/s | - | - | - | 258 KB | 0.17x |
| **Message** | 378.18 ms | 37.82 μs | 26.44K | 6.77 GB/s | - | - | - | 626 KB | 0.15x |
| **MessageZeroCopy** | 368.16 ms | 36.82 μs | 27.16K | 6.95 GB/s | - | - | - | 626 KB | 0.15x |

### Performance and GC Analysis

**Small Messages (64B)**: ByteArray (2.382 ms, 4.20M msg/sec, 1.00x) and ArrayPool (2.410 ms, 4.15M msg/sec, 1.01x) show nearly identical performance with only a 1% difference. However, ByteArray generates moderate GC pressure (3.91 Gen0 collections, 1719 KB allocated) while ArrayPool maintains near-zero allocation (1.08 KB). Message (4.275 ms, 1.79x slower) and MessageZeroCopy (5.897 ms, 2.48x slower) show substantial overhead due to native interop costs being proportionally high for small payloads.

**Medium Messages (512B)**: ArrayPool becomes the fastest (6.214 ms, 1.61M msg/sec, 0.94x = 6% faster) with minimal allocation (1.53 KB). ByteArray is slightly slower (6.584 ms, 1.52M msg/sec, 1.00x) and shows increasing GC pressure (23.44 Gen0 collections, 10.5 MB allocated). Message (7.930 ms, 1.20x) performs reasonably, while MessageZeroCopy (12.263 ms, 1.86x) shows unexpected overhead.

**Medium-Large Messages (1KB)**: ArrayPool maintains its lead (8.267 ms, 1.21M msg/sec, 0.95x = 5% faster) with minimal allocation (2.04 KB), while ByteArray (8.731 ms, 1.15M msg/sec, 1.00x) shows substantial GC pressure (46.88 Gen0 collections, 20 MB allocated). Message (9.309 ms, 1.07x) becomes more competitive, while MessageZeroCopy (13.296 ms, 1.52x) still lags.

**Large Messages (65KB)**: Native strategies dominate - Message achieves best performance (111.554 ms, 89.64K msg/sec, 0.79x = 21% faster than baseline), followed by MessageZeroCopy (123.824 ms, 80.76K msg/sec, 0.88x = 12% faster). ArrayPool (138.018 ms, 72.45K msg/sec, 0.98x = 2% faster) and ByteArray (140.630 ms, 71.11K msg/sec, 1.00x) are similar in speed but ByteArray triggers massive GC pressure (3333 Gen0 collections, 1.25 GB allocated).

**Very Large Messages (128KB)**: ByteArray suffers extreme GC pressure (57,000 Gen0/1/2 collections each, 2.5 GB allocated) taking 1,259 ms, while Message achieves 203.68 ms - **6.2x faster** (0.16x). ArrayPool is also 5x faster at 251.62 ms but 19% slower than Message.

**Extra Large Messages (256KB)**: ByteArray's GC pressure intensifies (105,000 Gen0/1/2 collections each, 5 GB allocated) taking 2,495 ms. Message (378.18 ms) and MessageZeroCopy (368.16 ms) are **6.6-6.8x faster**. ArrayPool (413.04 ms) is also 6x faster but 9-11% slower than Message-based strategies.

**Large Object Heap (LOH) Impact**: In .NET, objects ≥85KB are allocated on the LOH, causing GC costs to skyrocket. This explains ByteArray's dramatic performance degradation at 128KB/256KB. Message uses native memory, completely avoiding this issue.

**GC Pattern Transition**: ArrayPool and native strategies maintain zero GC collections across all message sizes. ByteArray shows exponential GC pressure growth: 3.91 Gen0 at 64B → 23.44 Gen0 at 512B → 46.88 Gen0 at 1KB → 3333 Gen0 at 65KB → 57,000 Gen0/1/2 at 128KB → 105,000 Gen0/1/2 at 256KB.

**Memory Allocation**: ArrayPool demonstrates exceptional efficiency (1.08-258 KB total allocation across all sizes - 99.3-99.99% reduction vs ByteArray). Message and MessageZeroCopy maintain consistent ~625 KB allocation regardless of message size (99.95-99.99% reduction vs ByteArray at large sizes).

### Message Buffer Strategy Selection Considerations

When choosing a message buffer strategy, consider:

**Message Size Based Recommendations**:
- **Small messages (≤512B)**: Use **`ArrayPool<byte>.Shared`** - equivalent to ByteArray performance, GC-free
- **Large messages (≥65KB)**: Use **`Message`** - 18-19% faster than ArrayPool, GC-free
- **Very large messages (≥128KB)**: Use **`Message`** or **`MessageZeroCopy`** - 6x+ faster than other strategies
- **MessageZeroCopy**: Use only for special cases where you need to transfer already-allocated unmanaged memory with zero-copy

**One-Size-Fits-All Recommendation**:

If message sizes vary or are unpredictable, we recommend **`Message`**:

1. **Consistent Performance**: Provides predictable performance across all message sizes
2. **GC-Free**: Zero GC pressure using native memory
3. **Large Message Optimization**: 18-19% faster than ArrayPool for ≥65KB messages
4. **LOH Avoidance**: Completely bypasses .NET LOH issues for 128KB+ messages
5. **Adequate Small Message Performance**: Even at 64B, processes 2.34M messages/sec

While ArrayPool is slightly faster for small messages (1% at 64B), Message is clearly superior for large messages, making **Message the safer choice** for a single strategy approach.

**ArrayPool Usage Pattern**:
```csharp
using Net.Zmq;
using System.Buffers;

// Rent buffer from pool
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Fill buffer with data
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

**MessageZeroCopy Usage Pattern** (Special Cases):
```csharp
using Net.Zmq;
using System.Runtime.InteropServices;

// Allocate unmanaged memory
nint nativePtr = Marshal.AllocHGlobal(dataSize);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, dataSize);
    sourceData.CopyTo(nativeSpan);
}

// Transfer ownership to libzmq
using var message = new Message(nativePtr, dataSize, ptr =>
{
    Marshal.FreeHGlobal(ptr); // Called when libzmq is done
});

socket.Send(message);
```

**GC Sensitivity**:
- Applications sensitive to GC pauses should prefer ArrayPool (small messages) or Message (large messages)
- Applications with infrequent messaging or small messages may find ByteArray acceptable
- High-throughput applications benefit from GC-free strategies (ArrayPool, Message)

**Code Complexity**:
- **ByteArray**: Simplest implementation with automatic memory management
- **ArrayPool**: Requires explicit Rent/Return calls and buffer lifecycle tracking
- **Message**: Native integration with moderate complexity
- **MessageZeroCopy**: Requires unmanaged memory management and free callbacks

**Performance Trade-offs**:
- **Small messages (≤ 512B)**: Managed strategies (ByteArray, ArrayPool) have lower overhead
- **Large messages (≥ 512B)**: Message delivers optimal performance
- **Consistency**: GC-free strategies (ArrayPool, Message) provide more predictable timing

## Running Benchmarks

To run these benchmarks yourself:

```bash
cd benchmarks/Net.Zmq.Benchmarks
dotnet run -c Release
```

For specific benchmarks:

```bash
# Run only receive mode benchmarks
dotnet run -c Release --filter "*ReceiveModeBenchmarks*"

# Run only message buffer strategy benchmarks
dotnet run -c Release --filter "*MessageBufferStrategyBenchmarks*"

# Run specific message size
dotnet run -c Release --filter "*MessageSize=64*"
```

## Notes

### Measurement Environment

- All benchmarks use `tcp://127.0.0.1` transport (localhost loopback)
- Concurrent mode simulates realistic producer/consumer scenarios
- Results represent steady-state performance after warmup
- BenchmarkDotNet's ShortRun job provides statistically valid measurements with reduced runtime

### Limitations and Considerations

- `tcp://127.0.0.1` loopback transport was used; actual network performance will vary based on network infrastructure
- Actual production performance depends on network characteristics, message patterns, and system load
- GC measurements reflect benchmark workload; application GC behavior depends on overall heap activity
- Latency measurements include both send and receive operations for 10K messages
- NonBlocking mode uses 1ms sleep interval; different sleep values would yield different results

### Interpreting Results

Performance ratios show relative performance where 1.00x is the baseline (slowest) within each test category. Lower mean times and higher throughput indicate better performance. Allocated memory and GC collections show memory management efficiency.

The benchmarks reflect the performance characteristics of different approaches rather than absolute "best" choices. Selection depends on specific application requirements, message patterns, and architectural constraints.

## Full Benchmark Data

For the complete BenchmarkDotNet output, see:
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.ReceiveModeBenchmarks-report-github.md`
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.MessageBufferStrategyBenchmarks-report-github.md`
