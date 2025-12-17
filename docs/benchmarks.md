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
| **Blocking** | 2.324 ms | 232.37 ns | 4.30M | 2.20 Gbps | 203 B | 1.00x |
| **Poller** | 2.414 ms | 241.36 ns | 4.14M | 2.12 Gbps | 323 B | 1.04x |
| NonBlocking (Sleep 1ms) | 3.366 ms | 336.57 ns | 2.97M | 1.52 Gbps | 203 B | 1.45x |

#### 1500-Byte Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 10.238 ms | 1.02 μs | 976.77K | 11.72 Gbps | 212 B | 1.00x |
| **Poller** | 10.652 ms | 1.07 μs | 938.82K | 11.27 Gbps | 332 B | 1.04x |
| NonBlocking (Sleep 1ms) | 13.377 ms | 1.34 μs | 747.53K | 8.97 Gbps | 212 B | 1.31x |

#### 65KB Messages

| Mode | Mean | Latency | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Poller** | 152.737 ms | 15.27 μs | 65.47K | 4.00 GB/s | 504 B | 0.88x |
| **Blocking** | 174.529 ms | 17.45 μs | 57.30K | 3.50 GB/s | 445 B | 1.01x |
| NonBlocking (Sleep 1ms) | 295.771 ms | 29.58 μs | 33.81K | 2.06 GB/s | 568 B | 1.70x |

### Performance Analysis

**Blocking vs Poller**: Performance is nearly identical across all message sizes (96-102% relative performance). Both modes use kernel-level waiting mechanisms that efficiently wake threads when messages arrive. Poller allocates slightly more memory (323-504 bytes vs 203-384 bytes for 10K messages) due to polling infrastructure, but the difference is negligible in practice.

**NonBlocking Performance**: NonBlocking mode with `Thread.Sleep(1ms)` is consistently slower than Blocking and Poller modes (1.31-1.70x slower) due to:
1. User-space polling with `TryRecv()` has overhead compared to kernel-level blocking
2. Thread.Sleep() adds latency even with minimal 1ms sleep interval
3. Blocking and Poller modes use efficient kernel mechanisms (`recv()` syscall and `zmq_poll()`) that wake threads immediately when messages arrive

**Message Size Impact**: The Sleep overhead is most pronounced with large messages (65KB) where NonBlocking is 1.70x slower, while for small messages (64B) it's 1.45x slower.

**Recommendation**: NonBlocking mode is not recommended for production use due to poor performance. Use Blocking for single-socket applications or Poller for multi-socket scenarios.

### Receive Mode Selection Considerations

When choosing a receive mode, consider:

**Recommended Approaches**:
- **Single Socket**: Use **Blocking** mode for simplicity and best performance
- **Multiple Sockets**: Use **Poller** mode to monitor multiple sockets with a single thread
- Both modes provide optimal CPU efficiency (0% when idle) and low latency

**NonBlocking Mode Limitations**:
- **Not recommended for production** due to poor performance (1.3-1.7x slower than Blocking/Poller)
- Thread.Sleep(1ms) adds latency overhead
- Only consider NonBlocking if you must integrate with an existing polling loop where you cannot use Blocking or Poller

**Performance Characteristics**:
- Blocking and Poller deliver similar performance (within 2-3% for small messages)
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
| **ByteArray** | 3.253 ms | 325.28 ns | 3.07M | 1.57 Gbps | 3.91 | 1719.07 KB | 1.00x |
| **ArrayPool** | 3.354 ms | 335.44 ns | 2.98M | 1.53 Gbps | - | 1.07 KB | 1.03x |
| **Message** | 5.614 ms | 561.37 ns | 1.78M | 0.91 Gbps | - | 625.32 KB | 1.73x |
| **MessageZeroCopy** | 6.538 ms | 653.79 ns | 1.53M | 0.78 Gbps | - | 625.32 KB | 2.01x |

#### 1500-Byte Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **Message** | 10.993 ms | 1.10 μs | 909.64K | 10.92 Gbps | - | 625.32 KB | 1.00x |
| **ByteArray** | 11.002 ms | 1.10 μs | 908.96K | 10.91 Gbps | 78.13 | 29844.07 KB | 1.00x |
| **ArrayPool** | 11.286 ms | 1.13 μs | 886.05K | 10.63 Gbps | - | 3.01 KB | 1.03x |
| **MessageZeroCopy** | 14.175 ms | 1.42 μs | 705.46K | 8.47 Gbps | - | 625.32 KB | 1.29x |

#### 65KB Messages

| Strategy | Mean | Latency | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **MessageZeroCopy** | 130.540 ms | 13.05 μs | 76.60K | 4.68 GB/s | - | - | 625.49 KB | 0.83x |
| **Message** | 131.940 ms | 13.19 μs | 75.79K | 4.63 GB/s | - | - | 625.49 KB | 0.84x |
| **ArrayPool** | 144.879 ms | 14.49 μs | 69.02K | 4.21 GB/s | - | - | 65.21 KB | 0.92x |
| **ByteArray** | 157.312 ms | 15.73 μs | 63.57K | 3.88 GB/s | 3333.33 | 250 | 1280469.3 KB | 1.00x |

### Performance and GC Analysis

**Small Messages (64B)**: Performance differences are modest across strategies. ByteArray and ArrayPool achieve highest throughput (3.79-3.85M msg/sec) with ArrayPool eliminating GC allocations. Message and MessageZeroCopy show 2-2.4x slower performance, likely due to native interop overhead being proportionally higher for small payloads.

**Medium Messages (1500B)**: Performance converges across strategies (689-886K msg/sec). ByteArray begins showing GC pressure with 78 Gen0 collections per 10K messages. ArrayPool, Message, and MessageZeroCopy maintain zero GC collections. The 1500-byte size approximates Ethernet MTU, representing a common message size in network applications.

**Large Messages (65KB)**: ByteArray strategy triggers significant garbage collection with 3250 Gen0 and 250 Gen1 collections, allocating 1.28GB for 10K messages. All pool-based and native strategies maintain zero GC collections. MessageZeroCopy achieves the highest throughput (74.28K msg/sec), while performance differences between strategies narrow to 0.90-1.00x relative range.

**GC Pattern Transition**: The transition from minimal to significant GC pressure occurs around the 1500-byte message size. Below this threshold, all strategies show manageable GC behavior. Above it, ByteArray's allocation cost becomes increasingly significant.

**Memory Allocation**: ArrayPool demonstrates the lowest overall allocation (1.07-65.21 KB across all sizes). ByteArray allocation scales linearly with message size and count. Message and MessageZeroCopy maintain consistent allocation (~625 KB) independent of message size.

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
