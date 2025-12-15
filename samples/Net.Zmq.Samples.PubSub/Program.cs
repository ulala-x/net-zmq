using Net.Zmq;

Console.WriteLine("NetZeroMQ PUB-SUB Sample");
Console.WriteLine("=====================");
Console.WriteLine();

var mode = args.Length > 0 ? args[0].ToLower() : "both";

if (mode == "pub" || mode == "both")
{
    if (mode == "both")
    {
        _ = Task.Run(RunPublisher);
        await Task.Delay(500);
    }
    else
    {
        RunPublisher();
        return;
    }
}

if (mode == "sub" || mode == "both")
{
    RunSubscriber();
}

void RunPublisher()
{
    Console.WriteLine("[Publisher] Starting...");
    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Pub);

    socket.SetOption(SocketOption.Linger, 0);
    socket.Bind("tcp://*:5556");
    Console.WriteLine("[Publisher] Binding to tcp://*:5556");

    Thread.Sleep(1000); // Allow subscribers to connect

    string[] topics = { "weather", "sports", "news" };
    for (int i = 0; i < 10; i++)
    {
        var topic = topics[i % topics.Length];
        var message = $"{topic} Update #{i + 1}";
        socket.Send(message);
        Console.WriteLine($"[Publisher] Sent: {message}");
        Thread.Sleep(500);
    }

    Console.WriteLine("[Publisher] Done");
}

void RunSubscriber()
{
    Console.WriteLine("[Subscriber] Starting...");
    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Sub);

    socket.SetOption(SocketOption.Linger, 0);
    socket.SetOption(SocketOption.Rcvtimeo, 2000);
    socket.Connect("tcp://localhost:5556");
    socket.Subscribe("weather");
    socket.Subscribe("news");
    Console.WriteLine("[Subscriber] Subscribed to 'weather' and 'news' topics");

    for (int i = 0; i < 10; i++)
    {
        try
        {
            var message = socket.RecvString();
            Console.WriteLine($"[Subscriber] Received: {message}");
        }
        catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN
        {
            Console.WriteLine("[Subscriber] Timeout, no message received");
            break;
        }
    }

    Console.WriteLine("[Subscriber] Done");
}
