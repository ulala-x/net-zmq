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
- **Transport**: inproc:// (in-process)
- **Pattern**: ROUTER-to-ROUTER (for receive mode tests)

## Receive Mode Benchmarks

### How Each Mode Works

**Blocking Mode (`socket.Recv()`)**: The calling thread blocks until a message arrives. This is the simplest approach and provides deterministic waiting with minimal CPU usage. The thread yields to the OS scheduler while waiting.

**NonBlocking Mode (`socket.TryRecv()`)**: Polling-based reception where the application repeatedly calls `TryRecv()` and sleeps for 10ms if no message is available. This approach prevents thread blocking but introduces latency due to the sleep interval.

**Poller Mode**: Event-driven reception using `zmq_poll()`. The application waits for socket events without busy-waiting. This mode supports monitoring multiple sockets simultaneously and provides efficient CPU usage while maintaining responsiveness.

### Performance Results

All tests use ROUTER-to-ROUTER pattern with concurrent sender and receiver.

#### 64-Byte Messages

| Mode | Mean | Latency | Throughput | Allocated | Ratio |
|------|------|---------|------------|-----------|-------|
| **Blocking** | 2.325 ms | 232.52 ns | 4.30M/sec | 203 B | 1.00x |
| **Poller** | 2.376 ms | 237.59 ns | 4.21M/sec | 323 B | 1.02x |
| **NonBlocking** | 11.447 ms | 1.14 μs | 873.62K/sec | 212 B | 4.92x |

#### 1500-Byte Messages

| Mode | Mean | Latency | Throughput | Allocated | Ratio |
|------|------|---------|------------|-----------|-------|
| **Poller** | 10.552 ms | 1.06 μs | 947.66K/sec | 332 B | 0.96x |
| **Blocking** | 11.040 ms | 1.10 μs | 905.79K/sec | 212 B | 1.00x |
| **NonBlocking** | 14.909 ms | 1.49 μs | 670.72K/sec | 212 B | 1.35x |

#### 65KB Messages

| Mode | Mean | Latency | Throughput | Allocated | Ratio |
|------|------|---------|------------|-----------|-------|
| **Poller** | 167.479 ms | 16.75 μs | 59.71K/sec | 504 B | 0.99x |
| **Blocking** | 168.915 ms | 16.89 μs | 59.20K/sec | 384 B | 1.00x |
| **NonBlocking** | 351.448 ms | 35.14 μs | 28.45K/sec | 445 B | 2.08x |

### Performance Analysis

**Blocking vs Poller**: Performance is nearly identical across all message sizes (96-102% relative performance). Poller allocates slightly more memory (323-504 bytes vs 203-384 bytes for 10K messages) due to polling infrastructure, but the difference is negligible in practice.

**NonBlocking Performance**: NonBlocking mode shows 1.35-4.92x slower performance compared to Blocking. This difference stems from the `Thread.Sleep(10ms)` call when no message is immediately available, which adds latency to each receive operation. The performance gap is most pronounced with small messages (64B) where the sleep overhead dominates total processing time.

**Latency Characteristics**: Blocking and Poller modes achieve sub-microsecond latency for small messages (232-238ns), while NonBlocking incurs microsecond-level latency due to polling overhead.

### Receive Mode Selection Considerations

When choosing a receive mode, consider:

**Single Socket Applications**:
- Blocking mode offers simple implementation and excellent performance when thread blocking is acceptable
- Poller mode provides similar performance with event-driven architecture
- NonBlocking mode may be suitable when integrating with existing polling loops

**Multiple Socket Applications**:
- Poller mode enables monitoring multiple sockets with a single thread
- Blocking mode would require one thread per socket
- NonBlocking mode could service multiple sockets but with higher latency

**Latency Requirements**:
- Blocking and Poller modes deliver similar latency (within 2-3%)
- NonBlocking mode adds 10ms+ latency per receive operation due to sleep intervals

**Thread Management**:
- Blocking mode dedicates a thread to each socket
- Poller mode allows one thread to service multiple sockets
- NonBlocking mode enables integration with application event loops

## Memory Strategy Benchmarks

### How Each Strategy Works

**ByteArray (`new byte[]`)**: Allocates a new byte array for each message. Simple and straightforward, but creates garbage collection pressure proportional to message size and frequency.

**ArrayPool (`ArrayPool<byte>.Shared`)**: Rents buffers from a shared pool and returns them after use. Reduces GC allocations by reusing memory, though requires manual return management.

**Message (`zmq_msg_t`)**: Uses libzmq's native message structure, which manages memory internally. The .NET wrapper marshals data between native and managed memory as needed.

**MessageZeroCopy (`Marshal.AllocHGlobal`)**: Allocates unmanaged memory directly and transfers ownership to libzmq via a free callback. Provides zero-copy semantics but requires careful lifecycle management.

### Performance Results

All tests use Poller mode for reception.

#### 64-Byte Messages

| Strategy | Mean | Latency | Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|------------|------|-----------|-------|
| **ArrayPool** | 2.595 ms | 259.53 ns | 3.85M/sec | - | 1.07 KB | 0.98x |
| **ByteArray** | 2.638 ms | 263.76 ns | 3.79M/sec | 3.91 | 1719.07 KB | 1.00x |
| **Message** | 5.364 ms | 536.41 ns | 1.86M/sec | - | 625.32 KB | 2.03x |
| **MessageZeroCopy** | 6.428 ms | 642.82 ns | 1.56M/sec | - | 625.32 KB | 2.44x |

#### 1500-Byte Messages

| Strategy | Mean | Latency | Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|------------|------|-----------|-------|
| **Message** | 11.287 ms | 1.13 μs | 886.00K/sec | - | 625.32 KB | 0.98x |
| **ByteArray** | 11.495 ms | 1.15 μs | 869.97K/sec | 78.13 | 29844.07 KB | 1.00x |
| **ArrayPool** | 11.929 ms | 1.19 μs | 838.30K/sec | - | 3.01 KB | 1.04x |
| **MessageZeroCopy** | 14.504 ms | 1.45 μs | 689.46K/sec | - | 625.32 KB | 1.26x |

#### 65KB Messages

| Strategy | Mean | Latency | Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|------------|------|------|-----------|-------|
| **MessageZeroCopy** | 134.626 ms | 13.46 μs | 74.28K/sec | - | - | 625.49 KB | 0.90x |
| **Message** | 142.068 ms | 14.21 μs | 70.39K/sec | - | - | 625.49 KB | 0.95x |
| **ArrayPool** | 148.562 ms | 14.86 μs | 67.31K/sec | - | - | 65.21 KB | 0.99x |
| **ByteArray** | 150.055 ms | 15.01 μs | 66.64K/sec | 3250 | 250 | 1280469.24 KB | 1.00x |

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

- All benchmarks use `inproc://` transport to eliminate network variability
- Concurrent mode simulates realistic producer/consumer scenarios
- Results represent steady-state performance after warmup
- BenchmarkDotNet's ShortRun job provides statistically valid measurements with reduced runtime

### Limitations and Considerations

- `inproc://` transport performance differs from `tcp://` or `ipc://` transports
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
