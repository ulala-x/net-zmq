using FluentAssertions;
using Xunit;
using Net.Zmq;

namespace Net.Zmq.Core.Tests;

public class SafeHandleTests
{
    [Fact]
    public void Context_ShouldBeCreatedAndDisposed()
    {
        // Arrange & Act
        var ctx = new Context();

        // Assert
        ctx.Should().NotBeNull();

        // Cleanup - disposal should not throw
        var action = () => ctx.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void Socket_ShouldBeCreatedAndDisposed()
    {
        // Arrange
        using var ctx = new Context();
        var socket = new Socket(ctx, SocketType.Req);

        // Assert
        socket.Should().NotBeNull();

        // Cleanup - disposal should not throw
        var action = () => socket.Dispose();
        action.Should().NotThrow();
    }
}
