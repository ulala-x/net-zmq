using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class PollerTests
{
    [Fact]
    public void Poller_InstanceBased_ShouldDetectReadableSocket()
    {
        using var ctx = new Context();
        using var server = new Socket(ctx, SocketType.Rep);
        using var client = new Socket(ctx, SocketType.Req);

        server.SetOption(SocketOption.Linger, 0);
        client.SetOption(SocketOption.Linger, 0);

        server.Bind("tcp://127.0.0.1:15560");
        client.Connect("tcp://127.0.0.1:15560");

        Thread.Sleep(100);

        // Send message to make server readable
        client.Send("Hello");

        // Instance-based polling
        using var poller = new Poller(1);
        int idx = poller.Add(server, PollEvents.In);
        var count = poller.Poll(1000);

        count.Should().Be(1);
        poller.IsReadable(idx).Should().BeTrue();
        idx.Should().Be(0);
    }

    [Fact]
    public void Poller_MultipleSockets_ShouldWork()
    {
        using var ctx = new Context();
        using var server1 = new Socket(ctx, SocketType.Rep);
        using var client1 = new Socket(ctx, SocketType.Req);
        using var server2 = new Socket(ctx, SocketType.Rep);
        using var client2 = new Socket(ctx, SocketType.Req);

        server1.SetOption(SocketOption.Linger, 0);
        client1.SetOption(SocketOption.Linger, 0);
        server2.SetOption(SocketOption.Linger, 0);
        client2.SetOption(SocketOption.Linger, 0);

        server1.Bind("tcp://127.0.0.1:15561");
        client1.Connect("tcp://127.0.0.1:15561");
        server2.Bind("tcp://127.0.0.1:15562");
        client2.Connect("tcp://127.0.0.1:15562");

        Thread.Sleep(100);

        // Send messages to both servers
        client1.Send("Hello1");
        client2.Send("Hello2");

        // Poll multiple sockets
        using var poller = new Poller(2);
        int idx1 = poller.Add(server1, PollEvents.In);
        int idx2 = poller.Add(server2, PollEvents.In);
        var count = poller.Poll(1000);

        count.Should().Be(2);
        poller.IsReadable(idx1).Should().BeTrue();
        poller.IsReadable(idx2).Should().BeTrue();
        idx1.Should().Be(0);
        idx2.Should().Be(1);
    }

    [Fact]
    public void Poller_Update_ShouldChangeEvents()
    {
        using var ctx = new Context();
        using var server = new Socket(ctx, SocketType.Rep);
        using var client = new Socket(ctx, SocketType.Req);

        server.SetOption(SocketOption.Linger, 0);
        client.SetOption(SocketOption.Linger, 0);

        server.Bind("tcp://127.0.0.1:15563");
        client.Connect("tcp://127.0.0.1:15563");

        Thread.Sleep(100);

        using var poller = new Poller(1);
        int idx = poller.Add(server, PollEvents.Out);

        // Update to poll for input events
        poller.Update(idx, PollEvents.In);

        // Send message
        client.Send("Test");

        var count = poller.Poll(1000);
        count.Should().Be(1);
        poller.IsReadable(idx).Should().BeTrue();
    }

    [Fact]
    public void Poller_Clear_ShouldAllowReuse()
    {
        using var ctx = new Context();
        using var server = new Socket(ctx, SocketType.Rep);
        using var client = new Socket(ctx, SocketType.Req);

        server.SetOption(SocketOption.Linger, 0);
        client.SetOption(SocketOption.Linger, 0);

        server.Bind("tcp://127.0.0.1:15564");
        client.Connect("tcp://127.0.0.1:15564");

        Thread.Sleep(100);

        using var poller = new Poller(2);

        // First use
        poller.Add(server, PollEvents.In);
        poller.Count.Should().Be(1);

        // Clear and reuse
        poller.Clear();
        poller.Count.Should().Be(0);

        // Send message
        client.Send("Test");

        // Reuse poller
        int idx = poller.Add(server, PollEvents.In);
        var count = poller.Poll(1000);

        count.Should().Be(1);
        poller.IsReadable(idx).Should().BeTrue();
    }

    [Fact]
    public void Poller_Capacity_ShouldBeRespected()
    {
        using var ctx = new Context();
        using var socket1 = new Socket(ctx, SocketType.Rep);
        using var socket2 = new Socket(ctx, SocketType.Rep);

        socket1.SetOption(SocketOption.Linger, 0);
        socket2.SetOption(SocketOption.Linger, 0);

        using var poller = new Poller(1);
        poller.Capacity.Should().Be(1);

        // Add first socket - should succeed
        poller.Add(socket1, PollEvents.In);

        // Add second socket - should throw
        var act = () => poller.Add(socket2, PollEvents.In);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Poller is at capacity (1 sockets)");
    }

    [Fact]
    public void Poller_GetSocket_ShouldReturnCorrectSocket()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Rep);
        socket.SetOption(SocketOption.Linger, 0);

        using var poller = new Poller(1);
        int idx = poller.Add(socket, PollEvents.In);

        var retrieved = poller.GetSocket(idx);
        retrieved.Should().BeSameAs(socket);
    }

    [Fact]
    public void Poller_Dispose_ShouldNotThrow()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Rep);
        socket.SetOption(SocketOption.Linger, 0);

        var poller = new Poller(1);
        poller.Add(socket, PollEvents.In);

        // Dispose should not throw
        var act = () => poller.Dispose();
        act.Should().NotThrow();

        // Double dispose should not throw
        act = () => poller.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Poller_TimeoutZero_ShouldReturnImmediately()
    {
        using var ctx = new Context();
        using var socket = new Socket(ctx, SocketType.Rep);
        socket.SetOption(SocketOption.Linger, 0);

        using var poller = new Poller(1);
        poller.Add(socket, PollEvents.In);

        // Poll with zero timeout should return immediately
        var count = poller.Poll(0);
        count.Should().Be(0);
    }

    [Fact]
    public void Poller_TimeSpan_ShouldWork()
    {
        using var ctx = new Context();
        using var server = new Socket(ctx, SocketType.Rep);
        using var client = new Socket(ctx, SocketType.Req);

        server.SetOption(SocketOption.Linger, 0);
        client.SetOption(SocketOption.Linger, 0);

        server.Bind("tcp://127.0.0.1:15565");
        client.Connect("tcp://127.0.0.1:15565");

        Thread.Sleep(100);
        client.Send("Test");

        using var poller = new Poller(1);
        int idx = poller.Add(server, PollEvents.In);

        // Poll with TimeSpan
        var count = poller.Poll(TimeSpan.FromSeconds(1));
        count.Should().Be(1);
        poller.IsReadable(idx).Should().BeTrue();
    }

}
