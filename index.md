# Net.Zmq

[![Build and Test](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Zmq.svg)](https://www.nuget.org/packages/Net.Zmq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern .NET 8+ binding for ZeroMQ (libzmq) with cppzmq-style API.

## Features

- **Modern .NET**: Built for .NET 8.0+ with `[LibraryImport]` source generators (no runtime marshalling overhead)
- **cppzmq Style**: Familiar API for developers coming from C++
- **Type Safe**: Strongly-typed socket options, message properties, and enums
- **Cross-Platform**: Supports Windows, Linux, and macOS (x64, ARM64)
- **Safe by Default**: SafeHandle-based resource management
- **High Performance**: 4.95M messages/sec throughput with 202ns latency

## Quick Start

### Installation

```bash
dotnet add package Net.Zmq
```

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

## Documentation

- [Getting Started Guide](docs/index.md) - Complete documentation and examples
- [API Reference](api/index.md) - Detailed API documentation
- [Performance Benchmarks](https://github.com/ulala-x/net-zmq/blob/main/BENCHMARKS.md) - Performance metrics

## Performance

Net.Zmq delivers exceptional performance with **4.95M messages/sec throughput** and **202ns latency** at peak performance.

### Highlights

- **Peak Throughput**: 4.95M msg/sec (PUSH/PULL, 64B, Blocking mode)
- **Ultra-Low Latency**: 202ns per message
- **Memory Efficient**: 441B allocation per 10K messages
- **Consistent**: All patterns achieve 4M+ msg/sec for small messages

### Performance by Message Size

| Message Size | Best Throughput | Latency | Pattern | Mode |
|--------------|-----------------|---------|---------|------|
| **64 bytes** | 4.95M/sec | 202ns | PUSH/PULL | Blocking |
| **1 KB** | 1.36M/sec | 736ns | PUB/SUB | Blocking |
| **64 KB** | 73.47K/sec | 13.61μs | ROUTER/ROUTER | Blocking |

**Test Environment**: Intel Core Ultra 7 265K, .NET 8.0.22, Ubuntu 24.04.3 LTS

## Supported Platforms

| OS | Architecture |
|----|--------------|
| Windows | x64, ARM64 |
| Linux | x64, ARM64 |
| macOS | x64, ARM64 |

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

## Requirements

- .NET 8.0 or later
- Native libzmq library (automatically provided via Net.Zmq.Native package)

## License

MIT License - see [LICENSE](https://github.com/ulala-x/net-zmq/blob/main/LICENSE) for details.

## Related Projects

- [libzmq](https://github.com/zeromq/libzmq) - ZeroMQ core library
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ binding (API inspiration)
- [libzmq-native](https://github.com/ulala-x/libzmq-native) - Native binaries
