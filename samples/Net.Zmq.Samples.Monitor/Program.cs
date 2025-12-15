using System.Text;
using Net.Zmq;

/// <summary>
/// Socket Monitor Sample
///
/// Demonstrates real-time socket event monitoring using ZeroMQ's monitoring capabilities.
/// This sample shows how to track connection events, disconnections, and other socket lifecycle events.
///
/// Key features:
/// - Monitor socket lifecycle events (LISTENING, ACCEPTED, DISCONNECTED, etc.)
/// - Receive events through PAIR socket pattern via inproc:// transport
/// - Real-time event visualization with timestamps and formatted output
/// - Hub-and-Spoke pattern with Router-to-Router monitoring
///
/// Monitoring events include:
/// - LISTENING: Socket is ready to accept connections
/// - ACCEPTED: New connection established
/// - CONNECTED: Outbound connection successful
/// - DISCONNECTED: Connection closed
/// - MONITOR_STOPPED: Monitoring has been stopped
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NetZeroMQ Socket Monitor Sample ===");
        Console.WriteLine("Router-to-Router with Real-time Event Monitoring");
        Console.WriteLine();

        // Run the Hub-and-Spoke example with monitoring
        HubAndSpokeWithMonitoring();

        Console.WriteLine();
        Console.WriteLine("Sample completed!");
    }

    /// <summary>
    /// Hub-and-Spoke pattern with real-time socket monitoring
    /// Demonstrates monitoring a Router (Hub) socket while multiple Router (Spoke) sockets connect to it
    /// </summary>
    static void HubAndSpokeWithMonitoring()
    {
        using var ctx = new Context();
        using var hub = new Socket(ctx, SocketType.Router);
        using var spoke1 = new Socket(ctx, SocketType.Router);
        using var spoke2 = new Socket(ctx, SocketType.Router);

        // Configure all sockets
        foreach (var socket in new[] { hub, spoke1, spoke2 })
        {
            socket.SetOption(SocketOption.Linger, 0);
            socket.SetOption(SocketOption.Rcvtimeo, 1000);
        }

        // Set explicit identities for Router-to-Router communication
        hub.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("HUB"));
        spoke1.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("SPOKE1"));
        spoke2.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("SPOKE2"));

        // Create monitor socket to receive events from the Hub
        using var monitor = new Socket(ctx, SocketType.Pair);
        const string monitorEndpoint = "inproc://hub-monitor";

        Console.WriteLine($"[Monitor] Attaching to Hub socket ({monitorEndpoint})");
        Console.WriteLine("[Monitor] Watching for: All events");
        Console.WriteLine();

        // Start monitoring the Hub socket - all events will be sent to the monitor socket
        hub.Monitor(monitorEndpoint, SocketMonitorEvent.All);

        // Connect monitor socket to receive events
        monitor.Connect(monitorEndpoint);
        monitor.SetOption(SocketOption.Rcvtimeo, 100);

        // Start monitor thread
        var monitorCts = new CancellationTokenSource();
        var monitorThread = new Thread(() => MonitorEventsThread(monitor, monitorCts.Token))
        {
            IsBackground = true,
            Name = "MonitorThread"
        };
        monitorThread.Start();

        // Give monitor time to initialize
        Thread.Sleep(100);

        // Hub binds - this will generate a LISTENING event
        Console.WriteLine("[Hub] Binding to tcp://*:5564...");
        hub.Bind("tcp://*:5564");
        Thread.Sleep(200); // Allow monitor to process LISTENING event

        // Spokes connect - each will generate an ACCEPTED event on the Hub
        Console.WriteLine("[Spoke1] Connecting to Hub...");
        spoke1.Connect("tcp://127.0.0.1:5564");
        Thread.Sleep(200); // Allow monitor to process ACCEPTED event

        Console.WriteLine("[Spoke2] Connecting to Hub...");
        spoke2.Connect("tcp://127.0.0.1:5564");
        Thread.Sleep(200); // Allow monitor to process ACCEPTED event

        // Spokes send registration messages to Hub
        Console.WriteLine();
        Console.WriteLine("--- Message Exchange ---");
        spoke1.Send(Encoding.UTF8.GetBytes("HUB"), SendFlags.SendMore);
        spoke1.Send("Hello from SPOKE1");

        spoke2.Send(Encoding.UTF8.GetBytes("HUB"), SendFlags.SendMore);
        spoke2.Send("Hello from SPOKE2");

        Thread.Sleep(100);

        // Hub receives messages
        var registeredSpokes = new List<string>();
        for (int i = 0; i < 2; i++)
        {
            var spokeId = Encoding.UTF8.GetString(hub.RecvBytes());
            hub.HasMore.Should(true, "Expected message frame");
            var message = hub.RecvString();
            registeredSpokes.Add(spokeId);
            Console.WriteLine($"[Hub] Received from [{spokeId}]: {message}");
        }

        // Hub broadcasts to all spokes
        Console.WriteLine();
        Console.WriteLine("[Hub] Broadcasting to all spokes...");
        foreach (var spokeId in registeredSpokes)
        {
            hub.Send(Encoding.UTF8.GetBytes(spokeId), SendFlags.SendMore);
            hub.Send($"Welcome {spokeId}!");
        }

        // Spokes receive broadcasts
        foreach (var (spoke, name) in new[] { (spoke1, "SPOKE1"), (spoke2, "SPOKE2") })
        {
            var from = Encoding.UTF8.GetString(spoke.RecvBytes());
            spoke.HasMore.Should(true, "Expected message frame");
            var msg = spoke.RecvString();
            Console.WriteLine($"[{name}] received: {msg}");
        }

        // Disconnect spoke1 - this will generate a DISCONNECTED event
        Console.WriteLine();
        Console.WriteLine("[Spoke1] Disconnecting...");
        spoke1.Dispose();
        Thread.Sleep(300); // Allow monitor to process DISCONNECTED event

        // Stop monitoring - this will generate a MONITOR_STOPPED event
        Console.WriteLine();
        Console.WriteLine("[Monitor] Stopping...");
        monitorCts.Cancel();
        Thread.Sleep(200);

        // Wait for monitor thread to finish
        monitorThread.Join(1000);
    }

    /// <summary>
    /// Monitor thread that continuously receives and displays socket events
    /// </summary>
    static void MonitorEventsThread(Socket monitor, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Receive monitor event (two-frame message)
                    // Frame 1: 6 bytes (event type: uint16 LE + value: int32 LE)
                    // Frame 2: endpoint address string
                    var eventFrame = monitor.RecvBytes();
                    if (!monitor.HasMore)
                    {
                        // Invalid monitor message format
                        continue;
                    }

                    var address = monitor.RecvString();

                    // Parse the event data
                    var eventData = SocketMonitorEventData.Parse(eventFrame, address);

                    // Display the event with formatted output
                    PrintMonitorEvent(eventData);
                }
                catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN
                {
                    // Timeout - continue polling
                    Thread.Sleep(10);
                }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or ZmqException)
        {
            // Socket was closed or monitoring stopped - exit gracefully
        }
    }

    /// <summary>
    /// Formats and prints a monitor event with timestamp and event-specific styling
    /// </summary>
    static void PrintMonitorEvent(SocketMonitorEventData eventData)
    {
        // Filter out unknown events (like 4096 which might be ZMQ_EVENT_HANDSHAKE_SUCCEEDED)
        // We only want to display the events defined in SocketMonitorEvent enum
        if (!Enum.IsDefined(typeof(SocketMonitorEvent), eventData.Event) || eventData.Event == SocketMonitorEvent.None)
        {
            return; // Skip unknown events
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var eventName = eventData.Event.ToString().ToUpperInvariant();
        var icon = GetEventIcon(eventData.Event);
        var color = GetEventColor(eventData.Event);

        Console.ForegroundColor = color;
        Console.WriteLine($"[{timestamp}] {icon} EVENT: {eventName}");
        Console.WriteLine($"           Address: {eventData.Address}");

        if (IsErrorEvent(eventData.Event))
        {
            Console.WriteLine($"           Error Code: {eventData.Value}");
        }
        else if (eventData.Event is SocketMonitorEvent.Connected or SocketMonitorEvent.Accepted)
        {
            Console.WriteLine($"           FD: {eventData.Value}");
        }

        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// Returns a visual icon for different event types
    /// </summary>
    static string GetEventIcon(SocketMonitorEvent eventType)
    {
        return eventType switch
        {
            SocketMonitorEvent.Listening => "▶",
            SocketMonitorEvent.Accepted => "✓",
            SocketMonitorEvent.Connected => "↗",
            SocketMonitorEvent.Disconnected => "✗",
            SocketMonitorEvent.Closed => "⊗",
            SocketMonitorEvent.ConnectDelayed => "⏸",
            SocketMonitorEvent.ConnectRetried => "↻",
            SocketMonitorEvent.BindFailed => "✗",
            SocketMonitorEvent.AcceptFailed => "✗",
            SocketMonitorEvent.CloseFailed => "✗",
            SocketMonitorEvent.MonitorStopped => "■",
            _ => "•"
        };
    }

    /// <summary>
    /// Returns a console color for different event types
    /// </summary>
    static ConsoleColor GetEventColor(SocketMonitorEvent eventType)
    {
        return eventType switch
        {
            SocketMonitorEvent.Listening => ConsoleColor.Green,
            SocketMonitorEvent.Accepted => ConsoleColor.Green,
            SocketMonitorEvent.Connected => ConsoleColor.Green,
            SocketMonitorEvent.Disconnected => ConsoleColor.Yellow,
            SocketMonitorEvent.Closed => ConsoleColor.Yellow,
            SocketMonitorEvent.ConnectDelayed => ConsoleColor.DarkYellow,
            SocketMonitorEvent.ConnectRetried => ConsoleColor.DarkYellow,
            SocketMonitorEvent.BindFailed => ConsoleColor.Red,
            SocketMonitorEvent.AcceptFailed => ConsoleColor.Red,
            SocketMonitorEvent.CloseFailed => ConsoleColor.Red,
            SocketMonitorEvent.MonitorStopped => ConsoleColor.Cyan,
            _ => ConsoleColor.White
        };
    }

    /// <summary>
    /// Checks if the event is an error event
    /// </summary>
    static bool IsErrorEvent(SocketMonitorEvent eventType)
    {
        return eventType is SocketMonitorEvent.BindFailed
            or SocketMonitorEvent.AcceptFailed
            or SocketMonitorEvent.CloseFailed;
    }
}

// Simple assertion helper
static class AssertExtensions
{
    public static void Should(this bool actual, bool expected, string message)
    {
        if (actual != expected)
            throw new Exception($"Assertion failed: {message}. Expected {expected}, got {actual}");
    }
}
