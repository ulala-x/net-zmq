using Xunit;

namespace Net.Zmq.Tests;

[Collection("Sequential")]
public class CoreTypesTests
{
    [Fact]
    public void Context_CanCreate()
    {
        using var context = new Context();
        Assert.NotNull(context);
    }

    [Fact]
    public void Context_CanSetOptions()
    {
        using var context = new Context(ioThreads: 2, maxSockets: 100);
        Assert.Equal(2, context.GetOption(ContextOption.IoThreads));
        Assert.Equal(100, context.GetOption(ContextOption.MaxSockets));
    }

    [Fact]
    public void Context_HasCapability()
    {
        Assert.True(Context.Has("ipc") || Context.Has("tcp"));
    }

    [Fact]
    public void Context_GetVersion()
    {
        var version = Context.Version;
        Assert.True(version.Major >= 4);
    }

    [Fact]
    public void Socket_CanCreate()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        Assert.NotNull(socket);
    }

    [Fact]
    public void Socket_CanBind()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        socket.SetOption(SocketOption.Linger, 0);
        socket.Bind("tcp://127.0.0.1:0");
    }

    [Fact]
    public void Socket_CanSetOptions()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);
        socket.SetOption(SocketOption.Linger, 1000);
        Assert.Equal(1000, socket.GetOption<int>(SocketOption.Linger));
    }

    [Fact]
    public void Message_CanCreate()
    {
        var msg = new Message();
        Assert.Equal(0, msg.Size);
        msg.Dispose();
    }

    [Fact]
    public void Message_CanCreateWithSize()
    {
        var msg = new Message(10);
        Assert.Equal(10, msg.Size);
        msg.Dispose();
    }

    [Fact]
    public void Message_CanCreateWithData()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var msg = new Message(data);
        Assert.Equal(5, msg.Size);
        Assert.Equal(data, msg.Data.ToArray());
        msg.Dispose();
    }

    [Fact]
    public void Message_CanCreateWithString()
    {
        var msg = new Message("Hello, World!");
        Assert.True(msg.Size > 0);
        Assert.Equal("Hello, World!", msg.ToString());
        msg.Dispose();
    }

    [Fact]
    public void PubSub_CanSendReceive()
    {
        using var context = new Context();
        using var pub = new Socket(context, SocketType.Pub);
        using var sub = new Socket(context, SocketType.Sub);

        pub.SetOption(SocketOption.Linger, 0);
        sub.SetOption(SocketOption.Linger, 0);
        sub.SetOption(SocketOption.Rcvtimeo, 1000);

        pub.Bind("tcp://127.0.0.1:0");
        var endpoint = pub.GetOptionString(SocketOption.Last_Endpoint);

        sub.Connect(endpoint);
        sub.SubscribeAll();

        // Give time for subscription to propagate
        Thread.Sleep(200);

        // Send a message
        pub.Send("Hello");
        Thread.Sleep(50);

        // Receive the message
        var received = sub.RecvString();
        Assert.Equal("Hello", received);
    }

    [Fact]
    public void ReqRep_CanSendReceive()
    {
        using var context = new Context();
        using var rep = new Socket(context, SocketType.Rep);
        using var req = new Socket(context, SocketType.Req);

        rep.SetOption(SocketOption.Linger, 0);
        req.SetOption(SocketOption.Linger, 0);
        rep.SetOption(SocketOption.Rcvtimeo, 1000);
        req.SetOption(SocketOption.Rcvtimeo, 1000);

        rep.Bind("tcp://127.0.0.1:0");
        var endpoint = rep.GetOptionString(SocketOption.Last_Endpoint);

        req.Connect(endpoint);
        Thread.Sleep(100);

        // Send request
        req.Send("Hello");

        // Receive request
        var request = rep.RecvString();
        Assert.Equal("Hello", request);

        // Send reply
        rep.Send("World");

        // Receive reply
        var reply = req.RecvString();
        Assert.Equal("World", reply);
    }

    [Fact]
    public void Socket_TrySendRecv_NonBlocking()
    {
        using var context = new Context();
        using var push = new Socket(context, SocketType.Push);
        using var pull = new Socket(context, SocketType.Pull);

        push.SetOption(SocketOption.Linger, 0);
        pull.SetOption(SocketOption.Linger, 0);
        pull.SetOption(SocketOption.Rcvtimeo, 500);

        pull.Bind("tcp://127.0.0.1:0");
        var endpoint = pull.GetOptionString(SocketOption.Last_Endpoint);
        push.Connect(endpoint);

        Thread.Sleep(100); // Allow connection to establish

        // Try receive on empty socket should return null
        Assert.Null(pull.RecvString(RecvFlags.DontWait));

        // Send and receive
        Assert.NotEqual(-1, push.Send("Test", SendFlags.DontWait));
        Thread.Sleep(50); // Allow message to propagate
        var text = pull.RecvString(RecvFlags.DontWait);
        Assert.NotNull(text);
        Assert.Equal("Test", text);
    }

    [Fact]
    public void SocketRef_Works()
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Pub);

        var socketRef = socket.Ref;
        Assert.True(socketRef.IsValid);
    }
}
