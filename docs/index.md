# Net.Zmq Documentation

Welcome to Net.Zmq! A modern .NET 8+ binding for ZeroMQ with a cppzmq-style API, delivering high-performance message queuing for distributed applications.

## What is Net.Zmq?

Net.Zmq is a .NET wrapper for ZeroMQ (libzmq), providing a clean, type-safe API for building distributed systems. It combines the power of ZeroMQ with modern .NET features like source generators and SafeHandles.

### Key Features

- **Modern .NET**: Built for .NET 8.0+ with `[LibraryImport]` source generators
- **High Performance**: 4.95M messages/sec throughput, 202ns latency
- **Type Safe**: Strongly-typed socket options and enums
- **Cross-Platform**: Windows, Linux, macOS (x64, ARM64)
- **Safe by Default**: SafeHandle-based resource management
- **cppzmq Style**: Familiar API for C++ developers

## Quick Start

Install via NuGet:

```bash
dotnet add package Net.Zmq
```

Simple request-reply example:

```csharp
using Net.Zmq;

using var context = new Context();

// Server
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

// Client
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");

// Communication
client.Send("Hello");
var request = server.RecvString();  // "Hello"
server.Send("World");
var reply = client.RecvString();    // "World"
```

## Documentation Guide

### For Beginners

Start with these guides to learn Net.Zmq from the ground up:

- **[Getting Started](getting-started.md)** - Installation, basic concepts, and your first application
- **[Messaging Patterns](patterns.md)** - REQ-REP, PUB-SUB, PUSH-PULL, and more

### For Developers

Deep dive into the API and advanced usage:

- **[API Usage Guide](api-usage.md)** - Detailed guide on Context, Socket, Message, and Poller
- **[Advanced Topics](advanced-topics.md)** - Performance tuning, best practices, security, and troubleshooting

### Reference

- **[API Reference](../api/index.html)** - Complete API documentation for all classes and methods

## Core Concepts

### Context

The context manages ZeroMQ resources (I/O threads, sockets). Create one per application:

```csharp
using var context = new Context();
```

### Socket Types

Net.Zmq supports all ZeroMQ socket types:

| Type | Description | Use Case |
|------|-------------|----------|
| REQ | Request | Client in client-server |
| REP | Reply | Server in client-server |
| PUB | Publish | Publisher in pub-sub |
| SUB | Subscribe | Subscriber in pub-sub |
| PUSH | Push | Producer in pipeline |
| PULL | Pull | Consumer in pipeline |
| DEALER | Dealer | Async request |
| ROUTER | Router | Async reply with routing |
| PAIR | Pair | Exclusive peer-to-peer |

### Messaging Patterns

Choose the right pattern for your use case:

- **Request-Reply (REQ-REP)**: Synchronous client-server communication
- **Publish-Subscribe (PUB-SUB)**: One-to-many message distribution
- **Push-Pull (Pipeline)**: Load-balanced work distribution
- **Router-Dealer**: Asynchronous request-reply with advanced routing
- **Pair**: Exclusive bidirectional connection

See the [Messaging Patterns](patterns.md) guide for detailed examples.

## Performance

Net.Zmq delivers exceptional performance:

| Message Size | Throughput | Latency | Pattern |
|--------------|------------|---------|---------|
| 64 bytes | 4.95M/sec | 202ns | PUSH/PULL |
| 1 KB | 1.36M/sec | 736ns | PUB/SUB |
| 64 KB | 73.47K/sec | 13.61μs | ROUTER/ROUTER |

**Test Environment**: Intel Core Ultra 7 265K, .NET 8.0.22, Ubuntu 24.04.3 LTS

See [docs/benchmarks.md](docs/benchmarks.md) for comprehensive performance metrics.

## Platform Support

| OS | Architecture | Status |
|----|--------------|--------|
| Windows | x64, ARM64 | ✅ Supported |
| Linux | x64, ARM64 | ✅ Supported |
| macOS | x64, ARM64 | ✅ Supported |

## Requirements

- **.NET 8.0 or later**
- **libzmq native library** (automatically included via Net.Zmq.Native package)

## Additional Resources

### Project Links

- [GitHub Repository](https://github.com/ulala-x/net-zmq) - Source code, issues, discussions
- [NuGet Package](https://www.nuget.org/packages/Net.Zmq) - Latest releases
- [Changelog](https://github.com/ulala-x/net-zmq/blob/main/CHANGELOG.md) - Release history

### ZeroMQ Resources

- [ZeroMQ Guide](https://zguide.zeromq.org/) - Comprehensive guide to ZeroMQ patterns
- [libzmq Documentation](https://libzmq.readthedocs.io/) - Core library documentation
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ binding (API inspiration)

### Community

- [Contributing Guide](https://github.com/ulala-x/net-zmq/blob/main/CONTRIBUTING.md) - How to contribute
- [GitHub Discussions](https://github.com/ulala-x/net-zmq/discussions) - Ask questions and share ideas
- [GitHub Issues](https://github.com/ulala-x/net-zmq/issues) - Report bugs and request features

## License

Net.Zmq is open source software licensed under the [MIT License](https://github.com/ulala-x/net-zmq/blob/main/LICENSE).
