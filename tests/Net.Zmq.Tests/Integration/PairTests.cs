using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// PAIR socket pattern tests.
/// PAIR sockets provide 1:1 bidirectional communication between two peers.
/// Each socket can only connect to one peer, and communication is strictly bidirectional.
/// </summary>
[Collection("Sequential")]
[Trait("Socket", "Pair")]
public class Pair_Socket
{
    /// <summary>
    /// Tests for bidirectional communication between PAIR sockets.
    /// </summary>
    public class Bidirectional_Communication
    {
        [Fact(DisplayName = "Should allow bidirectional message exchange between paired sockets")]
        public void Should_Allow_Bidirectional_Message_Exchange()
        {
            // Given: Two PAIR sockets connected via TCP
            using var ctx = new Context();
            using var socket1 = new Socket(ctx, SocketType.Pair);
            using var socket2 = new Socket(ctx, SocketType.Pair);

            socket1.SetOption(SocketOption.Linger, 0);
            socket2.SetOption(SocketOption.Linger, 0);

            socket1.Bind("tcp://127.0.0.1:15600");
            socket2.Connect("tcp://127.0.0.1:15600");

            Thread.Sleep(100); // Allow connection to establish

            // When: Socket1 sends to Socket2
            socket1.Send("Message from socket1");
            var received1 = socket2.RecvString();

            // Then: Message should be received correctly
            received1.Should().Be("Message from socket1");

            // When: Socket2 sends to Socket1 (bidirectional)
            socket2.Send("Message from socket2");
            var received2 = socket1.RecvString();

            // Then: Message should be received correctly
            received2.Should().Be("Message from socket2");

            // When: Another round of communication is attempted
            socket1.Send("Second message");
            var received3 = socket2.RecvString();

            // Then: Continued communication should work
            received3.Should().Be("Second message");
        }
    }

    /// <summary>
    /// Tests for PAIR sockets using inproc transport.
    /// </summary>
    public class Inproc_Transport
    {
        [Fact(DisplayName = "Should support bidirectional communication over inproc transport")]
        public void Should_Support_Bidirectional_Communication_Over_Inproc()
        {
            // Given: Two PAIR sockets connected via inproc transport
            using var ctx = new Context();
            using var socket1 = new Socket(ctx, SocketType.Pair);
            using var socket2 = new Socket(ctx, SocketType.Pair);

            socket1.SetOption(SocketOption.Linger, 0);
            socket2.SetOption(SocketOption.Linger, 0);

            socket1.Bind("inproc://pair-test");
            socket2.Connect("inproc://pair-test");

            Thread.Sleep(50); // Shorter delay for inproc

            // When: Socket1 sends a message over inproc
            socket1.Send("Inproc message 1");
            var received1 = socket2.RecvString();

            // Then: Message should be received correctly
            received1.Should().Be("Inproc message 1");

            // When: Socket2 sends a message back over inproc
            socket2.Send("Inproc message 2");
            var received2 = socket1.RecvString();

            // Then: Message should be received correctly
            received2.Should().Be("Inproc message 2");
        }
    }

