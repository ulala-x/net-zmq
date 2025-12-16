using Net.Zmq;
using System.Text;

Console.WriteLine("NetZeroMQ ROUTER-DEALER Async Broker Sample");
Console.WriteLine("==========================================");
Console.WriteLine();
Console.WriteLine("This sample demonstrates the async broker pattern:");
Console.WriteLine("- Broker with ROUTER frontend and ROUTER backend");
Console.WriteLine("- Multiple DEALER clients sending requests");
Console.WriteLine("- Multiple DEALER workers processing requests");
Console.WriteLine();

// Start broker
var brokerTask = Task.Run(RunBroker);
await Task.Delay(500); // Give broker time to bind

// Start workers
var worker1Task = Task.Run(() => RunWorker(1));
var worker2Task = Task.Run(() => RunWorker(2));
await Task.Delay(500); // Give workers time to connect

// Start clients
var client1Task = Task.Run(() => RunClient(1));
var client2Task = Task.Run(() => RunClient(2));

// Wait for clients to complete
await client1Task;
await client2Task;

// Give workers time to process remaining messages
await Task.Delay(1000);

Console.WriteLine();
Console.WriteLine("All clients completed. Press Ctrl+C to exit.");
Console.WriteLine();

// Keep broker and workers running
await Task.WhenAll(brokerTask, worker1Task, worker2Task);

void RunBroker()
{
    Console.WriteLine("[Broker] Starting...");
    using var ctx = new Context();
    using var frontend = new Socket(ctx, SocketType.Router);
    using var backend = new Socket(ctx, SocketType.Router);

    // Configure sockets
    frontend.SetOption(SocketOption.Linger, 0);
    backend.SetOption(SocketOption.Linger, 0);

    // Bind frontend for clients
    frontend.Bind("tcp://*:5555");
    Console.WriteLine("[Broker] Frontend listening on tcp://*:5555");

    // Bind backend for workers
    backend.Bind("tcp://*:5556");
    Console.WriteLine("[Broker] Backend listening on tcp://*:5556");
    Console.WriteLine("[Broker] Polling started...");

    var clientRequests = new Queue<(byte[] identity, byte[] request)>();
    var availableWorkers = new Queue<byte[]>();

    // Create poller with instance-based API
    using var poller = new Poller(2);
    int frontendIdx = poller.Add(frontend, PollEvents.In);
    int backendIdx = poller.Add(backend, PollEvents.In);

    while (true)
    {
        try
        {
            // Poll with 100ms timeout
            poller.Poll(100);

            // Check frontend (client requests)
            if (poller.IsReadable(frontendIdx))
            {
                // Receive from client: [client-identity][empty][request]
                var clientIdentity = RecvBytes(frontend);
                var empty = RecvBytes(frontend);
                var request = RecvBytes(frontend);

                var clientId = Encoding.UTF8.GetString(clientIdentity);
                var requestText = Encoding.UTF8.GetString(request);
                Console.WriteLine($"[Broker] Client {clientId} -> Request: {requestText}");

                // Queue the request
                clientRequests.Enqueue((clientIdentity, request));

                // Try to route if worker available
                if (availableWorkers.Count > 0 && clientRequests.Count > 0)
                {
                    var workerIdentity = availableWorkers.Dequeue();
                    var (reqClientId, reqData) = clientRequests.Dequeue();

                    // Send to worker: [worker-identity][empty][client-identity][empty][request]
                    backend.Send(workerIdentity, SendFlags.SendMore);
                    backend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                    backend.Send(reqClientId, SendFlags.SendMore);
                    backend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                    backend.Send(reqData, SendFlags.None);

                    var workerId = Encoding.UTF8.GetString(workerIdentity);
                    var clientIdStr = Encoding.UTF8.GetString(reqClientId);
                    Console.WriteLine($"[Broker] Routed to Worker {workerId} for Client {clientIdStr}");
                }
            }

            // Check backend (worker responses)
            if (poller.IsReadable(backendIdx))
            {
                // Receive from worker: [worker-identity][empty][client-identity][empty][reply]
                var workerIdentity = RecvBytes(backend);
                var empty1 = RecvBytes(backend);
                var clientIdentity = RecvBytes(backend);
                var empty2 = RecvBytes(backend);
                var reply = RecvBytes(backend);

                var workerId = Encoding.UTF8.GetString(workerIdentity);
                var clientId = Encoding.UTF8.GetString(clientIdentity);
                var replyText = Encoding.UTF8.GetString(reply);

                if (replyText == "READY")
                {
                    // Worker is ready
                    Console.WriteLine($"[Broker] Worker {workerId} is ready");
                    availableWorkers.Enqueue(workerIdentity);

                    // Try to route queued request
                    if (clientRequests.Count > 0)
                    {
                        var (reqClientId, reqData) = clientRequests.Dequeue();

                        backend.Send(workerIdentity, SendFlags.SendMore);
                        backend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                        backend.Send(reqClientId, SendFlags.SendMore);
                        backend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                        backend.Send(reqData, SendFlags.None);

                        var clientIdStr = Encoding.UTF8.GetString(reqClientId);
                        Console.WriteLine($"[Broker] Routed to Worker {workerId} for Client {clientIdStr}");
                    }
                }
                else
                {
                    // Forward reply to client: [client-identity][empty][reply]
                    Console.WriteLine($"[Broker] Worker {workerId} -> Client {clientId}: {replyText}");

                    frontend.Send(clientIdentity, SendFlags.SendMore);
                    frontend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                    frontend.Send(reply, SendFlags.None);

                    // Worker is available again
                    availableWorkers.Enqueue(workerIdentity);

                    // Try to route queued request
                    if (clientRequests.Count > 0)
                    {
                        var (reqClientId, reqData) = clientRequests.Dequeue();

                        backend.Send(workerIdentity, SendFlags.SendMore);
                        backend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                        backend.Send(reqClientId, SendFlags.SendMore);
                        backend.Send(Array.Empty<byte>(), SendFlags.SendMore);
                        backend.Send(reqData, SendFlags.None);

                        var clientIdStr = Encoding.UTF8.GetString(reqClientId);
                        Console.WriteLine($"[Broker] Routed to Worker {workerId} for Client {clientIdStr}");
                    }
                }
            }
        }
        catch (ZmqException ex)
        {
            Console.WriteLine($"[Broker] Error: {ex.Message}");
            break;
        }
    }
}

