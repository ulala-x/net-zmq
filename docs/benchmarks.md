# Net.Zmq Performance Benchmarks

This document contains comprehensive performance benchmark results for Net.Zmq, focusing on receive mode comparisons and memory strategy evaluations.

## Executive Summary

Net.Zmq provides multiple receive modes and memory strategies to accommodate different performance requirements and architectural patterns. This benchmark suite evaluates:

- **Receive Modes**: Blocking, NonBlocking, and Poller-based message reception
- **Memory Strategies**: ByteArray, ArrayPool, Message, and MessageZeroCopy approaches
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

#### Blocking Mode - I/O Blocking Pattern

**API**: `socket.Recv()`

**Internal Mechanism**:
1. Calls `recv()` syscall, transitioning from user space to kernel space
2. Thread enters sleep state in kernel's wait queue
3. When data arrives → network hardware triggers interrupt
4. Kernel moves thread to ready queue
5. Scheduler wakes thread and execution resumes

**Characteristics**:
- Simplest implementation with deterministic waiting
- **CPU usage: 0% while waiting** (thread is asleep in kernel)
- Kernel efficiently wakes thread exactly when needed
- One thread per socket required

#### Poller Mode - Reactor Pattern (I/O Multiplexing)

**API**: `zmq_poll()`

**Internal Mechanism**:
1. Calls `zmq_poll(sockets, timeout)` which internally uses OS multiplexing APIs:
   - Linux: `epoll_wait()`
   - BSD/macOS: `kqueue()`
   - Windows: `select()` or IOCP
2. Kernel monitors multiple sockets simultaneously
3. Any socket event → kernel immediately returns control
4. Indicates which sockets have events ready

**Characteristics**:
- Event-driven architecture monitoring multiple sockets with single thread
- **CPU usage: 0% while waiting** (kernel-level blocking)
- Kernel uses hardware interrupts to detect events efficiently
- Slightly more memory overhead for polling infrastructure

#### NonBlocking Mode - Polling Pattern (Busy-waiting)

**API**: `socket.TryRecv()`

**Internal Mechanism**:
1. Repeated loop in user space
2. `TryRecv()` checks for messages (internally returns `EAGAIN`/`EWOULDBLOCK` if none available)
3. Returns immediately with `false` if no message
4. User code calls `Thread.Sleep(1ms)` before retry
5. Loop continues without kernel assistance

**Characteristics**:
- **No kernel-level waiting** - all polling happens in user space
- `Thread.Sleep(1ms)` reduces CPU usage but adds latency overhead (1.3-1.7x slower)
- **Not recommended for production** due to poor performance

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

**Recommendation**: NonBlocking mode is not recommended for production use due to poor performance (25-86% slower). Use Poller for most scenarios (simplest API with best overall performance) or Blocking for single-socket applications.

### Receive Mode Selection Considerations

When choosing a receive mode, consider:

**Recommended Approaches**:
- **Single Socket**: Use **Blocking** mode for simplicity and best performance
- **Multiple Sockets**: Use **Poller** mode to monitor multiple sockets with a single thread
- Both modes provide optimal CPU efficiency (0% when idle) and low latency

**NonBlocking Mode Limitations**:
- **Not recommended for production** due to poor performance (1.2-2.4x slower than Blocking/Poller)
- Thread.Sleep(1ms) adds latency overhead
- Only consider NonBlocking if you must integrate with an existing polling loop where you cannot use Blocking or Poller

**Performance Characteristics**:
- Blocking and Poller deliver similar performance (within 5% for most cases)
- Both use kernel-level waiting that wakes threads immediately when messages arrive
- NonBlocking uses user-space polling which is inherently less efficient

## Memory Strategy Benchmarks

### How Each Strategy Works

**ByteArray (`new byte[]`)**: Allocates a new byte array for each message. Simple and straightforward, but creates garbage collection pressure proportional to message size and frequency.

**ArrayPool (`ArrayPool<byte>.Shared`)**: Rents buffers from a shared pool and returns them after use. Reduces GC allocations by reusing memory, though requires manual return management.

