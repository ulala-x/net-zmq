# Messaging Patterns

ZeroMQ provides several built-in messaging patterns for different communication scenarios. This guide covers all patterns supported by Net.Zmq with practical examples.

## Overview

| Pattern | Sockets | Use Case |
|---------|---------|----------|
| Request-Reply | REQ-REP | Synchronous client-server |
| Publish-Subscribe | PUB-SUB | One-to-many broadcast |
| Push-Pull | PUSH-PULL | Load-balanced pipeline |
| Router-Dealer | ROUTER-DEALER | Asynchronous client-server |
| Pair | PAIR | Exclusive two-way communication |

## Request-Reply Pattern (REQ-REP)

The REQ-REP pattern implements synchronous client-server communication. The client sends a request and waits for a reply.

### Characteristics

- **Synchronous**: Client blocks until reply is received
- **Lockstep**: Must alternate send-receive-send-receive
- **One-to-one**: Each request gets exactly one reply

### Example: Simple Echo Server

**Server (REP)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

Console.WriteLine("Echo server started on port 5555");

while (true)
{
    // Wait for request
    var request = server.RecvString();
    Console.WriteLine($"Received: {request}");

    // Send reply
    server.Send($"Echo: {request}");
}
```

**Client (REQ)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");

// Send request
client.Send("Hello World");
Console.WriteLine("Request sent");

// Wait for reply
var reply = client.RecvString();
Console.WriteLine($"Reply: {reply}");
```

### Best Practices

- Always match Send() with RecvString()/RecvBytes()
- Use try-catch to handle connection failures
- Set timeouts to avoid blocking forever
- Consider DEALER-ROUTER for asynchronous scenarios

## Publish-Subscribe Pattern (PUB-SUB)

The PUB-SUB pattern distributes messages from one publisher to many subscribers. Subscribers filter messages by topic.

### Characteristics

- **One-to-many**: Single publisher, multiple subscribers
- **Topic-based**: Subscribers filter by prefix matching
- **Fire-and-forget**: Publisher doesn't know who receives
- **Late joiner problem**: Subscribers miss messages sent before subscription

### Example: Weather Updates

**Publisher (PUB)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var publisher = new Socket(context, SocketType.Pub);
publisher.Bind("tcp://*:5556");

Console.WriteLine("Weather publisher started");

var random = new Random();
while (true)
{
    // Generate weather data
    var zipcode = random.Next(10000, 99999);
    var temperature = random.Next(-20, 40);
    var humidity = random.Next(10, 90);

    // Publish with topic (zipcode)
    var update = $"{zipcode} {temperature} {humidity}";
    publisher.Send(update);

    Console.WriteLine($"Published: {update}");
    Thread.Sleep(100);
}
```

**Subscriber (SUB)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");

// Subscribe to specific zipcode(s)
subscriber.Subscribe("10001");
subscriber.Subscribe("10002");

Console.WriteLine("Subscribed to zipcodes 10001 and 10002");

while (true)
{
    var update = subscriber.RecvString();
    var parts = update.Split(' ');

    var zipcode = parts[0];
    var temperature = int.Parse(parts[1]);
    var humidity = int.Parse(parts[2]);

    Console.WriteLine($"Zipcode: {zipcode}, Temp: {temperature}°C, Humidity: {humidity}%");
}
```

### Topic Filtering

Topics use prefix matching. A subscription to "A" will match "A", "AB", "ABC", etc.

```csharp
// Subscribe to all messages
subscriber.Subscribe("");

// Subscribe to specific topics
subscriber.Subscribe("weather.");
subscriber.Subscribe("stock.AAPL");

// Unsubscribe
subscriber.Unsubscribe("weather.");
```

### Best Practices

- Always call Subscribe() before receiving messages
- Use meaningful topic prefixes for filtering
- Consider slow joiner problem (add sleep after bind/connect)
- Publishers should be stable (bind), subscribers connect

## Push-Pull Pattern (Pipeline)

The PUSH-PULL pattern creates a pipeline for distributing tasks to workers. Tasks are load-balanced automatically.

### Characteristics

- **Load balancing**: Tasks distributed evenly among workers
- **Fair queuing**: Workers receive tasks in round-robin
- **One-way**: No replies sent back
- **Reliable**: Messages queue if worker is busy

### Example: Parallel Task Processing

**Task Producer (PUSH)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var pusher = new Socket(context, SocketType.Push);
pusher.Bind("tcp://*:5557");

Console.WriteLine("Task producer started");

for (int i = 0; i < 100; i++)
{
    var task = $"Task {i:D3}";
    pusher.Send(task);
    Console.WriteLine($"Sent: {task}");
    Thread.Sleep(10);
}
```

**Worker (PULL)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var puller = new Socket(context, SocketType.Pull);
puller.Connect("tcp://localhost:5557");

var workerId = Environment.ProcessId;
Console.WriteLine($"Worker {workerId} started");

while (true)
{
    var task = puller.RecvString();
    Console.WriteLine($"Worker {workerId} processing: {task}");

    // Simulate work
    Thread.Sleep(Random.Shared.Next(100, 500));

    Console.WriteLine($"Worker {workerId} completed: {task}");
}
```

**Result Collector (Optional)**:

For collecting results, use a separate PULL socket:

```csharp
// In worker, add a PUSH socket
using var resultPusher = new Socket(context, SocketType.Push);
resultPusher.Connect("tcp://localhost:5558");

// After processing
resultPusher.Send($"Result for {task}");

// Collector
using var resultPuller = new Socket(context, SocketType.Pull);
resultPuller.Bind("tcp://*:5558");

while (true)
{
    var result = resultPuller.RecvString();
    Console.WriteLine($"Collected: {result}");
}
```

### Best Practices

- Producer binds, workers connect (allows dynamic scaling)
- Use separate sockets for task distribution and result collection
- Consider ventilator-worker-sink pattern for complete pipelines
- Monitor queue sizes to detect slow workers

## Router-Dealer Pattern

ROUTER and DEALER sockets provide asynchronous request-reply with advanced routing.

### DEALER-DEALER (Asynchronous Request-Reply)

DEALER sockets can send multiple requests without waiting for replies.

```csharp
// Async server (DEALER)
using var server = new Socket(context, SocketType.Dealer);
server.Bind("tcp://*:5559");

// Async client (DEALER)
using var client = new Socket(context, SocketType.Dealer);
client.Connect("tcp://localhost:5559");

// Client can send multiple requests
client.Send("Request 1");
client.Send("Request 2");
client.Send("Request 3");

// Receive replies (may arrive out of order)
for (int i = 0; i < 3; i++)
{
    var reply = client.RecvString();
    Console.WriteLine($"Reply: {reply}");
}
```

### ROUTER-ROUTER (Peer-to-Peer with Identity)

ROUTER sockets add identity frames for explicit routing.

```csharp
using System.Text;
using Net.Zmq;

using var context = new Context();
using var peerA = new Socket(context, SocketType.Router);
using var peerB = new Socket(context, SocketType.Router);

// Set explicit identities
peerA.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_A"));
peerB.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_B"));

peerA.Bind("tcp://127.0.0.1:5560");
peerB.Connect("tcp://127.0.0.1:5560");

Thread.Sleep(100); // Allow connection to establish

// Peer B sends to Peer A (first frame = target identity)
peerB.Send(Encoding.UTF8.GetBytes("PEER_A"), SendFlags.SendMore);
peerB.Send("Hello from Peer B!");

// Peer A receives (first frame = sender identity)
var senderId = Encoding.UTF8.GetString(peerA.RecvBytes());
var message = peerA.RecvString();

Console.WriteLine($"From {senderId}: {message}");

// Peer A replies using sender's identity
peerA.Send(Encoding.UTF8.GetBytes(senderId), SendFlags.SendMore);
peerA.Send("Hello back from Peer A!");

// Peer B receives reply
var replyFrom = Encoding.UTF8.GetString(peerB.RecvBytes());
var reply = peerB.RecvString();

Console.WriteLine($"From {replyFrom}: {reply}");
```

### Best Practices

- Always set explicit identities for ROUTER-ROUTER
- First frame is always the identity (envelope)
- Use SendFlags.SendMore for multi-frame messages
- ROUTER is more complex; use REQ-REP or DEALER-REP for simpler cases

## Pair Pattern (PAIR)

PAIR sockets create an exclusive connection between two endpoints.

### Characteristics

- **Exclusive**: Only two endpoints can connect
- **Bidirectional**: Both sides can send and receive
- **No routing**: Direct peer-to-peer
- **Mainly for inproc**: Best used for thread communication

### Example: Inter-thread Communication

```csharp
using Net.Zmq;

using var context = new Context();

// Thread 1
var thread1 = new Thread(() =>
{
    using var pair = new Socket(context, SocketType.Pair);
    pair.Bind("inproc://pair-example");

    pair.Send("Message from Thread 1");
    var response = pair.RecvString();
    Console.WriteLine($"Thread 1 received: {response}");
});

// Thread 2
var thread2 = new Thread(() =>
{
    using var pair = new Socket(context, SocketType.Pair);
    pair.Connect("inproc://pair-example");

    var message = pair.RecvString();
    Console.WriteLine($"Thread 2 received: {message}");
    pair.Send("Message from Thread 2");
});

thread1.Start();
Thread.Sleep(100); // Ensure bind happens first
thread2.Start();

thread1.Join();
thread2.Join();
```

### Best Practices

- Use PAIR primarily for inproc:// communication
- For TCP, consider REQ-REP or other patterns
- Ensure bind happens before connect
- Not suitable for complex topologies

## Pattern Selection Guide

Choose the right pattern for your use case:

| Scenario | Recommended Pattern |
|----------|-------------------|
| Client-server with replies | REQ-REP or DEALER-REP |
| Broadcast to many clients | PUB-SUB |
| Distribute work to workers | PUSH-PULL (pipeline) |
| Asynchronous client-server | DEALER-ROUTER |
| Peer-to-peer messaging | ROUTER-ROUTER or PAIR |
| Inter-thread communication | PAIR (inproc) |
| Load balancing | PUSH-PULL or ROUTER-DEALER |

## Advanced: Combining Patterns

You can combine patterns for complex architectures:

### Majordomo Pattern (Broker)

Broker sits between clients and workers:

```
Client (REQ) → Broker (ROUTER-DEALER) → Worker (REP)
```

### Paranoid Pirate Pattern

Reliable request-reply with heartbeating:

```
Client (REQ) → Load Balancer (ROUTER) → Workers (DEALER) with heartbeats
```

See the [Advanced Topics](advanced-topics.md) guide for these patterns.

## Next Steps

- Learn about [API Usage](api-usage.md) for detailed API documentation
- Explore [Advanced Topics](advanced-topics.md) for complex patterns
- Check the [API Reference](../api/index.html) for complete documentation
