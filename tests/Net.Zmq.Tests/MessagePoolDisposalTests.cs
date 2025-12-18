using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

/// <summary>
/// Tests for Message disposal and pooling behavior in various scenarios.
/// Verifies that pooled messages are correctly returned to the pool and that
/// invalid operations (like sending a resend=false message) are prevented.
/// </summary>
[Collection("Sequential")]
public class MessagePoolDisposalTests
{
    /// <summary>
    /// Scenario: Message created with Rent(data) has callback
    /// - Sent successfully → callback returns buffer after ZMQ transmission
    /// - _isPooled = false (has callback)
    /// - _wasSuccessfullySent = true
    /// Expected: Buffer returned via ZMQ callback, not via Dispose
    /// </summary>
    [Fact]
    public void RentWithCallback_Sent_ReturnsViaCallback()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-callback-sent");
        pull.Connect("inproc://test-callback-sent");

        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Rent with callback (via Rent(data)), send, dispose
        using (var msg = pool.Rent(data))
        {
            msg._isPooled.Should().BeFalse("Rent(data) creates message with callback");
            push.Send(msg);
            // At this point, _wasSuccessfullySent = true
            // Dispose will NOT call MsgClose (because already sent)
            // Dispose will NOT call ReturnInternal (because _isPooled = false)
        }

        // Receive to complete ZMQ transmission
        var buffer = new byte[10];
        pull.Recv(buffer);

        // Wait for ZMQ callback
        Thread.Sleep(100);

