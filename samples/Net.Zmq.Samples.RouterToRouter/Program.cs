using System.Text;
using Net.Zmq;

/// <summary>
/// Router-to-Router Pattern Examples using MultipartMessage API
///
/// Router-to-Router is an advanced ZeroMQ pattern used for:
/// - Peer-to-peer communication where both sides need routing control
/// - Building message brokers and proxies
/// - Network topologies where nodes need to address each other directly
///
/// Key characteristics:
/// - Both sockets must have explicit identities set
/// - Messages must include target identity as first frame
/// - Received messages include sender identity as first frame
/// - Fully asynchronous bidirectional communication
///
/// This example demonstrates the new MultipartMessage API for cleaner code.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NetZeroMQ Router-to-Router Examples (MultipartMessage API) ===\n");

        // Example 1: Basic peer-to-peer communication
        BasicPeerToPeerExample();

        // Example 2: Hub and spoke pattern with multiple peers
        HubAndSpokeExample();

        // Example 3: Broker pattern (Router-Router-Router)
        BrokerPatternExample();

        Console.WriteLine("\nAll examples completed!");
    }

    /// <summary>
    /// Example 1: Basic Peer-to-Peer Communication
    /// Two Router sockets communicating directly with each other
    /// Using MultipartMessage for cleaner send/receive
    /// </summary>
    static void BasicPeerToPeerExample()
    {
        Console.WriteLine("--- Example 1: Basic Peer-to-Peer (MultipartMessage) ---");

        using var ctx = new Context();
        using var peerA = new Socket(ctx, SocketType.Router);
        using var peerB = new Socket(ctx, SocketType.Router);

        // Configure sockets
        peerA.SetOption(SocketOption.Linger, 0);
        peerB.SetOption(SocketOption.Linger, 0);
        peerA.SetOption(SocketOption.Rcvtimeo, 1000);
        peerB.SetOption(SocketOption.Rcvtimeo, 1000);

        // IMPORTANT: Set explicit identities for Router-to-Router
        peerA.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_A"));
        peerB.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_B"));

        // Peer A binds, Peer B connects
        peerA.Bind("tcp://127.0.0.1:15700");
        peerB.Connect("tcp://127.0.0.1:15700");
        Thread.Sleep(100);

        // Peer B sends to Peer A using MultipartMessage
        // Frame 1: Target identity (who to send to)
        // Frame 2: Message content
        Console.WriteLine("Peer B sending message to Peer A...");
        using (var outMsg = new MultipartMessage())
        {
            outMsg.Add("PEER_A");           // Target identity
            outMsg.Add("Hello from Peer B!");
            peerB.SendMultipart(outMsg);
        }

        // Peer A receives using RecvMultipart
        // Frame 1: Sender identity (who sent this)
        // Frame 2: Message content
        using (var inMsg = peerA.RecvMultipart())
        {
            var senderId = inMsg.PeekFirstString();
            var message = Encoding.UTF8.GetString(inMsg[1].Data);
            Console.WriteLine($"Peer A received from [{senderId}]: {message}");

            // Peer A replies back using sender's identity
            Console.WriteLine("Peer A replying to Peer B...");
            using var reply = new MultipartMessage();
            reply.Add(senderId);            // Use sender's identity for reply
            reply.Add("Hello back from Peer A!");
            peerA.SendMultipart(reply);
        }

        // Peer B receives reply
        using (var replyMsg = peerB.RecvMultipart())
        {
            var replyFrom = replyMsg.PeekFirstString();
            var reply = Encoding.UTF8.GetString(replyMsg[1].Data);
            Console.WriteLine($"Peer B received from [{replyFrom}]: {reply}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Hub and Spoke Pattern
    /// One central hub Router connecting to multiple peer Routers
    /// Using MultipartMessage for cleaner broadcast operations
    /// </summary>
    static void HubAndSpokeExample()
    {
        Console.WriteLine("--- Example 2: Hub and Spoke Pattern (MultipartMessage) ---");

        using var ctx = new Context();
        using var hub = new Socket(ctx, SocketType.Router);
        using var spoke1 = new Socket(ctx, SocketType.Router);
        using var spoke2 = new Socket(ctx, SocketType.Router);
        using var spoke3 = new Socket(ctx, SocketType.Router);

        // Configure all sockets
        foreach (var socket in new[] { hub, spoke1, spoke2, spoke3 })
        {
            socket.SetOption(SocketOption.Linger, 0);
            socket.SetOption(SocketOption.Rcvtimeo, 1000);
        }

        // Set identities
        hub.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("HUB"));
        spoke1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("SPOKE1"));
        spoke2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("SPOKE2"));
        spoke3.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("SPOKE3"));

        // Hub binds, spokes connect
        hub.Bind("tcp://127.0.0.1:15701");
        spoke1.Connect("tcp://127.0.0.1:15701");
        spoke2.Connect("tcp://127.0.0.1:15701");
        spoke3.Connect("tcp://127.0.0.1:15701");
        Thread.Sleep(200);

        // All spokes send registration message to hub using SendMultipart
        Console.WriteLine("Spokes sending registration to Hub...");
        spoke1.SendMultipart("HUB", "REGISTER:SPOKE1");
        spoke2.SendMultipart("HUB", "REGISTER:SPOKE2");
        spoke3.SendMultipart("HUB", "REGISTER:SPOKE3");

        Thread.Sleep(100);

        // Hub receives and processes registrations
        var registeredPeers = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            using var regMsg = hub.RecvMultipart();
            var peerId = regMsg.PeekFirstString();
            var regContent = Encoding.UTF8.GetString(regMsg[1].Data);
            registeredPeers.Add(peerId);
            Console.WriteLine($"Hub received: [{peerId}] -> {regContent}");
        }

        // Hub broadcasts message to all registered peers
        Console.WriteLine("\nHub broadcasting to all spokes...");
        foreach (var peer in registeredPeers)
        {
            hub.SendMultipart(peer, $"Welcome {peer}! You are connected.");
        }

        // Each spoke receives its message
        foreach (var (spoke, name) in new[] { (spoke1, "SPOKE1"), (spoke2, "SPOKE2"), (spoke3, "SPOKE3") })
        {
            using var msg = spoke.RecvMultipart();
            var from = msg.PeekFirstString();
            var content = Encoding.UTF8.GetString(msg[1].Data);
            Console.WriteLine($"{name} received from [{from}]: {content}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Broker Pattern
    /// A broker (Router) that routes messages between clients (Routers)
    /// Clients can send messages to each other through the broker
    /// Using MultipartMessage for complex multi-frame routing
    /// </summary>
    static void BrokerPatternExample()
    {
        Console.WriteLine("--- Example 3: Broker Pattern (MultipartMessage) ---");

        using var ctx = new Context();
        using var broker = new Socket(ctx, SocketType.Router);
        using var client1 = new Socket(ctx, SocketType.Router);
        using var client2 = new Socket(ctx, SocketType.Router);

        // Configure all sockets
        foreach (var socket in new[] { broker, client1, client2 })
        {
            socket.SetOption(SocketOption.Linger, 0);
            socket.SetOption(SocketOption.Rcvtimeo, 1000);
        }

        // Set identities
        broker.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("BROKER"));
        client1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("CLIENT1"));
        client2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("CLIENT2"));

        broker.Bind("tcp://127.0.0.1:15702");
        client1.Connect("tcp://127.0.0.1:15702");
        client2.Connect("tcp://127.0.0.1:15702");
        Thread.Sleep(200);

        // Client1 sends a message to be forwarded to Client2
        // Message format: [BROKER][TARGET_CLIENT][actual message]
        Console.WriteLine("Client1 sending message to Client2 via Broker...");
        client1.SendMultipart("BROKER", "CLIENT2", "Hello Client2, this is Client1!");

        // Broker receives and forwards
        using (var inMsg = broker.RecvMultipart())
        {
            var senderId = inMsg.PeekFirstString();
            var targetId = Encoding.UTF8.GetString(inMsg[1].Data);
            var forwardMsg = Encoding.UTF8.GetString(inMsg[2].Data);

            Console.WriteLine($"Broker received from [{senderId}]: forward to [{targetId}] -> {forwardMsg}");

            // Broker forwards to target, including original sender info
            broker.SendMultipart(targetId, senderId, forwardMsg);
        }

        // Client2 receives
        using (var recvMsg = client2.RecvMultipart())
        {
            var brokerFrom = recvMsg.PeekFirstString();
            var originalSender = Encoding.UTF8.GetString(recvMsg[1].Data);
            var received = Encoding.UTF8.GetString(recvMsg[2].Data);

            Console.WriteLine($"Client2 received from [{originalSender}] (via {brokerFrom}): {received}");

            // Client2 replies back to Client1 via broker
            Console.WriteLine("\nClient2 replying to Client1 via Broker...");
            client2.SendMultipart("BROKER", "CLIENT1", "Got your message! Reply from Client2.");
        }

        // Broker forwards reply
        using (var replyIn = broker.RecvMultipart())
        {
            var senderId = replyIn.PeekFirstString();
            var targetId = Encoding.UTF8.GetString(replyIn[1].Data);
            var forwardMsg = Encoding.UTF8.GetString(replyIn[2].Data);

            broker.SendMultipart(targetId, senderId, forwardMsg);
        }

        // Client1 receives reply
        using (var finalMsg = client1.RecvMultipart())
        {
            var brokerFrom = finalMsg.PeekFirstString();
            var originalSender = Encoding.UTF8.GetString(finalMsg[1].Data);
            var received = Encoding.UTF8.GetString(finalMsg[2].Data);

            Console.WriteLine($"Client1 received from [{originalSender}] (via {brokerFrom}): {received}");
        }

        Console.WriteLine();
    }
}
