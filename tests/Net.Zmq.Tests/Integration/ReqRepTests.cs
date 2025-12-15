using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Tests for the REQ-REP (Request-Reply) pattern.
/// REQ sockets send requests and receive replies in strict alternation.
/// REP sockets receive requests and send replies in strict alternation.
/// This pattern is ideal for remote procedure call (RPC) style interactions.
/// </summary>
[Collection("Sequential")]
[Trait("Socket", "ReqRep")]
public class ReqRep_Socket
{
    /// <summary>
    /// Tests basic request-response message exchange patterns.
    /// </summary>
    public class Request_Response
    {
        [Fact(DisplayName = "Should exchange messages between REQ and REP sockets")]
        public void Should_Exchange_Messages_Between_Req_And_Rep_Sockets()
        {
            // Given: A REP server and REQ client connected on the same endpoint
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            server.Bind("tcp://127.0.0.1:15556");
            client.Connect("tcp://127.0.0.1:15556");

            Thread.Sleep(100); // Allow connection to establish

            // When: Client sends a request
            client.Send("Hello");

            // Then: Server receives the request
            var request = server.RecvString();
            request.Should().Be("Hello");

            // When: Server sends a reply
            server.Send("World");

            // Then: Client receives the reply
            var reply = client.RecvString();
            reply.Should().Be("World");
        }
    }

    /// <summary>
    /// Tests request-response patterns using Message objects.
    /// </summary>
    public class Message_Types
    {
        [Fact(DisplayName = "Should work with Message objects for request and reply")]
        public void Should_Work_With_Message_Objects_For_Request_And_Reply()
        {
            // Given: A REP server and REQ client connected on the same endpoint
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            server.Bind("tcp://127.0.0.1:15557");
            client.Connect("tcp://127.0.0.1:15557");

            Thread.Sleep(100);

            // When: Client sends a request using Message object
            var request = new Message("Request");
            client.Send(request, SendFlags.None);
            request.Dispose();

            // Then: Server receives the request using Message object
            var received = new Message();
            server.Recv(received, RecvFlags.None);
            received.ToString().Should().Be("Request");
            received.Dispose();
        }
    }
}
