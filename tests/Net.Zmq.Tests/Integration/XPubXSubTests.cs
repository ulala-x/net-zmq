using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Integration tests for XPub and XSub socket types.
/// XPUB/XSUB is an extended publish-subscribe pattern that provides access to subscription messages.
/// XPUB exposes subscription/unsubscription messages, and XSUB allows manual subscription control.
/// </summary>
[Collection("Sequential")]
[Trait("Socket", "XPubXSub")]
public class XPub_XSub_Socket
{
    /// <summary>
    /// Tests for subscription and unsubscription message handling.
    /// </summary>
    public class Subscription_Messages
    {
        [Fact(DisplayName = "XPUB socket should receive subscription messages from subscribers")]
        public void Should_Receive_Subscription_Messages()
        {
            // Given: An XPUB socket bound and a SUB socket connected
            using var ctx = new Context();
            using var xpub = new Socket(ctx, SocketType.XPub);
            using var sub = new Socket(ctx, SocketType.Sub);

            xpub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Linger, 0);
            xpub.SetOption(SocketOption.Rcvtimeo, 1000);

            xpub.Bind("tcp://127.0.0.1:15630");
            sub.Connect("tcp://127.0.0.1:15630");

            Thread.Sleep(200);

            // When: Subscriber subscribes to a topic
            sub.Subscribe("news");
            Thread.Sleep(200);

            // Then: XPUB receives subscription message with 0x01 indicator
            var subMsg = xpub.RecvBytes();
            subMsg.Should().NotBeNull();
            subMsg[0].Should().Be(0x01); // Subscribe indicator
            var topic = System.Text.Encoding.UTF8.GetString(subMsg, 1, subMsg.Length - 1);
            topic.Should().Be("news");
        }

        [Fact(DisplayName = "XPUB socket should receive unsubscription messages from subscribers")]
        public void Should_Receive_Unsubscription_Messages()
        {
            // Given: An XPUB socket with an active subscription
            using var ctx = new Context();
            using var xpub = new Socket(ctx, SocketType.XPub);
            using var sub = new Socket(ctx, SocketType.Sub);

            xpub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Linger, 0);
            xpub.SetOption(SocketOption.Rcvtimeo, 1000);

            xpub.Bind("tcp://127.0.0.1:15633");
            sub.Connect("tcp://127.0.0.1:15633");

            Thread.Sleep(200);

            sub.Subscribe("topic");
            Thread.Sleep(200);
            var subMsg = xpub.RecvBytes();
            subMsg[0].Should().Be(0x01);

            // When: Subscriber unsubscribes from the topic
            sub.Unsubscribe("topic");
            Thread.Sleep(200);

            // Then: XPUB receives unsubscription message with 0x00 indicator
            var unsubMsg = xpub.RecvBytes();
            unsubMsg[0].Should().Be(0x00);
            var topic = System.Text.Encoding.UTF8.GetString(unsubMsg, 1, unsubMsg.Length - 1);
            topic.Should().Be("topic");
        }

        [Fact(DisplayName = "XPUB socket should send messages to subscribed subscribers")]
        public void Should_Send_Messages_To_Subscribers()
        {
            // Given: An XPUB socket connected to a subscribed SUB socket
            using var ctx = new Context();
            using var xpub = new Socket(ctx, SocketType.XPub);
            using var sub = new Socket(ctx, SocketType.Sub);

            xpub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Linger, 0);
            xpub.SetOption(SocketOption.Rcvtimeo, 500);
            sub.SetOption(SocketOption.Rcvtimeo, 1000);

            xpub.Bind("tcp://127.0.0.1:15631");
            sub.Connect("tcp://127.0.0.1:15631");

            Thread.Sleep(200);

            sub.SubscribeAll();
            Thread.Sleep(200);

            // Drain subscription message
            xpub.RecvBytes(RecvFlags.DontWait);

            // When: XPUB sends a message
            xpub.Send("Hello from XPub");

            // Then: Subscriber receives the message
            var msg = sub.RecvString();
            msg.Should().Be("Hello from XPub");
        }
    }

    /// <summary>
    /// Tests for manual subscription control using XSUB socket.
    /// </summary>
    public class Manual_Subscription
    {
        [Fact(DisplayName = "XSUB socket should receive messages after manual subscription")]
        public void Should_Receive_After_Manual_Subscription()
        {
            // Given: An XSUB socket connected to a PUB socket
            using var ctx = new Context();
            using var pub = new Socket(ctx, SocketType.Pub);
            using var xsub = new Socket(ctx, SocketType.XSub);

            pub.SetOption(SocketOption.Linger, 0);
            xsub.SetOption(SocketOption.Linger, 0);
            xsub.SetOption(SocketOption.Rcvtimeo, 1000);

            pub.Bind("tcp://127.0.0.1:15632");
            xsub.Connect("tcp://127.0.0.1:15632");

            Thread.Sleep(200);

            // When: XSUB sends a manual subscription message (0x01 for subscribe to all)
            var subscribeMsg = new byte[] { 0x01 };
            xsub.Send(subscribeMsg);

            Thread.Sleep(300);

            pub.Send("Message from Pub");

            Thread.Sleep(100);

            // Then: XSUB receives the published message
            var msg = xsub.RecvString();
            msg.Should().Be("Message from Pub");
        }
    }
}
