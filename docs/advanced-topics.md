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
socket.SetOption(SocketOption.SendHwm, 10000);

// Set receive high water mark
socket.SetOption(SocketOption.RcvHwm, 10000);

// For low-latency, use smaller HWM
socket.SetOption(SocketOption.SendHwm, 100);
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
