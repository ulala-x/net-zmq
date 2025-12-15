using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Integration tests for ZMQ multipart messaging functionality.
/// Multipart messages allow sending multiple frames as a single atomic unit,
/// using SendFlags.SendMore to indicate continuation and HasMore/More properties
/// to track remaining frames. Common patterns include empty frame delimiters,
/// Router envelope addressing, and structured protocol messages.
/// </summary>
[Collection("Sequential")]
[Trait("Feature", "Multipart")]
public class Multipart_Message
{
    /// <summary>
    /// Tests for basic multipart frame operations, including sending multiple frames
    /// with SendMore flag and tracking continuation with HasMore/Message.More properties.
    /// </summary>
    public class Frame_Operations
    {
        [Fact(DisplayName = "Should send and receive multiple frames with HasMore tracking")]
        public void Should_Send_And_Receive_Multiple_Frames_With_HasMore_Tracking()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15650");
            sender.Connect("tcp://127.0.0.1:15650");

            Thread.Sleep(100); // Allow connection to establish

            // When: Sending a 3-frame multipart message
            sender.Send("Frame1", SendFlags.SendMore);
            sender.Send("Frame2", SendFlags.SendMore);
            sender.Send("Frame3", SendFlags.None); // Last frame

            // Then: All frames are received in order with correct HasMore status
            var frame1 = receiver.RecvString();
            frame1.Should().Be("Frame1");
            receiver.HasMore.Should().BeTrue("more frames should follow");

            var frame2 = receiver.RecvString();
            frame2.Should().Be("Frame2");
            receiver.HasMore.Should().BeTrue("one more frame should follow");

            var frame3 = receiver.RecvString();
            frame3.Should().Be("Frame3");
            receiver.HasMore.Should().BeFalse("this was the last frame");
        }

        [Fact(DisplayName = "Should track Message.More property when using Message objects")]
        public void Should_Track_Message_More_Property_When_Using_Message_Objects()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15651");
            sender.Connect("tcp://127.0.0.1:15651");

            Thread.Sleep(100);

            // When: Sending multipart using Message objects
            var msg1 = new Message("Part1");
            sender.Send(msg1, SendFlags.SendMore);
            msg1.Dispose();

            var msg2 = new Message("Part2");
            sender.Send(msg2, SendFlags.SendMore);
            msg2.Dispose();

            var msg3 = new Message("Part3");
            sender.Send(msg3, SendFlags.None);
            msg3.Dispose();

            // Then: Message.More property correctly indicates continuation
            var recv1 = new Message();
            receiver.Recv(recv1, RecvFlags.None);
            recv1.ToString().Should().Be("Part1");
            recv1.More.Should().BeTrue("Message.More should indicate more parts");
            recv1.Dispose();

            var recv2 = new Message();
            receiver.Recv(recv2, RecvFlags.None);
            recv2.ToString().Should().Be("Part2");
            recv2.More.Should().BeTrue("Message.More should indicate more parts");
            recv2.Dispose();

