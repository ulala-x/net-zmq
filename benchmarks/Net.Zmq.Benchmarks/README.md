[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

# NetZeroMQ.Benchmarks

NetZeroMQ Performance Benchmark Tool

## Quick Start

```bash
cd benchmarks/NetZeroMQ.Benchmarks

# Run all benchmarks (27 tests, ~5 minutes)
dotnet run -c Release

# Quick test (1 iteration, ~30 seconds)
dotnet run -c Release -- --quick
```

## Execution Options

### Running Benchmarks

```bash
# Full run (ShortRun: 3 warmup + 3 iterations)
dotnet run -c Release

# Quick run (Dry: 1 iteration)
dotnet run -c Release -- --quick

# Run specific pattern only
dotnet run -c Release -- --filter "*PushPull*"
dotnet run -c Release -- --filter "*PubSub*"
dotnet run -c Release -- --filter "*RouterRouter*"

# Run specific mode only
dotnet run -c Release -- --filter "*Blocking*"
dotnet run -c Release -- --filter "*Poller*"

# Run specific message size only
dotnet run -c Release -- --filter "*64*"      # 64 bytes
dotnet run -c Release -- --filter "*1024*"    # 1 KB
dotnet run -c Release -- --filter "*65536*"   # 64 KB

# Combined filters
dotnet run -c Release -- --quick --filter "*PushPull*64*Blocking*"
```

### Diagnostic Tools

```bash
# Socket connection test
dotnet run -c Release -- --test

# Verify Receive mode behavior
dotnet run -c Release -- --mode-test

# Memory allocation analysis
dotnet run -c Release -- --alloc-test
```

### List Available Benchmarks

```bash
dotnet run -c Release -- --list flat
```

## Benchmark Structure

### ThroughputBenchmarks

| Parameter | Values |
|-----------|--------|
| **MessageSize** | 64, 1024, 65536 bytes |
| **MessageCount** | 10,000 messages |
| **Mode** | Blocking, NonBlocking, Poller |

**Patterns:**
- `PushPull_Throughput` - Unidirectional message transfer
- `PubSub_Throughput` - Pub/Sub broadcast
- `RouterRouter_Throughput` - Bidirectional routing

**Total 27 benchmarks** (3 patterns × 3 sizes × 3 modes)

## Output Columns

| Column | Description |
|--------|-------------|
| **Mean** | Average execution time (processing entire MessageCount) |
| **Latency** | Per-message latency (Mean / MessageCount) |
| **msg/sec** | Throughput per second |
| **Allocated** | Heap memory allocation |

## Receive Mode Comparison

| Mode | Description | Use Case |
|------|-------------|----------|
| **Blocking** | `Recv()` blocking call | Single socket, best performance |
| **NonBlocking** | `TryRecv()` + Sleep + burst | Multiple sockets, without polling |
| **Poller** | `Poll()` + `TryRecv()` burst | Multiple sockets, event-driven |

## Project Structure

```
NetZeroMQ.Benchmarks/
├── Program.cs                    # Entry point, CLI option handling
├── Configs/
│   └── BenchmarkConfig.cs        # Custom columns (Latency, msg/sec)
├── Benchmarks/
│   └── ThroughputBenchmarks.cs   # Main benchmarks
├── AllocTest.cs                  # Memory allocation diagnostics
└── ModeTest.cs                   # Receive mode comparison test
```

## Expected Results

### 64 bytes Messages

| Pattern | Mode | Latency | msg/sec |
|---------|------|---------|---------|
| PushPull | Blocking | ~300 ns | ~3M |
| PushPull | Poller | ~350 ns | ~2.8M |
| PushPull | NonBlocking | ~1.2 μs | ~800K |

### Memory Allocation

| Mode | Allocated |
|------|-----------|
| Blocking | ~900 B |
| NonBlocking | ~900 B |
| Poller | ~1.1 KB |

## Performance Optimization Guide

### 1. Detailed Benchmark Results

#### 1.1 Message Buffer Strategy Comparison (MessageBufferStrategy, 10,000 messages)

NetZeroMQ provides 4 message buffer strategies:

1. **ByteArray** (Baseline): Allocate new `byte[]` for each message - maximum GC pressure
2. **ArrayPool**: Reuse `ArrayPool<byte>.Shared` buffers - minimum GC pressure
3. **Message**: Native memory-backed Message objects - medium GC pressure
4. **MessageZeroCopy**: True zero-copy using `zmq_msg_init_data` - medium GC pressure

| Message Size | ByteArray (Baseline) | ArrayPool | Message | MessageZeroCopy | Winner |
|--------------|----------------------|-----------|---------|-----------------|--------|
| **64 bytes** | 2.80 ms (3.57M/s, 1719 KB) | 2.60 ms (3.84M/s, 1.08 KB) | 4.86 ms (2.06M/s, 625 KB) | 5.78 ms (1.73M/s, 625 KB) | **ArrayPool** (8% faster, 99.94% less GC) |
| **512 bytes** | 6.25 ms (1.60M/s, 10.5 MB) | 6.02 ms (1.66M/s, 1.52 KB) | 6.89 ms (1.45M/s, 625 KB) | 8.04 ms (1.24M/s, 625 KB) | **ArrayPool** (4% faster, 99.99% less GC) |
| **1 KB** | 9.53 ms (1.05M/s, 20.5 MB) | 8.49 ms (1.18M/s, 2.04 KB) | 9.08 ms (1.10M/s, 625 KB) | 11.49 ms (870K/s, 625 KB) | **ArrayPool** (11% faster, 99.99% less GC) |
| **64 KB** | 185.5 ms (53.9K/s, 1.25 GB) | 202.2 ms (49.5K/s, 65 KB) | **169.0 ms (59.2K/s, 626 KB)** | **166.5 ms (60.1K/s, 626 KB)** | **MessageZeroCopy** (10% faster, 99.95% less GC) |
| **131 KB** | 1491 ms (6.71K/s, 2.5 GB, Gen2) | 341 ms (29.3K/s, 129 KB) | 361 ms (27.7K/s, 627 KB) | 373 ms (26.8K/s, 627 KB) | **ArrayPool** (4.4x faster, 99.99% less GC) |
| **262 KB** | 2423 ms (4.13K/s, 5.0 GB, Gen2) | 720 ms (13.9K/s, 258 KB) | 706 ms (14.2K/s, 626 KB) | 697 ms (14.4K/s, 626 KB) | **MessageZeroCopy** (3.5x faster, 99.99% less GC) |

**Key Insights:**
- **Small messages (64B)**: ArrayPool best performance (8% faster, 99.94% less GC)
- **Medium messages (512B-1KB)**: ArrayPool best performance (4-11% faster, 99.99% less GC)
- **Large messages (64KB)**: MessageZeroCopy best performance (10% faster, 99.95% less GC)
- **Very large messages (131KB-262KB)**: ArrayPool/MessageZeroCopy 3.5-4.4x faster than ByteArray
- **Tipping point**: At 64KB and above, native strategies (Message/MessageZeroCopy) gain advantage
- **Critical issue**: ByteArray causes Gen2 GC on large messages → severe performance degradation

#### 1.2 Receive Mode Comparison (ReceiveMode, 10,000 messages)

| Message Size | Blocking | Poller | NonBlocking | Winner |
|--------------|----------|--------|-------------|--------|
| **64 bytes** | **2.19 ms (4,570 K/sec)** | 2.31 ms (4,330 K/sec) | 3.78 ms (2,640 K/sec) | **Blocking** (baseline, Poller 6% slower) |
| **512 bytes** | 4.90 ms (2,040 K/sec) | **4.72 ms (2,120 K/sec)** | 6.14 ms (1,630 K/sec) | **Poller** (4% faster) |
| **1 KB** | 7.54 ms (1,330 K/sec) | **7.74 ms (1,290 K/sec)** | 9.66 ms (1,040 K/sec) | **Blocking** (baseline, Poller 3% slower) |
| **64 KB** | **139.9 ms (71.5 K/sec)** | 141.7 ms (70.6 K/sec) | 260.0 ms (38.5 K/sec) | **Blocking** (baseline, Poller 1% slower) |

**Key Insights:**
- **Blocking vs Poller**: Nearly identical performance (96-106% range, 0-6% difference)
- **Poller mode**: Only 4% advantage at 512B, nearly identical to Blocking for the rest
- **NonBlocking mode**: Worst in all cases (25-73% slower due to Sleep overhead)
- **Recommendation**: Use Poller (same performance with multi-socket support and consistent API)

### 2. Scenario-Based Recommendations

#### 2.1 Sending Externally Allocated Memory (e.g., byte[] from API)

**Scenario**: Need to send already allocated byte[] data

**Recommendation**: Use basic `Send(byte[])` method (internally optimized)

```csharp
// Send existing byte[] data directly
byte[] apiData = await httpClient.GetByteArrayAsync(url);
socket.Send(apiData);
```

**Expected Performance**:
- 64B: ~3.57M msg/sec
- 512B: ~1.60M msg/sec
- 1KB: ~1.05M msg/sec
- 64KB: ~53.9K msg/sec

#### 2.2 Maximum Throughput Scenario (e.g., Log Collector, Metrics Sender)

**Scenario**: Send large volume of messages as fast as possible

**Recommendation**: Choose strategy by message size
- **Small-Medium messages (≤1KB)**: Use ArrayPool
- **Large messages (≥64KB)**: Use MessageZeroCopy
- **Receive**: Reuse Message objects

```csharp
// Sender: Small-Medium messages (≤1KB) - ArrayPool
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Write data to buffer
    WriteData(buffer.AsSpan(0, size));
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// Sender: Large messages (≥64KB) - MessageZeroCopy
nint nativePtr = Marshal.AllocHGlobal(size);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, size);
    sourceData.CopyTo(nativeSpan);
}
using var msg = new Message(nativePtr, size, ptr => Marshal.FreeHGlobal(ptr));
socket.Send(msg);

// Receiver: Message reuse + batch processing
using var recvMsg = new Message();
while (running)
{
    socket.Recv(recvMsg);
    ProcessMessage(recvMsg.Data);
}
```

**Expected Performance**:
- 64B: ~3.84M msg/sec (ArrayPool)
- 512B: ~1.66M msg/sec (ArrayPool)
- 1KB: ~1.18M msg/sec (ArrayPool)
- 64KB: ~60.1K msg/sec (MessageZeroCopy)
- 131KB: ~29.3K msg/sec (ArrayPool)
- 262KB: ~14.4K msg/sec (MessageZeroCopy)

#### 2.3 Low Latency Requirements (e.g., Trading, Real-time Gaming)

**Scenario**: Minimize per-message latency

**Recommendation**: ArrayPool send + Message receive

```csharp
// Send: ArrayPool for minimum latency
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    WriteData(buffer.AsSpan(0, size));
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// Receive: Message object reuse
using var msg = new Message();
socket.Recv(msg);
ProcessMessage(msg.Data);
```

**Expected Latency**:
- 64B: ~260 ns (ArrayPool)
- 512B: ~602 ns (ArrayPool)
- 1KB: ~849 ns (ArrayPool)
- 64KB: ~16.7 μs (MessageZeroCopy)

### 3. Performance Checklist

#### DO:

- **Sending**:
  - Small-Medium messages (≤1KB) → Use `ArrayPool<byte>.Shared`
  - Large messages (≥64KB) → Use `MessageZeroCopy`
  - Select appropriate strategy based on message size
- **Receiving**:
  - Reuse Message objects (`using var msg = new Message()`)
  - Use single Message object for repeated receives
  - Zero-copy access via `msg.Data`
- **General**:
  - Guarantee resource cleanup with `using` keyword
  - Minimize GC pressure through buffer reuse
  - Always Return ArrayPool buffers

#### DON'T:

- **Sending**:
  - Use Message/MessageZeroCopy for small-medium messages (ArrayPool up to 11% faster)
  - Use ByteArray for large messages (Gen2 GC, up to 4.4x slower)
  - Forget to Return ArrayPool buffers (memory leak)
- **Receiving**:
  - Allocate new Message on every receive (increases GC pressure)
  - Allocate new byte[] on every receive (Gen2 GC on large messages)
- **General**:
  - Forget to Dispose Message objects
  - Stick to single approach without choosing optimal strategy

### 4. Decision Flow

```
Send Strategy Selection:
├─ Message size ≤ 1KB?
│  └─ YES → Use ArrayPool (best performance, minimum GC)
│     └─ var buf = ArrayPool<byte>.Shared.Rent(size)
│        socket.Send(buf.AsSpan(0, size))
│        ArrayPool<byte>.Shared.Return(buf)
│
└─ NO (≥64KB) → Use MessageZeroCopy (zero-copy, minimum GC)
   └─ nint ptr = Marshal.AllocHGlobal(size)
      using var msg = new Message(ptr, size, p => Marshal.FreeHGlobal(p))
      socket.Send(msg)

Receive Strategy:
└─ Message object reuse (optimal for all sizes)
   └─ using var msg = new Message()
      while (running)
      {
          socket.Recv(msg)
          ProcessMessage(msg.Data)
      }

Optimal Combination: ArrayPool (≤1KB) or MessageZeroCopy (≥64KB) + Message reuse
```

### 5. Expected Performance Improvements

Expected improvement rates when applying optimizations to existing code:

| Improvement Item | Before | After | Improvement |
|----------|------|------|--------|
| **Small message send** (64B) | ByteArray (2.80ms) | ArrayPool (2.60ms) | **+8%** throughput, **-99.94%** allocations |
| **Medium message send** (512B-1KB) | ByteArray | ArrayPool | **+4-11%** throughput, **-99.99%** allocations |
| **Large message send** (64KB) | ByteArray (185ms) | MessageZeroCopy (167ms) | **+10%** throughput, **-99.95%** allocations |
| **Very large message send** (131KB) | ByteArray (1491ms, Gen2 GC) | ArrayPool (341ms) | **+4.4x** throughput, **-99.99%** allocations |
| **Very large message send** (262KB) | ByteArray (2423ms, Gen2 GC) | MessageZeroCopy (697ms) | **+3.5x** throughput, **-99.99%** allocations |

**Overall improvement example** (131KB messages, ByteArray → ArrayPool):
- Execution time: 1491 ms → 341 ms (**4.4x faster**)
- Throughput: 6.71K/sec → 29.3K/sec (**4.4x increase**)
- Memory: 2.5 GB (Gen2 GC) → 129 KB (**-99.99%**)
- GC pressure: Gen2 collections → Gen0/Gen1/Gen2 all 0
- **Overall effect: Dramatic performance improvement + complete GC elimination**

### 6. Additional Performance Recommendations

- **Message size-based strategy**:
  - ≤1KB: ArrayPool (best performance, minimum GC)
  - ≥64KB: MessageZeroCopy (zero-copy, minimum GC)
  - Mid-range sizes need benchmarking
- **Receive optimization**:
  - Reuse Message objects to minimize GC pressure
  - Zero-copy access via `msg.Data`
- **GC optimization**:
  - Always Return ArrayPool buffers
  - Never directly allocate byte[] for large messages (causes Gen2 GC)
- **Benchmark usage**:
  - Measure message size distribution in actual workload
  - Choose optimal strategy by size for up to 4.4x performance gain
