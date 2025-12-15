# Net.Zmq Documentation

Welcome to the Net.Zmq documentation! Net.Zmq is a modern .NET 8+ binding for ZeroMQ (libzmq) with a cppzmq-style API.

## Overview

Net.Zmq provides high-performance message queuing for distributed applications with a familiar, easy-to-use API inspired by cppzmq.

### Key Features

- **Modern .NET**: Built for .NET 8.0+ with `[LibraryImport]` source generators (no runtime marshalling overhead)
- **cppzmq Style**: Familiar API for developers coming from C++
- **Type Safe**: Strongly-typed socket options, message properties, and enums
- **Cross-Platform**: Supports Windows, Linux, and macOS (x64, ARM64)
- **Safe by Default**: SafeHandle-based resource management
- **High Performance**: 4.95M messages/sec throughput with 202ns latency

## Getting Started

### Installation

Install Net.Zmq via NuGet:

```bash
dotnet add package Net.Zmq
```

### Quick Example

```csharp
using Net.Zmq;

// Create a context
using var ctx = new Context();

// Server
using var server = new Socket(ctx, SocketType.Rep);
server.Bind("tcp://*:5555");

// Client
using var client = new Socket(ctx, SocketType.Req);
client.Connect("tcp://localhost:5555");

// Send and receive
client.Send("Hello");
var message = server.RecvString();
server.Send("World");
var reply = client.RecvString();
```

## Messaging Patterns

Net.Zmq supports all standard ZeroMQ patterns:

- **Request-Reply**: Synchronous client-server pattern
- **Publish-Subscribe**: One-to-many distribution pattern
- **Push-Pull**: Load-balanced pipeline pattern
- **Router-Dealer**: Asynchronous request-reply pattern
- **Pair**: Exclusive connection between two peers

## Performance

Net.Zmq delivers exceptional performance:

- **Peak Throughput**: 4.95M msg/sec (PUSH/PULL, 64B)
- **Ultra-Low Latency**: 202ns per message
- **Memory Efficient**: 441B allocation per 10K messages

See the [benchmarks documentation](https://github.com/ulala-x/net-zmq/blob/main/BENCHMARKS.md) for detailed performance metrics.

## API Reference

Browse the complete [API Reference](api/Net.Zmq.html) for detailed information about all classes, methods, and properties.

### Core Classes

- **[Context](api/Net.Zmq.Context.html)**: ZeroMQ context management
- **[Socket](api/Net.Zmq.Socket.html)**: Socket operations (send, receive, connect, bind)
- **[Message](api/Net.Zmq.Message.html)**: Message frame handling
- **[Poller](api/Net.Zmq.Poller.html)**: Multiplexing multiple sockets

## Platform Support

| Platform | Architecture |
|----------|--------------|
| Windows  | x64, ARM64   |
| Linux    | x64, ARM64   |
| macOS    | x64, ARM64   |

## Requirements

- .NET 8.0 or later
- Native libzmq library (automatically provided via Net.Zmq.Native package)

## Contributing

Contributions are welcome! Please see the [contributing guide](https://github.com/ulala-x/net-zmq/blob/main/CONTRIBUTING.md) for details.

## License

Net.Zmq is licensed under the MIT License. See [LICENSE](https://github.com/ulala-x/net-zmq/blob/main/LICENSE) for details.

## Resources

- [GitHub Repository](https://github.com/ulala-x/net-zmq)
- [NuGet Package](https://www.nuget.org/packages/Net.Zmq)
- [ZeroMQ Guide](https://zguide.zeromq.org/)
- [libzmq Documentation](https://libzmq.readthedocs.io/)
