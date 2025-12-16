# NetZeroMQ ROUTER-DEALER Async Broker Sample

This sample demonstrates the ZeroMQ Router-Dealer async broker pattern using NetZeroMQ.

## Pattern Overview

The async broker pattern provides a load-balancing message broker that routes requests from multiple clients to multiple workers asynchronously.

### Architecture

```
Clients (DEALER)  →  Broker (ROUTER-ROUTER)  →  Workers (DEALER)
                         Frontend | Backend
```

- **Frontend (ROUTER)**: Receives requests from clients
- **Backend (ROUTER)**: Distributes work to workers
- **Clients (DEALER)**: Send requests and receive replies asynchronously
- **Workers (DEALER)**: Process requests and send replies

## Key Patterns Demonstrated

### 1. Explicit Routing IDs
Both clients and workers set explicit routing IDs:
```csharp
socket.SetOption(SocketOption.Routing_Id, "client-1");
```

### 2. Multipart Message Format

**Client to Broker (DEALER → ROUTER)**:
```
[empty frame]
[request data]
```

**Broker receives from Client (ROUTER adds identity)**:
```
[client-identity]
[empty frame]
[request data]
```

**Broker to Worker (ROUTER → DEALER)**:
```
[worker-identity]
[empty frame]
[client-identity]
[empty frame]
[request data]
```

**Worker receives from Broker (DEALER strips identity)**:
```
[empty frame]
[client-identity]
[empty frame]
[request data]
```

### 3. Asynchronous Request/Reply
Unlike REQ-REP, DEALER sockets:
- Don't enforce strict request-reply order
- Can send multiple requests without waiting for replies
- Enable true async communication

### 4. Load Balancing
The broker maintains a queue of available workers and routes requests to the least recently used worker.

## Running the Sample

### Prerequisites
The sample requires the native libzmq library. For development, copy `libzmq.dll` to the output directory:

```bash
# Windows (PowerShell)
Copy-Item native\runtimes\win-x64\native\libzmq.dll samples\NetZeroMQ.Samples.RouterDealer\bin\Debug\net8.0\

# Linux
cp native/runtimes/linux-x64/native/libzmq.so samples/NetZeroMQ.Samples.RouterDealer/bin/Debug/net8.0/

# macOS
cp native/runtimes/osx-x64/native/libzmq.dylib samples/NetZeroMQ.Samples.RouterDealer/bin/Debug/net8.0/
```

### Build and Run

```bash
# Build
dotnet build

# Run
dotnet run
```

### Expected Output

```
NetZeroMQ ROUTER-DEALER Async Broker Sample
==========================================

[Broker] Starting...
[Broker] Frontend listening on tcp://*:5555
[Broker] Backend listening on tcp://*:5556
[Broker] Polling started...
[worker-1] Starting...
[worker-2] Starting...
[worker-1] Connected to broker
[worker-2] Connected to broker
[Broker] Worker worker-1 is ready
[Broker] Worker worker-2 is ready
[client-1] Starting...
[client-2] Starting...
[client-1] Connected to broker
[client-2] Connected to broker
[client-1] Sent: Request #1 from client-1
[Broker] Client client-1 -> Request: Request #1 from client-1
[Broker] Routed to Worker worker-1 for Client client-1
[worker-1] Processing request from client-1: Request #1 from client-1
[worker-1] Sent reply to client-1: Processed by worker-1
[Broker] Worker worker-1 -> Client client-1: Processed by worker-1
[client-1] Received: Processed by worker-1
...
```

## Code Highlights

### Broker Implementation
```csharp
// Create ROUTER sockets for both frontend and backend
using var frontend = new Socket(ctx, SocketType.Router);
using var backend = new Socket(ctx, SocketType.Router);

frontend.Bind("tcp://*:5555");  // For clients
backend.Bind("tcp://*:5556");   // For workers

// Create Poller and add both sockets
using var poller = new Poller(capacity: 2);
int frontendIdx = poller.Add(frontend, PollEvents.In);
int backendIdx = poller.Add(backend, PollEvents.In);

while (true)
{
    poller.Poll(timeout: 100);

    if (poller.IsReadable(frontendIdx))
    {
        // Handle client request
    }

    if (poller.IsReadable(backendIdx))
    {
        // Handle worker response or READY message
    }
}
```

### Client Implementation
```csharp
using var socket = new Socket(ctx, SocketType.Dealer);
socket.SetOption(SocketOption.Routing_Id, "client-1");
socket.Connect("tcp://localhost:5555");

// Send request (DEALER adds empty frame)
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("Request data", SendFlags.None);

// Receive reply
var empty = RecvBytes(socket);
var reply = socket.RecvString();
```

### Worker Implementation
```csharp
using var socket = new Socket(ctx, SocketType.Dealer);
socket.SetOption(SocketOption.Routing_Id, "worker-1");
socket.Connect("tcp://localhost:5556");

// Send READY message
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("READY", SendFlags.SendMore);
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("READY", SendFlags.None);

// Receive request
var empty1 = RecvBytes(socket);
var clientId = RecvBytes(socket);
var empty2 = RecvBytes(socket);
var request = RecvBytes(socket);

// Process and send reply
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send(clientId, SendFlags.SendMore);
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("Reply data", SendFlags.None);
```

## Use Cases

This pattern is ideal for:
- Load-balanced request processing
- Distributed task queues
- Microservice architectures
- Async RPC systems
- Worker pool management

## References

- [ZeroMQ Guide - Advanced Request-Reply](http://zguide.zeromq.org/page:all#Advanced-Request-Reply-Patterns)
- [ROUTER Socket Type](http://api.zeromq.org/4-3:zmq-socket#toc17)
- [DEALER Socket Type](http://api.zeromq.org/4-3:zmq-socket#toc15)
