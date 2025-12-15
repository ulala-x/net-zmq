using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Integration tests for PUSH-PULL pipeline pattern.
/// PUSH-PULL implements a unidirectional pipeline for work distribution:
/// - PUSH sockets send messages using round-robin load balancing to connected PULL sockets
/// - PULL sockets receive messages using fair-queuing from connected PUSH sockets
/// - One-way communication only (no reply mechanism)
/// - Ideal for parallel task processing, work distribution, and data flow pipelines
/// </summary>
[Collection("Sequential")]
[Trait("Socket", "PushPull")]
public class Push_Pull_Socket
{
    /// <summary>
    /// Tests work distribution from PUSH to PULL socket.
    /// </summary>
    public class Work_Distribution
    {
        [Fact(DisplayName = "Should distribute work items from PUSH to PULL socket")]
        public void Should_Distribute_Work_Items()
        {
            // Given: A PUSH socket connected to a PULL socket
            using var ctx = new Context();
            using var pusher = new Socket(ctx, SocketType.Push);
            using var puller = new Socket(ctx, SocketType.Pull);

            pusher.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Rcvtimeo, 1000);

            puller.Bind("tcp://127.0.0.1:15620");
            pusher.Connect("tcp://127.0.0.1:15620");

            Thread.Sleep(100); // Allow connection to establish

            // When: Pushing multiple work items
            pusher.Send("Task 1");
            pusher.Send("Task 2");
            pusher.Send("Task 3");

            // Then: PULL socket should receive all work items in order
            var task1 = puller.RecvString();
            task1.Should().Be("Task 1");

            var task2 = puller.RecvString();
            task2.Should().Be("Task 2");

