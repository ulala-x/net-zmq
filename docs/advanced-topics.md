[![English](https://img.shields.io/badge/lang:en-red.svg)](advanced-topics.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](advanced-topics.ko.md)

# Advanced Topics

This guide covers advanced Net.Zmq topics including performance optimization, best practices, security, and troubleshooting.

## Performance Optimization

Net.Zmq delivers exceptional performance, but proper configuration is essential to achieve optimal results.

### Performance Metrics

Net.Zmq achieves:

- **Peak Throughput**: 4.95M messages/sec (PUSH/PULL, 64B)
- **Ultra-Low Latency**: 202ns per message
- **Memory Efficient**: 441B allocation per 10K messages

See [BENCHMARKS.md](https://github.com/ulala-x/net-zmq/blob/main/BENCHMARKS.md) for detailed performance metrics.

### Receive Modes

Net.Zmq provides three receive modes with different performance characteristics and use cases.

#### How Each Mode Works

**Blocking Mode**: The calling thread blocks on `Recv()` until a message arrives. The thread yields to the operating system scheduler while waiting, consuming minimal CPU resources. This is the simplest approach with deterministic waiting behavior.

**NonBlocking Mode**: The application repeatedly calls `TryRecv()` to poll for messages. When no message is immediately available, the thread typically sleeps for a short interval (e.g., 10ms) before retrying. This prevents thread blocking but introduces latency due to the sleep interval.

**Poller Mode**: Event-driven reception using `zmq_poll()` internally. The application waits for socket events without busy-waiting or blocking individual sockets. This mode efficiently handles multiple sockets with a single thread and provides responsive event notification.

#### Usage Examples

Blocking mode provides the simplest implementation:

```csharp
using var context = new Context();
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Blocks until message arrives
var buffer = new byte[1024];
int size = socket.Recv(buffer);
ProcessMessage(buffer.AsSpan(0, size));
```

NonBlocking mode integrates with polling loops:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

var buffer = new byte[1024];
while (running)
{
    if (socket.TryRecv(buffer, out int size))
    {
        ProcessMessage(buffer.AsSpan(0, size));
    }
    else
    {
        Thread.Sleep(10); // Wait before retry
    }
}
```

Poller mode supports multiple sockets:

```csharp
using var socket1 = new Socket(context, SocketType.Pull);
using var socket2 = new Socket(context, SocketType.Pull);
socket1.Connect("tcp://localhost:5555");
socket2.Connect("tcp://localhost:5556");

using var poller = new Poller(2);
poller.Add(socket1, PollEvents.In);
poller.Add(socket2, PollEvents.In);

var buffer = new byte[1024];
while (running)
{
    int eventCount = poller.Poll(1000); // 1 second timeout

    if (eventCount > 0)
    {
        if (socket1.TryRecv(buffer, out int size))
        {
            ProcessMessage1(buffer.AsSpan(0, size));
        }

        if (socket2.TryRecv(buffer, out size))
        {
            ProcessMessage2(buffer.AsSpan(0, size));
        }
    }
}
```

#### Performance Characteristics

Benchmarked on ROUTER-to-ROUTER pattern with concurrent sender and receiver (10,000 messages, Intel Core Ultra 7 265K):

**64-Byte Messages**:
- Blocking: 2.187 ms (4.57M msg/sec, 218.7 ns latency)
- Poller: 2.311 ms (4.33M msg/sec, 231.1 ns latency)
- NonBlocking: 3.783 ms (2.64M msg/sec, 378.3 ns latency)

**512-Byte Messages**:
- Poller: 4.718 ms (2.12M msg/sec, 471.8 ns latency)
- Blocking: 4.902 ms (2.04M msg/sec, 490.2 ns latency)
- NonBlocking: 6.137 ms (1.63M msg/sec, 613.7 ns latency)

**1024-Byte Messages**:
- Blocking: 7.541 ms (1.33M msg/sec, 754.1 ns latency)
- Poller: 7.737 ms (1.29M msg/sec, 773.7 ns latency)
- NonBlocking: 9.661 ms (1.04M msg/sec, 966.1 ns latency)

**65KB Messages**:
- Blocking: 139.915 ms (71.47K msg/sec, 13.99 μs latency)
- Poller: 141.733 ms (70.56K msg/sec, 14.17 μs latency)
- NonBlocking: 260.014 ms (38.46K msg/sec, 26.00 μs latency)

Blocking and Poller modes deliver nearly identical performance (96-106% relative), with Poller allocating slightly more memory (456-640 bytes vs 336-664 bytes per 10K messages) for polling infrastructure. NonBlocking mode shows 1.25-1.86x slower performance due to sleep overhead when messages are not immediately available.

#### Selection Considerations

**Single Socket Applications**:
- Blocking mode offers simple implementation when thread blocking is acceptable
- Poller mode provides event-driven architecture with similar performance
- NonBlocking mode enables integration with existing polling loops

**Multiple Socket Applications**:
- Poller mode monitors multiple sockets with a single thread
- Blocking mode requires one thread per socket
- NonBlocking mode can service multiple sockets with higher latency

**Latency Requirements**:
- Blocking and Poller modes achieve sub-microsecond latency (218-231 ns for 64-byte messages)
- NonBlocking mode adds overhead due to sleep intervals (378 ns for 64-byte messages)

**Thread Management**:
- Blocking mode dedicates threads to sockets
- Poller mode allows one thread to service multiple sockets
- NonBlocking mode integrates with application event loops

### Memory Strategies

Net.Zmq supports multiple memory management strategies for send and receive operations, each with different performance and garbage collection characteristics.

#### How Each Strategy Works

**ByteArray**: Allocates a new byte array (`new byte[]`) for each message. This provides simple, automatic memory management but creates garbage collection pressure proportional to message size and frequency.

**ArrayPool**: Rents buffers from `ArrayPool<byte>.Shared` and returns them after use. This reduces GC allocations by reusing memory from a shared pool, though it requires manual rent/return lifecycle management.

**Message**: Uses libzmq's native message structure (`zmq_msg_t`) which manages memory internally. The .NET wrapper marshals data between native and managed memory as needed. This approach leverages native memory management.

**MessageZeroCopy**: Allocates unmanaged memory directly (`Marshal.AllocHGlobal`) and transfers ownership to libzmq via a free callback. This provides true zero-copy semantics by avoiding managed memory entirely, but requires careful lifecycle management.

**MessagePool**: A Net.Zmq exclusive feature (not available in cppzmq) that pools and reuses native memory buffers to eliminate GC pressure and enable high-performance messaging. Unlike ArrayPool which requires manual `Return()` calls, MessagePool automatically returns buffers to the pool via ZeroMQ's free callback when transmission completes.

#### Usage Examples

ByteArray approach uses standard .NET arrays:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Allocate new buffer for each receive
var buffer = new byte[1024];
int size = socket.Recv(buffer);

// Create output buffer for external delivery
var output = new byte[size];
buffer.AsSpan(0, size).CopyTo(output);
DeliverMessage(output);
```

ArrayPool approach reuses buffers:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Receive into fixed buffer
var recvBuffer = new byte[1024];
int size = socket.Recv(recvBuffer);

// Rent buffer from pool for external delivery
var output = ArrayPool<byte>.Shared.Rent(size);
try
{
    recvBuffer.AsSpan(0, size).CopyTo(output);
    DeliverMessage(output.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(output);
}
```

Message approach uses native memory:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Receive into native message
using var message = new Message();
socket.Recv(message);

// Access data directly without copying
ProcessMessage(message.Data); // ReadOnlySpan<byte>
```

MessageZeroCopy approach for sending:

```csharp
using var socket = new Socket(context, SocketType.Push);
socket.Connect("tcp://localhost:5555");

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

MessagePool approach for sending:

```csharp
using var socket = new Socket(context, SocketType.Push);
socket.Connect("tcp://localhost:5555");

// Basic usage: Rent buffer of specified size
var msg = MessagePool.Shared.Rent(1024);
socket.Send(msg);  // Auto-returned to pool when transmission completes

// Rent with data: Copies data to pooled buffer
var data = new byte[1024];
var msg = MessagePool.Shared.Rent(data);
socket.Send(msg);

// Prewarm pool for predictable performance
MessagePool.Shared.Prewarm(MessageSize.K1, 500);  // Warm up 500 x 1KB buffers
```

MessagePool approach for receiving (size-prefixed protocol):

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Sender side: Prefix message with size
var payload = new byte[8192];
socket.Send(BitConverter.GetBytes(payload.Length), SendFlags.SendMore);
socket.Send(payload);

// Receiver side: Read size first, then receive into pooled buffer
var sizeBuffer = new byte[4];
socket.Recv(sizeBuffer);
int expectedSize = BitConverter.ToInt32(sizeBuffer);

var msg = MessagePool.Shared.Rent(expectedSize);
socket.Recv(msg, expectedSize);  // Receive with known size
ProcessMessage(msg.Data);
```

MessagePool approach for receiving (fixed-size messages):

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// When message size is fixed and known
const int FixedMessageSize = 1024;
var msg = MessagePool.Shared.Rent(FixedMessageSize);
socket.Recv(msg, FixedMessageSize);
ProcessMessage(msg.Data);
```

#### Performance and GC Characteristics

Benchmarked with Poller mode on ROUTER-to-ROUTER pattern (10,000 messages, Intel Core Ultra 7 265K):

**64-Byte Messages**:
- ArrayPool: 2.428 ms (4.12M msg/sec), 0 GC, 1.85 KB allocated
- ByteArray: 2.438 ms (4.10M msg/sec), 9.77 Gen0, 9860.2 KB allocated
- Message: 4.279 ms (2.34M msg/sec), 0 GC, 168.54 KB allocated
- MessageZeroCopy: 5.917 ms (1.69M msg/sec), 0 GC, 168.61 KB allocated

**512-Byte Messages**:
- ArrayPool: 6.376 ms (1.57M msg/sec), 0 GC, 2.04 KB allocated
- ByteArray: 6.707 ms (1.49M msg/sec), 48.83 Gen0, 50017.99 KB allocated
- Message: 8.187 ms (1.22M msg/sec), 0 GC, 168.72 KB allocated
- MessageZeroCopy: 13.372 ms (748K msg/sec), 0 GC, 168.80 KB allocated

**1024-Byte Messages**:
- ArrayPool: 9.021 ms (1.11M msg/sec), 0 GC, 2.24 KB allocated
- ByteArray: 8.973 ms (1.11M msg/sec), 97.66 Gen0, 100033.11 KB allocated
- Message: 9.739 ms (1.03M msg/sec), 0 GC, 168.92 KB allocated
- MessageZeroCopy: 14.612 ms (684K msg/sec), 0 GC, 169.01 KB allocated

**65KB Messages**:
- Message: 119.164 ms (83.93K msg/sec), 0 GC, 171.47 KB allocated
- MessageZeroCopy: 124.720 ms (80.18K msg/sec), 0 GC, 171.56 KB allocated
- ArrayPool: 142.814 ms (70.02K msg/sec), 0 GC, 4.78 KB allocated
- ByteArray: 141.652 ms (70.60K msg/sec), 3906 Gen0 + 781 Gen1, 4001252.47 KB allocated

#### GC Pressure by Message Size

The transition from minimal to significant GC pressure is clearly visible in the benchmark data:

- **64B**: ByteArray shows 9.77 Gen0 collections (manageable)
- **512B**: ByteArray shows 48.83 Gen0 collections (increasing pressure)
- **1KB**: ByteArray shows 97.66 Gen0 collections (substantial pressure)
- **65KB**: ByteArray shows 3906 Gen0 + 781 Gen1 collections (severe pressure)

ArrayPool, Message, and MessageZeroCopy maintain zero GC collections regardless of message size, demonstrating their effectiveness for GC-sensitive applications.

#### MessagePool: Net.Zmq Exclusive Feature

MessagePool is a unique feature in Net.Zmq (not available in cppzmq or other ZeroMQ bindings) that provides pooled native memory buffers for high-performance, GC-free messaging.

**Architecture: Two-Tier Caching System**

MessagePool uses a sophisticated two-tier caching architecture for optimal performance:

- **Tier 1 - Thread-Local Cache**: Each thread maintains a lock-free cache with 8 buffers per bucket, minimizing lock contention
- **Tier 2 - Shared Pool**: A thread-safe `ConcurrentStack` serves as the global pool when thread-local caches are exhausted
- **19 Size Buckets**: Buffers range from 16 bytes to 4MB, organized in power-of-2 sizes for efficient allocation

**Key Advantages**

1. **Zero GC Pressure**: Eliminates Gen0/Gen1/Gen2 collections by reusing native memory buffers
2. **Automatic Return**: Unlike ArrayPool which requires manual `Return()` calls, MessagePool automatically returns buffers via ZeroMQ's free callback when transmission completes
3. **Perfect ZeroMQ Integration**: Seamlessly integrates with ZeroMQ's zero-copy architecture
4. **Lock-Free Fast Path**: Thread-local caches provide lock-free access for high-throughput scenarios
5. **Superior Performance**: 12% faster than ByteArray for 1KB messages, 3.4x faster for 128KB messages

**Performance Comparison**

Benchmarked on PUSH-to-PULL pattern (10,000 messages, Intel Core Ultra 7 265K):

| Message Size | MessagePool | ByteArray | ArrayPool | Speedup vs ByteArray |
|--------------|-------------|-----------|-----------|---------------------|
| 64B          | 1.881 ms    | 1.775 ms  | 1.793 ms  | 0.95x (5% slower)   |
| 1KB          | 5.314 ms    | 6.048 ms  | 5.361 ms  | 1.14x (12% faster)  |
| 128KB        | 342.125 ms  | 1159.675 ms | 367.083 ms | 3.39x (239% faster) |
| 256KB        | 708.083 ms  | 2399.208 ms | 719.708 ms | 3.39x (239% faster) |

**GC Collections (10,000 messages)**

| Message Size | MessagePool | ByteArray | ArrayPool |
|--------------|-------------|-----------|-----------|
| 64B          | 0 GC        | 10 Gen0   | 0 GC      |
| 1KB          | 0 GC        | 98 Gen0   | 0 GC      |
| 128KB        | 0 GC        | 9766 Gen0 + 9765 Gen1 + 7 Gen2 | 0 GC |
| 256KB        | 0 GC        | 19531 Gen0 + 19530 Gen1 + 13 Gen2 | 0 GC |

**Trade-offs and Constraints**

*Memory Considerations:*
- Native memory footprint: Pooled buffers reside in native memory, not managed heap
- Potential memory overhead: Up to `MaxBuffers × BucketSize` native memory per bucket
- Not subject to GC but counts toward process memory

*Performance Characteristics:*
- Small messages (64B): Slightly slower than ByteArray (~5%) due to callback overhead
- Medium messages (1KB-128KB): Significantly faster than ByteArray (12-239%)
- Comparable to ArrayPool for most sizes, with automatic return as added benefit

*Receive Constraints (Critical):*
- **Requires knowing message size in advance** for receive operations
- Must rent appropriately sized buffer before receiving
- Two common solutions:
  1. **Size-prefixed protocol**: Send message size in a preceding frame
  2. **Fixed-size messages**: Use predefined, constant message sizes
- Not suitable for dynamic-size message reception without protocol support

**When to Use MessagePool**

**Recommended: Use MessagePool whenever possible**

MessagePool should be your default choice for most scenarios due to its zero GC pressure, automatic buffer return, and high performance.

**For Sending:**
- Always use MessagePool for sending - it provides better performance and zero GC pressure
- Particularly beneficial for medium to large messages (>1KB)
- Even for small messages (64B), the 5% overhead is negligible compared to GC benefits

**For Receiving:**
- Use MessagePool when message size is known:
  - **Size-prefixed protocols**: Send `[size][payload]` as multipart message
  - **Fixed-size messages**: Use constant message sizes
- Fall back to `Message` only when size is unknown and unpredictable

**In practice, size is almost always known:**
- ZeroMQ multipart messages make size-prefixing trivial: `socket.Send(size, SendFlags.SendMore); socket.Send(payload);`
- The overhead of sending a 4-byte size prefix is minimal compared to GC savings
- Most real-world protocols already include message framing or size information

**Alternative: When size is truly unknown**
- If you cannot know the size beforehand, use `Message` for receiving
- Note that even ArrayPool benchmarks assume fixed-length messages
- Reading into a fixed buffer and copying is slower due to copy overhead
- In practice, this scenario is rare - most protocols support size indication

**Usage Best Practices**

```csharp
// Recommended: Size-prefixed protocol for variable-size messages
// Sender
var payload = GeneratePayload();
socket.Send(BitConverter.GetBytes(payload.Length), SendFlags.SendMore);
socket.Send(MessagePool.Shared.Rent(payload));

// Receiver
var sizeBuffer = new byte[4];
socket.Recv(sizeBuffer);
int size = BitConverter.ToInt32(sizeBuffer);
var msg = MessagePool.Shared.Rent(size);
socket.Recv(msg, size);

// Recommended: Fixed-size messages
const int MessageSize = 1024;
var msg = MessagePool.Shared.Rent(MessageSize);
socket.Send(msg);

// Optional: Prewarm pool for consistent performance
MessagePool.Shared.Prewarm(MessageSize.K1, 100);  // Pre-allocate 100 x 1KB buffers
```

#### Selection Considerations

**Quick Reference: When to Use Each Strategy**

For most applications, follow this decision tree:

1. **Default Choice: MessagePool**
   - Use for all scenarios where message size is known or can be communicated
   - Provides zero GC, automatic return, and best overall performance
   - Requires size-prefixed protocol or fixed-size messages for receiving

2. **When Size is Unknown: Message**
   - Use only when you cannot determine message size beforehand
   - Zero GC but slower than MessagePool for medium/large messages

3. **Legacy or Simple Code: ByteArray**
   - Use only when GC pressure is acceptable and simplicity is critical
   - Avoid for high-throughput or large message scenarios

4. **Manual Control: ArrayPool**
   - Use when you need explicit control over buffer lifecycle
   - Requires manual Return() calls
   - MessagePool is generally preferred due to automatic return

5. **Advanced Zero-Copy: MessageZeroCopy**
   - Use for custom memory management scenarios
   - Most use cases are better served by MessagePool

**Detailed Considerations**

**Message Size Distribution**:
- **Small messages (≤64B)**: ArrayPool/ByteArray have slight edge (~5%) due to lower callback overhead, but MessagePool's GC benefits outweigh this
- **Medium messages (1KB-64KB)**: MessagePool is fastest (12% faster than ByteArray), with zero GC
- **Large messages (≥128KB)**: MessagePool dominates (3.4x faster than ByteArray), with zero GC
- ByteArray generates exponentially increasing GC pressure as message size grows
- MessagePool, ArrayPool, and Message maintain zero GC pressure regardless of message size

**GC Sensitivity**:
- Applications sensitive to GC pauses should use MessagePool, ArrayPool, Message, or MessageZeroCopy
- MessagePool preferred for automatic buffer management
- High-throughput applications with variable message sizes benefit most from MessagePool
- ByteArray only acceptable for applications with infrequent messaging or where GC pressure is tolerable

**Code Complexity**:
- ByteArray: Simplest implementation with automatic memory management
- **MessagePool: Simple API with automatic return** - recommended for most use cases
- ArrayPool: Requires explicit Rent/Return calls and buffer lifecycle tracking
- Message: Native integration with moderate complexity
- MessageZeroCopy: Requires unmanaged memory management and free callbacks

**Protocol Requirements**:
- **MessagePool receive requires knowing message size:**
  - Use size-prefixed protocol: `Send([size][payload])`
  - Or use fixed-size messages
  - Most real-world protocols already support this
- Other strategies don't have this constraint for receiving

**Performance Requirements**:
- **Best overall performance: MessagePool** (when size is known)
  - 12% faster for 1KB messages, 3.4x faster for 128KB messages vs ByteArray
  - Zero GC pressure
  - Automatic buffer return
- When size is unknown: Message provides zero GC with moderate performance
- ByteArray only suitable when simplicity is paramount and GC pressure is acceptable

### I/O Threads

Configure I/O threads based on your workload:

```csharp
// Default: 1 I/O thread (suitable for most applications)
using var context = new Context();

// High-throughput: 2-4 I/O threads
using var context = new Context(ioThreads: 4);

// Rule of thumb: 1 thread per 4 CPU cores
var cores = Environment.ProcessorCount;
var threads = Math.Max(1, cores / 4);
using var context = new Context(ioThreads: threads);
```

**Guidelines**:
- 1 thread: Sufficient for most applications
- 2-4 threads: High-throughput applications
- More threads: Only if profiling shows I/O bottleneck

### High Water Marks (HWM)

Control message queuing with high water marks:

```csharp
using var socket = new Socket(context, SocketType.Pub);

// Set send high water mark (default: 1000)
socket.SetOption(SocketOption.Sndhwm, 10000);

// Set receive high water mark
socket.SetOption(SocketOption.Rcvhwm, 10000);

// For low-latency, use smaller HWM
socket.SetOption(SocketOption.Sndhwm, 100);
```

**Impact**:
- Higher HWM: More memory, better burst handling
- Lower HWM: Less memory, faster backpressure
- Default (1000): Good balance for most cases

### Batching Messages

Send messages in batches for higher throughput:

```csharp
using var socket = new Socket(context, SocketType.Push);
socket.Connect("tcp://localhost:5555");

// Batch sending
for (int i = 0; i < 10000; i++)
{
    socket.Send($"Message {i}", SendFlags.DontWait);
}

// Or use multi-part for logical batches
for (int batch = 0; batch < 100; batch++)
{
    for (int i = 0; i < 99; i++)
    {
        socket.Send($"Item {i}", SendFlags.SendMore);
    }
    socket.Send("Last item"); // Final frame
}
```

### Buffer Sizes

Adjust kernel socket buffers for throughput:

```csharp
using var socket = new Socket(context, SocketType.Push);

// Increase send buffer (default: OS-dependent)
socket.SetOption(SocketOption.Sndbuf, 256 * 1024); // 256KB

// Increase receive buffer
socket.SetOption(SocketOption.Rcvbuf, 256 * 1024);

// For ultra-high throughput
socket.SetOption(SocketOption.Sndbuf, 1024 * 1024); // 1MB
socket.SetOption(SocketOption.Rcvbuf, 1024 * 1024);
```

### Linger Time

Configure socket shutdown behavior:

```csharp
using var socket = new Socket(context, SocketType.Push);

// Wait up to 1 second for messages to send on close
socket.SetOption(SocketOption.Linger, 1000);

// Discard pending messages immediately (not recommended)
socket.SetOption(SocketOption.Linger, 0);

// Wait indefinitely (default: -1)
socket.SetOption(SocketOption.Linger, -1);
```

**Recommendations**:
- Development: 0 (fast shutdown)
- Production: 1000-5000 (graceful shutdown)
- Critical data: -1 (wait for all messages)

### Message Size Optimization

Choose appropriate message sizes:

```csharp
// Small messages (< 1KB): Best throughput
socket.Send("Small payload");

// Medium messages (1KB - 64KB): Good balance
var data = new byte[8192]; // 8KB
socket.Send(data);

// Large messages (> 64KB): Lower throughput but efficient
var largeData = new byte[1024 * 1024]; // 1MB
socket.Send(largeData);
```

**Performance by size**:
- 64B: 4.95M msg/sec
- 1KB: 1.36M msg/sec
- 64KB: 73K msg/sec

### Zero-Copy Operations

Use Message API for zero-copy:

```csharp
// Traditional: Creates copy
var data = socket.RecvBytes();
ProcessData(data);

// Zero-copy: No allocation
using var message = new Message();
socket.Recv(ref message, RecvFlags.None);
ProcessData(message.Data); // ReadOnlySpan<byte>
```

### Transport Selection

Choose the right transport for your use case:

| Transport | Performance | Use Case |
|-----------|-------------|----------|
| `inproc://` | Fastest | Same process, inter-thread |
| `ipc://` | Fast | Same machine, inter-process |
| `tcp://` | Good | Network communication |
| `pgm://` | Variable | Reliable multicast |

```csharp
// Fastest: inproc (memory copy only)
socket.Bind("inproc://fast-queue");

// Fast: IPC (Unix domain socket)
socket.Bind("ipc:///tmp/my-socket");

// Network: TCP
socket.Bind("tcp://*:5555");
```

## Best Practices

### Context Management

```csharp
// ✅ Correct: One context per application
using var context = new Context();
using var socket1 = new Socket(context, SocketType.Req);
using var socket2 = new Socket(context, SocketType.Rep);

// ❌ Incorrect: Multiple contexts
using var context1 = new Context();
using var context2 = new Context(); // Wasteful
```

### Socket Lifecycle

```csharp
// ✅ Correct: Always use 'using'
using var socket = new Socket(context, SocketType.Rep);
socket.Bind("tcp://*:5555");
// Socket automatically disposed

// ❌ Incorrect: Missing disposal
var socket = new Socket(context, SocketType.Rep);
socket.Bind("tcp://*:5555");
// Resource leak!

// ✅ Correct: Manual disposal
var socket = new Socket(context, SocketType.Rep);
try
{
    socket.Bind("tcp://*:5555");
    // Use socket...
}
finally
{
    socket.Dispose();
}
```

### Error Handling

```csharp
// ✅ Correct: Comprehensive error handling
try
{
    using var socket = new Socket(context, SocketType.Rep);
    socket.Bind("tcp://*:5555");

    while (true)
    {
        try
        {
            var msg = socket.RecvString();
            socket.Send(ProcessMessage(msg));
        }
        catch (ZmqException ex) when (ex.ErrorCode == ErrorCode.EAGAIN)
        {
            // Timeout, continue
            continue;
        }
    }
}
catch (ZmqException ex)
{
    Console.WriteLine($"ZMQ Error: {ex.ErrorCode} - {ex.Message}");
}

// ❌ Incorrect: Swallowing all exceptions
try
{
    var msg = socket.RecvString();
}
catch
{
    // Silent failure - bad!
}
```

### Bind vs Connect

```csharp
// ✅ Correct: Stable endpoints bind, dynamic endpoints connect
// Server (stable)
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

// Clients (dynamic)
using var client1 = new Socket(context, SocketType.Req);
client1.Connect("tcp://server:5555");

// ✅ Correct: Allows dynamic scaling
// Broker binds (stable)
broker.Bind("tcp://*:5555");

// Workers connect (can scale up/down)
worker1.Connect("tcp://broker:5555");
worker2.Connect("tcp://broker:5555");
```

### Pattern-Specific Practices

#### REQ-REP

```csharp
// ✅ Correct: Strict send-receive ordering
client.Send("Request");
var reply = client.RecvString();

// ❌ Incorrect: Out of order
client.Send("Request 1");
client.Send("Request 2"); // Error! Must receive first
```

#### PUB-SUB

```csharp
// ✅ Correct: Slow joiner handling
publisher.Bind("tcp://*:5556");
Thread.Sleep(100); // Allow subscribers to connect

// ✅ Correct: Always subscribe
subscriber.Subscribe("topic");
var msg = subscriber.RecvString();

// ❌ Incorrect: Missing subscription
var msg = subscriber.RecvString(); // Will never receive!
```

#### PUSH-PULL

```csharp
// ✅ Correct: Bind producer, connect workers
producer.Bind("tcp://*:5557");
worker.Connect("tcp://localhost:5557");

// ✅ Correct: Workers can scale dynamically
worker1.Connect("tcp://localhost:5557");
worker2.Connect("tcp://localhost:5557");
```

## Threading and Concurrency

### Thread Safety

ZeroMQ sockets are **NOT** thread-safe. Each socket should be used by only one thread.

```csharp
// ❌ Incorrect: Sharing socket across threads
using var socket = new Socket(context, SocketType.Push);

var thread1 = new Thread(() => socket.Send("From thread 1"));
var thread2 = new Thread(() => socket.Send("From thread 2"));
// RACE CONDITION!

// ✅ Correct: One socket per thread
var thread1 = new Thread(() =>
{
    using var socket = new Socket(context, SocketType.Push);
    socket.Connect("tcp://localhost:5555");
    socket.Send("From thread 1");
});

var thread2 = new Thread(() =>
{
    using var socket = new Socket(context, SocketType.Push);
    socket.Connect("tcp://localhost:5555");
    socket.Send("From thread 2");
});
```

### Inter-thread Communication

Use PAIR sockets with inproc:// for thread coordination:

```csharp
using var context = new Context();

var thread1 = new Thread(() =>
{
    using var socket = new Socket(context, SocketType.Pair);
    socket.Bind("inproc://thread-comm");

    socket.Send("Hello from thread 1");
    var reply = socket.RecvString();
    Console.WriteLine($"Thread 1 received: {reply}");
});

var thread2 = new Thread(() =>
{
    Thread.Sleep(100); // Ensure bind happens first
    using var socket = new Socket(context, SocketType.Pair);
    socket.Connect("inproc://thread-comm");

    var msg = socket.RecvString();
    Console.WriteLine($"Thread 2 received: {msg}");
    socket.Send("Hello from thread 2");
});

thread1.Start();
thread2.Start();
thread1.Join();
thread2.Join();
```

### Task-Based Async Pattern

Wrap blocking operations in tasks:

```csharp
using var context = new Context();
using var socket = new Socket(context, SocketType.Rep);
socket.Bind("tcp://*:5555");

// Async receive
var receiveTask = Task.Run(() =>
{
    return socket.RecvString();
});

// Wait with timeout
if (await Task.WhenAny(receiveTask, Task.Delay(5000)) == receiveTask)
{
    var message = await receiveTask;
    Console.WriteLine($"Received: {message}");
}
else
{
    Console.WriteLine("Timeout");
}
```

## Security

### CURVE Authentication

Enable encryption with CURVE:

```csharp
// Generate key pairs (do this once, store securely)
var (serverPublic, serverSecret) = GenerateCurveKeyPair();
var (clientPublic, clientSecret) = GenerateCurveKeyPair();

// Server
using var server = new Socket(context, SocketType.Rep);
server.SetOption(SocketOption.CurveServer, true);
server.SetOption(SocketOption.CurveSecretkey, serverSecret);
server.Bind("tcp://*:5555");

// Client
using var client = new Socket(context, SocketType.Req);
client.SetOption(SocketOption.CurveServerkey, serverPublic);
client.SetOption(SocketOption.CurvePublickey, clientPublic);
client.SetOption(SocketOption.CurveSecretkey, clientSecret);
client.Connect("tcp://localhost:5555");
```

**Note**: Check if CURVE is available:
```csharp
bool hasCurve = Context.Has("curve");
if (!hasCurve)
{
    Console.WriteLine("CURVE not available in this ZMQ build");
}
```

### IP Filtering

Restrict connections by IP:

```csharp
// TODO: Add IP filtering examples when SocketOption supports it
// This feature may require direct ZMQ API calls
```

## Monitoring and Diagnostics

### Socket Events

Monitor socket events (requires draft API):

```csharp
// Check if monitoring is available
bool hasDraft = Context.Has("draft");

if (hasDraft)
{
    // Monitor socket events
    // TODO: Add monitoring examples when API is available
}
```

### Logging

Implement custom logging:

```csharp
public class ZmqLogger
{
    public static void LogSend(Socket socket, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SEND: {message}");
    }

    public static void LogRecv(Socket socket, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RECV: {message}");
    }
}

// Usage
var message = "Hello";
ZmqLogger.LogSend(socket, message);
socket.Send(message);
```

## Troubleshooting

### Common Issues

#### Connection Refused

```csharp
// Problem: Server not running or wrong address
client.Connect("tcp://localhost:5555"); // Throws or hangs

// Solution: Verify server is running and address is correct
// Check with: netstat -an | grep 5555
```

#### Address Already in Use

```csharp
// Problem: Port already bound
socket.Bind("tcp://*:5555"); // Throws ZmqException

// Solution: Use different port or stop conflicting process
socket.Bind("tcp://*:5556");

// Or set SO_REUSEADDR (not recommended for most cases)
```

#### Messages Not Received (PUB-SUB)

```csharp
// Problem: No subscription or slow joiner
subscriber.Connect("tcp://localhost:5556");
var msg = subscriber.RecvString(); // Never receives

// Solution: Add subscription and delay
subscriber.Subscribe("");
Thread.Sleep(100); // Allow connection to establish
```

#### Socket Hangs on Close

```csharp
// Problem: Default linger waits indefinitely
socket.Dispose(); // Hangs if messages pending

// Solution: Set linger time
socket.SetOption(SocketOption.Linger, 1000); // Wait max 1 second
socket.Dispose();
```

#### High Memory Usage

```csharp
// Problem: High water marks too large
socket.SetOption(SocketOption.SendHwm, 1000000); // 1M messages!

// Solution: Reduce HWM or implement backpressure
socket.SetOption(SocketOption.SendHwm, 1000);
```

### Debugging Tips

#### Enable Verbose Logging

```csharp
public static class ZmqDebug
{
    public static void DumpSocketInfo(Socket socket)
    {
        var type = socket.GetOption<int>(SocketOption.Type);
        var rcvMore = socket.GetOption<bool>(SocketOption.RcvMore);
        var events = socket.GetOption<int>(SocketOption.Events);

        Console.WriteLine($"Socket Type: {type}");
        Console.WriteLine($"RcvMore: {rcvMore}");
        Console.WriteLine($"Events: {events}");
    }
}
```

#### Check ZeroMQ Version

```csharp
var (major, minor, patch) = Context.Version;
Console.WriteLine($"ZeroMQ Version: {major}.{minor}.{patch}");

// Check capabilities
Console.WriteLine($"CURVE: {Context.Has("curve")}");
Console.WriteLine($"DRAFT: {Context.Has("draft")}");
```

#### Test Connectivity

```csharp
public static bool TestConnection(string endpoint, int timeoutMs = 5000)
{
    try
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        socket.SetOption(SocketOption.SendTimeout, timeoutMs);
        socket.SetOption(SocketOption.RcvTimeout, timeoutMs);

        socket.Connect(endpoint);
        socket.Send("PING");

        var reply = socket.RecvString();
        return reply == "PONG";
    }
    catch
    {
        return false;
    }
}
```

## Platform-Specific Considerations

### Windows

- TCP works well for all scenarios
- IPC (Unix domain sockets) not available
- Use named pipes or TCP for inter-process

### Linux

- IPC preferred for inter-process (faster than TCP)
- TCP for network communication
- Consider `SO_REUSEPORT` for load balancing

### macOS

- Similar to Linux
- IPC available and recommended for inter-process
- TCP for network communication

## Migration Guide

### From NetMQ

NetMQ users will find Net.Zmq familiar but with some differences:

| NetMQ | Net.Zmq |
|-------|---------|
| `using (var socket = new RequestSocket())` | `using var socket = new Socket(ctx, SocketType.Req)` |
| `socket.SendFrame("msg")` | `socket.Send("msg")` |
| `var msg = socket.ReceiveFrameString()` | `var msg = socket.RecvString()` |
| `NetMQMessage` | Multi-part with `SendFlags.SendMore` |

### From pyzmq

Python ZeroMQ users will find similar patterns:

| pyzmq | Net.Zmq |
|-------|---------|
| `ctx = zmq.Context()` | `var ctx = new Context()` |
| `sock = ctx.socket(zmq.REQ)` | `var sock = new Socket(ctx, SocketType.Req)` |
| `sock.send_string("msg")` | `sock.Send("msg")` |
| `msg = sock.recv_string()` | `var msg = sock.RecvString()` |

## Next Steps

- Review [Getting Started](getting-started.md) for basics
- Study [Messaging Patterns](patterns.md) for pattern details
- Explore [API Usage](api-usage.md) for API documentation
- Check [API Reference](../api/index.html) for complete API docs
- Read [ZeroMQ Guide](https://zguide.zeromq.org/) for architectural patterns
