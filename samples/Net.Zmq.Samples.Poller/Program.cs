using Net.Zmq;

Console.WriteLine("NetZeroMQ Poller Sample");
Console.WriteLine("====================");
Console.WriteLine();
Console.WriteLine("This sample demonstrates:");
Console.WriteLine("  - Polling multiple sockets simultaneously");
Console.WriteLine("  - Non-blocking receive with timeout");
Console.WriteLine("  - Handling multiple message sources");
Console.WriteLine();

using var context = new Context();

// Create two PULL sockets to receive from different sources
using var receiver1 = new Socket(context, SocketType.Pull);
using var receiver2 = new Socket(context, SocketType.Pull);

receiver1.Bind("tcp://*:5561");
receiver2.Bind("tcp://*:5562");

Console.WriteLine("[Main] Receiver 1 bound to tcp://*:5561");
Console.WriteLine("[Main] Receiver 2 bound to tcp://*:5562");
Console.WriteLine();

// Start sender threads
var sender1 = new Thread(() => RunSender(context, "Sender-1", "tcp://localhost:5561", 300));
var sender2 = new Thread(() => RunSender(context, "Sender-2", "tcp://localhost:5562", 500));

sender1.IsBackground = true;
sender2.IsBackground = true;
sender1.Start();
sender2.Start();

// Allow senders to connect
Thread.Sleep(500);

// Create poller with instance-based API
using var poller = new Poller(2);
int idx1 = poller.Add(receiver1, PollEvents.In);
int idx2 = poller.Add(receiver2, PollEvents.In);

Console.WriteLine("[Main] Starting to poll both receivers...");
Console.WriteLine();

int totalMessages = 0;
int maxMessages = 20;

while (totalMessages < maxMessages)
{
    // Poll with 1 second timeout
    int ready = poller.Poll(1000);

    if (ready == 0)
    {
        Console.WriteLine("[Main] Poll timeout - no messages");
        continue;
    }

    // Check receiver 1
    if (poller.IsReadable(idx1))
    {
        var msg = receiver1.RecvString();
        Console.WriteLine($"[Receiver-1] {msg}");
        totalMessages++;
    }

    // Check receiver 2
    if (poller.IsReadable(idx2))
    {
        var msg = receiver2.RecvString();
        Console.WriteLine($"[Receiver-2] {msg}");
        totalMessages++;
    }
}

Console.WriteLine();
Console.WriteLine($"[Main] Received {totalMessages} total messages");
Console.WriteLine("[Main] Done");

void RunSender(Context ctx, string name, string endpoint, int intervalMs)
{
    using var socket = new Socket(ctx, SocketType.Push);
    socket.SetOption(SocketOption.Linger, 0);
    socket.Connect(endpoint);

    Console.WriteLine($"[{name}] Connected to {endpoint}");

    for (int i = 1; i <= 10; i++)
    {
        var message = $"Message #{i} from {name}";
        socket.Send(message);
        Thread.Sleep(intervalMs);
    }

    Console.WriteLine($"[{name}] Done sending");
}
