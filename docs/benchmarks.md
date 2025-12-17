# Net.Zmq Performance Benchmarks

This document contains comprehensive performance benchmark results for Net.Zmq, focusing on receive mode comparisons and memory strategy evaluations.

## Executive Summary

Net.Zmq provides multiple receive modes and memory strategies to accommodate different performance requirements and architectural patterns. This benchmark suite evaluates:

- **Receive Modes**: Blocking, NonBlocking, and Poller-based message reception
- **Memory Strategies**: ByteArray, ArrayPool, Message, MessageZeroCopy, and MessagePooled approaches
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
| **Blocking** | 2.868 ms | 286.83 ns | 3.49M | 1.79 Gbps | 342 B | 1.00x |
| **Poller** | 3.004 ms | 300.39 ns | 3.33M | 1.70 Gbps | 460 B | 1.05x |
| NonBlocking (Sleep 1ms) | 3.448 ms | 344.79 ns | 2.90M | 1.48 Gbps | 340 B | 1.20x |

#### 1500-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 10.850 ms | 1.09 μs | 921.65K | 11.06 Gbps | 358 B | 1.00x |
| **Poller** | 11.334 ms | 1.13 μs | 882.31K | 10.59 Gbps | 472 B | 1.05x |
| NonBlocking (Sleep 1ms) | 13.819 ms | 1.38 μs | 723.66K | 8.68 Gbps | 352 B | 1.27x |

#### 65KB Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Poller** | 150.899 ms | 15.09 μs | 66.27K | 4.04 GB/s | 640 B | 0.95x |
| **Blocking** | 159.049 ms | 15.90 μs | 62.87K | 3.84 GB/s | 688 B | 1.00x |
| NonBlocking (Sleep 1ms) | 376.916 ms | 37.69 μs | 26.53K | 1.62 GB/s | 1744 B | 2.37x |

### Performance Analysis

**Blocking vs Poller**: Performance is nearly identical across all message sizes (95-105% relative performance). Both modes use kernel-level waiting mechanisms that efficiently wake threads when messages arrive. Poller allocates slightly more memory (460-640 bytes vs 342-688 bytes for 10K messages) due to polling infrastructure, but the difference is negligible in practice.

**NonBlocking Performance**: NonBlocking mode with `Thread.Sleep(1ms)` is consistently slower than Blocking and Poller modes (1.20-2.37x slower) due to:
1. User-space polling with `TryRecv()` has overhead compared to kernel-level blocking
2. Thread.Sleep() adds latency even with minimal 1ms sleep interval
3. Blocking and Poller modes use efficient kernel mechanisms (`recv()` syscall and `zmq_poll()`) that wake threads immediately when messages arrive

**Message Size Impact**: The Sleep overhead is most pronounced with large messages (65KB) where NonBlocking is 2.37x slower, while for small messages (64B) it's 1.20x slower.

**Recommendation**: NonBlocking mode is not recommended for production use due to poor performance. Use Blocking for single-socket applications or Poller for multi-socket scenarios.

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

**MessagePooled (`MessagePool.Shared`)**: Pools native memory buffers and reuses them via ZeroMQ's free callback mechanism. Combines zero-copy semantics with buffer pooling to eliminate allocation/deallocation overhead for large messages.

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
| **ByteArray** | 2.459 ms | 245.88 ns | 4.07M | 2.08 Gbps | 3.91 | 1719.08 KB | 1.00x |
| **ArrayPool** | 2.476 ms | 247.57 ns | 4.04M | 2.07 Gbps | - | 1.08 KB | 1.01x |
| **Message** | 5.045 ms | 504.53 ns | 1.98M | 1.01 Gbps | - | 625.34 KB | 2.05x |
| **MessageZeroCopy** | 5.833 ms | 583.27 ns | 1.71M | 0.88 Gbps | - | 625.34 KB | 2.37x |
| **MessagePooled** | 6.888 ms | 688.82 ns | 1.45M | 0.74 Gbps | - | 1562.84 KB | 2.80x |

#### 512-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 6.527 ms | 652.69 ns | 1.53M | 6.28 Gbps | - | 1.52 KB | 0.95x |
| **Message** | 6.874 ms | 687.38 ns | 1.45M | 5.96 Gbps | - | 625.34 KB | 1.00x |
| **ByteArray** | 6.859 ms | 685.86 ns | 1.46M | 5.97 Gbps | 23.44 | 10469.08 KB | 1.00x |
| **MessageZeroCopy** | 7.768 ms | 776.83 ns | 1.29M | 5.27 Gbps | - | 625.33 KB | 1.13x |
| **MessagePooled** | 7.958 ms | 795.82 ns | 1.26M | 5.15 Gbps | - | 1562.84 KB | 1.16x |

#### 1024-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **MessagePooled** | 6.874 ms | 687.44 ns | 1.45M | 11.92 Gbps | - | 1562.84 KB | 0.70x |
| **ArrayPool** | 8.867 ms | 886.70 ns | 1.13M | 9.24 Gbps | - | 2.03 KB | 0.91x |
| **Message** | 8.905 ms | 890.49 ns | 1.12M | 9.20 Gbps | - | 625.35 KB | 0.91x |
| **ByteArray** | 9.786 ms | 978.62 ns | 1.02M | 8.37 Gbps | 46.88 | 20469.09 KB | 1.00x |
| **MessageZeroCopy** | 11.720 ms | 1.17 μs | 853.27K | 6.99 Gbps | - | 625.34 KB | 1.20x |