            var recv3 = new Message();
            receiver.Recv(recv3, RecvFlags.None);
            recv3.ToString().Should().Be("Part3");
            recv3.More.Should().BeFalse("Message.More should be false for last part");
            recv3.Dispose();
        }
    }

    /// <summary>
    /// Tests for empty frame delimiters, a ZMQ convention for separating
    /// message sections (e.g., header from body, envelope from payload).
    /// </summary>
    public class Delimiter_Frames
    {
        [Fact(DisplayName = "Should handle empty frame as delimiter between message sections")]
        public void Should_Handle_Empty_Frame_As_Delimiter_Between_Message_Sections()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15652");
            sender.Connect("tcp://127.0.0.1:15652");

            Thread.Sleep(100);

            // When: Sending header + empty delimiter + body (ZMQ convention)
            sender.Send("Header", SendFlags.SendMore);
            sender.Send(Array.Empty<byte>(), SendFlags.SendMore); // Empty frame delimiter
            sender.Send("Body", SendFlags.None);

            // Then: All frames including empty delimiter are received correctly
            var header = receiver.RecvString();
            header.Should().Be("Header");
            receiver.HasMore.Should().BeTrue();

            var delimiter = receiver.RecvBytes();
            delimiter.Should().BeEmpty("delimiter should be empty frame");
            receiver.HasMore.Should().BeTrue();

            var body = receiver.RecvString();
            body.Should().Be("Body");
            receiver.HasMore.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for Router socket envelope pattern, where Router automatically adds
    /// identity frames for addressing and strips them on reply routing.
    /// </summary>
    public class Envelope_Pattern
    {
        [Fact(DisplayName = "Should handle Router envelope with identity frames for request-reply routing")]
        public void Should_Handle_Router_Envelope_With_Identity_Frames_For_Request_Reply_Routing()
        {
            // Given: A Router-Dealer socket pair
            using var ctx = new Context();
            using var router = new Socket(ctx, SocketType.Router);
            using var dealer = new Socket(ctx, SocketType.Dealer);

            router.SetOption(SocketOption.Linger, 0);
            dealer.SetOption(SocketOption.Linger, 0);

            router.Bind("tcp://127.0.0.1:15653");
            dealer.Connect("tcp://127.0.0.1:15653");

            Thread.Sleep(100);

            // When: Dealer sends message to Router
            // Note: DEALER sockets don't add an empty delimiter - that's REQ socket behavior
            dealer.Send("Request", SendFlags.None);

            // Then: Router receives with automatic identity envelope
            // Router adds: [identity][message] (no empty delimiter from DEALER)
            var identity = router.RecvBytes();
            identity.Should().NotBeEmpty("Router should add identity frame");
            router.HasMore.Should().BeTrue();

            var message = router.RecvString();
            message.Should().Be("Request");
            router.HasMore.Should().BeFalse();

            // When: Router replies with envelope: identity + message
            router.Send(identity, SendFlags.SendMore);
            router.Send("Response", SendFlags.None);

            // Then: Dealer receives message (Router strips identity envelope automatically)
            var reply = dealer.RecvString();
            reply.Should().Be("Response");
            dealer.HasMore.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for multipart message atomicity, ensuring complete messages
    /// are delivered as indivisible units without interleaving.
    /// </summary>
    public class Atomicity
    {
        [Fact(DisplayName = "Should deliver multiple multipart messages atomically without interleaving")]
        public void Should_Deliver_Multiple_Multipart_Messages_Atomically_Without_Interleaving()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15654");
            sender.Connect("tcp://127.0.0.1:15654");

            Thread.Sleep(100);

            // When: Sending two complete multipart messages back-to-back
            // Message 1: A + B
            sender.Send("A", SendFlags.SendMore);
            sender.Send("B", SendFlags.None);

            // Message 2: X + Y + Z
            sender.Send("X", SendFlags.SendMore);
            sender.Send("Y", SendFlags.SendMore);
            sender.Send("Z", SendFlags.None);

            // Then: Message 1 is received completely before Message 2
            var a = receiver.RecvString();
            a.Should().Be("A");
            receiver.HasMore.Should().BeTrue();

            var b = receiver.RecvString();
            b.Should().Be("B");
            receiver.HasMore.Should().BeFalse("Message 1 complete");

            // And: Message 2 is received completely with no interleaving
            var x = receiver.RecvString();
            x.Should().Be("X");
            receiver.HasMore.Should().BeTrue();

            var y = receiver.RecvString();
            y.Should().Be("Y");
            receiver.HasMore.Should().BeTrue();

            var z = receiver.RecvString();
            z.Should().Be("Z");
            receiver.HasMore.Should().BeFalse("Message 2 complete");
        }
    }

    /// <summary>
    /// Tests for different data types in multipart frames, including
    /// binary frames and mixed string/binary combinations.
    /// </summary>
    public class Data_Types
    {
        [Fact(DisplayName = "Should send and receive pure binary frames")]
        public void Should_Send_And_Receive_Pure_Binary_Frames()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15655");
            sender.Connect("tcp://127.0.0.1:15655");

            Thread.Sleep(100);

            // When: Sending binary data frames
            var frame1 = new byte[] { 0x01, 0x02, 0x03 };
            var frame2 = new byte[] { 0xFF, 0xFE, 0xFD };
            var frame3 = new byte[] { 0xAA, 0xBB };

            sender.Send(frame1, SendFlags.SendMore);
            sender.Send(frame2, SendFlags.SendMore);
            sender.Send(frame3, SendFlags.None);

            // Then: Binary frames are received exactly as sent
            var recv1 = receiver.RecvBytes();
            recv1.Should().Equal(frame1);
            receiver.HasMore.Should().BeTrue();

            var recv2 = receiver.RecvBytes();
            recv2.Should().Equal(frame2);
            receiver.HasMore.Should().BeTrue();

            var recv3 = receiver.RecvBytes();
            recv3.Should().Equal(frame3);
            receiver.HasMore.Should().BeFalse();
        }

        [Fact(DisplayName = "Should handle mixed string and binary frames in single message")]
        public void Should_Handle_Mixed_String_And_Binary_Frames_In_Single_Message()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15658");
            sender.Connect("tcp://127.0.0.1:15658");

            Thread.Sleep(100);

            // When: Sending mixed string and binary frames
            sender.Send("StringHeader", SendFlags.SendMore);
            sender.Send(new byte[] { 0x01, 0x02, 0x03 }, SendFlags.SendMore);
            sender.Send("StringBody", SendFlags.SendMore);
            sender.Send(new byte[] { 0xFF, 0xFE }, SendFlags.None);

            // Then: Mixed frames are received correctly
            var header = receiver.RecvString();
            header.Should().Be("StringHeader");
            receiver.HasMore.Should().BeTrue();

            var binary1 = receiver.RecvBytes();
            binary1.Should().Equal(new byte[] { 0x01, 0x02, 0x03 });
            receiver.HasMore.Should().BeTrue();

            var body = receiver.RecvString();
            body.Should().Be("StringBody");
            receiver.HasMore.Should().BeTrue();

            var binary2 = receiver.RecvBytes();
            binary2.Should().Equal(new byte[] { 0xFF, 0xFE });
            receiver.HasMore.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for high-volume multipart messages with large numbers of frames
    /// to verify scalability and correct HasMore tracking.
    /// </summary>
    public class High_Volume_Frames
    {
        [Fact(DisplayName = "Should handle large number of frames in single multipart message")]
        public void Should_Handle_Large_Number_Of_Frames_In_Single_Multipart_Message()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:15657");
            sender.Connect("tcp://127.0.0.1:15657");

            Thread.Sleep(100);

            // When: Sending 100 frames in a single multipart message
            const int frameCount = 100;
            for (int i = 0; i < frameCount; i++)
            {
                var flags = (i < frameCount - 1) ? SendFlags.SendMore : SendFlags.None;
                sender.Send($"Frame{i}", flags);
            }

            // Then: All 100 frames are received in order with correct HasMore status
            for (int i = 0; i < frameCount; i++)
            {
                var frame = receiver.RecvString();
                frame.Should().Be($"Frame{i}");

                if (i < frameCount - 1)
                {
                    receiver.HasMore.Should().BeTrue($"frame {i} should have more");
                }
                else
                {
                    receiver.HasMore.Should().BeFalse("last frame should not have more");
                }
            }
        }
    }

    /// <summary>
    /// Tests for multipart messages with Pub-Sub pattern, verifying that
    /// all frames are delivered together to subscribers.
    /// </summary>
    public class PubSub_Pattern
    {
        [Fact(DisplayName = "Should deliver multipart messages atomically in Pub-Sub pattern")]
        public void Should_Deliver_Multipart_Messages_Atomically_In_PubSub_Pattern()
        {
            // Given: A connected Publisher-Subscriber socket pair
            using var ctx = new Context();
            using var publisher = new Socket(ctx, SocketType.Pub);
            using var subscriber = new Socket(ctx, SocketType.Sub);

            publisher.SetOption(SocketOption.Linger, 0);
            subscriber.SetOption(SocketOption.Linger, 0);

            publisher.Bind("tcp://127.0.0.1:15656");
            subscriber.Connect("tcp://127.0.0.1:15656");
            subscriber.SubscribeAll();

            Thread.Sleep(200); // PUB/SUB needs more time to establish

            // When: Publishing multipart message: topic + data1 + data2
            publisher.Send("TOPIC", SendFlags.SendMore);
            publisher.Send("Data1", SendFlags.SendMore);
            publisher.Send("Data2", SendFlags.None);

            Thread.Sleep(50); // Allow message propagation

            // Then: Subscriber receives all parts atomically
            var topic = subscriber.RecvString();
            topic.Should().Be("TOPIC");
            subscriber.HasMore.Should().BeTrue();

            var data1 = subscriber.RecvString();
            data1.Should().Be("Data1");
            subscriber.HasMore.Should().BeTrue();

            var data2 = subscriber.RecvString();
            data2.Should().Be("Data2");
            subscriber.HasMore.Should().BeFalse();
        }
    }
}
