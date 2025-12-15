# API Usage Guide

This guide provides detailed documentation on using the core Net.Zmq API classes: Context, Socket, Message, and Poller.

## Context

The `Context` class manages ZeroMQ resources, including I/O threads and sockets. Typically, you create one context per application.

### Creating a Context

```csharp
using Net.Zmq;

// Default context (1 I/O thread, 1024 max sockets)
using var context = new Context();

// Custom context with specific settings
using var context = new Context(ioThreads: 2, maxSockets: 2048);
```

### Context Options

You can configure the context using the `SetOption` and `GetOption` methods:

```csharp
using var context = new Context();

// Set I/O threads (must be set before creating sockets)
context.SetOption(ContextOption.IoThreads, 4);

// Set maximum number of sockets
context.SetOption(ContextOption.MaxSockets, 512);

// Set maximum message size (0 = unlimited)
context.SetOption(ContextOption.MaxMsgsz, 1024 * 1024); // 1MB

// Get current values
var ioThreads = context.GetOption(ContextOption.IoThreads);
var maxSockets = context.GetOption(ContextOption.MaxSockets);

Console.WriteLine($"I/O Threads: {ioThreads}, Max Sockets: {maxSockets}");
```

### Available Context Options

| Option | Type | Description |
|--------|------|-------------|
| `IoThreads` | int | Number of I/O threads (default: 1) |
| `MaxSockets` | int | Maximum number of sockets (default: 1024) |
| `MaxMsgsz` | int | Maximum message size in bytes (0 = unlimited) |
| `SocketLimit` | int | Largest configurable max sockets value |
| `Ipv6` | bool | Enable IPv6 support |
| `Blocky` | bool | Use blocking shutdown behavior |
| `ThreadPriority` | int | Thread scheduling priority |
| `ThreadSchedPolicy` | int | Thread scheduling policy |

### ZeroMQ Version and Capabilities

```csharp
// Get ZeroMQ library version
var (major, minor, patch) = Context.Version;
Console.WriteLine($"ZeroMQ Version: {major}.{minor}.{patch}");

// Check if a capability is supported
bool hasCurve = Context.Has("curve");      // Encryption support
bool hasDraft = Context.Has("draft");       // Draft API support
bool hasGssapi = Context.Has("gssapi");     // GSSAPI auth support

Console.WriteLine($"CURVE encryption: {hasCurve}");
```

### Resource Management

Always dispose the context when done:

```csharp
// Using statement (recommended)
using var context = new Context();

// Manual disposal
var context = new Context();
try
{
    // Use context...
}
finally
{
    context.Dispose();
}
```

## Socket

The `Socket` class represents a ZeroMQ socket endpoint for sending and receiving messages.

### Creating Sockets

```csharp
using var context = new Context();

// Create different socket types
using var req = new Socket(context, SocketType.Req);      // Request
using var rep = new Socket(context, SocketType.Rep);      // Reply
using var pub = new Socket(context, SocketType.Pub);      // Publish
using var sub = new Socket(context, SocketType.Sub);      // Subscribe
using var push = new Socket(context, SocketType.Push);    // Push
using var pull = new Socket(context, SocketType.Pull);    // Pull
using var dealer = new Socket(context, SocketType.Dealer); // Dealer
using var router = new Socket(context, SocketType.Router); // Router
using var pair = new Socket(context, SocketType.Pair);    // Pair
```

### Connecting and Binding

```csharp
using var socket = new Socket(context, SocketType.Rep);

// Bind (server-side, accepts connections)
socket.Bind("tcp://*:5555");                    // All interfaces
socket.Bind("tcp://192.168.1.100:5555");        // Specific interface
socket.Bind("ipc:///tmp/my-socket");            // Unix domain socket
socket.Bind("inproc://my-endpoint");            // In-process

// Connect (client-side, initiates connection)
socket.Connect("tcp://localhost:5555");
socket.Connect("tcp://192.168.1.100:5555");

// Unbind and disconnect
socket.Unbind("tcp://*:5555");
socket.Disconnect("tcp://localhost:5555");
```

### Sending Messages

Net.Zmq provides multiple methods for sending messages:

#### Send String

```csharp
// Simple string send
socket.Send("Hello World");

// Send with encoding
socket.Send("안녕하세요", Encoding.UTF8);

// Try send (non-blocking)
bool sent = socket.TrySend("Hello", out int bytesSent);
if (sent)
{
    Console.WriteLine($"Sent {bytesSent} bytes");
}
```

#### Send Bytes

```csharp
// Send byte array
byte[] data = [1, 2, 3, 4, 5];
socket.Send(data);

// Send with flags
socket.Send(data, SendFlags.DontWait);

// Try send bytes
bool sent = socket.TrySend(data, out int bytesSent);
```

#### Send Multi-part Messages

```csharp
// Send multi-part message
socket.Send("Header", SendFlags.SendMore);
socket.Send("Body", SendFlags.SendMore);
socket.Send("Footer"); // Last frame without SendMore

// With bytes
socket.Send(headerBytes, SendFlags.SendMore);
socket.Send(bodyBytes);
```