    /// <summary>
    /// Tests for PAIR sockets communicating across threads.
    /// </summary>
    public class Thread_Communication
    {
        [Fact(DisplayName = "Should support communication between separate threads")]
        public void Should_Support_Communication_Between_Separate_Threads()
        {
            // Given: Two PAIR sockets and synchronization primitives
            using var ctx = new Context();
            using var socket1 = new Socket(ctx, SocketType.Pair);
            using var socket2 = new Socket(ctx, SocketType.Pair);

            socket1.SetOption(SocketOption.Linger, 0);
            socket2.SetOption(SocketOption.Linger, 0);

            socket1.Bind("tcp://127.0.0.1:15601");
            socket2.Connect("tcp://127.0.0.1:15601");

            Thread.Sleep(100);

            var receivedMessages = new List<string>();
            var resetEvent = new ManualResetEventSlim(false);

            // Given: A receive thread listening on socket2
            var receiveThread = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var msg = socket2.RecvString();
                        lock (receivedMessages)
                        {
                            receivedMessages.Add(msg);
                        }
                    }
                    resetEvent.Set();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receive thread error: {ex.Message}");
                    resetEvent.Set();
                }
            });

            receiveThread.Start();

            // When: A send thread sends messages from socket1
            var sendThread = new Thread(() =>
            {
                try
                {
                    Thread.Sleep(50); // Small delay to ensure receiver is ready
                    socket1.Send("Thread message 1");
                    socket1.Send("Thread message 2");
                    socket1.Send("Thread message 3");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Send thread error: {ex.Message}");
                }
            });

            sendThread.Start();

            // Then: All messages should be received within timeout
            var completed = resetEvent.Wait(TimeSpan.FromSeconds(5));
            completed.Should().BeTrue("Receive thread should complete within timeout");

            sendThread.Join(1000);
            receiveThread.Join(1000);

            // Then: All messages should be received correctly
            receivedMessages.Should().HaveCount(3);
            receivedMessages.Should().Contain("Thread message 1");
            receivedMessages.Should().Contain("Thread message 2");
            receivedMessages.Should().Contain("Thread message 3");
        }
    }

    /// <summary>
    /// Tests for different message types supported by PAIR sockets.
    /// </summary>
    public class Message_Types
    {
        [Fact(DisplayName = "Should support Message object for send and receive operations")]
        public void Should_Support_Message_Object_Operations()
        {
            // Given: Two PAIR sockets connected via TCP
            using var ctx = new Context();
            using var socket1 = new Socket(ctx, SocketType.Pair);
            using var socket2 = new Socket(ctx, SocketType.Pair);

            socket1.SetOption(SocketOption.Linger, 0);
            socket2.SetOption(SocketOption.Linger, 0);

            socket1.Bind("tcp://127.0.0.1:15602");
            socket2.Connect("tcp://127.0.0.1:15602");

            Thread.Sleep(100);

            // When: Sending with Message object
            var outgoingMsg = new Message("Pair message");
            socket1.Send(outgoingMsg, SendFlags.None);
            outgoingMsg.Dispose();

            // Then: Receiving with Message object should work correctly
            var incomingMsg = new Message();
            socket2.Recv(incomingMsg, RecvFlags.None);
            incomingMsg.ToString().Should().Be("Pair message");
            incomingMsg.Dispose();
        }

        [Fact(DisplayName = "Should support byte array for send and receive operations")]
        public void Should_Support_Byte_Array_Operations()
        {
            // Given: Two PAIR sockets connected via TCP
            using var ctx = new Context();
            using var socket1 = new Socket(ctx, SocketType.Pair);
            using var socket2 = new Socket(ctx, SocketType.Pair);

            socket1.SetOption(SocketOption.Linger, 0);
            socket2.SetOption(SocketOption.Linger, 0);

            socket1.Bind("tcp://127.0.0.1:15603");
            socket2.Connect("tcp://127.0.0.1:15603");

            Thread.Sleep(100);

            // When: Sending byte array
            var data = new byte[] { 1, 2, 3, 4, 5 };
            socket1.Send(data);

            // Then: Receiving byte array should return exact same bytes
            var received = socket2.RecvBytes();
            received.Should().Equal(data);
        }
    }

    /// <summary>
    /// Tests for message ordering guarantees in PAIR sockets.
    /// </summary>
    public class Message_Ordering
    {
        [Fact(DisplayName = "Should maintain FIFO order for multiple sequential messages")]
        public void Should_Maintain_FIFO_Order_For_Sequential_Messages()
        {
            // Given: Two PAIR sockets connected via TCP
            using var ctx = new Context();
            using var socket1 = new Socket(ctx, SocketType.Pair);
            using var socket2 = new Socket(ctx, SocketType.Pair);

            socket1.SetOption(SocketOption.Linger, 0);
            socket2.SetOption(SocketOption.Linger, 0);

            socket1.Bind("tcp://127.0.0.1:15604");
            socket2.Connect("tcp://127.0.0.1:15604");

            Thread.Sleep(100);

            // When: Multiple messages are sent in sequence
            var messages = new[] { "First", "Second", "Third", "Fourth", "Fifth" };
            foreach (var msg in messages)
            {
                socket1.Send(msg);
            }

            // Then: Messages should be received in the same order (FIFO)
            var receivedMessages = new List<string>();
            for (int i = 0; i < messages.Length; i++)
            {
                receivedMessages.Add(socket2.RecvString());
            }

            receivedMessages.Should().Equal(messages);
        }
    }
}
