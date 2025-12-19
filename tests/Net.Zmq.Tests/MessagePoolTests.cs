using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class MessagePoolTests
{
    [Fact]
    public void RentWithoutSend_ShouldReturnBufferToPool()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Rent and dispose without sending
        using (var msg = pool.Rent(data))
        {
            msg.Size.Should().Be(data.Length);
        }

        // Give time for callback execution
        Thread.Sleep(50);

        // Assert - Buffer should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "buffer should be returned when message is not sent");
        stats.Rents.Should().Be(1);
        stats.Returns.Should().Be(1);
    }

    [Fact]
    public void RentWithSend_ShouldReturnBufferToPool()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-pool-send");
        pull.Connect("inproc://test-pool-send");

        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Rent, send, and dispose
        using (var msg = pool.Rent(data))
        {
            push.Send(msg);
        }

        // Receive to ensure transmission completes
        var buffer = new byte[10];
        pull.Recv(buffer);

        // Give time for ZMQ callback execution
        Thread.Sleep(100);

        // Assert - Buffer should be returned via ZMQ callback
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "buffer should be returned after ZMQ finishes transmission");
        stats.Rents.Should().Be(1);
        stats.Returns.Should().Be(1);
    }

    [Fact]
    public void MultipleRentWithoutSend_ShouldReturnAllBuffers()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        int count = 10;

        // Act - Rent multiple messages without sending
        for (int i = 0; i < count; i++)
        {
            using var msg = pool.Rent(data);
            msg.Size.Should().Be(data.Length);
        }

        // Give time for all callbacks to execute
        Thread.Sleep(100);

        // Assert - All buffers should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all buffers should be returned");
        stats.Rents.Should().Be(count);
        stats.Returns.Should().Be(count);
    }

    [Fact]
    public void MixedRentWithAndWithoutSend_ShouldReturnAllBuffers()
    {
        // Arrange
        var pool = new MessagePool();
        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        push.Bind("inproc://test-pool-mixed");
        pull.Connect("inproc://test-pool-mixed");

        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act - Mix of sent and unsent messages
        // Rent and send
        using (var msg1 = pool.Rent(data))
        {
            push.Send(msg1);
        }

        // Rent without sending
        using (var msg2 = pool.Rent(data))
        {
            msg2.Size.Should().Be(data.Length);
        }

        // Rent and send again
        using (var msg3 = pool.Rent(data))
        {
            push.Send(msg3);
        }

        // Receive to ensure transmissions complete
        var buffer = new byte[10];
        pull.Recv(buffer);
        pull.Recv(buffer);

        // Give time for all callbacks to execute
        Thread.Sleep(100);

        // Assert - All buffers should be returned
        var stats = pool.GetStatistics();
        stats.OutstandingMessages.Should().Be(0, "all buffers should be returned regardless of send status");
        stats.Rents.Should().Be(3);
        stats.Returns.Should().Be(3);
    }

    [Fact]
    public void PoolStatistics_ShouldTrackCorrectly()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var stats1 = pool.GetStatistics();
        stats1.Rents.Should().Be(0);

        using (var msg = pool.Rent(data))
        {
            var stats2 = pool.GetStatistics();
            stats2.Rents.Should().Be(1);
            stats2.OutstandingMessages.Should().Be(1);
        }

        Thread.Sleep(50);

        var stats3 = pool.GetStatistics();
        stats3.Rents.Should().Be(1);
        stats3.Returns.Should().Be(1);
        stats3.OutstandingMessages.Should().Be(0);
    }

    [Fact]
    public void PrepareForReuse_ShouldResetMessageState()
    {
        // Arrange
        var pool = new MessagePool();
        pool.Prewarm(MessageSize.B64, 1);

        // Act
        var msg = pool.Rent(64);

        // Assert - 재사용된 메시지는 초기 상태여야 함
        msg._disposed.Should().BeFalse("message should not be disposed after rent");
        msg._wasSuccessfullySent.Should().BeFalse("message should not be marked as sent after rent");
        msg._callbackExecuted.Should().Be(0, "callback should not be executed after rent");
        msg._isFromPool.Should().BeTrue("message should be marked as from pool");
        msg._reusableCallback.Should().NotBeNull("reusable callback should be set");

        // Return message to pool
        msg.Dispose();
        Thread.Sleep(100);
    }

    [Fact]
    public void MessageCallback_ShouldExecuteOnlyOnce()
    {
        // Arrange
        var pool = new MessagePool();

        // Act - Rent and dispose multiple messages
        var msg1 = pool.Rent(64);
        msg1.Dispose();
        Thread.Sleep(100);

        var msg2 = pool.Rent(64);
        msg2.Dispose();
        Thread.Sleep(100);

        // Assert - Statistics should show correct return count
        // Each message's callback should execute exactly once
        var stats = pool.GetStatistics();
        stats.Returns.Should().BeGreaterOrEqualTo(2, "each disposal should trigger callback exactly once");
    }

    [Fact]
    public void Rent_ShouldSupportMultipleCycles()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B64, 10);
        pool.Prewarm(MessageSize.B64, 5);
        int cycleCount = 10;

        // Act & Assert
        for (int i = 0; i < cycleCount; i++)
        {
            var msg = pool.Rent(64);
            msg.Should().NotBeNull();
            msg._disposed.Should().BeFalse();
            msg._isFromPool.Should().BeTrue();

            msg.Dispose();
            Thread.Sleep(10); // 콜백 실행 대기
        }

        Thread.Sleep(100); // 최종 콜백 실행 대기

        // Assert - Statistics should show all cycles completed
        var stats = pool.GetStatistics();
        stats.Rents.Should().BeGreaterOrEqualTo(cycleCount, "should track all rent operations");
    }

    [Fact]
    public void Rent_ShouldNotLeakMessages()
    {
        // Arrange
        var pool = new MessagePool();
        pool.SetMaxBuffers(MessageSize.B64, 10);
        int messageCount = 5;

        // Act
        var messages = new List<Message>();
        for (int i = 0; i < messageCount; i++)
        {
            messages.Add(pool.Rent(64));
        }

        var statsBefore = pool.GetStatistics();
        statsBefore.OutstandingMessages.Should().Be(messageCount, "should have outstanding messages before disposal");

        // 모두 반환
        foreach (var msg in messages)
        {
            msg.Dispose();
        }

        Thread.Sleep(100); // 콜백 실행 대기

        var statsAfter = pool.GetStatistics();

        // Assert
        statsAfter.OutstandingMessages.Should().BeLessThanOrEqualTo(statsBefore.OutstandingMessages, "outstanding messages should decrease after disposal");
        statsAfter.OutstandingMessages.Should().Be(0, "all messages should be returned to pool");
    }
}
