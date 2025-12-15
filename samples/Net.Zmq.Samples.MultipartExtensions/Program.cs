using Net.Zmq;

namespace Net.Zmq.Samples.MultipartExtensions;

/// <summary>
/// Demonstrates the Socket extension methods for convenient multipart message handling.
/// The SocketExtensions class provides SendMultipart and RecvMultipart methods that
/// simplify sending and receiving complete multipart messages in a single call.
/// </summary>
class Program
{
    static void Main()
    {
        Console.WriteLine("NetZeroMQ Multipart Extensions Demo");
        Console.WriteLine("=================================\n");

        // Run all examples
        Example1_SendMultipartWithStrings();
        Example2_SendMultipartWithByteArrays();
        Example3_SendMultipartWithMultipartMessage();
        Example4_RecvMultipart();
        Example5_TryRecvMultipart();
        Example6_RouterDealerWithExtensions();

        Console.WriteLine("\nAll examples completed successfully!");
    }

    /// <summary>
    /// Example 1: Send multipart using string params (most convenient for simple cases).
    /// </summary>
    static void Example1_SendMultipartWithStrings()
    {
        Console.WriteLine("Example 1: SendMultipart with string params");
        Console.WriteLine("--------------------------------------------");

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.SetOption(SocketOption.Linger, 0);
        receiver.SetOption(SocketOption.Linger, 0);

        receiver.Bind("tcp://127.0.0.1:20001");
        sender.Connect("tcp://127.0.0.1:20001");

        Thread.Sleep(100);

        // Send multipart message with simple string params
        sender.SendMultipart("Header", "Body", "Footer");
        Console.WriteLine("Sent: Header, Body, Footer");

        // Receive using traditional method
        var header = receiver.RecvString();
        var body = receiver.RecvString();
        var footer = receiver.RecvString();

        Console.WriteLine($"Received: {header}, {body}, {footer}");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Send multipart using IEnumerable of byte arrays.
    /// </summary>
    static void Example2_SendMultipartWithByteArrays()
    {
        Console.WriteLine("Example 2: SendMultipart with byte arrays");
        Console.WriteLine("------------------------------------------");

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.SetOption(SocketOption.Linger, 0);
        receiver.SetOption(SocketOption.Linger, 0);

        receiver.Bind("tcp://127.0.0.1:20002");
        sender.Connect("tcp://127.0.0.1:20002");

        Thread.Sleep(100);

        // Prepare binary frames
        var frames = new List<byte[]>
        {
            new byte[] { 0x01, 0x02, 0x03 },
            new byte[] { 0x04, 0x05 },
            new byte[] { 0x06, 0x07, 0x08, 0x09 }
        };

        sender.SendMultipart(frames);
        Console.WriteLine($"Sent {frames.Count} binary frames");

        // Receive frames
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = receiver.RecvBytes();
            Console.WriteLine($"Frame {i + 1}: [{string.Join(", ", frame.Select(b => $"0x{b:X2}"))}]");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Send multipart using MultipartMessage container.
    /// </summary>
    static void Example3_SendMultipartWithMultipartMessage()
    {
        Console.WriteLine("Example 3: SendMultipart with MultipartMessage");
        Console.WriteLine("-----------------------------------------------");

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.SetOption(SocketOption.Linger, 0);
        receiver.SetOption(SocketOption.Linger, 0);

        receiver.Bind("tcp://127.0.0.1:20003");
        sender.Connect("tcp://127.0.0.1:20003");

        Thread.Sleep(100);

        // Build multipart message with mixed content types
        using var message = new MultipartMessage();
        message.Add("Command");
        message.AddEmptyFrame(); // Delimiter
        message.Add(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }); // Binary data
        message.Add("Payload");

        sender.SendMultipart(message);
        Console.WriteLine($"Sent MultipartMessage with {message.Count} frames");

        // Receive and display
        var cmd = receiver.RecvString();
        var delimiter = receiver.RecvBytes();
        var binary = receiver.RecvBytes();
        var payload = receiver.RecvString();

        Console.WriteLine($"Command: {cmd}");
        Console.WriteLine($"Delimiter: {(delimiter.Length == 0 ? "empty" : "not empty")}");
        Console.WriteLine($"Binary: [{string.Join(", ", binary.Select(b => $"0x{b:X2}"))}]");
        Console.WriteLine($"Payload: {payload}");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 4: Receive multipart using RecvMultipart (blocking).
    /// </summary>
    static void Example4_RecvMultipart()
    {
        Console.WriteLine("Example 4: RecvMultipart (blocking receive)");
        Console.WriteLine("--------------------------------------------");

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.SetOption(SocketOption.Linger, 0);
        receiver.SetOption(SocketOption.Linger, 0);

        receiver.Bind("tcp://127.0.0.1:20004");
        sender.Connect("tcp://127.0.0.1:20004");

        Thread.Sleep(100);

        // Send multipart
        sender.SendMultipart("Part1", "Part2", "Part3");
        Console.WriteLine("Sent: Part1, Part2, Part3");

        // Receive complete message in one call
        using var received = receiver.RecvMultipart();
        Console.WriteLine($"Received {received.Count} frames:");

        for (int i = 0; i < received.Count; i++)
        {
            Console.WriteLine($"  Frame {i + 1}: {received[i].ToString()}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 5: Non-blocking receive with TryRecvMultipart.
    /// </summary>
    static void Example5_TryRecvMultipart()
    {
        Console.WriteLine("Example 5: TryRecvMultipart (non-blocking receive)");
        Console.WriteLine("---------------------------------------------------");

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.SetOption(SocketOption.Linger, 0);
        receiver.SetOption(SocketOption.Linger, 0);

        receiver.Bind("tcp://127.0.0.1:20005");
        sender.Connect("tcp://127.0.0.1:20005");

        Thread.Sleep(100);

        // Try to receive when no message is available
        var result1 = receiver.TryRecvMultipart(out var message1);
        Console.WriteLine($"First try (no message): {(result1 ? "Success" : "Would block")}");

        // Send a message
        sender.SendMultipart("Data1", "Data2");
        Thread.Sleep(50);

        // Try to receive when message is available
        var result2 = receiver.TryRecvMultipart(out var message2);
        Console.WriteLine($"Second try (message available): {(result2 ? "Success" : "Would block")}");

        if (message2 != null)
        {
            Console.WriteLine($"Received {message2.Count} frames:");
            for (int i = 0; i < message2.Count; i++)
            {
                Console.WriteLine($"  Frame {i + 1}: {message2[i].ToString()}");
            }
            message2.Dispose();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 6: Router-Dealer pattern with multipart extensions.
    /// Demonstrates how extension methods simplify envelope handling.
    /// </summary>
    static void Example6_RouterDealerWithExtensions()
    {
        Console.WriteLine("Example 6: Router-Dealer with extension methods");
        Console.WriteLine("------------------------------------------------");

        using var ctx = new Context();
        using var router = new Socket(ctx, SocketType.Router);
        using var dealer = new Socket(ctx, SocketType.Dealer);

        router.SetOption(SocketOption.Linger, 0);
        dealer.SetOption(SocketOption.Linger, 0);

        router.Bind("tcp://127.0.0.1:20006");
        dealer.Connect("tcp://127.0.0.1:20006");

        Thread.Sleep(100);

        // Dealer sends request
        dealer.SendMultipart("REQUEST", "GetData");
        Console.WriteLine("Dealer sent: REQUEST, GetData");

        // Router receives with automatic identity envelope
        using var request = router.RecvMultipart();
        Console.WriteLine($"Router received {request.Count} frames:");
        Console.WriteLine($"  Identity: {request[0].Size} bytes");
        Console.WriteLine($"  Frame 1: {request[1].ToString()}");
        Console.WriteLine($"  Frame 2: {request[2].ToString()}");

        // Router replies by echoing identity and adding response
        var identity = request[0].ToArray();
        router.SendMultipart(new List<byte[]>
        {
            identity,
            System.Text.Encoding.UTF8.GetBytes("RESPONSE"),
            System.Text.Encoding.UTF8.GetBytes("DataPayload")
        });
        Console.WriteLine("Router replied with RESPONSE, DataPayload");

        // Dealer receives response (identity stripped by Router)
        using var response = dealer.RecvMultipart();
        Console.WriteLine($"Dealer received {response.Count} frames:");
        for (int i = 0; i < response.Count; i++)
        {
            Console.WriteLine($"  Frame {i + 1}: {response[i].ToString()}");
        }

        Console.WriteLine();
    }
}
