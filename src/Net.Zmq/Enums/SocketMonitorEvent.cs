namespace Net.Zmq;

/// <summary>
/// Socket monitoring events that can be received from a monitored socket.
/// </summary>
[Flags]
public enum SocketMonitorEvent
{
    /// <summary>
    /// No event.
    /// </summary>
    None = 0,

    /// <summary>
    /// The socket has successfully connected to a remote peer.
    /// </summary>
    Connected = 1,

    /// <summary>
    /// A synchronous connect call was delayed.
    /// </summary>
    ConnectDelayed = 2,

    /// <summary>
    /// A connect call was retried.
    /// </summary>
    ConnectRetried = 4,

    /// <summary>
    /// The socket is listening for incoming connections.
    /// </summary>
    Listening = 8,

    /// <summary>
    /// The socket failed to bind to a local address.
    /// </summary>
    BindFailed = 16,

    /// <summary>
    /// The socket has accepted a connection from a remote peer.
    /// </summary>
    Accepted = 32,

    /// <summary>
    /// The socket failed to accept a connection.
    /// </summary>
    AcceptFailed = 64,

    /// <summary>
    /// The socket connection was closed.
    /// </summary>
    Closed = 128,

    /// <summary>
    /// The socket failed to close a connection.
    /// </summary>
    CloseFailed = 256,

    /// <summary>
    /// The socket was disconnected unexpectedly.
    /// </summary>
    Disconnected = 512,

    /// <summary>
    /// The socket monitoring has been stopped.
    /// </summary>
    MonitorStopped = 1024,

    /// <summary>
    /// All socket monitoring events.
    /// </summary>
    All = 0xFFFF
}
