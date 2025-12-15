# Getting Started

Welcome to Net.Zmq! This guide will help you get started with Net.Zmq, a modern .NET 8+ binding for ZeroMQ.

## Installation

Install Net.Zmq via NuGet Package Manager:

### Using .NET CLI

```bash
dotnet add package Net.Zmq
```

### Using Package Manager Console

```powershell
Install-Package Net.Zmq
```

### Using Visual Studio

1. Right-click on your project in Solution Explorer
2. Select "Manage NuGet Packages"
3. Search for "Net.Zmq"
4. Click "Install"

## Requirements

- **.NET 8.0 or later**: Net.Zmq is built for modern .NET
- **Native libzmq library**: Automatically included via the Net.Zmq.Native package dependency

## Your First Net.Zmq Application

Let's create a simple request-reply application to understand the basics.

### 1. Create a Server

The server will listen for incoming requests and send back replies.

```csharp
using Net.Zmq;

// Create a ZeroMQ context
using var context = new Context();

// Create a REP (Reply) socket
using var server = new Socket(context, SocketType.Rep);

// Bind to a TCP endpoint
server.Bind("tcp://*:5555");

Console.WriteLine("Server is listening on port 5555...");

while (true)
{
    // Wait for a request
    var request = server.RecvString();
    Console.WriteLine($"Received: {request}");

    // Send a reply
    server.Send("World");
}
```

### 2. Create a Client

The client will send requests and wait for replies.

```csharp
using Net.Zmq;

// Create a ZeroMQ context
using var context = new Context();

// Create a REQ (Request) socket
using var client = new Socket(context, SocketType.Req);

// Connect to the server
client.Connect("tcp://localhost:5555");

// Send a request
client.Send("Hello");
Console.WriteLine("Sent: Hello");

// Wait for reply
var reply = client.RecvString();
Console.WriteLine($"Received: {reply}");
```

### 3. Run the Application

1. Start the server application first
2. Run the client application
3. You should see "Hello" on the server console and "World" on the client console

## Basic Concepts

### Context

The `Context` is the container for all sockets in a single process. It manages I/O threads and internal resources.

```csharp
// Default context (1 I/O thread, 1024 max sockets)
using var context = new Context();

// Custom context
using var context = new Context(ioThreads: 2, maxSockets: 2048);
```

**Best Practice**: Use one context per application. Creating multiple contexts is rarely needed.

### Socket

A `Socket` is an endpoint for sending and receiving messages. Each socket has a type that defines its behavior.

```csharp
// Create a socket
using var socket = new Socket(context, SocketType.Rep);

// Bind to an endpoint (server-side)
socket.Bind("tcp://*:5555");

// Connect to an endpoint (client-side)
socket.Connect("tcp://localhost:5555");
```

### Message

Messages are the data units sent between sockets. Net.Zmq provides multiple ways to work with messages:

```csharp
// Send a string
socket.Send("Hello World");

// Send bytes
byte[] data = [1, 2, 3, 4, 5];
socket.Send(data);

// Use Message object for advanced scenarios
using var message = new Message("Hello");
socket.Send(ref message, SendFlags.None);

// Receive a string
string text = socket.RecvString();

// Receive bytes
byte[] received = socket.RecvBytes();
```

### Endpoints

Net.Zmq supports multiple transport protocols:

| Transport | Format | Description |
|-----------|--------|-------------|
| TCP | `tcp://hostname:port` | Network communication |
| IPC | `ipc:///tmp/socket` | Inter-process (Unix domain sockets) |
| In-Process | `inproc://name` | In-process communication (fastest) |
| PGM/EPGM | `pgm://interface;multicast` | Reliable multicast |

**Examples**:
```csharp
socket.Bind("tcp://*:5555");              // TCP on all interfaces
socket.Connect("tcp://192.168.1.100:5555"); // TCP to specific host
socket.Bind("ipc:///tmp/my-socket");       // Unix domain socket
socket.Bind("inproc://my-queue");          // In-process
```

## Socket Patterns

Net.Zmq supports several messaging patterns. The most common are:

### Request-Reply (REQ-REP)

Synchronous client-server pattern. Client sends a request, server sends a reply.

```csharp
// Server
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");
var request = server.RecvString();
server.Send("Response");

// Client
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");
client.Send("Request");
var reply = client.RecvString();
```

### Publish-Subscribe (PUB-SUB)

One-to-many distribution. Publisher sends messages to all subscribers.

```csharp
// Publisher
using var publisher = new Socket(context, SocketType.Pub);
publisher.Bind("tcp://*:5556");
publisher.Send("topic data");

// Subscriber
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");
subscriber.Subscribe("topic");
var message = subscriber.RecvString();
```

### Push-Pull (Pipeline)

Load-balanced work distribution. Tasks are distributed among workers.

```csharp
// Push (Producer)
using var pusher = new Socket(context, SocketType.Push);
pusher.Bind("tcp://*:5557");
pusher.Send("work item");

// Pull (Worker)
using var puller = new Socket(context, SocketType.Pull);
puller.Connect("tcp://localhost:5557");
var work = puller.RecvString();
```

See the [Messaging Patterns](patterns.md) guide for detailed information on all patterns.

## Resource Management

Net.Zmq uses `IDisposable` for proper resource cleanup. Always use `using` statements:

```csharp
// Correct: Using statement ensures proper cleanup
using var context = new Context();
using var socket = new Socket(context, SocketType.Rep);

// Also correct: Explicit dispose
var context = new Context();
try
{
    var socket = new Socket(context, SocketType.Rep);
    // Use socket...
}
finally
{
    context.Dispose();
}
```

## Error Handling

Net.Zmq throws exceptions for errors. Always handle them appropriately:

```csharp
try
{
    socket.Bind("tcp://*:5555");
    var message = socket.RecvString();
}
catch (ZmqException ex)
{
    Console.WriteLine($"ZMQ Error: {ex.ErrorCode} - {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Next Steps

- Learn about [Messaging Patterns](patterns.md) in detail
- Explore the [API Usage Guide](api-usage.md) for advanced features
- Check out [Advanced Topics](advanced-topics.md) for performance tuning and best practices
- Browse the [API Reference](../api/index.html) for complete documentation

## Additional Resources

- [ZeroMQ Guide](https://zguide.zeromq.org/) - The official ZeroMQ guide
- [GitHub Repository](https://github.com/ulala-x/net-zmq) - Source code and examples
- [NuGet Package](https://www.nuget.org/packages/Net.Zmq) - Latest releases
