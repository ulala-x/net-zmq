using Net.Zmq.Core.Native;

namespace Net.Zmq;

/// <summary>
/// ZeroMQ proxy utilities.
/// </summary>
public static class Proxy
{
    /// <summary>
    /// Starts a built-in proxy connecting frontend and backend sockets.
    /// This function blocks until the context is terminated.
    /// </summary>
    public static void Start(Socket frontend, Socket backend, Socket? capture = null)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentNullException.ThrowIfNull(backend);

        var result = LibZmq.Proxy(
            frontend.Handle,
            backend.Handle,
            capture?.Handle ?? IntPtr.Zero
        );

        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Starts a steerable proxy connecting frontend and backend sockets.
    /// The control socket can receive commands to control the proxy:
    /// - "PAUSE": Pause the proxy (messages are queued)
    /// - "RESUME": Resume the proxy
    /// - "TERMINATE": Terminate the proxy
    /// - "STATISTICS": Request statistics (proxy sends back 8 uint64 values)
    /// </summary>
    /// <param name="frontend">The frontend socket.</param>
    /// <param name="backend">The backend socket.</param>
    /// <param name="control">The control socket (must be a PAIR, PUB, or SUB socket).</param>
    /// <param name="capture">Optional capture socket for message inspection.</param>
    public static void StartSteerable(Socket frontend, Socket backend, Socket control, Socket? capture = null)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(control);

        var result = LibZmq.ProxySteerable(
            frontend.Handle,
            backend.Handle,
            capture?.Handle ?? IntPtr.Zero,
            control.Handle
        );

        ZmqException.ThrowIfError(result);
    }
}
