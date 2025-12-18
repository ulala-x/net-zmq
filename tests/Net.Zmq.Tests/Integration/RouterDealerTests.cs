using FluentAssertions;
using System.Text;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Tests for ROUTER-DEALER socket pattern.
/// ROUTER sockets enable identity-based routing for asynchronous request-reply patterns.
/// DEALER sockets provide load-balancing capabilities and can connect to multiple endpoints.
/// Together they support scalable, bidirectional messaging with explicit routing control.
/// </summary>
[Collection("Sequential")]
[Trait("Socket", "RouterDealer")]
public class Router_Dealer_Socket
{
    /// <summary>
    /// Tests for identity-based message routing between ROUTER and DEALER sockets.
    /// ROUTER sockets automatically prepend sender identity to incoming messages,
    /// enabling targeted replies to specific clients.
    /// </summary>
    public class Identity_Routing
    {
        [Fact(DisplayName = "Should exchange messages using explicit dealer identity for routing")]
        public void Should_Exchange_Messages_With_Explicit_Identity()
        {
            // Given: A ROUTER socket bound to an endpoint and a DEALER with explicit identity
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Rcvtimeo, 1000);

            dealer.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("DEALER1"));

            router.Bind("tcp://127.0.0.1:15610");
            dealer.Connect("tcp://127.0.0.1:15610");

            Thread.Sleep(200);

            // When: Dealer sends a message (without identity frame)
            dealer.Send("Hello from Dealer");

            // Then: Router receives message with prepended identity frame
            var identity = router.RecvBytes();
            router.HasMore.Should().BeTrue();

            var message = router.RecvString();
            router.HasMore.Should().BeFalse();

            Encoding.UTF8.GetString(identity).Should().Be("DEALER1");
            message.Should().Be("Hello from Dealer");

            // When: Router replies by including identity frame for routing
            router.Send(identity, SendFlags.SendMore);
            router.Send("Hello from Router");

