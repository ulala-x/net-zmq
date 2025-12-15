# Net.Zmq

[![Build and Test](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Zmq.svg)](https://www.nuget.org/packages/Net.Zmq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern .NET 8+ binding for ZeroMQ (libzmq) with cppzmq-style API.

## Features

- **Modern .NET**: Built for .NET 8.0+ with `[LibraryImport]` source generators
- **High Performance**: 4.95M messages/sec throughput, 202ns latency
- **Type Safe**: Strongly-typed socket options, message properties, and enums
- **Cross-Platform**: Windows, Linux, macOS (x64, ARM64)
- **Safe by Default**: SafeHandle-based resource management
- **cppzmq Style**: Familiar API for C++ developers

## Quick Start

### Installation

```bash
dotnet add package Net.Zmq
```

### Request-Reply Example

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

### Publish-Subscribe Example

```csharp
using Net.Zmq;

using var context = new Context();

// Publisher
using var publisher = new Socket(context, SocketType.Pub);
publisher.Bind("tcp://*:5556");
publisher.Send("weather.tokyo 25°C");

// Subscriber
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");
subscriber.Subscribe("weather.");
var update = subscriber.RecvString();  // "weather.tokyo 25°C"
```

## Documentation

Complete documentation is available at **[https://ulala-x.github.io/net-zmq/](https://ulala-x.github.io/net-zmq/)**

### Quick Links

- **[Getting Started](docs/getting-started.md)** - Installation and basic concepts
- **[Messaging Patterns](docs/patterns.md)** - REQ-REP, PUB-SUB, PUSH-PULL, and more
- **[API Usage Guide](docs/api-usage.md)** - Detailed API documentation
- **[Advanced Topics](docs/advanced-topics.md)** - Performance tuning and best practices
- **[API Reference](api/index.html)** - Complete API documentation

## Performance

Net.Zmq delivers exceptional performance:

| Message Size | Throughput | Latency | Pattern |
|--------------|------------|---------|---------|
| 64 bytes | 4.95M/sec | 202ns | PUSH/PULL |
| 1 KB | 1.36M/sec | 736ns | PUB/SUB |
| 64 KB | 73.47K/sec | 13.61μs | ROUTER/ROUTER |

**Test Environment**: Intel Core Ultra 7 265K, .NET 8.0.22, Ubuntu 24.04.3 LTS

See [BENCHMARKS.md](https://github.com/ulala-x/net-zmq/blob/main/BENCHMARKS.md) for detailed benchmarks.

## Supported Platforms

| OS | Architecture | Status |
|----|--------------|--------|
| Windows | x64, ARM64 | ✅ Supported |
| Linux | x64, ARM64 | ✅ Supported |
| macOS | x64, ARM64 | ✅ Supported |

## Socket Types

Net.Zmq supports all ZeroMQ socket types:

| Type | Description | Pattern |
|------|-------------|---------|
| `Req` | Request | Client-Server |
| `Rep` | Reply | Client-Server |
| `Pub` | Publish | Pub-Sub |
| `Sub` | Subscribe | Pub-Sub |
| `Push` | Push | Pipeline |
| `Pull` | Pull | Pipeline |
| `Dealer` | Async Request | Advanced |
| `Router` | Async Reply | Advanced |
| `Pair` | Exclusive Pair | Peer-to-Peer |

## Requirements

- **.NET 8.0 or later**
- **libzmq native library** (automatically included via Net.Zmq.Native package)

## Contributing

Contributions are welcome! Please see the [Contributing Guide](https://github.com/ulala-x/net-zmq/blob/main/CONTRIBUTING.md) for details.

## License

Net.Zmq is licensed under the [MIT License](https://github.com/ulala-x/net-zmq/blob/main/LICENSE).

## Related Projects

- [libzmq](https://github.com/zeromq/libzmq) - ZeroMQ core library
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ binding (API inspiration)
- [libzmq-native](https://github.com/ulala-x/libzmq-native) - Native binaries for Net.Zmq
