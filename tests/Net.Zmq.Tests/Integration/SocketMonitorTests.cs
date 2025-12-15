using FluentAssertions;
using Xunit;

namespace Net.Zmq.Tests.Integration;

/// <summary>
/// Tests for Socket Monitor functionality.
/// Socket Monitor provides real-time event monitoring for socket lifecycle events
/// including connection, binding, disconnection, and other state changes.
/// </summary>
[Collection("Sequential")]
[Trait("Feature", "Monitor")]
public class Socket_Monitor
{
    /// <summary>
    /// Tests for connection-related monitor events (Connected, Accepted).
    /// </summary>
    public class Connection_Events
    {
        [Fact(DisplayName = "Should receive Connected event when client connects to server")]
        public void Should_Receive_Connected_Event_When_Client_Connects()
        {
            // Given: A REQ-REP socket pair with monitor configured for Connected events
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            const string monitorEndpoint = "inproc://monitor-connected";
            client.Monitor(monitorEndpoint, SocketMonitorEvent.Connected);

            using var monitor = new Socket(ctx, SocketType.Pair);
            monitor.SetOption(SocketOption.Rcvtimeo, 2000);
            monitor.Connect(monitorEndpoint);

            server.Bind("tcp://127.0.0.1:15750");

            // When: Client connects to the server
            client.Connect("tcp://127.0.0.1:15750");

            // Then: Monitor should receive a Connected event with the correct address
            var eventFrame = monitor.RecvBytes();
            var addressFrame = monitor.RecvString();

            var eventData = SocketMonitorEventData.Parse(eventFrame, addressFrame);
            eventData.Event.Should().Be(SocketMonitorEvent.Connected);
            eventData.Address.Should().Be("tcp://127.0.0.1:15750");
        }

        [Fact(DisplayName = "Should receive Accepted event when server accepts client connection")]
        public void Should_Receive_Accepted_Event_When_Server_Accepts_Connection()
        {
            // Given: A REQ-REP socket pair with monitor configured for Listening and Accepted events
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            const string monitorEndpoint = "inproc://monitor-accepted";
            server.Monitor(monitorEndpoint, SocketMonitorEvent.Listening | SocketMonitorEvent.Accepted);

            using var monitor = new Socket(ctx, SocketType.Pair);
            monitor.SetOption(SocketOption.Rcvtimeo, 2000);
            monitor.Connect(monitorEndpoint);

            server.Bind("tcp://127.0.0.1:15752");

            // Receive Listening event first
            var listeningEventFrame = monitor.RecvBytes();
            var listeningAddressFrame = monitor.RecvString();
            var listeningEvent = SocketMonitorEventData.Parse(listeningEventFrame, listeningAddressFrame);
            listeningEvent.Event.Should().Be(SocketMonitorEvent.Listening);

            // When: Client connects to the server
            client.Connect("tcp://127.0.0.1:15752");
            Thread.Sleep(100); // Allow connection to establish

            // Then: Monitor should receive an Accepted event
            var acceptedEventFrame = monitor.RecvBytes();
            var acceptedAddressFrame = monitor.RecvString();

            var eventData = SocketMonitorEventData.Parse(acceptedEventFrame, acceptedAddressFrame);
            eventData.Event.Should().Be(SocketMonitorEvent.Accepted);
            eventData.Address.Should().StartWith("tcp://127.0.0.1");
        }
    }

    /// <summary>
    /// Tests for binding-related monitor events (Listening).
    /// </summary>
    public class Binding_Events
    {
        [Fact(DisplayName = "Should receive Listening event when socket starts listening")]
        public void Should_Receive_Listening_Event_When_Socket_Binds()
        {
            // Given: A REP socket with monitor configured for Listening events
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);

            server.SetOption(SocketOption.Linger, 0);

            const string monitorEndpoint = "inproc://monitor-listening";
            server.Monitor(monitorEndpoint, SocketMonitorEvent.Listening);

            using var monitor = new Socket(ctx, SocketType.Pair);
            monitor.SetOption(SocketOption.Rcvtimeo, 2000);
            monitor.Connect(monitorEndpoint);

            // When: Server binds to an address
            server.Bind("tcp://127.0.0.1:15751");

            // Then: Monitor should receive a Listening event with the correct address
            var eventFrame = monitor.RecvBytes();
            var addressFrame = monitor.RecvString();

