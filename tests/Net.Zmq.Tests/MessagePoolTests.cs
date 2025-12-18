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
        stats.OutstandingBuffers.Should().Be(0, "buffer should be returned when message is not sent");
        stats.TotalRents.Should().Be(1);
        stats.TotalReturns.Should().Be(1);
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
        stats.OutstandingBuffers.Should().Be(0, "buffer should be returned after ZMQ finishes transmission");
        stats.TotalRents.Should().Be(1);
        stats.TotalReturns.Should().Be(1);
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
        stats.OutstandingBuffers.Should().Be(0, "all buffers should be returned");
        stats.TotalRents.Should().Be(count);
        stats.TotalReturns.Should().Be(count);
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
        stats.OutstandingBuffers.Should().Be(0, "all buffers should be returned regardless of send status");
        stats.TotalRents.Should().Be(3);
        stats.TotalReturns.Should().Be(3);
    }

    [Fact]
    public void PoolStatistics_ShouldTrackCorrectly()
    {
        // Arrange
        var pool = new MessagePool();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var stats1 = pool.GetStatistics();
        stats1.TotalRents.Should().Be(0);

        using (var msg = pool.Rent(data))
        {
            var stats2 = pool.GetStatistics();
            stats2.TotalRents.Should().Be(1);
            stats2.OutstandingBuffers.Should().Be(1);
        }

        Thread.Sleep(50);

        var stats3 = pool.GetStatistics();
        stats3.TotalRents.Should().Be(1);
        stats3.TotalReturns.Should().Be(1);
        stats3.OutstandingBuffers.Should().Be(0);
    }
}