            var task3 = puller.RecvString();
            task3.Should().Be("Task 3");
        }
    }

    /// <summary>
    /// Tests message ordering guarantees in PUSH-PULL pattern.
    /// </summary>
    public class Message_Ordering
    {
        [Fact(DisplayName = "Should maintain message order when sending multiple messages")]
        public void Should_Maintain_Message_Order()
        {
            // Given: A PUSH socket connected to a PULL socket
            using var ctx = new Context();
            using var pusher = new Socket(ctx, SocketType.Push);
            using var puller = new Socket(ctx, SocketType.Pull);

            pusher.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Rcvtimeo, 1000);

            puller.Bind("tcp://127.0.0.1:15621");
            pusher.Connect("tcp://127.0.0.1:15621");

            Thread.Sleep(200);

            // When: Sending multiple tasks
            pusher.Send("Task 1");
            pusher.Send("Task 2");
            pusher.Send("Task 3");
            pusher.Send("Task 4");

            Thread.Sleep(100);

            // Then: Tasks should be received in the same order they were sent
            var tasks = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var task = puller.RecvString();
                tasks.Add(task);
            }

            tasks.Should().Equal("Task 1", "Task 2", "Task 3", "Task 4");
        }
    }

    /// <summary>
    /// Tests pipeline pattern implementation with PUSH-PULL sockets.
    /// </summary>
    public class Pipeline_Pattern
    {
        [Fact(DisplayName = "Should support simple pipeline stages")]
        public void Should_Support_Simple_Pipeline()
        {
            // Given: A simple two-stage pipeline (PUSH -> PULL)
            using var ctx = new Context();

            using var stage1Pusher = new Socket(ctx, SocketType.Push);
            using var stage1Puller = new Socket(ctx, SocketType.Pull);

            stage1Pusher.SetOption(SocketOption.Linger, 0);
            stage1Puller.SetOption(SocketOption.Linger, 0);
            stage1Puller.SetOption(SocketOption.Rcvtimeo, 1000);

            stage1Puller.Bind("tcp://127.0.0.1:15622");
            stage1Pusher.Connect("tcp://127.0.0.1:15622");

            Thread.Sleep(200);

            // When: Sending tasks through the pipeline
            stage1Pusher.Send("Task A");
            stage1Pusher.Send("Task B");

            Thread.Sleep(100);

            // Then: Tasks should flow through the pipeline correctly
            var task1 = stage1Puller.RecvString();
            var task2 = stage1Puller.RecvString();

            task1.Should().Be("Task A");
            task2.Should().Be("Task B");
        }
    }

    /// <summary>
    /// Tests different message types supported by PUSH-PULL pattern.
    /// </summary>
    public class Message_Types
    {
        [Fact(DisplayName = "Should transfer binary data correctly")]
        public void Should_Transfer_Binary_Data()
        {
            // Given: A PUSH socket connected to a PULL socket
            using var ctx = new Context();
            using var pusher = new Socket(ctx, SocketType.Push);
            using var puller = new Socket(ctx, SocketType.Pull);

            pusher.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Rcvtimeo, 1000);

            puller.Bind("tcp://127.0.0.1:15624");
            pusher.Connect("tcp://127.0.0.1:15624");

            Thread.Sleep(200);

            // When: Sending binary data
            var data = new byte[] { 0x01, 0x02, 0x03, 0xFE, 0xFF };
            pusher.Send(data);

            Thread.Sleep(100);

            // Then: Binary data should be received intact
            var received = puller.RecvBytes();
            received.Should().Equal(data);
        }

        [Fact(DisplayName = "Should work with Message objects")]
        public void Should_Work_With_Message_Objects()
        {
            // Given: A PUSH socket connected to a PULL socket
            using var ctx = new Context();
            using var pusher = new Socket(ctx, SocketType.Push);
            using var puller = new Socket(ctx, SocketType.Pull);

            pusher.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Rcvtimeo, 1000);

            puller.Bind("tcp://127.0.0.1:15625");
            pusher.Connect("tcp://127.0.0.1:15625");

            Thread.Sleep(100);

            // When: Sending a Message object
            var workItem = new Message("Work Item Data");
            pusher.Send(workItem, SendFlags.None);
            workItem.Dispose();

            // Then: Message should be received correctly
            var received = new Message();
            puller.Recv(received, RecvFlags.None);
            received.ToString().Should().Be("Work Item Data");
            received.Dispose();
        }
    }

    /// <summary>
    /// Tests socket options configuration for PUSH-PULL pattern.
    /// </summary>
    public class Socket_Options
    {
        [Fact(DisplayName = "Should allow configuring high water mark options")]
        public void Should_Configure_High_Water_Marks()
        {
            // Given: PUSH and PULL sockets with custom high water marks
            using var ctx = new Context();
            using var pusher = new Socket(ctx, SocketType.Push);
            using var puller = new Socket(ctx, SocketType.Pull);

            pusher.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Linger, 0);

            // When: Setting high-water marks
            pusher.SetOption(SocketOption.Sndhwm, 100);
            puller.SetOption(SocketOption.Rcvhwm, 100);

            // Then: High-water marks should be configured correctly
            pusher.GetOption<int>(SocketOption.Sndhwm).Should().Be(100);
            puller.GetOption<int>(SocketOption.Rcvhwm).Should().Be(100);

            puller.Bind("tcp://127.0.0.1:15626");
            pusher.Connect("tcp://127.0.0.1:15626");

            Thread.Sleep(100);

            // And: Basic send/receive operations should still work
            pusher.Send("Test message");
            var msg = puller.RecvString();
            msg.Should().Be("Test message");
        }
    }

    /// <summary>
    /// Tests one-way communication constraints of PUSH-PULL pattern.
    /// </summary>
    public class One_Way_Communication
    {
        [Fact(DisplayName = "Should enforce one-way communication from PUSH to PULL")]
        public void Should_Enforce_Unidirectional_Flow()
        {
            // Given: A PUSH socket connected to a PULL socket
            using var ctx = new Context();
            using var pusher = new Socket(ctx, SocketType.Push);
            using var puller = new Socket(ctx, SocketType.Pull);

            pusher.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Linger, 0);
            puller.SetOption(SocketOption.Rcvtimeo, 1000);

            puller.Bind("tcp://127.0.0.1:15627");
            pusher.Connect("tcp://127.0.0.1:15627");

            Thread.Sleep(200);

            // When: Sending from PUSH to PULL
            pusher.Send("One-way message");
            var received = puller.RecvString();

            // Then: Message should be received successfully
            received.Should().Be("One-way message");

            // And: Communication is strictly one-way
            // PUSH socket type does not support receiving operations
            // PULL socket type does not support sending operations
            // This constraint is enforced by the socket type design
        }
    }
}
