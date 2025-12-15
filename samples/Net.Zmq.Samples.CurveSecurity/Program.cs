using Net.Zmq;

Console.WriteLine("NetZeroMQ CURVE Security Sample");
Console.WriteLine("============================");
Console.WriteLine();
Console.WriteLine("This sample demonstrates:");
Console.WriteLine("  - CURVE keypair generation");
Console.WriteLine("  - Secure encrypted communication");
Console.WriteLine("  - Server and client authentication setup");
Console.WriteLine();

// Check if CURVE is available
if (!Context.Has("curve"))
{
    Console.WriteLine("ERROR: CURVE security is not available in this build of libzmq");
    Console.WriteLine("Please rebuild libzmq with libsodium support");
    return;
}

Console.WriteLine("[Setup] CURVE security is available");
Console.WriteLine();

// Generate server keypair
var (serverPublic, serverSecret) = Curve.GenerateKeypair();
Console.WriteLine("[Server] Generated keypair:");
Console.WriteLine($"  Public:  {serverPublic}");
Console.WriteLine($"  Secret:  {serverSecret}");
Console.WriteLine();

// Generate client keypair
var (clientPublic, clientSecret) = Curve.GenerateKeypair();
Console.WriteLine("[Client] Generated keypair:");
Console.WriteLine($"  Public:  {clientPublic}");
Console.WriteLine($"  Secret:  {clientSecret}");
Console.WriteLine();

// Demonstrate public key derivation
var derivedPublic = Curve.DerivePublicKey(clientSecret);
Console.WriteLine($"[Client] Derived public key from secret: {derivedPublic}");
Console.WriteLine($"[Client] Keys match: {derivedPublic == clientPublic}");
Console.WriteLine();

using var context = new Context();

// Start server in background
var serverThread = new Thread(() => RunSecureServer(context, serverPublic, serverSecret));
serverThread.IsBackground = true;
serverThread.Start();

Thread.Sleep(500);

// Run client
RunSecureClient(context, clientPublic, clientSecret, serverPublic);

Console.WriteLine();
Console.WriteLine("[Main] Secure communication completed successfully!");

void RunSecureServer(Context ctx, string publicKey, string secretKey)
{
    Console.WriteLine("[Server] Starting secure server...");

    using var socket = new Socket(ctx, SocketType.Rep);

    // Configure as CURVE server
    socket.SetOption(SocketOption.Curve_Server, 1);
    socket.SetOption(SocketOption.Curve_Secretkey, secretKey);

    socket.SetOption(SocketOption.Linger, 0);
    socket.SetOption(SocketOption.Rcvtimeo, 5000);
    socket.Bind("tcp://*:5563");

    Console.WriteLine("[Server] Bound to tcp://*:5563 with CURVE encryption");

    for (int i = 0; i < 3; i++)
    {
        try
        {
            var request = socket.RecvString();
            Console.WriteLine($"[Server] Received encrypted: {request}");

            var response = $"Secure response #{i + 1}";
            socket.Send(response);
            Console.WriteLine($"[Server] Sent encrypted: {response}");
        }
        catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN
        {
            Console.WriteLine("[Server] Timeout waiting for request");
            break;
        }
    }

    Console.WriteLine("[Server] Done");
}

void RunSecureClient(Context ctx, string publicKey, string secretKey, string serverPublicKey)
{
    Console.WriteLine("[Client] Starting secure client...");

    using var socket = new Socket(ctx, SocketType.Req);

    // Configure as CURVE client
    socket.SetOption(SocketOption.Curve_Serverkey, serverPublicKey);
    socket.SetOption(SocketOption.Curve_Publickey, publicKey);
    socket.SetOption(SocketOption.Curve_Secretkey, secretKey);

    socket.SetOption(SocketOption.Linger, 0);
    socket.SetOption(SocketOption.Rcvtimeo, 5000);
    socket.Connect("tcp://localhost:5563");

    Console.WriteLine("[Client] Connected to tcp://localhost:5563 with CURVE encryption");

    for (int i = 0; i < 3; i++)
    {
        var request = $"Secure request #{i + 1}";
        socket.Send(request);
        Console.WriteLine($"[Client] Sent encrypted: {request}");

        try
        {
            var response = socket.RecvString();
            Console.WriteLine($"[Client] Received encrypted: {response}");
        }
        catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN
        {
            Console.WriteLine("[Client] Timeout waiting for response");
            break;
        }

        Thread.Sleep(200);
    }

    Console.WriteLine("[Client] Done");
}
