[![English](https://img.shields.io/badge/lang:en-red.svg)](benchmarks.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](benchmarks.ko.md)

# Net.Zmq Performance Benchmarks

This document contains comprehensive performance benchmark results for Net.Zmq, focusing on receive mode comparisons and memory strategy evaluations.

## Executive Summary

Net.Zmq provides multiple receive modes and memory strategies to accommodate different performance requirements and architectural patterns. This benchmark suite evaluates:

- **Receive Modes**: PureBlocking, BlockingWithBatch, NonBlocking, and Poller-based message reception
- **Message Buffer Strategies**: ByteArray (baseline), ArrayPool (recommended), Message, MessageZeroCopy (special cases only)
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

### GC Optimization Settings

The benchmarks use the following GC settings for maximum throughput:

**Project Configuration (csproj)**:
```xml
<ServerGarbageCollection>true</ServerGarbageCollection>
```

**Runtime Configuration (code)**:
```csharp
if (GCSettings.IsServerGC)
{
    GCSettings.LatencyMode = GCLatencyMode.Batch;
}
```

**Server GC**:
- Designed for high throughput in multi-threaded environments
- Allocates separate heaps for each processor for concurrent collection
- Larger heap sizes and less frequent GC compared to Workstation GC

**Batch Latency Mode**:
- Mode optimized for maximum throughput
- Allows application pauses during Gen2 GC
- Reduces GC frequency to improve overall throughput
- Not recommended for interactive applications (latency spikes possible)

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
| **PureBlocking** | 2.418 ms | 241.8 ns | 4.14M | 2.12 Gbps | 340 B | 1.00x |
| **BlockingWithBatch** | 2.374 ms | 237.4 ns | 4.21M | 2.16 Gbps | 340 B | 0.98x |
| **Poller** | 2.380 ms | 238.0 ns | 4.20M | 2.15 Gbps | 460 B | 0.98x |
| NonBlocking (Sleep 1ms) | 3.468 ms | 346.8 ns | 2.88M | 1.48 Gbps | 339 B | 1.43x |

#### 512-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 5.289 ms | 528.9 ns | 1.89M | 7.74 Gbps | 344 B | 1.00x |
| **BlockingWithBatch** | 5.493 ms | 549.3 ns | 1.82M | 7.46 Gbps | 344 B | 1.04x |
| **Poller** | 5.318 ms | 531.8 ns | 1.88M | 7.70 Gbps | 467 B | 1.01x |
| NonBlocking (Sleep 1ms) | 6.819 ms | 681.9 ns | 1.47M | 6.01 Gbps | 344 B | 1.29x |

#### 1024-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 8.263 ms | 826.3 ns | 1.21M | 9.91 Gbps | 352 B | 1.00x |
| **BlockingWithBatch** | 8.066 ms | 806.6 ns | 1.24M | 10.16 Gbps | 352 B | 0.98x |
| **Poller** | 8.367 ms | 836.7 ns | 1.20M | 9.79 Gbps | 472 B | 1.01x |
| NonBlocking (Sleep 1ms) | 10.220 ms | 1.02 μs | 978.46K | 8.02 Gbps | 352 B | 1.24x |

#### 65KB Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 148.122 ms | 14.81 μs | 67.51K | 4.12 GB/s | 352 B | 1.00x |
| **BlockingWithBatch** | 143.933 ms | 14.39 μs | 69.48K | 4.24 GB/s | 688 B | 0.97x |
| **Poller** | 144.763 ms | 14.48 μs | 69.08K | 4.22 GB/s | 640 B | 0.98x |
| NonBlocking (Sleep 1ms) | 359.381 ms | 35.94 μs | 27.83K | 1.70 GB/s | 1360 B | 2.43x |

### Performance Analysis

**PureBlocking, BlockingWithBatch, and Poller Comparison**: All three blocking-based modes show nearly identical performance across all message sizes (97-104% relative performance). For small messages (64B), BlockingWithBatch and Poller are about 2% faster than PureBlocking. For medium messages (512B-1KB), all three modes perform nearly equally. For large messages (65KB), BlockingWithBatch is 3% faster. All blocking-based modes use kernel-level waiting mechanisms that efficiently wake threads when messages arrive.

**NonBlocking Performance**: NonBlocking mode with `Thread.Sleep(1ms)` is consistently slower than blocking-based modes (1.24-2.43x slower) due to:
1. User-space polling with `Recv(RecvFlags.DontWait)` has overhead compared to kernel-level blocking
2. Thread.Sleep() adds latency even with minimal 1ms sleep interval
3. Blocking-based modes use efficient kernel mechanisms (`recv()` syscall and `zmq_poll()`) that wake threads immediately when messages arrive

**Message Size Impact**: The Sleep overhead is more pronounced with large messages. For small messages (64B), NonBlocking is 1.43x slower, while for large messages (65KB) it's 2.43x slower.

**Recommendation**: NonBlocking mode has relatively lower performance (24-143% slower), so there's no need to use it. Use Poller for most scenarios (multi-socket support with best overall performance) or PureBlocking/BlockingWithBatch for single-socket applications.

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

**ByteArray (`new byte[]`)**: Allocates a new byte array for each message. Simple and straightforward, but creates garbage collection pressure proportional to message size and frequency. Used as a baseline for performance comparisons.

**ArrayPool (`ArrayPool<byte>.Shared`)**: Rents buffers from a shared pool and returns them after use. Significantly reduces GC allocations by reusing memory, providing the best performance for small messages. Requires manual return management, but is the recommended strategy for most cases.

**Message (`zmq_msg_t`)**: Uses libzmq's native message structure, which manages memory internally. The .NET wrapper marshals data between native and managed memory as needed. Shows comparable performance to ArrayPool for large messages.

**MessageZeroCopy (`Marshal.AllocHGlobal`)**: Allocates unmanaged memory directly and transfers ownership to libzmq via a free callback. In most cases, it is slower than Message. Should only be used in special cases when native memory already exists (e.g., integration with other native libraries).

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
| **ByteArray** | 2.409 ms | 240.89 ns | 4.15M | 2.13 Gbps | 3.91 | 1719.08 KB | 1.00x |
| **ArrayPool** | 2.723 ms | 272.34 ns | 3.67M | 1.88 Gbps | - | 1.08 KB | 1.13x |
| **Message** | 4.859 ms | 485.86 ns | 2.06M | 1.05 Gbps | - | 1406.58 KB | 2.02x |
| **MessageZeroCopy** | 5.926 ms | 592.64 ns | 1.69M | 0.86 Gbps | - | 1406.58 KB | 2.46x |

#### 512-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 5.708 ms | 570.83 ns | 1.75M | 7.18 Gbps | 23.44 | 10469.09 KB | 1.00x |
| **ArrayPool** | 5.355 ms | 535.52 ns | 1.87M | 7.65 Gbps | - | 1.52 KB | 0.94x |
| **Message** | 6.258 ms | 625.85 ns | 1.60M | 6.54 Gbps | - | 1406.59 KB | 1.10x |
| **MessageZeroCopy** | 6.886 ms | 688.59 ns | 1.45M | 5.95 Gbps | - | 1406.59 KB | 1.21x |

#### 1024-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 8.637 ms | 863.68 ns | 1.16M | 9.48 Gbps | 46.88 | 20469.09 KB | 1.00x |
| **ArrayPool** | 7.820 ms | 782.02 ns | 1.28M | 10.48 Gbps | - | 2.04 KB | 0.91x |
| **Message** | 8.495 ms | 849.46 ns | 1.18M | 9.64 Gbps | - | 1406.59 KB | 0.98x |
| **MessageZeroCopy** | 10.678 ms | 1.07 μs | 936.47K | 7.67 Gbps | - | 1406.59 KB | 1.24x |

#### 65KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **ByteArray** | 170.14 ms | 17.01 μs | 58.77K | 3.59 GB/s | 3333.33 | 666.67 | 1280469.54 KB | 1.00x |
| **ArrayPool** | 160.17 ms | 16.02 μs | 62.44K | 3.81 GB/s | - | - | 65.29 KB | 0.94x |
| **Message** | 175.25 ms | 17.52 μs | 57.06K | 3.48 GB/s | - | - | 1406.82 KB | 1.03x |
| **MessageZeroCopy** | 164.90 ms | 16.49 μs | 60.64K | 3.70 GB/s | - | - | 1407.04 KB | 0.97x |

#### 128KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Gen2 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|------|-----------|-------|
| **ByteArray** | 1,259 ms | 125.91 μs | 7.94K | 0.97 GB/s | 51000 | 51000 | 51000 | 2.5 GB | 1.00x |
| **ArrayPool** | 342.74 ms | 34.27 μs | 29.18K | 3.56 GB/s | - | - | - | 129.48 KB | 0.27x |
| **Message** | 375.43 ms | 37.54 μs | 26.64K | 3.25 GB/s | - | - | - | 1407.95 KB | 0.30x |
| **MessageZeroCopy** | 361.82 ms | 36.18 μs | 27.64K | 3.37 GB/s | - | - | - | 1407.95 KB | 0.29x |

#### 256KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Gen2 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|------|-----------|-------|
| **ByteArray** | 2,485 ms | 248.52 μs | 4.02K | 0.98 GB/s | 100000 | 100000 | 100000 | 5 GB | 1.00x |
| **ArrayPool** | 719.49 ms | 71.95 μs | 13.90K | 3.39 GB/s | - | - | - | 257.70 KB | 0.29x |
| **Message** | 698.36 ms | 69.84 μs | 14.32K | 3.50 GB/s | - | - | - | 1407.95 KB | 0.28x |
| **MessageZeroCopy** | 716.22 ms | 71.62 μs | 13.96K | 3.41 GB/s | - | - | - | 1407.95 KB | 0.29x |

### Performance and GC Analysis

**Small Messages (64B)**: ByteArray (2.409 ms, 4.15M msg/sec, 1.00x) is fastest but generates GC pressure (3.91 Gen0, 1719 KB allocated). **ArrayPool** (2.723 ms, 3.67M msg/sec, 1.13x) provides nearly equal performance with minimal memory allocation (1.08 KB) and is **recommended for small messages**. Message (4.859 ms, 2.02x) and MessageZeroCopy (5.926 ms, 2.46x) show substantial overhead due to native interop costs and are not recommended.

**Medium Messages (512B)**: **ArrayPool** (5.355 ms, 0.94x) is fastest, 6% faster than ByteArray (5.708 ms, 1.00x). ArrayPool is GC-free (0 Gen0) with minimal allocation (1.52 KB), while ByteArray shows increasing GC pressure (23.44 Gen0, 10.5 MB allocated). Message (6.258 ms, 1.10x) and MessageZeroCopy (6.886 ms, 1.21x) are slower.

**Medium-Large Messages (1KB)**: **ArrayPool** (7.820 ms, 0.91x) is fastest, 9% faster than ByteArray (8.637 ms, 1.00x). Message (8.495 ms, 0.98x) also shows competitive performance. MessageZeroCopy (10.678 ms, 1.24x) is 24% slower than Message and is not recommended. ByteArray shows substantial GC pressure (46.88 Gen0, 20 MB allocated).

**Large Messages (65KB)**: **ArrayPool** (160.17 ms, 0.94x) is fastest, 6% faster than ByteArray (170.14 ms, 1.00x). MessageZeroCopy (164.90 ms, 0.97x) and Message (175.25 ms, 1.03x) show similar performance. ByteArray triggers massive GC pressure (3333 Gen0, 1.25 GB allocated).

**Very Large Messages (128KB)**: ByteArray suffers extreme GC pressure (51,000 Gen0/1/2 collections each, 2.5 GB allocated) taking 1,259 ms. **ArrayPool** (342.74 ms, 0.27x) is fastest - **3.7x faster**. MessageZeroCopy (361.82 ms, 0.29x) and Message (375.43 ms, 0.30x) both perform over 3x faster, but ArrayPool is still faster.

**Extra Large Messages (256KB)**: ByteArray's GC pressure intensifies (100,000 Gen0/1/2 collections each, 5 GB allocated) taking 2,485 ms. **Message** (698.36 ms, 0.28x) is fastest - **3.6x faster**. ArrayPool (719.49 ms, 0.29x) and MessageZeroCopy (716.22 ms, 0.29x) show similar performance, all near Message.

**Large Object Heap (LOH) Impact**: In .NET, objects ≥85KB are allocated on the LOH, causing GC costs to skyrocket. This explains ByteArray's dramatic performance degradation at 128KB/256KB. ArrayPool, Message, and MessageZeroCopy strategies completely avoid this issue.

**GC Pattern**: ArrayPool, Message, and MessageZeroCopy maintain zero GC collections across all message sizes. ByteArray shows exponential GC pressure growth: 3.91 Gen0 at 64B → 23.44 Gen0 at 512B → 46.88 Gen0 at 1KB → 3333 Gen0 at 65KB → 51,000 Gen0/1/2 at 128KB → 100,000 Gen0/1/2 at 256KB.

**Memory Allocation**: ArrayPool is most efficient (1.08-258 KB total allocation), while Message also shows consistently low allocation (~1407 KB). ByteArray's allocation grows proportionally with message size (1.7MB at 64B → 5GB at 256KB).

### Message Buffer Strategy Selection Considerations

When choosing a message buffer strategy, consider:

**Message Size Based Recommendations**:
- **Small messages (≤1KB)**: **`ArrayPool<byte>.Shared`** recommended - best performance (6-13% faster), minimal GC, minimal memory allocation
- **Large messages (≥64KB)**: **`ArrayPool`** or **`Message`** - nearly equal performance, GC-free
- **Very large messages (≥128KB)**: **`ArrayPool`** or **`Message`** - 3.5x+ faster than ByteArray, LOH avoidance
- **Native memory integration**: **`MessageZeroCopy`** - only for special cases when native memory already exists

**One-Size-Fits-All Recommendation**:

If message sizes vary or are unpredictable, we recommend **`ArrayPool<byte>.Shared`**:

1. **Consistent Best Performance**: Provides best or near-best performance from small to very large messages
2. **GC-Free**: Zero GC pressure across all message sizes
3. **Minimal Memory Allocation**: Low memory allocation in the 1KB~260KB range
4. **LOH Avoidance**: Completely bypasses .NET LOH issues for 128KB+ messages
5. **Standard .NET API**: ArrayPool is a standard .NET API that integrates well with other libraries

ArrayPool requires manual return management, but provides the best performance and minimal memory usage in most cases.

**ArrayPool Usage Pattern** (Recommended):
```csharp
using Net.Zmq;
using System.Buffers;

// Send: Rent buffer from pool and send
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Copy data to buffer
    sourceData.CopyTo(buffer.AsSpan(0, size));
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    // Return to pool after use
    ArrayPool<byte>.Shared.Return(buffer);
}

// Receive: Rent buffer from pool and receive
var recvBuffer = ArrayPool<byte>.Shared.Rent(expectedSize);
try
{
    var received = socket.Recv(recvBuffer);
    // Process data...
    ProcessData(recvBuffer.AsSpan(0, received));
}
finally
{
    ArrayPool<byte>.Shared.Return(recvBuffer);
}
```

**Message Usage Pattern**:
```csharp
using Net.Zmq;

// Send: Use Message
using var sendMsg = new Message(dataSize);
sourceData.CopyTo(sendMsg.Data);
socket.Send(sendMsg);

// Receive: Use Message
using var recvMsg = new Message();
socket.Recv(recvMsg);
// Process data...
ProcessData(recvMsg.Data);
```

**MessageZeroCopy Usage Pattern** (Only when native memory already exists):
```csharp
using Net.Zmq;
using System.Runtime.InteropServices;

// Use only when native memory already exists
// Example: memory received from another native library
nint nativePtr = GetNativeMemoryFromSomewhereElse();

// Transfer ownership to libzmq (zero-copy)
using var message = new Message(nativePtr, dataSize, ptr =>
{
    // Called when libzmq is done
    FreeNativeMemory(ptr);
});

socket.Send(message);

// Note: In general cases, ArrayPool or Message are faster and simpler
```

**GC Sensitivity**:
- High-throughput applications sensitive to GC pauses should use **ArrayPool** (best for all sizes, GC-free)
- Infrequent messaging or prototyping stages may find ByteArray simple and acceptable
- Real-time or latency-critical applications require GC-free strategies (ArrayPool, Message)

**Code Complexity**:
- **ByteArray**: Simplest with automatic memory management, but generates GC pressure
- **ArrayPool**: Requires explicit Rent/Return calls, but provides best performance/memory efficiency
- **Message**: Can be used simply with using pattern, similar performance to ArrayPool for large messages
- **MessageZeroCopy**: Requires complex memory management, slower than Message in most cases (only for special cases)

**Performance Trade-offs Summary**:
- **Small messages (≤ 1KB)**: ArrayPool is best (6-13% faster than ByteArray)
- **Large messages (≥ 64KB)**: ArrayPool and Message are nearly equal, both significantly faster than ByteArray
- **Very large messages (≥ 128KB)**: ArrayPool and Message are 3.5x+ faster than ByteArray
- **Consistency**: ArrayPool provides best or near-best performance across all sizes

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
