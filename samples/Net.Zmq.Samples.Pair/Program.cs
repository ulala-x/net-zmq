using Net.Zmq;

Console.WriteLine("NetZeroMQ PAIR Socket Sample");
Console.WriteLine("==========================");
Console.WriteLine();
Console.WriteLine("Demonstrating 1:1 bidirectional communication using inproc transport");
Console.WriteLine();

// Create two threads with Pair sockets communicating via inproc
var thread1 = Task.Run(RunPairA);
var thread2 = Task.Run(RunPairB);

// Wait for both threads to complete
await Task.WhenAll(thread1, thread2);

Console.WriteLine();
Console.WriteLine("Sample completed");

void RunPairA()
{
    Console.WriteLine("[Pair-A] Starting...");
    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Pair);

    socket.SetOption(SocketOption.Linger, 0);
    socket.Bind("inproc://pair-example");
    Console.WriteLine("[Pair-A] Bound to inproc://pair-example");

    // Allow time for the other socket to connect
    Thread.Sleep(100);

    // Send initial message
    var message1 = "Hello from Pair-A";
    socket.Send(message1);
    Console.WriteLine($"[Pair-A] Sent: {message1}");

    // Receive response
    var received1 = socket.RecvString();
    Console.WriteLine($"[Pair-A] Received: {received1}");

    // Send another message
    var message2 = "How are you, Pair-B?";
    socket.Send(message2);
    Console.WriteLine($"[Pair-A] Sent: {message2}");

    // Receive final response
    var received2 = socket.RecvString();
    Console.WriteLine($"[Pair-A] Received: {received2}");

    // Send final message
    var message3 = "Goodbye from Pair-A";
    socket.Send(message3);
    Console.WriteLine($"[Pair-A] Sent: {message3}");

    Console.WriteLine("[Pair-A] Done");
}

void RunPairB()
{
    Console.WriteLine("[Pair-B] Starting...");
    using var ctx = new Context();
    using var socket = new Socket(ctx, SocketType.Pair);

    socket.SetOption(SocketOption.Linger, 0);

    // Wait briefly to ensure Pair-A has bound
    Thread.Sleep(50);

    socket.Connect("inproc://pair-example");
    Console.WriteLine("[Pair-B] Connected to inproc://pair-example");

    // Receive first message
    var received1 = socket.RecvString();
    Console.WriteLine($"[Pair-B] Received: {received1}");

    // Send response
    var message1 = "Hi Pair-A, this is Pair-B";
    socket.Send(message1);
    Console.WriteLine($"[Pair-B] Sent: {message1}");

    // Receive second message
    var received2 = socket.RecvString();
    Console.WriteLine($"[Pair-B] Received: {received2}");

    // Send final response
    var message2 = "I'm doing great! Thanks for asking.";
    socket.Send(message2);
    Console.WriteLine($"[Pair-B] Sent: {message2}");

    // Receive final message
    var received3 = socket.RecvString();
    Console.WriteLine($"[Pair-B] Received: {received3}");

    Console.WriteLine("[Pair-B] Done");
}
