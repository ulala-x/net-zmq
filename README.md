# Net.Zmq

[![Build and Test](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Zmq.svg)](https://www.nuget.org/packages/Net.Zmq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Documentation](https://img.shields.io/badge/docs-online-blue.svg)](https://ulala-x.github.io/net-zmq/)
[![Changelog](https://img.shields.io/badge/changelog-v0.1.0-green.svg)](CHANGELOG.md)

A modern .NET 8+ binding for ZeroMQ (libzmq) with cppzmq-style API.

## Features

- **Modern .NET**: Built for .NET 8.0+ with `[LibraryImport]` source generators (no runtime marshalling overhead)
- **cppzmq Style**: Familiar API for developers coming from C++
- **Type Safe**: Strongly-typed socket options, message properties, and enums
- **Cross-Platform**: Supports Windows, Linux, and macOS (x64, ARM64)
- **Safe by Default**: SafeHandle-based resource management

## Installation

```bash
dotnet add package Net.Zmq
```

## Quick Start

### REQ-REP Pattern

```csharp
using Net.Zmq;

// Server
using var ctx = new Context();
using var server = new Socket(ctx, SocketType.Rep);
server.Bind("tcp://*:5555");

var request = server.RecvString();
server.Send("World");

// Client
using var client = new Socket(ctx, SocketType.Req);
client.Connect("tcp://localhost:5555");
client.Send("Hello");
var reply = client.RecvString();
```

### PUB-SUB Pattern

```csharp
using Net.Zmq;

// Publisher
using var ctx = new Context();
using var pub = new Socket(ctx, SocketType.Pub);
pub.Bind("tcp://*:5556");
pub.Send("topic1 Hello subscribers!");

// Subscriber
using var sub = new Socket(ctx, SocketType.Sub);
sub.Connect("tcp://localhost:5556");
sub.Subscribe("topic1");
var message = sub.RecvString();
```

### Router-to-Router Pattern

```csharp
using System.Text;
using Net.Zmq;

using var ctx = new Context();
using var peerA = new Socket(ctx, SocketType.Router);
using var peerB = new Socket(ctx, SocketType.Router);

// Set explicit identities for Router-to-Router
peerA.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_A"));
peerB.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_B"));

peerA.Bind("tcp://127.0.0.1:5555");
peerB.Connect("tcp://127.0.0.1:5555");

// Peer B sends to Peer A (first frame = target identity)
peerB.Send(Encoding.UTF8.GetBytes("PEER_A"), SendFlags.SendMore);
peerB.Send("Hello from Peer B!");

// Peer A receives (first frame = sender identity)
var senderId = Encoding.UTF8.GetString(peerA.RecvBytes());
var message = peerA.RecvString();

// Peer A replies using sender's identity
peerA.Send(Encoding.UTF8.GetBytes(senderId), SendFlags.SendMore);
peerA.Send("Hello back from Peer A!");
```

### Polling

```csharp
using Net.Zmq;

// Create Poller instance
using var poller = new Poller(capacity: 2);

// Add sockets and store their indices
int idx1 = poller.Add(socket1, PollEvents.In);
int idx2 = poller.Add(socket2, PollEvents.In);

// Poll for events
if (poller.Poll(timeout: 1000) > 0)
{
    if (poller.IsReadable(idx1)) { /* handle socket1 */ }
    if (poller.IsReadable(idx2)) { /* handle socket2 */ }
}
```

### Message API

```csharp
using Net.Zmq;

// Create and send message
using var msg = new Message("Hello World");
socket.Send(ref msg, SendFlags.None);

// Receive message
using var reply = new Message();
socket.Recv(ref reply, RecvFlags.None);
Console.WriteLine(reply.ToString());
```

## Socket Types

| Type | Description |
|------|-------------|
| `SocketType.Req` | Request socket (client) |
| `SocketType.Rep` | Reply socket (server) |
| `SocketType.Pub` | Publish socket |
| `SocketType.Sub` | Subscribe socket |
| `SocketType.Push` | Push socket (pipeline) |
| `SocketType.Pull` | Pull socket (pipeline) |
| `SocketType.Dealer` | Async request |
| `SocketType.Router` | Async reply |
| `SocketType.Pair` | Exclusive pair |

## API Reference

### Context

```csharp
var ctx = new Context();                           // Default
var ctx = new Context(ioThreads: 2, maxSockets: 1024);  // Custom

ctx.SetOption(ContextOption.IoThreads, 4);
var threads = ctx.GetOption(ContextOption.IoThreads);

var (major, minor, patch) = Context.Version;       // Get ZMQ version
bool hasCurve = Context.Has("curve");              // Check capability
```

### Socket

```csharp
var socket = new Socket(ctx, SocketType.Req);

// Connection
socket.Bind("tcp://*:5555");
socket.Connect("tcp://localhost:5555");
socket.Unbind("tcp://*:5555");
socket.Disconnect("tcp://localhost:5555");

// Send
socket.Send("Hello");
socket.Send(byteArray);
socket.Send(ref message, SendFlags.SendMore);
int result = socket.Send(data, SendFlags.DontWait); // -1 if would block

// Receive
string str = socket.RecvString();
byte[] data = socket.Recv(buffer);
socket.Recv(ref message);
bool received = socket.TryRecvString(out string? result);

// Options
socket.SetOption(SocketOption.Linger, 0);
int linger = socket.GetOption<int>(SocketOption.Linger);
```

## Performance

Net.Zmq offers multiple strategies for high-performance messaging. Choose the right approach based on your use case.

### Quick Start Guide

**Sending Messages:**

1. **Using External Memory** - Flexible, automatic optimization:
   ```csharp
   byte[] data = GetDataFromSomewhere();
   socket.SendOptimized(data);  // Automatically chooses best strategy by size
   ```

2. **Maximum Performance** - Consistent zero-copy for all sizes:
   ```csharp
   using var msg = MessagePool.Shared.Rent(MessageSize.Small);
   // Write your data to msg.Data
   socket.Send(ref msg, SendFlags.None);
   ```

**Receiving Messages:**

```csharp
using var poller = new Poller(1);
int idx = poller.Add(socket, PollEvents.In);

using var msg = new Message();
if (poller.Poll(timeout) > 0 && poller.IsReadable(idx))
{
    socket.Recv(ref msg, RecvFlags.None);
    // Process msg.Data
}
```

### Benchmark Results Summary

| Message Size | Best Send Strategy | Throughput | Best Receive Mode | Throughput |
|--------------|-------------------|------------|-------------------|------------|
| **64 bytes** | ArrayPool | 4,082 K/sec | Poller | 5,464 K/sec |
| **512 bytes** | ArrayPool | 1,570 K/sec | Poller | 1,969 K/sec |
| **1 KB** | MessagePool | 1,424 K/sec | Poller | 1,377 K/sec |
| **64 KB** | MessagePool | 83.2 K/sec | Blocking | 69.8 K/sec |

**Key Insights:**

- **Send Strategies:**
  - Small messages (<1KB): ArrayPool is fastest (auto-selected by `SendOptimized()`)
  - Large messages (≥1KB): MessagePool is 18-23% faster, provides consistent zero-copy

- **Receive Modes:**
  - Poller mode is best for most scenarios (simple API + high performance)
  - Blocking mode slightly better for very large messages (64KB+)
  - NonBlocking mode is consistently slowest (avoid unless necessary)

**Test Environment**: Intel Core Ultra 7 265K (20 cores), .NET 8.0.22, Ubuntu 24.04.3 LTS

For detailed benchmark results, usage examples, and decision flowcharts, see [benchmarks/Net.Zmq.Benchmarks/README.md](benchmarks/Net.Zmq.Benchmarks/README.md).

## Supported Platforms

| OS | Architecture |
|----|--------------|
| Windows | x64, ARM64 |
| Linux | x64, ARM64 |
| macOS | x64, ARM64 |

## Documentation

Complete API documentation is available at: [https://ulala-x.github.io/net-zmq/](https://ulala-x.github.io/net-zmq/)

The documentation includes:
- API Reference for all classes and methods
- Usage examples and patterns
- Performance benchmarks
- Platform-specific guides

## Requirements

- .NET 8.0 or later
- Native libzmq library (automatically provided via Net.Zmq.Native package)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Related Projects

- [libzmq](https://github.com/zeromq/libzmq) - ZeroMQ core library
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ binding (API inspiration)
- [libzmq-native](https://github.com/ulala-x/libzmq-native) - Native binaries
