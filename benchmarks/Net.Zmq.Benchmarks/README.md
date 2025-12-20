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

#### 1.1 Send Strategy Comparison (MemoryStrategy, 10,000 messages)

| Message Size | ArrayPool | ByteArray | Message | MessageZeroCopy | Winner |
|--------------|-----------|-----------|---------|-----------------|--------|
| **64 bytes** | 2.43 ms (4,120 K/sec) | 2.44 ms (4,100 K/sec) | 4.28 ms (2,340 K/sec) | 5.92 ms (1,690 K/sec) | **ArrayPool** (1% faster, 99.98% less GC) |
| **512 bytes** | 6.38 ms (1,570 K/sec) | 6.71 ms (1,490 K/sec) | 8.19 ms (1,220 K/sec) | 13.37 ms (748 K/sec) | **ArrayPool** (5% faster, 99.99% less GC) |
| **1 KB** | 9.02 ms (1,110 K/sec) | 8.97 ms (1,110 K/sec) | 9.74 ms (1,030 K/sec) | 14.61 ms (684 K/sec) | **ByteArray** (0.5% faster, massive GC) |
| **64 KB** | 142.8 ms (70.0 K/sec) | 141.7 ms (70.6 K/sec) | **119.2 ms (83.9 K/sec)** | 124.7 ms (80.2 K/sec) | **Message** (16% faster, 99.95% less GC) |

**Key Insights:**
- **Small messages (≤512B)**: ArrayPool delivers best performance (1-5% faster with 99.98-99.99% GC reduction)
- **Medium messages (1KB)**: Similar performance, but ByteArray causes massive GC (100MB allocation)
- **Large messages (≥64KB)**: Message delivers best performance (16% faster with 99.95% GC reduction)
- **Tipping point**: At 64KB, native strategies (Message/MessageZeroCopy) gain 12-16% advantage

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

**Recommendation**: Use `SendOptimized()` (automatic optimization by size)

```csharp
// Automatically selects ArrayPool or MessagePool based on size
byte[] apiData = await httpClient.GetByteArrayAsync(url);
socket.SendOptimized(apiData);
```

**Expected Performance**:
- 64B: ~4.1M msg/sec (ArrayPool)
- 512B: ~1.6M msg/sec (ArrayPool)
- 1KB: ~1.1M msg/sec (ArrayPool)
- 64KB: ~84K msg/sec (Message)

#### 2.2 Maximum Throughput Scenario (e.g., Log Collector, Metrics Sender)

**Scenario**: Send large volume of messages as fast as possible

**Recommendation**: ArrayPool (≤512B) / MessageZeroCopy (>512B) + Poller combination

```csharp
// Sender: Select strategy by size
// Small messages (≤512B): ArrayPool
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Write data to buffer
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// Large messages (>512B): MessageZeroCopy
nint nativePtr = Marshal.AllocHGlobal(size);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, size);
    sourceData.CopyTo(nativeSpan);
}
using var msg = new Message(nativePtr, size, ptr => Marshal.FreeHGlobal(ptr));
socket.Send(msg);

// Receiver: Poller + Message
using var poller = new Poller(capacity: 10);
int idx = poller.Add(socket, PollEvents.In);

using var recvMsg = new Message();
while (running)
{
    if (poller.Poll(timeout: 100) > 0)
    {
        while (poller.IsReadable(idx))
        {
            socket.Recv(ref recvMsg, RecvFlags.None);
            ProcessMessage(recvMsg.Data);
        }
    }
}
```

**Expected Performance**:
- 512B: ~2.1M msg/sec (ArrayPool + Poller)
- 1KB: ~1.3M msg/sec (ArrayPool + Poller)
- 64KB: ~71K msg/sec (Message + Blocking)

#### 2.3 Low Latency Requirements (e.g., Trading, Real-time Gaming)

**Scenario**: Minimize per-message latency

