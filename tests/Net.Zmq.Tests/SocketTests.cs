using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class SocketTests
{
    [Fact]
    public void Socket_ShouldBeCreatedForAllTypes()
    {
        using var ctx = new Context();

        foreach (SocketType type in Enum.GetValues<SocketType>())
        {
            using var socket = new Socket(ctx, type);
            socket.Should().NotBeNull();
        }
    }

    [Fact]
    public void Socket_ShouldBindAndUnbind()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Rep);

        // Act & Assert
        var action = () => socket.Bind("tcp://127.0.0.1:15555");
        action.Should().NotThrow();

        var unbindAction = () => socket.Unbind("tcp://127.0.0.1:15555");
        unbindAction.Should().NotThrow();
    }

    [Fact]
    public void Socket_Options_ShouldBeSetAndGet()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Req);

        // Act
        socket.SetOption(SocketOption.Linger, 0);
        socket.SetOption(SocketOption.Sndtimeo, 1000);
        socket.SetOption(SocketOption.Rcvtimeo, 1000);

        // Assert
        socket.GetOption<int>(SocketOption.Linger).Should().Be(0);
        socket.GetOption<int>(SocketOption.Sndtimeo).Should().Be(1000);
        socket.GetOption<int>(SocketOption.Rcvtimeo).Should().Be(1000);
    }

    [Fact]
    public void Socket_Subscribe_ShouldWork()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Sub);

        // Act & Assert
        var action = () =>
        {
            socket.Subscribe("topic1");
            socket.SubscribeAll();
            socket.Unsubscribe("topic1");
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void Socket_Ref_ShouldReturnValidReference()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Req);

        // Act
        var socketRef = socket.Ref;

        // Assert
        socketRef.IsValid.Should().BeTrue();
    }
}
