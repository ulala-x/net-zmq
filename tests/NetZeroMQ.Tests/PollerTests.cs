using FluentAssertions;
using Xunit;

namespace NetZeroMQ.Tests;

[Collection("Sequential")]
public class PollerTests
{
    [Fact]
    public void Poller_ShouldDetectReadableSocket()
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

        // Poll
        var items = new PollItem[1];
        items[0] = new PollItem(server, PollEvents.In);

        var count = Poller.Poll(items, 1000);

        count.Should().Be(1);
        items[0].IsReadable.Should().BeTrue();
    }

    [Fact]
    public void Poller_SingleSocket_ShouldWork()
    {
        using var ctx = new Context();
        using var server = new Socket(ctx, SocketType.Rep);
        using var client = new Socket(ctx, SocketType.Req);

        server.SetOption(SocketOption.Linger, 0);
        client.SetOption(SocketOption.Linger, 0);

        server.Bind("tcp://127.0.0.1:15561");
        client.Connect("tcp://127.0.0.1:15561");

        Thread.Sleep(100);
        client.Send("Test");

        // Single socket poll
        var readable = Poller.Poll(server, PollEvents.In, 1000);
        readable.Should().BeTrue();
    }
}