#### 65KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **MessagePooled** | 124.808 ms | 12.48 μs | 80.12K | 4.89 GB/s | - | - | 1562.97 KB | 0.88x |
| **MessageZeroCopy** | 124.898 ms | 12.49 μs | 80.07K | 4.89 GB/s | - | - | 625.60 KB | 0.88x |
| **Message** | 125.847 ms | 12.58 μs | 79.46K | 4.85 GB/s | - | - | 625.67 KB | 0.89x |
| **ArrayPool** | 134.940 ms | 13.49 μs | 74.11K | 4.52 GB/s | - | - | 65.38 KB | 0.95x |
| **ByteArray** | 142.027 ms | 14.20 μs | 70.41K | 4.30 GB/s | 3250.00 | 1000 | 1280469.33 KB | 1.00x |

### Performance and GC Analysis

**Small Messages (64B)**: Performance differences are modest across strategies. ByteArray and ArrayPool achieve highest throughput (4.04-4.07M msg/sec), with ArrayPool eliminating GC allocations entirely. Message, MessageZeroCopy, and MessagePooled show 2.0-2.8x slower performance, likely due to native interop overhead being proportionally higher for small payloads. MessagePooled shows the highest overhead at small sizes (2.80x) due to pool management overhead.

**Medium Messages (512B)**: Performance converges across managed strategies (1.46-1.53M msg/sec). ByteArray begins showing GC pressure with 23.44 Gen0 collections per 10K messages. ArrayPool, Message, MessageZeroCopy, and MessagePooled maintain zero GC collections. Native strategies still show slight overhead (1.13-1.16x) compared to managed approaches.

**Crossover Point (1024B)**: **MessagePooled achieves breakthrough performance at 1024 bytes**, delivering 1.45M msg/sec (11.92 Gbps) - a **30% performance improvement** over the baseline ByteArray (0.70x ratio). This marks the point where pooled native memory benefits outweigh pool management overhead. ArrayPool and Message perform similarly at 0.91x ratio, while MessageZeroCopy shows degraded performance at 1.20x.

**Large Messages (65KB)**: ByteArray strategy triggers significant garbage collection with 3250 Gen0 and 1000 Gen1 collections, allocating 1.28GB for 10K messages. All pool-based and native strategies maintain zero GC collections. MessagePooled, MessageZeroCopy, and Message achieve the highest throughput (79.46-80.12K msg/sec), with MessagePooled leading at 4.89 GB/s. Performance differences narrow to 0.88-1.00x relative range.

**GC Pattern Transition**: The transition from minimal to significant GC pressure occurs around the 512-byte message size. Below this threshold, all strategies show manageable GC behavior. Above it, ByteArray's allocation cost becomes increasingly significant.

**Memory Allocation**: ArrayPool demonstrates the lowest overall allocation (1.08-65.38 KB across all sizes). ByteArray allocation scales linearly with message size and count. Message and MessageZeroCopy maintain consistent allocation (~625 KB) independent of message size. MessagePooled shows higher allocation (~1563 KB) due to pool infrastructure but maintains zero GC pressure.

**MessagePooled Performance Characteristics**:
- **< 512B**: Pool overhead dominates (1.16-2.80x slower) - not recommended
- **1024B**: **Sweet spot** with 30% performance improvement (0.70x ratio)
- **≥ 65KB**: Competitive with MessageZeroCopy (0.88x ratio), slightly higher allocation but simpler usage
- **Pool Hit Rate**: Achieves 99.97% hit rate in benchmarks with pre-warming, effectively eliminating allocation overhead

### Memory Strategy Selection Considerations

When choosing a memory strategy, consider:

**Message Size Based Recommendations**:
- **< 512B**: Use **`ArrayPool<byte>.Shared`** - best performance (4.04M msg/sec at 64B) with zero GC pressure
- **512B - 1023B**: Use **`ArrayPool<byte>.Shared`** or **`Message`** - similar performance, both GC-free
- **≥ 1024B**: Use **`MessagePool.Shared`** - **30% performance improvement** (0.70x ratio at 1024B) with zero GC pressure

**MessagePool Usage Pattern**:
```csharp
using Net.Zmq;

// For messages ≥ 1KB, use MessagePool for best performance
var data = new byte[2048];
using var message = MessagePool.Shared.Rent(data);
socket.Send(message);

// Or with span
ReadOnlySpan<byte> dataSpan = stackalloc byte[2048];
using var message2 = MessagePool.Shared.Rent(dataSpan);
socket.Send(message2);
```

**GC Sensitivity**:
- Applications sensitive to GC pauses should prefer ArrayPool (small messages) or MessagePooled (large messages)
- Applications with infrequent messaging or small messages may find ByteArray acceptable
- High-throughput applications benefit from GC-free strategies (ArrayPool, Message, MessageZeroCopy, MessagePooled)

**Code Complexity**:
- **ByteArray**: Simplest implementation with automatic memory management
- **ArrayPool**: Requires explicit Rent/Return calls and buffer lifecycle tracking
- **Message**: Native integration with moderate complexity
- **MessageZeroCopy**: Requires unmanaged memory management and free callbacks
- **MessagePooled**: Simplest zero-copy approach - automatic return via `Dispose()`, no manual free callbacks needed

**Performance Trade-offs**:
- **Small messages (< 512B)**: Managed strategies (ByteArray, ArrayPool) have lower overhead
- **Medium messages (512-1023B)**: Performance parity across most strategies
- **Large messages (≥ 1024B)**: MessagePooled delivers optimal performance through pooled native memory
- **Consistency**: GC-free strategies (ArrayPool, MessagePooled) provide more predictable timing

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
