using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Integration tests for async socket extension methods.
/// These methods provide Task-based async/await support for ZeroMQ sockets.
/// </summary>
[Collection("Sequential")]
[Trait("Feature", "Async")]
public class Async_Socket
{
    /// <summary>
    /// Tests basic async send and receive operations.
    /// </summary>
    public class Async_Send_Receive
    {
        [Fact(DisplayName = "Should send and receive bytes asynchronously")]
        public async Task Should_Send_Receive_Bytes_Async()
        {
            // Given: A connected REQ-REP socket pair
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            server.Bind("tcp://127.0.0.1:17001");
            client.Connect("tcp://127.0.0.1:17001");

            Thread.Sleep(100); // Allow connection to establish

            // When: Client sends bytes asynchronously
            var requestData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var bytesSent = await client.SendAsync(requestData);

            // Then: Server receives the bytes asynchronously
            var receivedData = await server.RecvBytesAsync();
            receivedData.Should().Equal(requestData);
            bytesSent.Should().Be(requestData.Length);

            // When: Server sends response asynchronously
            var responseData = new byte[] { 0x0A, 0x0B, 0x0C };
            await server.SendAsync(responseData);

            // Then: Client receives the response asynchronously
            var receivedResponse = await client.RecvBytesAsync();
            receivedResponse.Should().Equal(responseData);
        }

        [Fact(DisplayName = "Should send and receive string asynchronously")]
        public async Task Should_Send_Receive_String_Async()
        {
            // Given: A connected REQ-REP socket pair
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            server.Bind("tcp://127.0.0.1:17002");
            client.Connect("tcp://127.0.0.1:17002");

            Thread.Sleep(100);

            // When: Client sends string asynchronously
            var request = "Hello, Async World!";
            await client.SendAsync(request);

            // Then: Server receives the string asynchronously
            var receivedRequest = await server.RecvStringAsync();
            receivedRequest.Should().Be(request);

            // When: Server sends response asynchronously
            var response = "Async response received";
            await server.SendAsync(response);

            // Then: Client receives the response asynchronously
            var receivedResponse = await client.RecvStringAsync();
            receivedResponse.Should().Be(response);
        }

        [Fact(DisplayName = "Should send ReadOnlyMemory<byte> asynchronously")]
        public async Task Should_Send_ReadOnlyMemory_Async()
        {
            // Given: A connected PUSH-PULL socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17003");
            sender.Connect("tcp://127.0.0.1:17003");

            Thread.Sleep(100);

            // When: Sending ReadOnlyMemory<byte> asynchronously
            var data = new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB };
            var memory = new ReadOnlyMemory<byte>(data);
            var bytesSent = await sender.SendAsync(memory);

            // Then: Data is received correctly
            var received = await receiver.RecvBytesAsync();
            received.Should().Equal(data);
            bytesSent.Should().Be(data.Length);
        }

        [Fact(DisplayName = "Should handle Unicode strings asynchronously")]
        public async Task Should_Handle_Unicode_Strings_Async()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17004");
            sender.Connect("tcp://127.0.0.1:17004");

            Thread.Sleep(100);

            // When: Sending Unicode strings asynchronously
            var koreanText = "안녕하세요 비동기";
            var chineseText = "你好 异步";
            var emojiText = "Hello 🚀 Async";

            await sender.SendAsync(koreanText);
            var received1 = await receiver.RecvStringAsync();
            received1.Should().Be(koreanText);

            await sender.SendAsync(chineseText);
            var received2 = await receiver.RecvStringAsync();
            received2.Should().Be(chineseText);