#### Send Flags

| Flag | Description |
|------|-------------|
| `None` | Blocking send |
| `DontWait` | Non-blocking send |
| `SendMore` | More message frames to follow |

### Receiving Messages

#### Receive String

```csharp
// Blocking receive
string message = socket.RecvString();

// With encoding
string message = socket.RecvString(Encoding.UTF8);

// Non-blocking receive
bool received = socket.TryRecvString(out string? result);
if (received)
{
    Console.WriteLine($"Received: {result}");
}
```

#### Receive Bytes

```csharp
// Receive into new array
byte[] data = socket.RecvBytes();

// Receive into existing buffer
byte[] buffer = new byte[1024];
int bytesReceived = socket.Recv(buffer);
Console.WriteLine($"Received {bytesReceived} bytes");

// Non-blocking receive
bool received = socket.TryRecvBytes(out byte[]? result);
if (received && result != null)
{
    Console.WriteLine($"Received {result.Length} bytes");
}
```

#### Receive Multi-part Messages

```csharp
// Check if more frames are available
var part1 = socket.RecvString();
bool hasMore = socket.GetOption<bool>(SocketOption.RcvMore);

if (hasMore)
{
    var part2 = socket.RecvString();
}

// Receive all parts
var parts = new List<string>();
do
{
    parts.Add(socket.RecvString());
} while (socket.GetOption<bool>(SocketOption.RcvMore));
```

#### Receive Flags

| Flag | Description |
|------|-------------|
| `None` | Blocking receive |
| `DontWait` | Non-blocking receive |

### Socket Options

Configure socket behavior using options:

```csharp
using var socket = new Socket(context, SocketType.Rep);

// Set options
socket.SetOption(SocketOption.Linger, 1000);           // Linger time (ms)
socket.SetOption(SocketOption.SendHwm, 1000);          // Send high water mark
socket.SetOption(SocketOption.RcvHwm, 1000);           // Receive high water mark
socket.SetOption(SocketOption.SendTimeout, 5000);      // Send timeout (ms)
socket.SetOption(SocketOption.RcvTimeout, 5000);       // Receive timeout (ms)
socket.SetOption(SocketOption.Sndbuf, 131072);         // Send buffer size
socket.SetOption(SocketOption.Rcvbuf, 131072);         // Receive buffer size

// Get options
int linger = socket.GetOption<int>(SocketOption.Linger);
int sendHwm = socket.GetOption<int>(SocketOption.SendHwm);

Console.WriteLine($"Linger: {linger}ms, Send HWM: {sendHwm}");
```

### Common Socket Options

| Option | Type | Description |
|--------|------|-------------|
| `Linger` | int | Time to wait for pending messages on close (ms) |
| `SendHwm` | int | High water mark for outbound messages |
| `RcvHwm` | int | High water mark for inbound messages |
| `SendTimeout` | int | Send timeout in milliseconds |
| `RcvTimeout` | int | Receive timeout in milliseconds |
| `Sndbuf` | int | Kernel send buffer size |
| `Rcvbuf` | int | Kernel receive buffer size |
| `Routing_Id` | byte[] | Socket identity for ROUTER sockets |
| `RcvMore` | bool | More message frames available |

### Subscribe/Unsubscribe (SUB sockets only)

```csharp
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");

// Subscribe to topics
subscriber.Subscribe("weather.");
subscriber.Subscribe("stock.AAPL");
subscriber.Subscribe("");  // All messages

// Unsubscribe
subscriber.Unsubscribe("weather.");
```

## Message

The `Message` class provides low-level control over message frames.

### Creating Messages

```csharp
using Net.Zmq;

// Empty message
using var msg1 = new Message();

// From string
using var msg2 = new Message("Hello World");

// From byte array
byte[] data = [1, 2, 3, 4, 5];
using var msg3 = new Message(data);

// With specific size
using var msg4 = new Message(1024); // Allocates 1KB
```

### Message Properties

```csharp
using var message = new Message("Hello");

// Get message data as span
ReadOnlySpan<byte> data = message.Data;

// Get size
int size = message.Size;

// Convert to string
string text = message.ToString();
string utf8Text = message.ToString(Encoding.UTF8);

// Get byte array
byte[] bytes = message.ToByteArray();

// Check if more frames follow
bool hasMore = message.More;
```

### Sending Messages

```csharp
using var message = new Message("Hello World");

// Send message (note: ref keyword required)
socket.Send(ref message, SendFlags.None);

// Multi-part send
using var header = new Message("Header");
using var body = new Message("Body");

socket.Send(ref header, SendFlags.SendMore);
socket.Send(ref body, SendFlags.None);
```

### Receiving Messages

```csharp
using var message = new Message();

// Receive into message
socket.Recv(ref message, RecvFlags.None);

// Process message
Console.WriteLine($"Size: {message.Size}");
Console.WriteLine($"Content: {message.ToString()}");

// Non-blocking receive
using var msg = new Message();
bool received = socket.TryRecv(ref msg);
if (received)
{
    Console.WriteLine($"Received: {msg.ToString()}");
}
```

### Message Metadata

