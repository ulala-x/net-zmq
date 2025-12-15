# Sample Applications

Net.Zmq includes comprehensive sample applications demonstrating various messaging patterns and features. Each sample is a complete, runnable application.

## Running Samples

All samples can be run using the .NET CLI:

```bash
# Clone the repository
git clone https://github.com/ulala-x/net-zmq.git
cd net-zmq

# Run a specific sample
dotnet run --project samples/Net.Zmq.Samples.ReqRep
dotnet run --project samples/Net.Zmq.Samples.PubSub
dotnet run --project samples/Net.Zmq.Samples.PushPull
```

---

## Request-Reply (REQ-REP)

The classic synchronous request-reply pattern. Client sends a request and waits for a reply.

**Source**: [Net.Zmq.Samples.ReqRep](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.ReqRep)

[!code-csharp[](../samples/Net.Zmq.Samples.ReqRep/Program.cs)]

---

## Publish-Subscribe (PUB-SUB)

One-to-many message distribution with topic filtering.

**Source**: [Net.Zmq.Samples.PubSub](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PubSub)

[!code-csharp[](../samples/Net.Zmq.Samples.PubSub/Program.cs)]

---

## Push-Pull Pipeline

Load-balanced task distribution using the Ventilator-Worker-Sink pattern.

**Source**: [Net.Zmq.Samples.PushPull](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PushPull)

[!code-csharp[](../samples/Net.Zmq.Samples.PushPull/Program.cs)]

---

## Poller

Multiplexing multiple sockets with non-blocking I/O.

**Source**: [Net.Zmq.Samples.Poller](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Poller)

[!code-csharp[](../samples/Net.Zmq.Samples.Poller/Program.cs)]

---

## Router-Dealer Async Broker

Asynchronous request-reply with a broker that routes messages between clients and workers.

**Source**: [Net.Zmq.Samples.RouterDealer](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.RouterDealer)

[!code-csharp[](../samples/Net.Zmq.Samples.RouterDealer/Program.cs)]

---

## CURVE Security

End-to-end encryption using ZeroMQ's CURVE security mechanism.

**Source**: [Net.Zmq.Samples.CurveSecurity](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.CurveSecurity)

[!code-csharp[](../samples/Net.Zmq.Samples.CurveSecurity/Program.cs)]

---

## Socket Monitor

Real-time socket event monitoring for connection lifecycle tracking.

**Source**: [Net.Zmq.Samples.Monitor](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Monitor)

[!code-csharp[](../samples/Net.Zmq.Samples.Monitor/Program.cs)]

---

## All Samples

| Sample | Pattern | Description |
|--------|---------|-------------|
| [ReqRep](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.ReqRep) | REQ-REP | Basic request-reply |
| [PubSub](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PubSub) | PUB-SUB | Topic-based pub-sub |
| [PushPull](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PushPull) | PUSH-PULL | Pipeline pattern |
| [Poller](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Poller) | Multiple | Socket multiplexing |
| [RouterDealer](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.RouterDealer) | ROUTER-DEALER | Async broker |
| [RouterToRouter](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.RouterToRouter) | ROUTER-ROUTER | Peer-to-peer |
| [Pair](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Pair) | PAIR | Inter-thread |
| [Proxy](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Proxy) | Proxy | Message forwarding |
| [SteerableProxy](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.SteerableProxy) | Proxy | Controllable proxy |
| [CurveSecurity](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.CurveSecurity) | Security | CURVE encryption |
| [Monitor](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Monitor) | Monitoring | Socket events |
| [MultipartExtensions](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.MultipartExtensions) | Multipart | Multi-frame messages |
