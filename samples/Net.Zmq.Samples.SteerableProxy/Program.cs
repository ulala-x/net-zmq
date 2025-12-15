using Net.Zmq;

Console.WriteLine("NetZeroMQ Steerable Proxy Sample");
Console.WriteLine("=============================");
Console.WriteLine();
Console.WriteLine("This sample demonstrates:");
Console.WriteLine("  - Steerable proxy with control socket");
Console.WriteLine("  - PAUSE/RESUME/TERMINATE commands");
Console.WriteLine("  - Dynamic proxy control at runtime");
Console.WriteLine();

using var context = new Context();

// Start proxy in background
var proxyThread = new Thread(() => RunSteerableProxy(context));
proxyThread.IsBackground = true;
proxyThread.Start();

Thread.Sleep(500);

// Start publisher
var pubThread = new Thread(() => RunPublisher(context));
pubThread.IsBackground = true;
pubThread.Start();

Thread.Sleep(500);

// Start subscriber
var subThread = new Thread(() => RunSubscriber(context));
subThread.IsBackground = true;
subThread.Start();

// Run control commands
RunController(context);

// Wait for completion
subThread.Join(TimeSpan.FromSeconds(5));

Console.WriteLine();
Console.WriteLine("[Main] Done");

void RunSteerableProxy(Context ctx)
{
    Console.WriteLine("[Proxy] Starting steerable proxy...");

    using var frontend = new Socket(ctx, SocketType.XSub);
    using var backend = new Socket(ctx, SocketType.XPub);
    using var control = new Socket(ctx, SocketType.Pair);

    frontend.Bind("tcp://*:5564");
    backend.Bind("tcp://*:5565");
    control.Bind("inproc://proxy-control");

    Console.WriteLine("[Proxy] Frontend XSub: tcp://*:5564");
    Console.WriteLine("[Proxy] Backend XPub:  tcp://*:5565");
    Console.WriteLine("[Proxy] Control:       inproc://proxy-control");
    Console.WriteLine();

    try
    {
        // Start steerable proxy - blocks until TERMINATE
        Proxy.StartSteerable(frontend, backend, control);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Proxy] Stopped: {ex.Message}");
    }

    Console.WriteLine("[Proxy] Terminated");
}

void RunController(Context ctx)
{
    Console.WriteLine("[Controller] Starting...");

    using var control = new Socket(ctx, SocketType.Pair);
    control.Connect("inproc://proxy-control");

    Console.WriteLine("[Controller] Connected to proxy control socket");
    Console.WriteLine();

    // Let some messages flow
    Thread.Sleep(2000);

    // Pause the proxy
    Console.WriteLine("[Controller] >>> Sending PAUSE command");
    control.Send("PAUSE");
    Console.WriteLine("[Controller] Proxy paused - messages will be queued");
    Thread.Sleep(2000);

    // Resume the proxy
    Console.WriteLine("[Controller] >>> Sending RESUME command");
    control.Send("RESUME");
    Console.WriteLine("[Controller] Proxy resumed - queued messages will flow");
    Thread.Sleep(2000);

    // Terminate the proxy
    Console.WriteLine("[Controller] >>> Sending TERMINATE command");
    control.Send("TERMINATE");
    Console.WriteLine("[Controller] Proxy termination requested");

    Console.WriteLine("[Controller] Done");
}

void RunPublisher(Context ctx)
{
    Console.WriteLine("[Publisher] Starting...");

    using var socket = new Socket(ctx, SocketType.Pub);
    socket.SetOption(SocketOption.Linger, 0);
    socket.Connect("tcp://localhost:5564");

    Thread.Sleep(500);

    for (int i = 1; i <= 15; i++)
    {
        var message = $"news Message #{i}";
        try
        {
            socket.Send(message);
            Console.WriteLine($"[Publisher] Sent: {message}");
        }
        catch
        {
            break;
        }
        Thread.Sleep(500);
    }

    Console.WriteLine("[Publisher] Done");
}

void RunSubscriber(Context ctx)
{
    Console.WriteLine("[Subscriber] Starting...");

    using var socket = new Socket(ctx, SocketType.Sub);
    socket.SetOption(SocketOption.Linger, 0);
    socket.SetOption(SocketOption.Rcvtimeo, 1000);
    socket.Connect("tcp://localhost:5565");
    socket.Subscribe("news");

    Console.WriteLine("[Subscriber] Subscribed to 'news'");
    Console.WriteLine();

    Thread.Sleep(500);

    int received = 0;
    int timeouts = 0;

    while (timeouts < 3)
    {
        try
        {
            var message = socket.RecvString();
            Console.WriteLine($"[Subscriber] Received: {message}");
            received++;
            timeouts = 0;
        }
        catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN
        {
            timeouts++;
            if (timeouts < 3)
                Console.WriteLine("[Subscriber] Waiting for messages...");
        }
        catch
        {
            break;
        }
    }

    Console.WriteLine($"[Subscriber] Received {received} messages total");
    Console.WriteLine("[Subscriber] Done");
}