            await sender.SendAsync(emojiText);
            var received3 = await receiver.RecvStringAsync();
            received3.Should().Be(emojiText);
        }

        [Fact(DisplayName = "Should use fast path when socket is ready")]
        public async Task Should_Use_Fast_Path_When_Socket_Ready()
        {
            // Given: A connected socket pair with high water mark
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);
            sender.SetOption(SocketOption.Sndhwm, 1000);

            receiver.Bind("tcp://127.0.0.1:17005");
            sender.Connect("tcp://127.0.0.1:17005");

            Thread.Sleep(100);

            // When: Sending multiple messages rapidly (should use fast path)
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var msg = $"Message {i}";
                tasks.Add(sender.SendAsync(msg).AsTask());
            }

            await Task.WhenAll(tasks);

            // Then: All messages are received
            for (int i = 0; i < 10; i++)
            {
                var received = await receiver.RecvStringAsync();
                received.Should().Be($"Message {i}");
            }
        }
    }

    /// <summary>
    /// Tests async multipart message operations.
    /// </summary>
    public class Async_Multipart
    {
        [Fact(DisplayName = "Should send and receive multipart message asynchronously")]
        public async Task Should_Send_Receive_Multipart_Async()
        {
            // Given: A connected PUSH-PULL socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17006");
            sender.Connect("tcp://127.0.0.1:17006");

            Thread.Sleep(100);

            // When: Sending multipart message asynchronously
            using var message = new MultipartMessage();
            message.Add("Header");
            message.Add("Body content");
            message.Add("Footer");

            await sender.SendMultipartAsync(message);

            // Then: Complete multipart message is received asynchronously
            using var received = await receiver.RecvMultipartAsync();
            received.Count.Should().Be(3);
            received[0].ToString().Should().Be("Header");
            received[1].ToString().Should().Be("Body content");
            received[2].ToString().Should().Be("Footer");
        }

        [Fact(DisplayName = "Should handle binary frames in multipart messages")]
        public async Task Should_Handle_Binary_Frames_In_Multipart()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17007");
            sender.Connect("tcp://127.0.0.1:17007");

            Thread.Sleep(100);

            // When: Sending multipart with binary data
            using var message = new MultipartMessage();
            message.Add("Text frame");
            message.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            message.Add(new byte[] { 0xFF, 0xFE, 0xFD });

            await sender.SendMultipartAsync(message);

            // Then: Binary frames are preserved
            using var received = await receiver.RecvMultipartAsync();
            received.Count.Should().Be(3);
            received[0].ToString().Should().Be("Text frame");
            received[1].ToArray().Should().Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            received[2].ToArray().Should().Equal(new byte[] { 0xFF, 0xFE, 0xFD });
        }

        [Fact(DisplayName = "Should handle single frame multipart message")]
        public async Task Should_Handle_Single_Frame_Multipart()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17008");
            sender.Connect("tcp://127.0.0.1:17008");

            Thread.Sleep(100);

            // When: Sending single frame multipart message
            using var message = new MultipartMessage();
            message.Add("Only frame");

            await sender.SendMultipartAsync(message);

            // Then: Single frame is received
            using var received = await receiver.RecvMultipartAsync();
            received.Count.Should().Be(1);
            received[0].ToString().Should().Be("Only frame");
        }

        [Fact(DisplayName = "Should throw InvalidOperationException for empty multipart message")]
        public async Task Should_Throw_For_Empty_Multipart_Message()
        {
            // Given: A socket and empty multipart message
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);
            using var message = new MultipartMessage();

            socket.SetOption(SocketOption.Linger, 0);

            // When/Then: Sending empty message should throw
            var act = async () => await socket.SendMultipartAsync(message);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cannot send empty multipart message");
        }

        [Fact(DisplayName = "Should send multiple multipart messages sequentially")]
        public async Task Should_Send_Multiple_Multipart_Messages_Sequentially()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17009");
            sender.Connect("tcp://127.0.0.1:17009");

            Thread.Sleep(100);

            // When: Sending multiple multipart messages
            for (int i = 0; i < 5; i++)
            {
                using var message = new MultipartMessage();
                message.Add($"Message {i}");
                message.Add($"Frame 1 of {i}");
                message.Add($"Frame 2 of {i}");

                await sender.SendMultipartAsync(message);
            }

            // Then: All messages are received in order
            for (int i = 0; i < 5; i++)
            {
                using var received = await receiver.RecvMultipartAsync();
                received.Count.Should().Be(3);
                received[0].ToString().Should().Be($"Message {i}");
                received[1].ToString().Should().Be($"Frame 1 of {i}");
                received[2].ToString().Should().Be($"Frame 2 of {i}");
            }
        }
    }

    /// <summary>
    /// Tests async operations with PUB-SUB pattern.
    /// </summary>
    public class Async_PubSub
    {
        [Fact(DisplayName = "Should publish and subscribe asynchronously")]
        public async Task Should_Publish_Subscribe_Async()
        {
            // Given: A connected PUB-SUB socket pair
            using var ctx = new Context();
            using var publisher = new Socket(ctx, SocketType.Pub);
            using var subscriber = new Socket(ctx, SocketType.Sub);

            publisher.SetOption(SocketOption.Linger, 0);
            subscriber.SetOption(SocketOption.Linger, 0);

            publisher.Bind("tcp://127.0.0.1:17010");
            subscriber.Connect("tcp://127.0.0.1:17010");
            subscriber.Subscribe(""); // Subscribe to all messages

            Thread.Sleep(200); // Allow subscription to establish

            // When: Publishing messages asynchronously
            await publisher.SendAsync("Topic1: Message 1");
            await publisher.SendAsync("Topic2: Message 2");
            await publisher.SendAsync("Topic1: Message 3");

            // Then: Subscriber receives all messages asynchronously
            var msg1 = await subscriber.RecvStringAsync();
            msg1.Should().Be("Topic1: Message 1");

            var msg2 = await subscriber.RecvStringAsync();
            msg2.Should().Be("Topic2: Message 2");

            var msg3 = await subscriber.RecvStringAsync();
            msg3.Should().Be("Topic1: Message 3");
        }

        [Fact(DisplayName = "Should publish multipart messages asynchronously")]
        public async Task Should_Publish_Multipart_Messages_Async()
        {
            // Given: A connected PUB-SUB socket pair
            using var ctx = new Context();
            using var publisher = new Socket(ctx, SocketType.Pub);
            using var subscriber = new Socket(ctx, SocketType.Sub);

            publisher.SetOption(SocketOption.Linger, 0);
            subscriber.SetOption(SocketOption.Linger, 0);

            publisher.Bind("tcp://127.0.0.1:17011");
            subscriber.Connect("tcp://127.0.0.1:17011");
            subscriber.Subscribe("TOPIC");

            Thread.Sleep(200);

            // When: Publishing multipart message asynchronously
            using var message = new MultipartMessage();
            message.Add("TOPIC");
            message.Add("Header data");
            message.Add("Payload data");

            await publisher.SendMultipartAsync(message);

            // Then: Subscriber receives complete multipart message
            using var received = await subscriber.RecvMultipartAsync();
            received.Count.Should().Be(3);
            received[0].ToString().Should().Be("TOPIC");
            received[1].ToString().Should().Be("Header data");
            received[2].ToString().Should().Be("Payload data");
        }
    }

    /// <summary>
    /// Tests cancellation token support in async operations.
    /// </summary>
    public class Cancellation_Support
    {
        [Fact(DisplayName = "Should cancel receive operation when token is cancelled")]
        public async Task Should_Cancel_Receive_When_Token_Cancelled()
        {
            // Given: A socket with no incoming messages and a cancellation token
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Pull);

            socket.SetOption(SocketOption.Linger, 0);
            socket.Bind("tcp://127.0.0.1:17012");

            using var cts = new CancellationTokenSource();

            // When: Starting an async receive and then canceling it
            var receiveTask = socket.RecvStringAsync(cts.Token);

            // Cancel after a short delay
            await Task.Delay(50);
            cts.Cancel();

            // Then: OperationCanceledException should be thrown
            var act = async () => await receiveTask;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact(DisplayName = "Should cancel send operation when token is cancelled")]
        public async Task Should_Cancel_Send_When_Token_Cancelled()
        {
            // Given: A socket that may block on send and a cancellation token
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);

            socket.SetOption(SocketOption.Linger, 0);
            socket.SetOption(SocketOption.Sndhwm, 1);
            socket.SetOption(SocketOption.Sndtimeo, 0);

            // Don't connect - this will cause sends to potentially block

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            // When: Attempting to send with cancellation token
            var act = async () => await socket.SendAsync("Test message", cts.Token);

            // Then: OperationCanceledException should be thrown
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact(DisplayName = "Should cancel multipart receive when token is cancelled")]
        public async Task Should_Cancel_Multipart_Receive_When_Token_Cancelled()
        {
            // Given: A socket with no incoming messages
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Pull);

            socket.SetOption(SocketOption.Linger, 0);
            socket.Bind("tcp://127.0.0.1:17013");

            using var cts = new CancellationTokenSource();

            // When: Starting multipart receive and canceling
            var receiveTask = socket.RecvMultipartAsync(cts.Token);

            await Task.Delay(50);
            cts.Cancel();

            // Then: OperationCanceledException should be thrown
            var act = async () => await receiveTask;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact(DisplayName = "Should cancel multipart send when token is cancelled")]
        public async Task Should_Cancel_Multipart_Send_When_Token_Cancelled()
        {
            // Given: A socket and a multipart message
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);

            socket.SetOption(SocketOption.Linger, 0);
            socket.SetOption(SocketOption.Sndhwm, 1);

            using var message = new MultipartMessage();
            message.Add("Frame 1");
            message.Add("Frame 2");

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            // When: Attempting to send multipart with cancellation
            var act = async () => await socket.SendMultipartAsync(message, cts.Token);

            // Then: OperationCanceledException should be thrown
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact(DisplayName = "Should complete successfully if not cancelled")]
        public async Task Should_Complete_Successfully_If_Not_Cancelled()
        {
            // Given: A connected socket pair with cancellation token
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17014");
            sender.Connect("tcp://127.0.0.1:17014");

            Thread.Sleep(100);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(5000); // Long timeout

            // When: Sending and receiving with cancellation token
            await sender.SendAsync("Test message", cts.Token);
            var received = await receiver.RecvStringAsync(cts.Token);

            // Then: Operation completes successfully
            received.Should().Be("Test message");
            cts.IsCancellationRequested.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests performance characteristics of async operations.
    /// </summary>
    public class Performance
    {
        [Fact(DisplayName = "Should handle rapid sequential send/receive operations")]
        public async Task Should_Handle_Rapid_Sequential_Operations()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);
            sender.SetOption(SocketOption.Sndhwm, 1000);
            receiver.SetOption(SocketOption.Rcvhwm, 1000);

            receiver.Bind("tcp://127.0.0.1:17015");
            sender.Connect("tcp://127.0.0.1:17015");

            Thread.Sleep(100);

            // When: Performing many rapid send operations
            const int messageCount = 100;
            var sendTasks = new List<Task>();

            for (int i = 0; i < messageCount; i++)
            {
                sendTasks.Add(sender.SendAsync($"Message {i}").AsTask());
            }

            await Task.WhenAll(sendTasks);

            // Then: All messages are received correctly
            for (int i = 0; i < messageCount; i++)
            {
                var received = await receiver.RecvStringAsync();
                received.Should().Be($"Message {i}");
            }
        }

        [Fact(DisplayName = "Should handle concurrent send operations on different sockets")]
        public async Task Should_Handle_Concurrent_Send_Operations()
        {
            // Given: Multiple socket pairs
            using var ctx = new Context();

            var sockets = new List<(Socket sender, Socket receiver)>();
            for (int i = 0; i < 5; i++)
            {
                var sender = new Socket(ctx, SocketType.Push);
                var receiver = new Socket(ctx, SocketType.Pull);

                sender.SetOption(SocketOption.Linger, 0);
                receiver.SetOption(SocketOption.Linger, 0);

                var port = 17020 + i;
                receiver.Bind($"tcp://127.0.0.1:{port}");
                sender.Connect($"tcp://127.0.0.1:{port}");

                sockets.Add((sender, receiver));
            }

            Thread.Sleep(100);

            try
            {
                // When: Sending concurrently on all socket pairs
                var tasks = sockets.Select(async (pair, index) =>
                {
                    await pair.sender.SendAsync($"Message from socket {index}");
                    var received = await pair.receiver.RecvStringAsync();
                    return (index, received);
                }).ToList();

                var results = await Task.WhenAll(tasks);

                // Then: All messages are received correctly
                foreach (var (index, received) in results)
                {
                    received.Should().Be($"Message from socket {index}");
                }
            }
            finally
            {
                // Cleanup
                foreach (var (sender, receiver) in sockets)
                {
                    sender.Dispose();
                    receiver.Dispose();
                }
            }
        }

        [Fact(DisplayName = "Should efficiently use fast path for ready sockets")]
        public async Task Should_Efficiently_Use_Fast_Path()
        {
            // Given: A connected socket pair with high water marks
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);
            sender.SetOption(SocketOption.Sndhwm, 10000);
            receiver.SetOption(SocketOption.Rcvhwm, 10000);

            receiver.Bind("tcp://127.0.0.1:17016");
            sender.Connect("tcp://127.0.0.1:17016");

            Thread.Sleep(100);

            // When: Sending many messages (should mostly use fast path)
            var startTime = DateTime.UtcNow;

            for (int i = 0; i < 50; i++)
            {
                await sender.SendAsync($"Fast message {i}");
            }

            var sendDuration = DateTime.UtcNow - startTime;

            // Then: Messages are sent efficiently
            // (Fast path should make this very quick)
            sendDuration.Should().BeLessThan(TimeSpan.FromSeconds(2));

            // Verify all messages received
            for (int i = 0; i < 50; i++)
            {
                var received = await receiver.RecvStringAsync();
                received.Should().Be($"Fast message {i}");
            }
        }
    }

    /// <summary>
    /// Tests argument validation for async methods.
    /// </summary>
    public class Argument_Validation
    {
        [Fact(DisplayName = "Should throw ArgumentNullException for null socket in SendAsync")]
        public async Task Should_Throw_For_Null_Socket_In_SendAsync()
        {
            // Given: A null socket
            Socket? socket = null;

            // When/Then: Should throw ArgumentNullException
            var act1 = async () => await socket!.SendAsync(new byte[] { 0x01 });
            await act1.Should().ThrowAsync<ArgumentNullException>();

            var act2 = async () => await socket!.SendAsync("test");
            await act2.Should().ThrowAsync<ArgumentNullException>();

            var act3 = async () => await socket!.SendAsync(ReadOnlyMemory<byte>.Empty);
            await act3.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null data in SendAsync")]
        public async Task Should_Throw_For_Null_Data_In_SendAsync()
        {
            // Given: A valid socket but null data
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);

            socket.SetOption(SocketOption.Linger, 0);

            // When/Then: Should throw ArgumentNullException
            var act1 = async () => await socket.SendAsync((byte[])null!);
            await act1.Should().ThrowAsync<ArgumentNullException>();

            var act2 = async () => await socket.SendAsync((string)null!);
            await act2.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null socket in RecvAsync")]
        public async Task Should_Throw_For_Null_Socket_In_RecvAsync()
        {
            // Given: A null socket
            Socket? socket = null;

            // When/Then: Should throw ArgumentNullException
            var act1 = async () => await socket!.RecvBytesAsync();
            await act1.Should().ThrowAsync<ArgumentNullException>();

            var act2 = async () => await socket!.RecvStringAsync();
            await act2.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null socket in multipart async")]
        public async Task Should_Throw_For_Null_Socket_In_Multipart_Async()
        {
            // Given: A null socket
            Socket? socket = null;
            using var message = new MultipartMessage();
            message.Add("test");

            // When/Then: Should throw ArgumentNullException
            var act1 = async () => await socket!.SendMultipartAsync(message);
            await act1.Should().ThrowAsync<ArgumentNullException>();

            var act2 = async () => await socket!.RecvMultipartAsync();
            await act2.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact(DisplayName = "Should throw ArgumentNullException for null message in SendMultipartAsync")]
        public async Task Should_Throw_For_Null_Message_In_SendMultipartAsync()
        {
            // Given: A valid socket but null message
            using var ctx = new Context();
            using var socket = new Socket(ctx, SocketType.Push);

            socket.SetOption(SocketOption.Linger, 0);

            // When/Then: Should throw ArgumentNullException
            var act = async () => await socket.SendMultipartAsync(null!);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }
    }

    /// <summary>
    /// Tests edge cases and error conditions.
    /// </summary>
    public class Edge_Cases
    {
        [Fact(DisplayName = "Should handle empty byte array")]
        public async Task Should_Handle_Empty_Byte_Array()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17017");
            sender.Connect("tcp://127.0.0.1:17017");

            Thread.Sleep(100);

            // When: Sending empty byte array
            var emptyData = Array.Empty<byte>();
            await sender.SendAsync(emptyData);

            // Then: Empty array is received
            var received = await receiver.RecvBytesAsync();
            received.Should().BeEmpty();
        }

        [Fact(DisplayName = "Should handle empty string")]
        public async Task Should_Handle_Empty_String()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17018");
            sender.Connect("tcp://127.0.0.1:17018");

            Thread.Sleep(100);

            // When: Sending empty string
            await sender.SendAsync(string.Empty);

            // Then: Empty string is received
            var received = await receiver.RecvStringAsync();
            received.Should().BeEmpty();
        }

        [Fact(DisplayName = "Should handle large messages asynchronously")]
        public async Task Should_Handle_Large_Messages_Async()
        {
            // Given: A connected socket pair
            using var ctx = new Context();
            using var sender = new Socket(ctx, SocketType.Push);
            using var receiver = new Socket(ctx, SocketType.Pull);

            sender.SetOption(SocketOption.Linger, 0);
            receiver.SetOption(SocketOption.Linger, 0);

            receiver.Bind("tcp://127.0.0.1:17019");
            sender.Connect("tcp://127.0.0.1:17019");

            Thread.Sleep(100);

            // When: Sending a large message (1 MB)
            var largeData = new byte[1024 * 1024];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            await sender.SendAsync(largeData);

            // Then: Large message is received correctly
            var received = await receiver.RecvBytesAsync();
            received.Length.Should().Be(largeData.Length);
            received.Should().Equal(largeData);
        }
    }
}