**Recommendation**: Poller (small messages) / Blocking (large messages)

```csharp
// Small messages (<64KB): Use Poller
using var poller = new Poller(1);
int idx = poller.Add(socket, PollEvents.In);

using var msg = new Message();
if (poller.Poll(timeout: 1) > 0 && poller.IsReadable(idx))
{
    socket.Recv(ref msg, RecvFlags.None);
    // Latency: 64B ~231ns, 512B ~472ns, 1KB ~774ns
}

// Large messages (≥64KB): Use Blocking
socket.Recv(ref msg, RecvFlags.None);
// Latency: 64KB ~14.0μs
```

### 3. Performance Checklist

#### DO:

- **Sending**:
  - Small messages (≤512B) → Use `ArrayPool<byte>.Shared`
  - Large messages (>512B) → Use `MessageZeroCopy`
  - Select appropriate strategy based on message size
- **Receiving**:
  - Prioritize Poller mode (optimal for most cases)
  - Reuse Message objects (using var msg = new Message())
  - Batch processing (Poller.IsReadable() loop)
- **General**:
  - Guarantee resource cleanup with using keyword
  - Minimize GC pressure through buffer reuse

#### DON'T:

- **Sending**:
  - Force MessageZeroCopy for small messages (ArrayPool is faster)
  - Use ByteArray for large messages (increases GC pressure)
- **Receiving**:
  - Use NonBlocking mode (worst performance due to Sleep overhead)
  - Allocate new Message on every receive (GC pressure)
  - TryRecv() loop without Poller (CPU waste)
- **General**:
  - Forget manual Dispose of Message objects
  - Process only single message and wait (miss batch opportunities)

### 4. Decision Flow

```
Send Strategy Selection:
├─ Message size ≤ 512B?
│  └─ YES → Use ArrayPool (best performance)
│     └─ ArrayPool<byte>.Shared.Rent(size)
│
└─ NO → Use MessageZeroCopy (zero-copy)
   └─ Marshal.AllocHGlobal + Message(ptr, size, freeCallback)

Receive Mode Selection:
├─ Message size < 64KB?
│  └─ YES → Recommend Poller mode (nearly identical to Blocking, 0-6% difference)
│     └─ Poller + Message + batch processing
│
└─ NO → Both Blocking or Poller acceptable (1% difference)
   └─ Recommend Poller (consistent API, multi-socket support)

Recommendation: ArrayPool (≤512B) / Message (≥64KB) + Poller
```

### 5. Expected Performance Improvements

Expected improvement rates when applying optimizations to existing code:

| Improvement Item | Before | After | Improvement |
|----------|------|------|--------|
| **Small message send** (64B-512B) | ByteArray/Message | ArrayPool | **+1-5%** throughput, **-99.98%** allocations |
| **Large message send** (≥64KB) | ByteArray/ArrayPool | Message | **+16%** throughput, **-99.95%** allocations |
| **Receive mode** (all sizes) | NonBlocking | Poller | **+25-73%** throughput |
| **Receive mode** (64KB) | NonBlocking | Blocking | **+86%** throughput |
| **Memory allocation** (64KB) | ByteArray | Message | **-99.95%** allocations (4GB → 171KB) |

**Overall improvement example** (512B messages, existing ByteArray+NonBlocking → optimal ArrayPool+Poller):
- Send: 1,490 K/sec → 1,570 K/sec (+5%)
- Receive: 1,630 K/sec → 2,120 K/sec (+30%)
- Memory: 50MB → 2KB (-99.99%)
- **Overall pipeline: ~35% throughput improvement + massive GC reduction**

### 6. Additional Performance Recommendations

- **Single socket**: Blocking or Poller mode (similar performance)
- **Multiple sockets**: Poller mode required (efficient event waiting)
- **Burst processing**: Process all available messages with Poller.IsReadable() loop
- **Buffer reuse**: Reuse Message objects to minimize GC pressure
