using Net.Zmq;

Console.WriteLine("NetZeroMQ REQ-REP Sample");
Console.WriteLine("=====================");
Console.WriteLine();

var mode = args.Length > 0 ? args[0].ToLower() : "both";

if (mode == "server" || mode == "both")
{
    if (mode == "both")
    {
        _ = Task.Run(RunServer);
        await Task.Delay(500);
    }
    else
    {
        RunServer();
        return;
    }
}

if (mode == "client" || mode == "both")
{
    RunClient();
}

void RunServer()
{
    Console.WriteLine("[Server] Starting...");
    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Rep);

    socket.SetOption(SocketOption.Linger, 0);
    socket.Bind("tcp://*:5555");
    Console.WriteLine("[Server] Listening on tcp://*:5555");

    for (int i = 0; i < 5; i++)
    {
        var request = socket.RecvString();
        Console.WriteLine($"[Server] Received: {request}");

        Thread.Sleep(100);

        var reply = $"Reply #{i + 1}";
        socket.Send(reply);
        Console.WriteLine($"[Server] Sent: {reply}");
    }

    Console.WriteLine("[Server] Done");
}

void RunClient()
{
    Console.WriteLine("[Client] Starting...");
    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Req);

    socket.SetOption(SocketOption.Linger, 0);
    socket.Connect("tcp://localhost:5555");
    Console.WriteLine("[Client] Connected to tcp://localhost:5555");

    for (int i = 0; i < 5; i++)
    {
        var request = $"Hello #{i + 1}";
        socket.Send(request);
        Console.WriteLine($"[Client] Sent: {request}");

        var reply = socket.RecvString();
        Console.WriteLine($"[Client] Received: {reply}");
    }

    Console.WriteLine("[Client] Done");
}
