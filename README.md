# Net.Zmq

[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

[![Build and Test](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Zmq.svg)](https://www.nuget.org/packages/Net.Zmq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Documentation](https://img.shields.io/badge/docs-online-blue.svg)](https://ulala-x.github.io/net-zmq/)
[![Changelog](https://img.shields.io/badge/changelog-v0.2.0-green.svg)](CHANGELOG.md)

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
peerA.SetOption(SocketOption.Routing_Id, "PEER_A"u8.ToArray());
peerB.SetOption(SocketOption.Routing_Id, "PEER_B"u8.ToArray());

peerA.Bind("tcp://127.0.0.1:5555");
peerB.Connect("tcp://127.0.0.1:5555");

// Peer B sends to Peer A (first frame = target identity)
peerB.Send("PEER_A"u8, SendFlags.SendMore);
peerB.Send("Hello from Peer B!");

// Peer A receives (first frame = sender identity)
Span<byte> idBuffer = stackalloc byte[64];
int idLen = peerA.Recv(idBuffer);
var senderId = idBuffer[..idLen];
var message = peerA.RecvString();

// Peer A replies using sender's identity
peerA.Send(senderId, SendFlags.SendMore);
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
socket.Send(msg);

// Receive message
using var reply = new Message();
socket.Recv(reply);
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
socket.Send(message, SendFlags.SendMore);
bool sent = socket.Send(data, SendFlags.DontWait); // false if would block

// Receive
string str = socket.RecvString();
int bytesRead = socket.Recv(buffer);
socket.Recv(message);
bool received = socket.TryRecvString(out string result);
bool gotData = socket.TryRecv(buffer, out int size);

// Options
socket.SetOption(SocketOption.Linger, 0);
int linger = socket.GetOption<int>(SocketOption.Linger);
```

## Performance

### Recommended Message Strategies

Net.Zmq provides multiple message buffer strategies to accommodate different performance requirements:

**Available Strategies:**
- **Basic Message**: Simple `Message` object for general use
- **ArrayPool**: Uses `ArrayPool<byte>.Shared` for buffer reuse (manual return required)
- **MessageZeroCopy**: Zero-copy messages using `Marshal.AllocHGlobal` for large data

**Recommendations by Message Size:**
- **Small messages (≤1KB)**: **`ArrayPool<byte>.Shared`** - Best performance with minimal GC pressure
- **Large messages (≥64KB)**: **`MessageZeroCopy`** - Zero-copy semantics with minimal GC overhead

**Receive Mode:**
- Single socket → Blocking
- Multiple sockets → `Poller`

```csharp
// Small messages: ArrayPool (recommended)
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Fill buffer with data
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// Large messages: MessageZeroCopy
nint nativePtr = Marshal.AllocHGlobal(dataSize);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, dataSize);
    sourceData.CopyTo(nativeSpan);
}
using var message = new Message(nativePtr, dataSize, ptr =>
{
    Marshal.FreeHGlobal(ptr); // Called when libzmq is done
});
socket.Send(message);

// Receive: Blocking with Message
using var msg = new Message();
socket.Recv(msg);
// Process msg.Data
```

For fine-tuning options and detailed benchmarks, see [docs/benchmarks.md](docs/benchmarks.md).

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