**Message (`zmq_msg_t`)**: Uses libzmq's native message structure, which manages memory internally. The .NET wrapper marshals data between native and managed memory as needed.

**MessageZeroCopy (`Marshal.AllocHGlobal`)**: Allocates unmanaged memory directly and transfers ownership to libzmq via a free callback. Provides zero-copy semantics but requires careful lifecycle management.

### Understanding Memory Benchmark Metrics

In addition to the [standard benchmark metrics](#understanding-benchmark-metrics), memory strategy benchmarks include:

| Column | Description |
|--------|-------------|
| **Gen0** | Number of Generation 0 garbage collections during the benchmark (lower is better) |
| **Gen1** | Number of Generation 1 garbage collections (only appears for large allocations) |

**GC Impact**: Higher Gen0/Gen1 values indicate more GC pressure, which can cause performance degradation and unpredictable latency spikes. A dash (-) means zero collections occurred.

### Performance Results

All tests use Poller mode for reception.

#### 64-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 2.428 ms | 242.8 ns | 4.12M | 2.11 Gbps | - | 1.85 KB | 0.99x |
| **Message** | 4.279 ms | 427.9 ns | 2.34M | 1.20 Gbps | - | 168.54 KB | 1.76x |
| **MessageZeroCopy** | 5.917 ms | 591.7 ns | 1.69M | 0.87 Gbps | - | 168.61 KB | 2.43x |
| **ByteArray** | 2.438 ms | 243.8 ns | 4.10M | 2.10 Gbps | 9.77 | 9860.2 KB | 1.00x |

#### 512-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 6.376 ms | 637.6 ns | 1.57M | 6.43 Gbps | - | 2.04 KB | 0.95x |
| **Message** | 8.187 ms | 818.7 ns | 1.22M | 5.01 Gbps | - | 168.72 KB | 1.22x |
| **ByteArray** | 6.707 ms | 670.7 ns | 1.49M | 6.11 Gbps | 48.83 | 50017.99 KB | 1.00x |
| **MessageZeroCopy** | 13.372 ms | 1.34 μs | 748K | 3.07 Gbps | - | 168.80 KB | 1.99x |

#### 1024-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 9.021 ms | 902.1 ns | 1.11M | 9.05 Gbps | - | 2.24 KB | 1.01x |
| **Message** | 9.739 ms | 973.9 ns | 1.03M | 8.39 Gbps | - | 168.92 KB | 1.09x |
| **ByteArray** | 8.973 ms | 897.3 ns | 1.11M | 9.10 Gbps | 97.66 | 100033.11 KB | 1.00x |
| **MessageZeroCopy** | 14.612 ms | 1.46 μs | 684K | 5.60 Gbps | - | 169.01 KB | 1.63x |

#### 65KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **Message** | 119.164 ms | 11.92 μs | 83.93K | 5.13 GB/s | - | - | 171.47 KB | 0.84x |
| **MessageZeroCopy** | 124.720 ms | 12.47 μs | 80.18K | 4.90 GB/s | - | - | 171.56 KB | 0.88x |
| **ArrayPool** | 142.814 ms | 14.28 μs | 70.02K | 4.28 GB/s | - | - | 4.78 KB | 1.01x |
| **ByteArray** | 141.652 ms | 14.17 μs | 70.60K | 4.31 GB/s | 3906.25 | 781.25 | 4001252.47 KB | 1.00x |

### Performance and GC Analysis

**Small Messages (64B)**: ArrayPool delivers the best performance (2.428 ms, 4.12M msg/sec) with near-zero GC pressure (1.85 KB allocated). ByteArray is comparable in speed (2.438 ms, 4.10M msg/sec) but generates significant GC pressure (9.77 Gen0 collections, 9860.2 KB allocated). Message (4.279 ms, 1.76x slower) and MessageZeroCopy (5.917 ms, 2.43x slower) show substantial overhead due to native interop costs being proportionally high for small payloads.

**Medium Messages (512B)**: ArrayPool remains fastest (6.376 ms, 1.57M msg/sec, 0.95x) with minimal allocation (2.04 KB). ByteArray is slightly slower (6.707 ms, 1.49M msg/sec) but shows increasing GC pressure (48.83 Gen0 collections, 50 MB allocated). Message (8.187 ms, 1.22x) performs reasonably, while MessageZeroCopy (13.372 ms, 1.99x) shows unexpected overhead.

**Medium-Large Messages (1KB)**: Performance converges between ArrayPool (9.021 ms, 1.11M msg/sec, 1.01x) and ByteArray (8.973 ms, 1.11M msg/sec, 1.00x), with ByteArray now showing substantial GC pressure (97.66 Gen0 collections, 100 MB allocated). Message (9.739 ms, 1.09x) becomes competitive, while MessageZeroCopy (14.612 ms, 1.63x) still lags.

**Large Messages (65KB)**: Native strategies dominate - Message achieves best performance (119.164 ms, 83.93K msg/sec, 0.84x = 16% faster than baseline), followed by MessageZeroCopy (124.720 ms, 80.18K msg/sec, 0.88x = 12% faster). ArrayPool (142.814 ms, 70.02K msg/sec, 1.01x) and ByteArray (141.652 ms, 70.60K msg/sec, 1.00x) are similar in speed but ByteArray triggers massive GC pressure (3906 Gen0 + 781 Gen1 collections, 4 GB allocated).

**GC Pattern Transition**: ArrayPool and native strategies maintain zero GC collections across all message sizes. ByteArray shows exponential GC pressure growth: 9.77 Gen0 at 64B → 48.83 Gen0 at 512B → 97.66 Gen0 at 1KB → 3906 Gen0 + 781 Gen1 at 65KB.

**Memory Allocation**: ArrayPool demonstrates exceptional efficiency (1.85-4.78 KB total allocation across all sizes - 99.8-99.99% reduction vs ByteArray). Message and MessageZeroCopy maintain consistent ~170 KB allocation regardless of message size (99.95% reduction vs ByteArray at 65KB).

### Memory Strategy Selection Considerations

When choosing a memory strategy, consider:

**Message Size Based Recommendations**:
- **Small messages (≤512B)**: Use **`ArrayPool<byte>.Shared`** - fastest performance (1-5% faster than ByteArray) with 99.8-99.99% less allocation
- **Large messages (≥64KB)**: Use **`Message`** or **`MessageZeroCopy`** - 12-16% faster with 99.95% less allocation
- **Transition zone (1KB)**: Both ArrayPool and Message perform similarly; choose based on code simplicity vs GC requirements

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

**MessageZeroCopy Usage Pattern**:
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
- Applications sensitive to GC pauses should prefer ArrayPool (small messages) or MessageZeroCopy (large messages)
- Applications with infrequent messaging or small messages may find ByteArray acceptable
- High-throughput applications benefit from GC-free strategies (ArrayPool, Message, MessageZeroCopy)

**Code Complexity**:
- **ByteArray**: Simplest implementation with automatic memory management
- **ArrayPool**: Requires explicit Rent/Return calls and buffer lifecycle tracking
- **Message**: Native integration with moderate complexity
- **MessageZeroCopy**: Requires unmanaged memory management and free callbacks

**Performance Trade-offs**:
- **Small messages (≤ 512B)**: Managed strategies (ByteArray, ArrayPool) have lower overhead
- **Large messages (> 512B)**: MessageZeroCopy delivers optimal performance through zero-copy semantics
- **Consistency**: GC-free strategies (ArrayPool, MessageZeroCopy) provide more predictable timing

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

# Run only memory strategy benchmarks
dotnet run -c Release --filter "*MemoryStrategyBenchmarks*"

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
- NonBlocking mode uses 10ms sleep interval; different sleep values would yield different results

### Interpreting Results

Performance ratios show relative performance where 1.00x is the baseline (slowest) within each test category. Lower mean times and higher throughput indicate better performance. Allocated memory and GC collections show memory management efficiency.

The benchmarks reflect the performance characteristics of different approaches rather than absolute "best" choices. Selection depends on specific application requirements, message patterns, and architectural constraints.

## Full Benchmark Data

For the complete BenchmarkDotNet output, see:
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.ReceiveModeBenchmarks-report-github.md`
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.MemoryStrategyBenchmarks-report-github.md`
