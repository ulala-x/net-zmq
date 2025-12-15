using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Integration tests for Socket extension methods for multipart messaging.
/// These extension methods provide convenient APIs for sending and receiving
/// complete multipart messages in a single call.
/// </summary>
[Collection("Sequential")]
[Trait("Feature", "SocketExtensions")]
public class Socket_Extensions
{
    /// <summary>
    /// Tests for SendMultipart with MultipartMessage container.
    /// </summary>
    public class SendMultipart_With_MultipartMessage
    {
        [Fact(DisplayName = "Should send MultipartMessage with correct SendMore flags")]
        public void Should_Send_MultipartMessage_With_Correct_SendMore_Flags()
        {
            // Given: A connected Push-Pull socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16001");
            sender.Connect("tcp://127.0.0.1:16001");

            Thread.Sleep(100);

            // When: Sending a MultipartMessage with 3 frames
            using var message = new MultipartMessage();
            message.Add("Frame1");
            message.Add("Frame2");
            message.Add("Frame3");

            sender.SendMultipart(message);

            // Then: All frames are received with correct HasMore status
            var frame1 = receiver.RecvString();
            frame1.Should().Be("Frame1");
            receiver.HasMore.Should().BeTrue();

            var frame2 = receiver.RecvString();
            frame2.Should().Be("Frame2");
            receiver.HasMore.Should().BeTrue();

            var frame3 = receiver.RecvString();
            frame3.Should().Be("Frame3");
            receiver.HasMore.Should().BeFalse();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null socket")]
        public void Should_Throw_ArgumentNullException_For_Null_Socket()
        {
            // Given: A null socket
            Socket? socket = null;
            using var message = new MultipartMessage();
            message.Add("Test");

            // When/Then: Calling SendMultipart should throw
            var act = () => socket!.SendMultipart(message);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null message")]
        public void Should_Throw_ArgumentNullException_For_Null_Message()
        {
            // Given: A socket and null message
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);
            MultipartMessage? message = null;

            // When/Then: Calling SendMultipart should throw
            var act = () => socket.SendMultipart(message!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact(DisplayName = "Should throw InvalidOperationException for empty message")]
        public void Should_Throw_InvalidOperationException_For_Empty_Message()
        {
            // Given: A socket and empty message
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);
            using var message = new MultipartMessage();

            // When/Then: Calling SendMultipart should throw
            var act = () => socket.SendMultipart(message);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot send empty multipart message");
        }

        [Fact(DisplayName = "Should handle single frame message")]
        public void Should_Handle_Single_Frame_Message()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16002");
            sender.Connect("tcp://127.0.0.1:16002");

            Thread.Sleep(100);

            // When: Sending single frame message
            using var message = new MultipartMessage();
            message.Add("OnlyFrame");
            sender.SendMultipart(message);

            // Then: Frame is received without SendMore
            var frame = receiver.RecvString();
            frame.Should().Be("OnlyFrame");
            receiver.HasMore.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for SendMultipart with IEnumerable of byte arrays.
    /// </summary>
    public class SendMultipart_With_ByteArrays
    {
        [Fact(DisplayName = "Should send byte array collection as multipart")]
        public void Should_Send_ByteArray_Collection_As_Multipart()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16003");
            sender.Connect("tcp://127.0.0.1:16003");

            Thread.Sleep(100);

            // When: Sending collection of byte arrays
            var frames = new List<byte[]>
            {
                new byte[] { 0x01, 0x02 },
                new byte[] { 0x03, 0x04, 0x05 },
                new byte[] { 0x06 }
            };

            sender.SendMultipart(frames);

            // Then: All frames are received correctly
            var frame1 = receiver.RecvBytes();
            frame1.Should().Equal(new byte[] { 0x01, 0x02 });
            receiver.HasMore.Should().BeTrue();

            var frame2 = receiver.RecvBytes();
            frame2.Should().Equal(new byte[] { 0x03, 0x04, 0x05 });
            receiver.HasMore.Should().BeTrue();

            var frame3 = receiver.RecvBytes();
            frame3.Should().Equal(new byte[] { 0x06 });
            receiver.HasMore.Should().BeFalse();
        }

        [Fact(DisplayName = "Should throw ArgumentException for empty collection")]
        public void Should_Throw_ArgumentException_For_Empty_Collection()
        {
            // Given: A socket and empty collection
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);
            var frames = new List<byte[]>();

            // When/Then: Should throw
            var act = () => socket.SendMultipart(frames);
            act.Should().Throw<ArgumentException>()
                .WithMessage("Frame collection cannot be empty*");
        }

        [Fact(DisplayName = "Should throw ArgumentException for collection with null values")]
        public void Should_Throw_ArgumentException_For_Collection_With_Null_Values()
        {
            // Given: A socket and collection with null
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);
            var frames = new List<byte[]> { new byte[] { 0x01 }, null!, new byte[] { 0x02 } };

