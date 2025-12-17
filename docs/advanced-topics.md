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
- Blocking: 2.325 ms (4.30M msg/sec, 232.52 ns latency)
- Poller: 2.376 ms (4.21M msg/sec, 237.59 ns latency)
- NonBlocking: 11.447 ms (873.62K msg/sec, 1.14 μs latency)

**1500-Byte Messages**:
- Poller: 10.552 ms (947.66K msg/sec, 1.06 μs latency)
- Blocking: 11.040 ms (905.79K msg/sec, 1.10 μs latency)
- NonBlocking: 14.909 ms (670.72K msg/sec, 1.49 μs latency)

**65KB Messages**:
- Poller: 167.479 ms (59.71K msg/sec, 16.75 μs latency)
- Blocking: 168.915 ms (59.20K msg/sec, 16.89 μs latency)
- NonBlocking: 351.448 ms (28.45K msg/sec, 35.14 μs latency)

Blocking and Poller modes deliver similar performance (96-102% relative), with Poller allocating slightly more memory (323-504 bytes vs 203-384 bytes per 10K messages) for polling infrastructure. NonBlocking mode shows 1.35-4.92x slower performance due to sleep overhead when messages are not immediately available.

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
- Blocking and Poller modes achieve sub-microsecond latency (232-238 ns for 64-byte messages)
- NonBlocking mode adds millisecond-level latency due to sleep intervals

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

#### Performance and GC Characteristics

Benchmarked with Poller mode on ROUTER-to-ROUTER pattern (10,000 messages, Intel Core Ultra 7 265K):

**64-Byte Messages**:
- ArrayPool: 2.595 ms (3.85M msg/sec), 0 GC, 1.07 KB allocated
- ByteArray: 2.638 ms (3.79M msg/sec), 3.91 Gen0, 1719.07 KB allocated
- Message: 5.364 ms (1.86M msg/sec), 0 GC, 625.32 KB allocated
- MessageZeroCopy: 6.428 ms (1.56M msg/sec), 0 GC, 625.32 KB allocated

**1500-Byte Messages**:
- Message: 11.287 ms (886.00K msg/sec), 0 GC, 625.32 KB allocated
- ByteArray: 11.495 ms (869.97K msg/sec), 78.13 Gen0, 29844.07 KB allocated
- ArrayPool: 11.929 ms (838.30K msg/sec), 0 GC, 3.01 KB allocated
- MessageZeroCopy: 14.504 ms (689.46K msg/sec), 0 GC, 625.32 KB allocated

**65KB Messages**:
- MessageZeroCopy: 134.626 ms (74.28K msg/sec), 0 GC, 625.49 KB allocated
- Message: 142.068 ms (70.39K msg/sec), 0 GC, 625.49 KB allocated
- ArrayPool: 148.562 ms (67.31K msg/sec), 0 GC, 65.21 KB allocated
- ByteArray: 150.055 ms (66.64K msg/sec), 3250 Gen0 + 250 Gen1, 1280469.24 KB allocated

#### The 1500-Byte Boundary

The transition from minimal to significant GC pressure occurs around 1500 bytes, which approximates Ethernet MTU (Maximum Transmission Unit):

- Below 1500B: All strategies show manageable GC behavior
- At 1500B: ByteArray begins showing GC pressure (78 Gen0 collections)
- Above 1500B: ByteArray triggers substantial garbage collection (3250 Gen0 + 250 Gen1 collections at 65KB)

ArrayPool, Message, and MessageZeroCopy maintain zero GC collections regardless of message size.

#### Selection Considerations

**Message Size Distribution**:
- For small messages (<1500B), performance differences are modest and GC pressure is manageable across all strategies
- For large messages (>1500B), ByteArray generates substantial GC pressure
- ArrayPool and native strategies maintain zero GC pressure regardless of message size

**GC Sensitivity**:
- Applications sensitive to GC pauses benefit from ArrayPool, Message, or MessageZeroCopy
- Applications with infrequent messaging or consistently small messages may find ByteArray acceptable
- High-throughput applications with variable message sizes benefit from GC-free strategies

**Code Complexity**:
- ByteArray offers the simplest implementation with automatic memory management
- ArrayPool requires explicit Rent/Return calls and buffer lifecycle tracking
- Message provides native integration with moderate complexity
- MessageZeroCopy requires unmanaged memory management and free callbacks

**Interop Overhead**:
- For small messages, managed strategies (ByteArray, ArrayPool) show lower overhead
- For large messages, native strategies (Message, MessageZeroCopy) can avoid managed/unmanaged copying
- The performance crossover depends on message size and access patterns

**Performance Requirements**:
- When throughput is critical and messages are small, ByteArray or ArrayPool are effective
- When throughput is critical and messages are large, Message or MessageZeroCopy reduce GC impact
- When latency consistency matters, GC-free strategies provide more predictable timing

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
