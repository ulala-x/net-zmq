using Net.Zmq;

Console.WriteLine("NetZeroMQ XPub-XSub Proxy Pattern Sample");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("Architecture:");
Console.WriteLine("  Publishers -> XSub (Frontend) -> Proxy -> XPub (Backend) -> Subscribers");
Console.WriteLine();
Console.WriteLine("This sample demonstrates:");
Console.WriteLine("  - XSub socket receiving from multiple publishers");
Console.WriteLine("  - XPub socket distributing to multiple subscribers");
Console.WriteLine("  - Built-in Proxy forwarding messages and subscriptions");
Console.WriteLine("  - Dynamic subscription handling");
Console.WriteLine();

// Create shared context for all sockets
using var context = new Context();

// Start the proxy in a background thread
var proxyThread = new Thread(() => RunProxy(context))
{
    IsBackground = true,
    Name = "Proxy-Thread"
};
proxyThread.Start();

// Allow proxy to initialize
Thread.Sleep(500);

// Start publishers
var publisher1Thread = new Thread(() => RunPublisher(context, "Publisher-1", "weather", 5557))
{
    IsBackground = true,
    Name = "Publisher1-Thread"
};

var publisher2Thread = new Thread(() => RunPublisher(context, "Publisher-2", "sports", 5558))
{
    IsBackground = true,
    Name = "Publisher2-Thread"
};

publisher1Thread.Start();
publisher2Thread.Start();

// Allow publishers to initialize
Thread.Sleep(500);

// Start subscribers
var subscriber1Thread = new Thread(() => RunSubscriber(context, "Subscriber-1", ["weather"]))
{
    IsBackground = true,
    Name = "Subscriber1-Thread"
};

var subscriber2Thread = new Thread(() => RunSubscriber(context, "Subscriber-2", ["sports"]))
{
    IsBackground = true,
    Name = "Subscriber2-Thread"
};

var subscriber3Thread = new Thread(() => RunSubscriber(context, "Subscriber-3", ["weather", "sports"]))
{
    IsBackground = true,
    Name = "Subscriber3-Thread"
};

subscriber1Thread.Start();
subscriber2Thread.Start();
subscriber3Thread.Start();

// Wait for subscribers to complete
subscriber1Thread.Join();
subscriber2Thread.Join();
subscriber3Thread.Join();

Console.WriteLine();
Console.WriteLine("All subscribers completed. Press any key to exit...");
Console.ReadKey();

void RunProxy(Context ctx)
{
    Console.WriteLine("[Proxy] Starting XPub-XSub proxy...");

    // Create frontend XSub socket (for publishers)
    using var frontend = new Socket(ctx, SocketType.XSub);
    frontend.Bind("tcp://*:5559");
    Console.WriteLine("[Proxy] Frontend XSub bound to tcp://*:5559 (for publishers)");

    // Create backend XPub socket (for subscribers)
    using var backend = new Socket(ctx, SocketType.XPub);
    backend.Bind("tcp://*:5560");
    Console.WriteLine("[Proxy] Backend XPub bound to tcp://*:5560 (for subscribers)");
    Console.WriteLine("[Proxy] Proxy running - forwarding messages and subscriptions...");
    Console.WriteLine();

    try
    {
        // Start the built-in proxy
        // This blocks and forwards messages from frontend to backend
        // and subscriptions from backend to frontend
        Proxy.Start(frontend, backend);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Proxy] Error: {ex.Message}");
    }
}

void RunPublisher(Context ctx, string name, string topic, int directPort)
{
    Console.WriteLine($"[{name}] Starting...");

    using var socket = new Socket(ctx, SocketType.Pub);
    socket.SetOption(SocketOption.Linger, 0);

    // Connect to the proxy's frontend XSub
    socket.Connect("tcp://localhost:5559");
    Console.WriteLine($"[{name}] Connected to proxy frontend (tcp://localhost:5559)");
    Console.WriteLine($"[{name}] Publishing topic: '{topic}'");
    Console.WriteLine();

    // Allow time for connection
    Thread.Sleep(1000);

    // Publish messages
    for (int i = 1; i <= 10; i++)
    {
        var message = $"{topic} Update #{i} from {name}";
        socket.Send(message);
        Console.WriteLine($"[{name}] Sent: {message}");
        Thread.Sleep(800);
    }

    Console.WriteLine($"[{name}] Publishing completed");
}

void RunSubscriber(Context ctx, string name, string[] topics)
{
    Console.WriteLine($"[{name}] Starting...");

    using var socket = new Socket(ctx, SocketType.Sub);
    socket.SetOption(SocketOption.Linger, 0);
    socket.SetOption(SocketOption.Rcvtimeo, 15000); // 15 second timeout

    // Connect to the proxy's backend XPub
    socket.Connect("tcp://localhost:5560");
    Console.WriteLine($"[{name}] Connected to proxy backend (tcp://localhost:5560)");

    // Subscribe to topics
    foreach (var topic in topics)
    {
        socket.Subscribe(topic);
        Console.WriteLine($"[{name}] Subscribed to topic: '{topic}'");
    }
    Console.WriteLine();

    // Allow subscriptions to propagate
    Thread.Sleep(500);

    int messageCount = 0;
    int maxMessages = 15; // Expect ~10 messages per topic

    while (messageCount < maxMessages)
    {
        try
        {
            var message = socket.RecvString();
            messageCount++;
            Console.WriteLine($"[{name}] Received: {message}");
        }
        catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN (timeout)
        {
            Console.WriteLine($"[{name}] Timeout - no more messages");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{name}] Error: {ex.Message}");
            break;
        }
    }

    Console.WriteLine($"[{name}] Received {messageCount} messages. Unsubscribing...");

    // Unsubscribe from topics (demonstrates dynamic subscription handling)
    foreach (var topic in topics)
    {
        socket.Unsubscribe(topic);
        Console.WriteLine($"[{name}] Unsubscribed from topic: '{topic}'");
    }

    Console.WriteLine($"[{name}] Completed");
}