        // Assert - Buffer should be returned via callback
        var stats = pool.GetStatistics();
        stats.OutstandingBuffers.Should().Be(0, "buffer should be returned via ZMQ callback");
        stats.TotalRents.Should().Be(1);
        stats.TotalReturns.Should().Be(1);
    }

    /// <summary>
    /// Scenario: Message created with Rent(data) has callback
    /// - NOT sent → Dispose calls MsgClose → callback invoked immediately
    /// - _isPooled = false (has callback)
    /// - _wasSuccessfullySent = false
    /// Expected: Buffer returned via callback invoked by MsgClose
    /// </summary>
    [Fact]
    public void RentWithCallback_NotSent_ReturnsViaMsgClose()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Rent with callback, dispose without sending
        using (var msg = pool.Rent(data))
        {
            msg._isPooled.Should().BeFalse("Rent(data) creates message with callback");
            msg.Size.Should().Be(data.Length);
            // Dispose will call MsgClose (because not sent)
            // MsgClose will invoke callback immediately
            // Dispose will NOT call ReturnInternal (because _isPooled = false)
        }

        // Wait for callback execution
        Thread.Sleep(50);

        // Assert - Buffer should be returned via callback
        var stats = pool.GetStatistics();
        stats.OutstandingBuffers.Should().Be(0, "buffer should be returned via MsgClose callback");
        stats.TotalRents.Should().Be(1);
        stats.TotalReturns.Should().Be(1);
    }

    /// <summary>
    /// Scenario: Message created with Rent(size, withCallback=false)
    /// - NOT sent → Dispose calls ReturnInternal
    /// - _isPooled = true (no callback)
    /// - _wasSuccessfullySent = false
    /// Expected: Buffer returned via Dispose → ReturnInternal
    /// </summary>
    [Fact]
    public void RentNoCallback_NotSent_ReturnsViaDispose()
    {
        // Arrange
        var pool = new MessagePool();

        // Act - Rent without callback, dispose without sending
        using (var msg = pool.Rent(64, withCallback: false))
        {
            msg._isPooled.Should().BeTrue("Rent(size, withCallback=false) creates message without callback");
            msg.Size.Should().Be(64);
            // Dispose will call MsgClose (because not sent)
            // Dispose will call ReturnInternal (because _isPooled = true)
        }

        // No need to wait - ReturnInternal is synchronous

        // Assert - Buffer should be returned via Dispose
        var stats = pool.GetStatistics();
        stats.OutstandingBuffers.Should().Be(0, "buffer should be returned via Dispose → ReturnInternal");
        stats.TotalRents.Should().Be(1);
        stats.TotalReturns.Should().Be(1);
    }

    /// <summary>
    /// Scenario: Message created with Rent(size, withCallback=false)
    /// - Attempted to send → Should throw InvalidOperationException
    /// - _isPooled = true (no callback)
    /// Expected: Exception prevents the bug where buffer is returned while ZMQ is using it
    /// </summary>
    [Fact]
    public void RentNoCallback_Send_ThrowsException()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        push.Bind("inproc://test-nocallback-send");

        var msg = pool.Rent(64, withCallback: false);

        // Act & Assert
        msg._isPooled.Should().BeTrue("Rent(size, withCallback=false) should set _isPooled=true");

        var act = () => push.Send(msg);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*resend=false*");

        // Cleanup
        msg.Dispose();

        var stats = pool.GetStatistics();
        stats.OutstandingBuffers.Should().Be(0);
        stats.TotalReturns.Should().Be(1);
    }

    /// <summary>
    /// Scenario: Message received with ReceiveWithPool(resend=false)
    /// - Attempted to send → Should throw InvalidOperationException
    /// - _isPooled = true (no callback)
    /// Expected: Prevents forwarding messages that are meant for consumption only
    /// </summary>
    [Fact]
    public void ReceiveWithPool_ResendFalse_Send_ThrowsException()
    {
        // Arrange
        var pool = MessagePool.Shared;
        pool.Prewarm(MessageSize.B64, 10);

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        using var forwarder = new Socket(ctx, SocketType.Push);

        sender.Bind("inproc://test-receive-send");
        receiver.Connect("inproc://test-receive-send");
        forwarder.Bind("inproc://test-forward");

        // Send a message
        var data = new byte[] { 1, 2, 3, 4, 5 };
        sender.Send(data);

        // Act - Receive with resend=false, try to forward
        using var msg = receiver.ReceiveWithPool(resend: false);
        msg.Should().NotBeNull();
        msg!._isPooled.Should().BeTrue("ReceiveWithPool(resend=false) should set _isPooled=true");

        // Assert - Sending should throw
        var act = () => forwarder.Send(msg);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*resend=false*");
    }

    /// <summary>
    /// Scenario: Message received with ReceiveWithPool(resend=true)
    /// - Sent successfully → callback returns buffer after ZMQ transmission
    /// - _isPooled = false (has callback)
    /// Expected: Message can be forwarded, buffer returned via callback
    /// </summary>
    [Fact]
    public void ReceiveWithPool_ResendTrue_Send_Success()
    {
        // Arrange
        var pool = MessagePool.Shared;
        pool.Prewarm(MessageSize.B64, 10);

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);
        using var forwarder = new Socket(ctx, SocketType.Push);
        using var finalReceiver = new Socket(ctx, SocketType.Pull);

        sender.Bind("inproc://test-resend-in");
        receiver.Connect("inproc://test-resend-in");
        forwarder.Bind("inproc://test-resend-out");
        finalReceiver.Connect("inproc://test-resend-out");

        // Send a message
        var data = new byte[] { 1, 2, 3, 4, 5 };
        sender.Send(data);

        var statsBefore = pool.GetStatistics();

        // Act - Receive with resend=true, forward it
        using (var msg = receiver.ReceiveWithPool(resend: true))
        {
            msg.Should().NotBeNull();
            msg!._isPooled.Should().BeFalse("ReceiveWithPool(resend=true) should set _isPooled=false");

            // This should succeed
            forwarder.Send(msg);
        }

        // Receive forwarded message
        var buffer = new byte[10];
        finalReceiver.Recv(buffer);

        // Wait for callback
        Thread.Sleep(100);

        // Assert - Buffer should be returned via callback
        var statsAfter = pool.GetStatistics();
        var rents = statsAfter.TotalRents - statsBefore.TotalRents;
        var returns = statsAfter.TotalReturns - statsBefore.TotalReturns;

        rents.Should().Be(1, "should rent one buffer for received message");
        returns.Should().Be(1, "buffer should be returned after forwarding");
    }

    /// <summary>
    /// Scenario: Message received with ReceiveWithPool(resend=true)
    /// - NOT sent → callback invoked via MsgClose on Dispose
    /// - _isPooled = false (has callback)
    /// Expected: Buffer returned even if not forwarded
    /// </summary>
    [Fact]
    public void ReceiveWithPool_ResendTrue_NotSent_ReturnsViaCallback()
    {
        // Arrange
        var pool = MessagePool.Shared;
        pool.Prewarm(MessageSize.B64, 10);

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.Bind("inproc://test-resend-nosend");
        receiver.Connect("inproc://test-resend-nosend");

        // Send a message
        var data = new byte[] { 1, 2, 3, 4, 5 };
        sender.Send(data);

        var statsBefore = pool.GetStatistics();

        // Act - Receive with resend=true, but don't forward
        using (var msg = receiver.ReceiveWithPool(resend: true))
        {
            msg.Should().NotBeNull();
            msg!._isPooled.Should().BeFalse("ReceiveWithPool(resend=true) should set _isPooled=false");
            // Don't send - just dispose
        }

        // Wait for callback
        Thread.Sleep(100);

        // Assert - Buffer should be returned via MsgClose callback
        var statsAfter = pool.GetStatistics();
        var rents = statsAfter.TotalRents - statsBefore.TotalRents;
        var returns = statsAfter.TotalReturns - statsBefore.TotalReturns;

        rents.Should().Be(1, "should rent one buffer");
        returns.Should().Be(1, "buffer should be returned via MsgClose callback");
    }

    /// <summary>
    /// Scenario: Message received with ReceiveWithPool(resend=false)
    /// - NOT sent → Dispose calls ReturnInternal
    /// - _isPooled = true (no callback)
    /// Expected: Normal consumption path, buffer returned synchronously
    /// </summary>
    [Fact]
    public void ReceiveWithPool_ResendFalse_NotSent_ReturnsViaDispose()
    {
        // Arrange
        var pool = MessagePool.Shared;
        pool.Prewarm(MessageSize.B64, 10);

        using var ctx = new Context();
        using var sender = new Socket(ctx, SocketType.Push);
        using var receiver = new Socket(ctx, SocketType.Pull);

        sender.Bind("inproc://test-consume");
        receiver.Connect("inproc://test-consume");

        // Send a message
        var data = new byte[] { 1, 2, 3, 4, 5 };
        sender.Send(data);

        var statsBefore = pool.GetStatistics();

        // Act - Receive and consume (normal usage)
        using (var msg = receiver.ReceiveWithPool(resend: false))
        {
            msg.Should().NotBeNull();
            msg!._isPooled.Should().BeTrue("ReceiveWithPool(resend=false) should set _isPooled=true");
            msg.Data.ToArray()[0].Should().Be(1);
        }

        // Assert - Buffer should be returned immediately via Dispose
        var statsAfter = pool.GetStatistics();
        var rents = statsAfter.TotalRents - statsBefore.TotalRents;
        var returns = statsAfter.TotalReturns - statsBefore.TotalReturns;

        rents.Should().Be(1, "should rent one buffer");
        returns.Should().Be(1, "buffer should be returned immediately via Dispose");
    }

    /// <summary>
    /// Scenario: Multiple messages with mixed callback/no-callback
    /// Expected: All buffers returned correctly via appropriate paths
    /// </summary>
    [Fact]
    public void MixedCallbackScenarios_AllBuffersReturned()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B64, 20);

        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-mixed");
        pull.Connect("inproc://test-mixed");

        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Mix of all scenarios
        // 1. Rent with callback, send
        using (var msg1 = pool.Rent(data))
        {
            push.Send(msg1);
        }
        pull.Recv(new byte[10]);

        // 2. Rent with callback, don't send
        using (var msg2 = pool.Rent(data))
        {
            msg2.Size.Should().Be(5);
        }

        // 3. Rent without callback, don't send
        using (var msg3 = pool.Rent(64, withCallback: false))
        {
            msg3.Size.Should().Be(64);
        }

        // 4. Rent without callback, try to send (should throw)
        var msg4 = pool.Rent(64, withCallback: false);
        var act = () => push.Send(msg4);
        act.Should().Throw<InvalidOperationException>();
        msg4.Dispose();

        // Wait for callbacks
        Thread.Sleep(100);

        // Assert - All buffers should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingBuffers.Should().Be(0, "all buffers should be returned via appropriate paths");
        stats.TotalRents.Should().Be(4);
        stats.TotalReturns.Should().Be(4);
    }

    /// <summary>
    /// Scenario: Verify that double-dispose doesn't cause double-return
    /// Expected: ReturnInternal is idempotent via Interlocked.Exchange
    /// </summary>
    [Fact]
    public void DoubleDispose_NoDoubleReturn()
    {
        // Arrange
        var pool = new MessagePool();
        var msg = pool.Rent(64, withCallback: false);

        // Act - Dispose twice
        msg.Dispose();
        msg.Dispose();

        // Assert - Only one return should be recorded
        var stats = pool.GetStatistics();
        stats.TotalRents.Should().Be(1);
        stats.TotalReturns.Should().Be(1);
        stats.OutstandingBuffers.Should().Be(0);
    }
}