            // When/Then: Should throw
            var act = () => socket.SendMultipart(frames);
            act.Should().Throw<ArgumentException>()
                .WithMessage("Frame collection cannot contain null values*");
        }
    }

    /// <summary>
    /// Tests for SendMultipart with string params array.
    /// </summary>
    public class SendMultipart_With_Strings
    {
        [Fact(DisplayName = "Should send string array as multipart with UTF-8 encoding")]
        public void Should_Send_String_Array_As_Multipart_With_UTF8_Encoding()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16004");
            sender.Connect("tcp://127.0.0.1:16004");

            Thread.Sleep(100);

            // When: Sending string params
            sender.SendMultipart("Hello", "World", "Test");

            // Then: All strings are received correctly
            var str1 = receiver.RecvString();
            str1.Should().Be("Hello");
            receiver.HasMore.Should().BeTrue();

            var str2 = receiver.RecvString();
            str2.Should().Be("World");
            receiver.HasMore.Should().BeTrue();

            var str3 = receiver.RecvString();
            str3.Should().Be("Test");
            receiver.HasMore.Should().BeFalse();
        }

        [Fact(DisplayName = "Should handle Unicode strings correctly")]
        public void Should_Handle_Unicode_Strings_Correctly()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16005");
            sender.Connect("tcp://127.0.0.1:16005");

            Thread.Sleep(100);

            // When: Sending Unicode strings
            sender.SendMultipart("안녕하세요", "Hello", "你好");

            // Then: Unicode is preserved
            var korean = receiver.RecvString();
            korean.Should().Be("안녕하세요");
            receiver.HasMore.Should().BeTrue();

            var english = receiver.RecvString();
            english.Should().Be("Hello");
            receiver.HasMore.Should().BeTrue();

            var chinese = receiver.RecvString();
            chinese.Should().Be("你好");
            receiver.HasMore.Should().BeFalse();
        }

        [Fact(DisplayName = "Should throw ArgumentException for empty string array")]
        public void Should_Throw_ArgumentException_For_Empty_String_Array()
        {
            // Given: A socket and empty string array
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);

            // When/Then: Should throw
            var act = () => socket.SendMultipart();
            act.Should().Throw<ArgumentException>()
                .WithMessage("Frame array cannot be empty*");
        }

        [Fact(DisplayName = "Should throw ArgumentException for string array with null values")]
        public void Should_Throw_ArgumentException_For_String_Array_With_Null_Values()
        {
            // Given: A socket and string array with null
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);

            // When/Then: Should throw
            var act = () => socket.SendMultipart("Hello", null!, "World");
            act.Should().Throw<ArgumentException>()
                .WithMessage("Frame array cannot contain null values*");
        }
    }

    /// <summary>
    /// Tests for SendMultipart with IEnumerable of Message objects.
    /// </summary>
    public class SendMultipart_With_Messages
    {
        [Fact(DisplayName = "Should send Message collection as multipart")]
        public void Should_Send_Message_Collection_As_Multipart()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16006");
            sender.Connect("tcp://127.0.0.1:16006");

            Thread.Sleep(100);

            // When: Sending collection of Message objects
            var msg1 = new Message("Message1");
            var msg2 = new Message("Message2");
            var msg3 = new Message("Message3");

            var messages = new List<Message> { msg1, msg2, msg3 };
            sender.SendMultipart(messages);

            // Clean up messages
            msg1.Dispose();
            msg2.Dispose();
            msg3.Dispose();

            // Then: All messages are received correctly
            var recv1 = receiver.RecvString();
            recv1.Should().Be("Message1");
            receiver.HasMore.Should().BeTrue();

            var recv2 = receiver.RecvString();
            recv2.Should().Be("Message2");
            receiver.HasMore.Should().BeTrue();

            var recv3 = receiver.RecvString();
            recv3.Should().Be("Message3");
            receiver.HasMore.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for RecvMultipart blocking receive.
    /// </summary>
    public class RecvMultipart_Blocking
    {
        [Fact(DisplayName = "Should receive complete multipart message")]
        public void Should_Receive_Complete_Multipart_Message()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16007");
            sender.Connect("tcp://127.0.0.1:16007");

            Thread.Sleep(100);

            // When: Sending multipart and receiving with RecvMultipart
            sender.Send("Part1", SendFlags.SendMore);
            sender.Send("Part2", SendFlags.SendMore);
            sender.Send("Part3", SendFlags.None);

            using var received = receiver.RecvMultipart();

            // Then: All parts are in the MultipartMessage
            received.Count.Should().Be(3);
            received[0].ToString().Should().Be("Part1");
            received[1].ToString().Should().Be("Part2");
            received[2].ToString().Should().Be("Part3");
        }

        [Fact(DisplayName = "Should handle single frame multipart message")]
        public void Should_Handle_Single_Frame_Multipart_Message()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16008");
            sender.Connect("tcp://127.0.0.1:16008");

            Thread.Sleep(100);

            // When: Sending single frame
            sender.Send("OnlyFrame", SendFlags.None);

            using var received = receiver.RecvMultipart();

            // Then: Single frame is in the MultipartMessage
            received.Count.Should().Be(1);
            received[0].ToString().Should().Be("OnlyFrame");
        }

        [Fact(DisplayName = "Should handle binary frames")]
        public void Should_Handle_Binary_Frames()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16009");
            sender.Connect("tcp://127.0.0.1:16009");

            Thread.Sleep(100);

            // When: Sending binary frames
            sender.Send(new byte[] { 0x01, 0x02 }, SendFlags.SendMore);
            sender.Send(new byte[] { 0x03, 0x04, 0x05 }, SendFlags.None);

            using var received = receiver.RecvMultipart();

            // Then: Binary data is preserved
            received.Count.Should().Be(2);
            received[0].ToArray().Should().Equal(new byte[] { 0x01, 0x02 });
            received[1].ToArray().Should().Equal(new byte[] { 0x03, 0x04, 0x05 });
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null socket")]
        public void Should_Throw_ArgumentNullException_For_Null_Socket()
        {
            // Given: A null socket
            Socket? socket = null;

            // When/Then: Should throw
            var act = () => socket!.RecvMultipart();
            act.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// Tests for TryRecvMultipart non-blocking receive.
    /// </summary>
    public class TryRecvMultipart_NonBlocking
    {
        [Fact(DisplayName = "Should return false when no message available")]
        public void Should_Return_False_When_No_Message_Available()
        {
            // Given: A socket with no incoming messages
            using var ctx = new Context();
            using var receiver = new Socket(ctx, SocketType.Pull);

            receiver.SetOption(SocketOption.Linger, 0);
            receiver.Bind("tcp://127.0.0.1:16010");

            // When: Trying to receive without blocking
            var result = receiver.TryRecvMultipart(out var message);

            // Then: Should return false with null message
            result.Should().BeFalse();
            message.Should().BeNull();
        }

        [Fact(DisplayName = "Should return true and receive complete message when available")]
        public void Should_Return_True_And_Receive_Complete_Message_When_Available()
        {
            // Given: A connected socket pair with a message
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16011");
            sender.Connect("tcp://127.0.0.1:16011");

            Thread.Sleep(100);

            // When: Sending message and trying to receive
            sender.Send("Frame1", SendFlags.SendMore);
            sender.Send("Frame2", SendFlags.SendMore);
            sender.Send("Frame3", SendFlags.None);

            Thread.Sleep(50); // Allow message to arrive

            var result = receiver.TryRecvMultipart(out var message);

            // Then: Should return true with complete message
            result.Should().BeTrue();
            message.Should().NotBeNull();
            message!.Count.Should().Be(3);
            message[0].ToString().Should().Be("Frame1");
            message[1].ToString().Should().Be("Frame2");
            message[2].ToString().Should().Be("Frame3");

            message.Dispose();
        }

        [Fact(DisplayName = "Should handle single frame message")]
        public void Should_Handle_Single_Frame_Message()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16012");
            sender.Connect("tcp://127.0.0.1:16012");

            Thread.Sleep(100);

            // When: Sending single frame
            sender.Send("SingleFrame", SendFlags.None);
            Thread.Sleep(50);

            var result = receiver.TryRecvMultipart(out var message);

            // Then: Should receive single frame
            result.Should().BeTrue();
            message.Should().NotBeNull();
            message!.Count.Should().Be(1);
            message[0].ToString().Should().Be("SingleFrame");

            message.Dispose();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null socket")]
        public void Should_Throw_ArgumentNullException_For_Null_Socket()
        {
            // Given: A null socket
            Socket? socket = null;

            // When/Then: Should throw
            var act = () => socket!.TryRecvMultipart(out var message);
            act.Should().Throw<ArgumentNullException>();
        }
    }

    /// <summary>
    /// Tests for round-trip send and receive using extension methods.
    /// </summary>
    public class RoundTrip_Integration
    {
        [Fact(DisplayName = "Should send and receive using extension methods end-to-end")]
        public void Should_Send_And_Receive_Using_Extension_Methods_EndToEnd()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16013");
            sender.Connect("tcp://127.0.0.1:16013");

            Thread.Sleep(100);

            // When: Sending with SendMultipart and receiving with RecvMultipart
            sender.SendMultipart("Header", "Body", "Footer");

            using var received = receiver.RecvMultipart();

            // Then: Message is received completely
            received.Count.Should().Be(3);
            received[0].ToString().Should().Be("Header");
            received[1].ToString().Should().Be("Body");
            received[2].ToString().Should().Be("Footer");
        }

        [Fact(DisplayName = "Should send MultipartMessage and receive using RecvMultipart")]
        public void Should_Send_MultipartMessage_And_Receive_Using_RecvMultipart()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:16014");
            sender.Connect("tcp://127.0.0.1:16014");

            Thread.Sleep(100);

            // When: Creating and sending MultipartMessage
            using var sendMsg = new MultipartMessage();
            sendMsg.Add("Part1");
            sendMsg.Add(new byte[] { 0xFF, 0xFE });
            sendMsg.Add("Part3");

            sender.SendMultipart(sendMsg);

            using var received = receiver.RecvMultipart();

            // Then: Message content matches
            received.Count.Should().Be(3);
            received[0].ToString().Should().Be("Part1");
            received[1].ToArray().Should().Equal(new byte[] { 0xFF, 0xFE });
            received[2].ToString().Should().Be("Part3");
        }
    }
}
