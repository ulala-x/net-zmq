# NetZeroMQ

[![Build and Test](https://github.com/ulala-x/netzeromq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/netzeromq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/NetZeroMQ.svg)](https://www.nuget.org/packages/NetZeroMQ)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern .NET 8+ binding for ZeroMQ (libzmq) with cppzmq-style API.

## Features

- **Modern .NET**: Built for .NET 8.0+ with LibraryImport source generators
- **cppzmq Style**: Familiar API for developers coming from C++
- **Type Safe**: Strongly-typed socket options, message properties, and enums
- **Cross-Platform**: Supports Windows, Linux, and macOS (x64, ARM64)
- **Safe by Default**: SafeHandle-based resource management

## Installation

```bash
dotnet add package NetZeroMQ
```

## Quick Start

### REQ-REP Pattern

```csharp
using NetZeroMQ;

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
using NetZeroMQ;

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
using NetZeroMQ;

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
using NetZeroMQ;

var items = new PollItem[]
{
    new(socket1, PollEvents.In),
    new(socket2, PollEvents.In)
};

if (Poller.Poll(items, timeout: 1000) > 0)
{
    if (items[0].IsReadable) { /* handle socket1 */ }
    if (items[1].IsReadable) { /* handle socket2 */ }
}
```

### Message API

```csharp
using NetZeroMQ;

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
bool sent = socket.TrySend(data, out int bytesSent);

// Receive
string str = socket.RecvString();
byte[] data = socket.Recv(buffer);
socket.Recv(ref message);
bool received = socket.TryRecvString(out string? result);

// Options
socket.SetOption(SocketOption.Linger, 0);
int linger = socket.GetOption<int>(SocketOption.Linger);
```

## Requirements

- .NET 8.0 or later
- Native libzmq library (automatically provided via NetZeroMQ.Native package)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Related Projects

- [libzmq](https://github.com/zeromq/libzmq) - ZeroMQ core library
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ binding (API inspiration)
- [libzmq-native](https://github.com/ulala-x/libzmq-native) - Native binaries
