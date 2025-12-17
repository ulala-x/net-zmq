# Net.Zmq Performance Benchmarks

This document contains comprehensive performance benchmark results for Net.Zmq, focusing on receive mode comparisons and memory strategy evaluations.

## Executive Summary

Net.Zmq provides multiple receive modes and memory strategies to accommodate different performance requirements and architectural patterns. This benchmark suite evaluates:

- **Receive Modes**: Blocking, NonBlocking, and Poller-based message reception
- **Memory Strategies**: ByteArray, ArrayPool, Message, and MessageZeroCopy approaches
- **Message Sizes**: 64 bytes (small), 1500 bytes (MTU-sized), and 65KB (large)

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
| **ArrayPool** | 2.612 ms | 261.17 ns | 3.83M | 1.96 Gbps | - | 1.08 KB | 0.99x |
| **ByteArray** | 2.654 ms | 265.43 ns | 3.77M | 1.93 Gbps | 3.91 | 1719.08 KB | 1.00x |
| **Message** | 5.415 ms | 541.49 ns | 1.85M | 0.95 Gbps | - | 625.34 KB | 2.04x |
| **MessageZeroCopy** | 6.371 ms | 637.07 ns | 1.57M | 0.80 Gbps | - | 625.33 KB | 2.40x |

#### 1500-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **Message** | 11.209 ms | 1.12 μs | 892.15K | 10.71 Gbps | - | 625.35 KB | 0.89x |
| **ArrayPool** | 11.743 ms | 1.17 μs | 851.58K | 10.22 Gbps | - | 3.03 KB | 0.93x |
| **ByteArray** | 12.573 ms | 1.26 μs | 795.36K | 9.54 Gbps | 78.13 | 29844.1 KB | 1.00x |
| **MessageZeroCopy** | 14.443 ms | 1.44 μs | 692.37K | 8.31 Gbps | - | 625.34 KB | 1.15x |

#### 65KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **MessageZeroCopy** | 131.953 ms | 13.20 μs | 75.78K | 4.63 GB/s | - | - | 625.67 KB | 0.81x |
| **Message** | 132.809 ms | 13.28 μs | 75.30K | 4.60 GB/s | - | - | 625.67 KB | 0.81x |
| **ArrayPool** | 157.573 ms | 15.76 μs | 63.46K | 3.87 GB/s | - | - | 65.38 KB | 0.96x |
| **ByteArray** | 163.748 ms | 16.37 μs | 61.07K | 3.73 GB/s | 3333.33 | 1000 | 1280469.54 KB | 1.00x |

### Performance and GC Analysis

**Small Messages (64B)**: Performance differences are modest across strategies. ArrayPool and ByteArray achieve highest throughput (3.77-3.83M msg/sec) with ArrayPool eliminating GC allocations. Message and MessageZeroCopy show 2.0-2.4x slower performance, likely due to native interop overhead being proportionally higher for small payloads.

**Medium Messages (1500B)**: Performance converges across strategies (692-892K msg/sec). ByteArray begins showing GC pressure with 78 Gen0 collections per 10K messages. ArrayPool, Message, and MessageZeroCopy maintain zero GC collections. The 1500-byte size approximates Ethernet MTU, representing a common message size in network applications.

**Large Messages (65KB)**: ByteArray strategy triggers significant garbage collection with 3333 Gen0 and 1000 Gen1 collections, allocating 1.28GB for 10K messages. All pool-based and native strategies maintain zero GC collections. MessageZeroCopy and Message achieve the highest throughput (75.30-75.78K msg/sec), while performance differences between strategies narrow to 0.81-1.00x relative range.

**GC Pattern Transition**: The transition from minimal to significant GC pressure occurs around the 1500-byte message size. Below this threshold, all strategies show manageable GC behavior. Above it, ByteArray's allocation cost becomes increasingly significant.

**Memory Allocation**: ArrayPool demonstrates the lowest overall allocation (1.08-65.38 KB across all sizes). ByteArray allocation scales linearly with message size and count. Message and MessageZeroCopy maintain consistent allocation (~625 KB) independent of message size.

### Memory Strategy Selection Considerations

When choosing a memory strategy, consider:

**Message Size Distribution**:
- For small messages (<1500B), performance differences are modest and GC pressure is manageable across all strategies
- For large messages (>1500B), ByteArray generates substantial GC pressure
- ArrayPool and native strategies maintain zero GC pressure regardless of message size

**GC Sensitivity**:
- Applications sensitive to GC pauses may prefer ArrayPool, Message, or MessageZeroCopy
- Applications with infrequent messaging or small messages may find ByteArray acceptable
- High-throughput applications benefit from GC-free strategies

**Code Complexity**:
- ByteArray offers the simplest implementation with automatic memory management
- ArrayPool requires explicit Rent/Return calls and buffer lifecycle tracking
- Message provides native integration with moderate complexity
- MessageZeroCopy requires unmanaged memory management and free callbacks

**Interop Overhead**:
- For small messages, managed strategies (ByteArray, ArrayPool) show lower overhead
- For large messages, native strategies (Message, MessageZeroCopy) can avoid managed/unmanaged copying

**Performance Requirements**:
- When throughput is critical and messages are small, ByteArray or ArrayPool are effective
- When throughput is critical and messages are large, Message or MessageZeroCopy reduce GC impact
- When latency consistency matters, GC-free strategies provide more predictable timing

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