void RunClient(int id)
{
    var clientId = $"client-{id}";
    Console.WriteLine($"[{clientId}] Starting...");

    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Dealer);

    // Set routing ID for this client
    socket.SetOption(SocketOption.Routing_Id, clientId);
    socket.SetOption(SocketOption.Linger, 0);

    socket.Connect("tcp://localhost:5555");
    Console.WriteLine($"[{clientId}] Connected to broker");

    for (int i = 0; i < 3; i++)
    {
        var request = $"Request #{i + 1} from {clientId}";

        // Send to broker: [empty][request]
        socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
        socket.Send(request, SendFlags.None);
        Console.WriteLine($"[{clientId}] Sent: {request}");

        // Receive reply: [empty][reply]
        var empty = RecvBytes(socket);
        var reply = socket.RecvString();
        Console.WriteLine($"[{clientId}] Received: {reply}");

        Thread.Sleep(500); // Simulate work
    }

    Console.WriteLine($"[{clientId}] Done");
}

void RunWorker(int id)
{
    var workerId = $"worker-{id}";
    Console.WriteLine($"[{workerId}] Starting...");

    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Dealer);

    // Set routing ID for this worker
    socket.SetOption(SocketOption.Routing_Id, workerId);
    socket.SetOption(SocketOption.Linger, 0);

    socket.Connect("tcp://localhost:5556");
    Console.WriteLine($"[{workerId}] Connected to broker");

    // Send READY message: [empty][client-identity][empty][READY]
    socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
    socket.Send("READY", SendFlags.SendMore);
    socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
    socket.Send("READY", SendFlags.None);

    while (true)
    {
        try
        {
            // Receive from broker: [empty][client-identity][empty][request]
            var empty1 = RecvBytes(socket);
            var clientIdentity = RecvBytes(socket);
            var empty2 = RecvBytes(socket);
            var request = RecvBytes(socket);

            var clientId = Encoding.UTF8.GetString(clientIdentity);
            var requestText = Encoding.UTF8.GetString(request);
            Console.WriteLine($"[{workerId}] Processing request from {clientId}: {requestText}");

            // Simulate work
            Thread.Sleep(300);

            var reply = $"Processed by {workerId}";

            // Send reply to broker: [empty][client-identity][empty][reply]
            socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
            socket.Send(clientIdentity, SendFlags.SendMore);
            socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
            socket.Send(reply, SendFlags.None);
            Console.WriteLine($"[{workerId}] Sent reply to {clientId}: {reply}");
        }
        catch (ZmqException ex)
        {
            Console.WriteLine($"[{workerId}] Error: {ex.Message}");
            break;
        }
    }

    Console.WriteLine($"[{workerId}] Done");
}

static byte[] RecvBytes(Socket socket)
{
    var buffer = new byte[256];
    var size = socket.Recv(buffer);
    return buffer[..size];
}