```csharp
using var message = new Message();
socket.Recv(ref message);

// Get metadata property (e.g., for ZMTP 3.0 properties)
string? property = message.Gets("Property-Name");
if (property != null)
{
    Console.WriteLine($"Property: {property}");
}
```

## Poller

The `Poller` class enables multiplexing I/O events across multiple sockets.

### Basic Polling

```csharp
using Net.Zmq;

using var context = new Context();
using var socket1 = new Socket(context, SocketType.Pull);
using var socket2 = new Socket(context, SocketType.Pull);

socket1.Bind("tcp://*:5555");
socket2.Bind("tcp://*:5556");

// Create poll items
var items = new PollItem[]
{
    new PollItem(socket1, PollEvents.In),
    new PollItem(socket2, PollEvents.In)
};

// Poll with 1 second timeout
while (true)
{
    int events = Poller.Poll(items, timeout: 1000);

    if (events > 0)
    {
        // Check which sockets are ready
        if (items[0].IsReadable)
        {
            var msg = socket1.RecvString();
            Console.WriteLine($"Socket 1: {msg}");
        }

        if (items[1].IsReadable)
        {
            var msg = socket2.RecvString();
            Console.WriteLine($"Socket 2: {msg}");
        }
    }
    else
    {
        Console.WriteLine("Poll timeout");
    }
}
```

### Poll Events

```csharp
// Create poll items with different event types
var items = new PollItem[]
{
    new PollItem(socket1, PollEvents.In),           // Read events
    new PollItem(socket2, PollEvents.Out),          // Write events
    new PollItem(socket3, PollEvents.In | PollEvents.Out), // Both
    new PollItem(socket4, PollEvents.Err)           // Error events
};

int count = Poller.Poll(items, timeout: 1000);

// Check event types
if (items[0].IsReadable) { /* Handle read */ }
if (items[1].IsWritable) { /* Handle write */ }
if (items[2].IsReadable || items[2].IsWritable) { /* Handle both */ }
if (items[3].HasError) { /* Handle error */ }
```

### Poll Timeout

```csharp
// Block indefinitely until event occurs
Poller.Poll(items, timeout: -1);

// Return immediately (non-blocking)
Poller.Poll(items, timeout: 0);

// Wait up to 5 seconds
Poller.Poll(items, timeout: 5000);
```

### Advanced Polling Example

```csharp
using Net.Zmq;

using var context = new Context();

// Create multiple sockets
using var receiver = new Socket(context, SocketType.Pull);
using var sender = new Socket(context, SocketType.Push);
using var control = new Socket(context, SocketType.Pair);

receiver.Bind("tcp://*:5555");
sender.Connect("tcp://localhost:5556");
control.Bind("inproc://control");

// Setup poll items
var items = new PollItem[]
{
    new PollItem(receiver, PollEvents.In),
    new PollItem(control, PollEvents.In)
};

bool running = true;
while (running)
{
    // Poll with 100ms timeout
    if (Poller.Poll(items, timeout: 100) > 0)
    {
        // Handle incoming messages
        if (items[0].IsReadable)
        {
            var msg = receiver.RecvString();
            Console.WriteLine($"Received: {msg}");

            // Forward to sender
            sender.Send($"Processed: {msg}");
        }

        // Handle control messages
        if (items[1].IsReadable)
        {
            var cmd = control.RecvString();
            if (cmd == "STOP")
            {
                running = false;
            }
        }
    }
}
```

## Error Handling

Net.Zmq throws exceptions for errors:

```csharp
using Net.Zmq;

try
{
    using var context = new Context();
    using var socket = new Socket(context, SocketType.Rep);

    socket.Bind("tcp://*:5555");
    var message = socket.RecvString();
}
catch (ZmqException ex)
{
    Console.WriteLine($"ZMQ Error {ex.ErrorCode}: {ex.Message}");

    // Common error codes
    if (ex.ErrorCode == ErrorCode.EADDRINUSE)
    {
        Console.WriteLine("Address already in use");
    }
    else if (ex.ErrorCode == ErrorCode.EAGAIN)
    {
        Console.WriteLine("Resource temporarily unavailable");
    }
}
catch (ObjectDisposedException)
{
    Console.WriteLine("Socket or context already disposed");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Best Practices

### Context

- Create one context per application
- Dispose context only after all sockets are closed
- Set I/O threads based on CPU cores (typically 1 per 4 cores)

### Socket

- Always use `using` statements for automatic disposal
- Set timeouts to prevent indefinite blocking
- Configure high water marks to prevent memory issues
- Use appropriate socket types for your pattern

### Message

- Use string/byte methods for simple cases
- Use Message class for zero-copy scenarios
- Always dispose messages explicitly
- Avoid copying large message data unnecessarily

### Poller

- Use polling for multiple socket I/O
- Set reasonable timeout values
- Handle all possible events
- Consider using separate threads for blocking I/O

## Next Steps

- Explore [Messaging Patterns](patterns.md) for practical examples
- Read [Advanced Topics](advanced-topics.md) for performance optimization
- Browse the [API Reference](../api/index.html) for complete documentation