            var eventData = SocketMonitorEventData.Parse(eventFrame, addressFrame);
            eventData.Event.Should().Be(SocketMonitorEvent.Listening);
            eventData.Address.Should().Be("tcp://127.0.0.1:15751");
        }
    }

    /// <summary>
    /// Tests for disconnection-related monitor events (Disconnected).
    /// </summary>
    public class Disconnection_Events
    {
        [Fact(DisplayName = "Should receive Disconnected event when client disconnects")]
        public void Should_Receive_Disconnected_Event_When_Client_Disconnects()
        {
            // Given: A REP socket with monitor configured for multiple events including Disconnected
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);

            server.SetOption(SocketOption.Linger, 0);

            const string monitorEndpoint = "inproc://monitor-disconnected";
            server.Monitor(monitorEndpoint, SocketMonitorEvent.Listening | SocketMonitorEvent.Accepted | SocketMonitorEvent.Disconnected);

            using var monitor = new Socket(ctx, SocketType.Pair);
            monitor.SetOption(SocketOption.Rcvtimeo, 2000);
            monitor.Connect(monitorEndpoint);

            server.Bind("tcp://127.0.0.1:15755");

            // Receive Listening event
            var listeningEventFrame = monitor.RecvBytes();
            var listeningAddressFrame = monitor.RecvString();
            var listeningEvent = SocketMonitorEventData.Parse(listeningEventFrame, listeningAddressFrame);
            listeningEvent.Event.Should().Be(SocketMonitorEvent.Listening);

            // When: Client connects and then disconnects
            using (var client = new Socket(ctx, SocketType.Req))
            {
                client.SetOption(SocketOption.Linger, 0);
                client.Connect("tcp://127.0.0.1:15755");
                Thread.Sleep(100); // Allow connection to establish

                // Receive Accepted event
                var acceptedEventFrame = monitor.RecvBytes();
                var acceptedAddressFrame = monitor.RecvString();
                var acceptedEvent = SocketMonitorEventData.Parse(acceptedEventFrame, acceptedAddressFrame);
                acceptedEvent.Event.Should().Be(SocketMonitorEvent.Accepted);

                // Client will be disposed here, causing disconnect
            }

            Thread.Sleep(100); // Allow disconnect to propagate

            // Then: Monitor should receive a Disconnected event
            var disconnectedEventFrame = monitor.RecvBytes();
            var disconnectedAddressFrame = monitor.RecvString();

            var eventData = SocketMonitorEventData.Parse(disconnectedEventFrame, disconnectedAddressFrame);
            eventData.Event.Should().Be(SocketMonitorEvent.Disconnected);
            eventData.Address.Should().StartWith("tcp://127.0.0.1");
        }
    }

    /// <summary>
    /// Tests for event filtering functionality in Socket Monitor.
    /// </summary>
    public class Event_Filtering
    {
        [Fact(DisplayName = "Should only receive events matching the configured filter")]
        public void Should_Only_Receive_Filtered_Events()
        {
            // Given: A monitor configured to only receive Connected events
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);
            using var client = new Socket(ctx, SocketType.Req);

            server.SetOption(SocketOption.Linger, 0);
            client.SetOption(SocketOption.Linger, 0);

            const string monitorEndpoint = "inproc://monitor-filtered";
            client.Monitor(monitorEndpoint, SocketMonitorEvent.Connected);

            using var monitor = new Socket(ctx, SocketType.Pair);
            monitor.SetOption(SocketOption.Rcvtimeo, 2000);
            monitor.Connect(monitorEndpoint);

            server.Bind("tcp://127.0.0.1:15753");

            // When: Client connects and then disconnects
            client.Connect("tcp://127.0.0.1:15753");

            // Then: Should receive Connected event
            var eventFrame = monitor.RecvBytes();
            var addressFrame = monitor.RecvString();

            var eventData = SocketMonitorEventData.Parse(eventFrame, addressFrame);
            eventData.Event.Should().Be(SocketMonitorEvent.Connected);

            // When: Client is disposed (triggering disconnect)
            client.Dispose();

            // Then: Should NOT receive Disconnected event because it's filtered out
            var timedOut = false;
            try
            {
                monitor.RecvBytes();
            }
            catch (ZmqException ex) when (ex.ErrorNumber == (OperatingSystem.IsMacOS() ? 35 : 11)) // EAGAIN
            {
                timedOut = true;
            }

            timedOut.Should().BeTrue("No Disconnected event should be received due to filtering");
        }
    }

    /// <summary>
    /// Tests for monitor lifecycle management (start/stop).
    /// </summary>
    public class Monitor_Lifecycle
    {
        [Fact(DisplayName = "Should receive MonitorStopped event when monitoring is stopped")]
        public void Should_Receive_MonitorStopped_Event_When_Monitoring_Stops()
        {
            // Given: A socket with active monitoring configured for all events
            using var ctx = new Context();
            using var server = new Socket(ctx, SocketType.Rep);

            server.SetOption(SocketOption.Linger, 0);

            const string monitorEndpoint = "inproc://monitor-stop";
            server.Monitor(monitorEndpoint, SocketMonitorEvent.All);

            using var monitor = new Socket(ctx, SocketType.Pair);
            monitor.SetOption(SocketOption.Rcvtimeo, 2000);
            monitor.Connect(monitorEndpoint);

            server.Bind("tcp://127.0.0.1:15754");

            // Receive initial Listening event
            var listeningEventFrame = monitor.RecvBytes();
            var listeningAddressFrame = monitor.RecvString();
            var listeningEvent = SocketMonitorEventData.Parse(listeningEventFrame, listeningAddressFrame);
            listeningEvent.Event.Should().Be(SocketMonitorEvent.Listening);

            // When: Monitoring is stopped by calling Monitor with null endpoint and 0 events
            server.Monitor(null, 0);

            // Then: Monitor should receive a MonitorStopped event
            var stoppedEventFrame = monitor.RecvBytes();
            var stoppedAddressFrame = monitor.RecvString();

            var eventData = SocketMonitorEventData.Parse(stoppedEventFrame, stoppedAddressFrame);
            eventData.Event.Should().Be(SocketMonitorEvent.MonitorStopped);
        }
    }
}