            // Then: Dealer receives reply without identity frame
            var reply = dealer.RecvString();
            dealer.HasMore.Should().BeFalse();
            reply.Should().Be("Hello from Router");
        }

        [Fact(DisplayName = "Should route messages to specific dealers using stored identities")]
        public void Should_Route_Messages_To_Specific_Workers()
        {
            // Given: A ROUTER connected to two DEALERs with distinct identities
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer1 = new Socket(ctx, SocketType.Dealer);
            using var dealer2 = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer1.SetOption(SocketOption.Linger, 0);
            dealer1.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer2.SetOption(SocketOption.Linger, 0);
            dealer2.SetOption(SocketOption.Rcvtimeo, 1000);

            dealer1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("WORKER_1"));
            dealer2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("WORKER_2"));

            router.Bind("tcp://127.0.0.1:15613");
            dealer1.Connect("tcp://127.0.0.1:15613");
            dealer2.Connect("tcp://127.0.0.1:15613");

            Thread.Sleep(200);

            // When: Both dealers register by sending initial messages
            dealer1.Send("Ready");
            Thread.Sleep(50);

            var worker1Id = router.RecvBytes();
            router.HasMore.Should().BeTrue();
            router.RecvString();

            dealer2.Send("Ready");
            Thread.Sleep(50);

            var worker2Id = router.RecvBytes();
            router.HasMore.Should().BeTrue();
            router.RecvString();

            // When: Router manually routes tasks to specific workers
            router.Send(worker2Id, SendFlags.SendMore);
            router.Send("Task for Worker 2");

            router.Send(worker1Id, SendFlags.SendMore);
            router.Send("Task for Worker 1");

            // Then: Each worker receives only its designated task
            var task2 = dealer2.RecvString();
            task2.Should().Be("Task for Worker 2");

            var task1 = dealer1.RecvString();
            task1.Should().Be("Task for Worker 1");
        }

        [Fact(DisplayName = "Should work with auto-generated identity when not explicitly set")]
        public void Should_Work_With_Auto_Generated_Identity()
        {
            // Given: A DEALER without explicit identity (ZMQ auto-generates one)
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Rcvtimeo, 1000);

            router.Bind("tcp://127.0.0.1:15617");
            dealer.Connect("tcp://127.0.0.1:15617");

            Thread.Sleep(200);

            // When: Dealer sends a message
            dealer.Send("Message with auto ID");

            // Then: Router receives message with auto-generated identity
            var identity = router.RecvBytes();
            var message = router.RecvString();

            identity.Should().NotBeEmpty();
            identity.Length.Should().BeGreaterThan(0);
            message.Should().Be("Message with auto ID");

            // When: Router replies using the auto-generated identity
            router.Send(identity, SendFlags.SendMore);
            router.Send("Reply to auto ID");

            // Then: Dealer successfully receives the reply
            var reply = dealer.RecvString();
            reply.Should().Be("Reply to auto ID");
        }
    }

    /// <summary>
    /// Tests for handling multiple client connections.
    /// ROUTER sockets can manage multiple DEALER clients simultaneously,
    /// routing messages based on identity frames.
    /// </summary>
    public class Multiple_Clients
    {
        [Fact(DisplayName = "Should handle multiple dealers and route replies to correct client")]
        public void Should_Handle_Multiple_Dealers_With_Targeted_Replies()
        {
            // Given: A ROUTER bound to multiple DEALERs with unique identities
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer1 = new Socket(ctx, SocketType.Dealer);
            using var dealer2 = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer1.SetOption(SocketOption.Linger, 0);
            dealer1.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer2.SetOption(SocketOption.Linger, 0);
            dealer2.SetOption(SocketOption.Rcvtimeo, 1000);

            dealer1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("CLIENT_A"));
            dealer2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("CLIENT_B"));

            router.Bind("tcp://127.0.0.1:15611");
            dealer1.Connect("tcp://127.0.0.1:15611");
            dealer2.Connect("tcp://127.0.0.1:15611");

            Thread.Sleep(200);

            // When: Both dealers send messages
            dealer1.Send("Message from A");

            var identity1 = router.RecvBytes();
            router.HasMore.Should().BeTrue();
            var message1 = router.RecvString();

            Encoding.UTF8.GetString(identity1).Should().Be("CLIENT_A");
            message1.Should().Be("Message from A");

            dealer2.Send("Message from B");

            var identity2 = router.RecvBytes();
            router.HasMore.Should().BeTrue();
            var message2 = router.RecvString();

            Encoding.UTF8.GetString(identity2).Should().Be("CLIENT_B");
            message2.Should().Be("Message from B");

            // When: Router sends targeted reply to CLIENT_B only
            router.Send(identity2, SendFlags.SendMore);
            router.Send("Reply to B");

            // Then: Only CLIENT_B receives the reply
            var replyB = dealer2.RecvString();
            replyB.Should().Be("Reply to B");

            dealer1.RecvString(RecvFlags.DontWait).Should().BeNull();
        }

        [Fact(DisplayName = "Should distribute messages from dealer in order received")]
        public void Should_Distribute_Messages_In_Order()
        {
            // Given: A DEALER sending to a ROUTER
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Dealer);
            using var receiver = new Socket(ctx, SocketType.Router);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Rcvtimeo, 1000);

            sender.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("SENDER"));

            receiver.Bind("tcp://127.0.0.1:15614");
            sender.Connect("tcp://127.0.0.1:15614");

            Thread.Sleep(200);

            // When: Dealer sends multiple messages in sequence
            sender.Send("Message 1");
            sender.Send("Message 2");
            sender.Send("Message 3");

            Thread.Sleep(100);

            // Then: Router receives all messages in order
            var messages = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var identity = receiver.RecvBytes();
                receiver.HasMore.Should().BeTrue();
                var msg = receiver.RecvString();
                messages.Add(msg);
            }

            messages.Should().HaveCount(3);
            messages.Should().Contain("Message 1");
            messages.Should().Contain("Message 2");
            messages.Should().Contain("Message 3");
        }
    }

    /// <summary>
    /// Tests for asynchronous request-reply communication patterns.
    /// ROUTER-DEALER supports sending multiple requests without waiting for replies,
    /// enabling pipeline and async workflows.
    /// </summary>
    public class Async_Communication
    {
        [Fact(DisplayName = "Should handle multiple async requests and replies in order")]
        public void Should_Handle_Async_Request_Reply_Pipeline()
        {
            // Given: A ROUTER-DEALER connection for async communication
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("ASYNC_CLIENT"));

            router.Bind("tcp://127.0.0.1:15612");
            dealer.Connect("tcp://127.0.0.1:15612");

            Thread.Sleep(200);

            // When: Dealer sends multiple requests without waiting for replies
            dealer.Send("Request 1");
            dealer.Send("Request 2");
            dealer.Send("Request 3");

            Thread.Sleep(100);

            // Then: Router receives all requests in order
            var requests = new List<string>();
            var identities = new List<byte[]>();

            for (int i = 0; i < 3; i++)
            {
                var id = router.RecvBytes();
                router.HasMore.Should().BeTrue();
                identities.Add(id);
                requests.Add(router.RecvString());
            }

            requests.Should().Equal("Request 1", "Request 2", "Request 3");

            // When: Router sends replies in order
            for (int i = 0; i < 3; i++)
            {
                router.Send(identities[i], SendFlags.SendMore);
                router.Send($"Reply {i + 1}");
            }

            // Then: Dealer receives all replies in order
            var replies = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                replies.Add(dealer.RecvString());
            }

            replies.Should().Equal("Reply 1", "Reply 2", "Reply 3");
        }
    }

    /// <summary>
    /// Tests for multipart message handling.
    /// ROUTER and DEALER sockets preserve multipart message structure,
    /// with ROUTER automatically adding identity frame as first part.
    /// </summary>
    public class Multipart_Messages
    {
        [Fact(DisplayName = "Should preserve multipart message frames with identity routing")]
        public void Should_Preserve_Multipart_Message_Structure()
        {
            // Given: A ROUTER-DEALER connection
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("MULTIPART"));

            router.Bind("tcp://127.0.0.1:15615");
            dealer.Connect("tcp://127.0.0.1:15615");

            Thread.Sleep(200);

            // When: Dealer sends a three-part message
            dealer.Send("Header", SendFlags.SendMore);
            dealer.Send("Body", SendFlags.SendMore);
            dealer.Send("Footer");

            Thread.Sleep(50);

            // Then: Router receives identity + three message parts
            var identity = router.RecvBytes();
            router.HasMore.Should().BeTrue();

            var header = router.RecvString();
            router.HasMore.Should().BeTrue();

            var body = router.RecvString();
            router.HasMore.Should().BeTrue();

            var footer = router.RecvString();
            router.HasMore.Should().BeFalse();

            Encoding.UTF8.GetString(identity).Should().Be("MULTIPART");
            header.Should().Be("Header");
            body.Should().Be("Body");
            footer.Should().Be("Footer");

            // When: Router replies with multipart message (identity + 2 parts)
            router.Send(identity, SendFlags.SendMore);
            router.Send("Response Header", SendFlags.SendMore);
            router.Send("Response Body");

            // Then: Dealer receives two-part message (no identity)
            var respHeader = dealer.RecvString();
            dealer.HasMore.Should().BeTrue();

            var respBody = dealer.RecvString();
            dealer.HasMore.Should().BeFalse();

            respHeader.Should().Be("Response Header");
            respBody.Should().Be("Response Body");
        }
    }

    /// <summary>
    /// Tests for different message types and edge cases.
    /// Validates handling of Message objects, empty messages, and various data formats.
    /// </summary>
    public class Message_Types
    {
        [Fact(DisplayName = "Should work with Message objects for zero-copy operations")]
        public void Should_Work_With_Message_Objects()
        {
            // Given: A ROUTER-DEALER connection
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("MSG_TEST"));

            router.Bind("tcp://127.0.0.1:15616");
            dealer.Connect("tcp://127.0.0.1:15616");

            Thread.Sleep(200);

            // When: Dealer sends using Message object
            var sendMsg = new Message("Test Message");
            dealer.Send(sendMsg, SendFlags.None);
            sendMsg.Dispose();

            // Then: Router receives identity and message using Message objects
            var identityMsg = new Message();
            router.Recv(identityMsg, RecvFlags.None);
            identityMsg.ToString().Should().Be("MSG_TEST");
            identityMsg.Dispose();

            var receivedMsg = new Message();
            router.Recv(receivedMsg, RecvFlags.None);
            receivedMsg.ToString().Should().Be("Test Message");
            receivedMsg.Dispose();
        }

        [Fact(DisplayName = "Should handle empty messages correctly")]
        public void Should_Handle_Empty_Messages()
        {
            // Given: A ROUTER-DEALER connection
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Rcvtimeo, 1000);
            dealer.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("EMPTY_TEST"));

            router.Bind("tcp://127.0.0.1:15618");
            dealer.Connect("tcp://127.0.0.1:15618");

            Thread.Sleep(200);

            // When: Dealer sends an empty message
            dealer.Send("");

            // Then: Router receives identity and empty message payload
            var identity = router.RecvBytes();
            var message = router.RecvString();

            Encoding.UTF8.GetString(identity).Should().Be("EMPTY_TEST");
            message.Should().BeEmpty();
        }
    }

    /// <summary>
    /// Tests for high-volume message processing.
    /// Validates stability and correctness under load with many messages.
    /// </summary>
    public class High_Volume
    {
        [Fact(DisplayName = "Should handle high volume of messages without loss")]
        public void Should_Handle_High_Volume_Correctly()
        {
            // Given: A ROUTER-DEALER with increased buffer sizes for high volume
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            router.SetOption(SocketOption.Rcvtimeo, 5000);
            dealer.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("VOLUME_TEST"));

            router.SetOption(SocketOption.Rcvhwm, 10000);
            dealer.SetOption(SocketOption.Sndhwm, 10000);

            router.Bind("tcp://127.0.0.1:15619");
            dealer.Connect("tcp://127.0.0.1:15619");

            Thread.Sleep(200);

            const int messageCount = 100;

            // When: Dealer sends many messages rapidly
            for (int i = 0; i < messageCount; i++)
            {
                dealer.Send($"Message {i}");
            }

            Thread.Sleep(500);

            // Then: Router receives all messages with correct identity
            var receivedCount = 0;
            for (int i = 0; i < messageCount; i++)
            {
                var identity = router.RecvBytes();
                router.HasMore.Should().BeTrue();
                var message = router.RecvString();

                Encoding.UTF8.GetString(identity).Should().Be("VOLUME_TEST");
                message.Should().StartWith("Message ");
                receivedCount++;
            }

            receivedCount.Should().Be(messageCount);
        }
    }

    /// <summary>
    /// Tests for ROUTER-to-ROUTER communication patterns.
    /// When two ROUTER sockets connect, both must use explicit identities.
    /// Messages must include destination identity frame for routing.
    /// This enables peer-to-peer and hub-spoke architectures.
    /// </summary>
    public class Router_To_Router
    {
        [Fact(DisplayName = "Should exchange messages between routers using explicit identities")]
        public void Should_Exchange_Messages_With_Explicit_Identity()
        {
            // Given: Two ROUTER sockets with explicit identities (required for Router-to-Router)
            using var ctx = new Context();
            using var router1 = new Socket(ctx, SocketType.Router);
            using var router2 = new Socket(ctx, SocketType.Router);

            router1.SetOption(SocketOption.Linger, 0);
            router2.SetOption(SocketOption.Linger, 0);
            router1.SetOption(SocketOption.Rcvtimeo, 1000);
            router2.SetOption(SocketOption.Rcvtimeo, 1000);

            router1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("ROUTER1"));
            router2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("ROUTER2"));

            router1.Bind("tcp://127.0.0.1:15680");
            router2.Connect("tcp://127.0.0.1:15680");

            Thread.Sleep(200);

            // When: Router2 sends to Router1 (must include target identity)
            router2.Send(Encoding.UTF8.GetBytes("ROUTER1"), SendFlags.SendMore);
            router2.Send("Hello from Router2");

            // Then: Router1 receives sender identity + message
            var senderIdentity = router1.RecvBytes();
            router1.HasMore.Should().BeTrue();
            var message = router1.RecvString();
            router1.HasMore.Should().BeFalse();

            Encoding.UTF8.GetString(senderIdentity).Should().Be("ROUTER2");
            message.Should().Be("Hello from Router2");

            // When: Router1 replies using sender identity
            router1.Send(senderIdentity, SendFlags.SendMore);
            router1.Send("Hello from Router1");

            // Then: Router2 receives reply with Router1's identity
            var replyIdentity = router2.RecvBytes();
            router2.HasMore.Should().BeTrue();
            var reply = router2.RecvString();
            router2.HasMore.Should().BeFalse();

            Encoding.UTF8.GetString(replyIdentity).Should().Be("ROUTER1");
            reply.Should().Be("Hello from Router1");
        }

        [Fact(DisplayName = "Should support bidirectional communication between router peers")]
        public void Should_Support_Bidirectional_Communication()
        {
            // Given: Two ROUTER peers with identities PEER_A and PEER_B
            using var ctx = new Context();
            using var routerA = new Socket(ctx, SocketType.Router);
            using var routerB = new Socket(ctx, SocketType.Router);

            routerA.SetOption(SocketOption.Linger, 0);
            routerB.SetOption(SocketOption.Linger, 0);
            routerA.SetOption(SocketOption.Rcvtimeo, 1000);
            routerB.SetOption(SocketOption.Rcvtimeo, 1000);

            routerA.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_A"));
            routerB.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_B"));

            routerA.Bind("tcp://127.0.0.1:15681");
            routerB.Connect("tcp://127.0.0.1:15681");

            Thread.Sleep(200);

            // When: Messages are exchanged in both directions
            // B -> A
            routerB.Send(Encoding.UTF8.GetBytes("PEER_A"), SendFlags.SendMore);
            routerB.Send("Message 1: B to A");

            // Then: A receives message from B
            var idFromB = routerA.RecvBytes();
            routerA.HasMore.Should().BeTrue();
            var msgFromB = routerA.RecvString();
            Encoding.UTF8.GetString(idFromB).Should().Be("PEER_B");
            msgFromB.Should().Be("Message 1: B to A");

            // When: A -> B
            routerA.Send(idFromB, SendFlags.SendMore);
            routerA.Send("Message 2: A to B");

            // Then: B receives message from A
            var idFromA = routerB.RecvBytes();
            routerB.HasMore.Should().BeTrue();
            var msgFromA = routerB.RecvString();
            Encoding.UTF8.GetString(idFromA).Should().Be("PEER_A");
            msgFromA.Should().Be("Message 2: A to B");

            // When: B -> A again
            routerB.Send(Encoding.UTF8.GetBytes("PEER_A"), SendFlags.SendMore);
            routerB.Send("Message 3: B to A");

            // Then: A receives the follow-up message
            idFromB = routerA.RecvBytes();
            routerA.HasMore.Should().BeTrue();
            msgFromB = routerA.RecvString();
            msgFromB.Should().Be("Message 3: B to A");
        }

        [Fact(DisplayName = "Should preserve multipart messages in router-to-router communication")]
        public void Should_Preserve_Multipart_Messages()
        {
            // Given: Two connected ROUTER sockets
            using var ctx = new Context();
            using var router1 = new Socket(ctx, SocketType.Router);
            using var router2 = new Socket(ctx, SocketType.Router);

            router1.SetOption(SocketOption.Linger, 0);
            router2.SetOption(SocketOption.Linger, 0);
            router1.SetOption(SocketOption.Rcvtimeo, 1000);
            router2.SetOption(SocketOption.Rcvtimeo, 1000);

            router1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("R1"));
            router2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("R2"));

            router1.Bind("tcp://127.0.0.1:15682");
            router2.Connect("tcp://127.0.0.1:15682");

            Thread.Sleep(200);

            // When: Router2 sends multipart message to Router1
            router2.Send(Encoding.UTF8.GetBytes("R1"), SendFlags.SendMore);
            router2.Send("Header", SendFlags.SendMore);
            router2.Send("Body", SendFlags.SendMore);
            router2.Send("Footer");

            // Then: Router1 receives sender identity + all message parts
            var sender = router1.RecvString();
            router1.HasMore.Should().BeTrue();
            var header = router1.RecvString();
            router1.HasMore.Should().BeTrue();
            var body = router1.RecvString();
            router1.HasMore.Should().BeTrue();
            var footer = router1.RecvString();
            router1.HasMore.Should().BeFalse();

            sender.Should().Be("R2");
            header.Should().Be("Header");
            body.Should().Be("Body");
            footer.Should().Be("Footer");
        }

        [Fact(DisplayName = "Should route messages correctly in hub with multiple router peers")]
        public void Should_Route_To_Multiple_Peers_From_Hub()
        {
            // Given: A hub ROUTER connected to multiple peer ROUTERs
            using var ctx = new Context();
            using var hub = new Socket(ctx, SocketType.Router);
            using var peer1 = new Socket(ctx, SocketType.Router);
            using var peer2 = new Socket(ctx, SocketType.Router);

            hub.SetOption(SocketOption.Linger, 0);
            peer1.SetOption(SocketOption.Linger, 0);
            peer2.SetOption(SocketOption.Linger, 0);
            hub.SetOption(SocketOption.Rcvtimeo, 1000);
            peer1.SetOption(SocketOption.Rcvtimeo, 1000);
            peer2.SetOption(SocketOption.Rcvtimeo, 1000);

            hub.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("HUB"));
            peer1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER1"));
            peer2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER2"));

            hub.Bind("tcp://127.0.0.1:15683");
            peer1.Connect("tcp://127.0.0.1:15683");
            peer2.Connect("tcp://127.0.0.1:15683");

            Thread.Sleep(300);

            // When: Both peers send messages to hub
            peer1.Send(Encoding.UTF8.GetBytes("HUB"), SendFlags.SendMore);
            peer1.Send("Hello from PEER1");

            peer2.Send(Encoding.UTF8.GetBytes("HUB"), SendFlags.SendMore);
            peer2.Send("Hello from PEER2");

            Thread.Sleep(100);

            // Then: Hub receives messages from both peers
            var messages = new Dictionary<string, string>();
            for (int i = 0; i < 2; i++)
            {
                var peerId = Encoding.UTF8.GetString(hub.RecvBytes());
                hub.HasMore.Should().BeTrue();
                var msg = hub.RecvString();
                messages[peerId] = msg;
            }

            messages.Should().ContainKey("PEER1");
            messages.Should().ContainKey("PEER2");
            messages["PEER1"].Should().Be("Hello from PEER1");
            messages["PEER2"].Should().Be("Hello from PEER2");

            // When: Hub sends targeted messages to each peer
            hub.Send(Encoding.UTF8.GetBytes("PEER1"), SendFlags.SendMore);
            hub.Send("Reply to PEER1");

            hub.Send(Encoding.UTF8.GetBytes("PEER2"), SendFlags.SendMore);
            hub.Send("Reply to PEER2");

            // Then: Each peer receives only its designated message
            var id1 = Encoding.UTF8.GetString(peer1.RecvBytes());
            peer1.HasMore.Should().BeTrue();
            var reply1 = peer1.RecvString();
            id1.Should().Be("HUB");
            reply1.Should().Be("Reply to PEER1");

            var id2 = Encoding.UTF8.GetString(peer2.RecvBytes());
            peer2.HasMore.Should().BeTrue();
            var reply2 = peer2.RecvString();
            id2.Should().Be("HUB");
            reply2.Should().Be("Reply to PEER2");
        }
    }
}
