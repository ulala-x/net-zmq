using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// PUB-SUB Socket Pattern
/// Publisher broadcasts messages to multiple subscribers.
/// Subscribers can filter messages by topic prefix.
/// </summary>
[Collection("Sequential")]
[Trait("Socket", "PubSub")]
public class PubSub_Socket
{
    public class Basic_Messaging
    {
        [Fact(DisplayName = "Should deliver messages to all subscribers")]
        public void Delivers_messages_to_subscribers()
        {
            // Given: A publisher and subscriber connected
            using var ctx = new Context();
            using var pub = new Socket(ctx, SocketType.Pub);
            using var sub = new Socket(ctx, SocketType.Sub);

            pub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Rcvtimeo, 1000);

            pub.Bind("tcp://127.0.0.1:15558");
            sub.Connect("tcp://127.0.0.1:15558");
            sub.SubscribeAll();

            Thread.Sleep(200); // Allow subscription to propagate

            // When: Publisher sends a message
            pub.Send("Test message");

            // Then: Subscriber receives the message
            var msg = sub.RecvString();
            msg.Should().Be("Test message");
        }
    }

    public class Topic_Filtering
    {
        [Fact(DisplayName = "Should filter messages by topic prefix")]
        public void Filters_by_topic_prefix()
        {
            // Given: A publisher and subscriber with topic filter
            using var ctx = new Context();
            using var pub = new Socket(ctx, SocketType.Pub);
            using var sub = new Socket(ctx, SocketType.Sub);

            pub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Linger, 0);
            sub.SetOption(SocketOption.Rcvtimeo, 500);

            pub.Bind("tcp://127.0.0.1:15559");
            sub.Connect("tcp://127.0.0.1:15559");
            sub.Subscribe("topic1");

            Thread.Sleep(200);

            // When: Publisher sends a message to subscribed topic
            pub.Send("topic1 Hello");

            // Then: Subscriber receives the message
            var msg = sub.RecvString();
            msg.Should().Be("topic1 Hello");
        }
    }
}
