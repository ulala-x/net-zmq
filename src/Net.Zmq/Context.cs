using System.Runtime.InteropServices;
using Net.Zmq.Core.Native;
using Net.Zmq.Core.SafeHandles;

namespace Net.Zmq;

/// <summary>
/// ZeroMQ context. Equivalent to cppzmq's context_t.
/// Thread-safe. Manages I/O threads and socket lifecycle.
/// </summary>
public sealed class Context : IDisposable
{
    private readonly ZmqContextHandle _handle;
    private bool _disposed;

    /// <summary>
    /// Creates a new ZMQ context with default settings.
    /// </summary>
    public Context()
    {
        var ptr = LibZmq.CtxNew();
        ZmqException.ThrowIfNull(ptr);
        _handle = new ZmqContextHandle(ptr, true);
    }

    /// <summary>
    /// Creates a new ZMQ context with specified I/O threads and max sockets.
    /// </summary>
    /// <param name="ioThreads">Number of I/O threads for the context.</param>
    /// <param name="maxSockets">Maximum number of sockets allowed.</param>
    public Context(int ioThreads, int maxSockets = 1023) : this()
    {
        SetOption(ContextOption.IoThreads, ioThreads);
        SetOption(ContextOption.MaxSockets, maxSockets);
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>
    /// Gets a context option value.
    /// </summary>
    /// <param name="option">The option to retrieve.</param>
    /// <returns>The option value.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int GetOption(ContextOption option)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = LibZmq.CtxGet(Handle, (int)option);
        if (result == -1)
        {
            ZmqException.ThrowIfError(-1);
        }
        return result;
    }

    /// <summary>
    /// Sets a context option value.
    /// </summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SetOption(ContextOption option, int value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = LibZmq.CtxSet(Handle, (int)option, value);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Shuts down the context.
    /// </summary>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Shutdown()
    {
        if (!_disposed)
        {
            var result = LibZmq.CtxShutdown(Handle);
            ZmqException.ThrowIfError(result);
        }
    }

    /// <summary>
    /// Checks if a capability is available.
    /// </summary>
    /// <param name="capability">The capability name to check.</param>
    /// <returns>True if the capability is available; otherwise, false.</returns>
    public static bool Has(string capability) => LibZmq.Has(capability) == 1;

    /// <summary>
    /// Gets the ZMQ library version.
    /// </summary>
    public static (int Major, int Minor, int Patch) Version
    {
        get
        {
            LibZmq.Version(out int major, out int minor, out int patch);
            return (major, minor, patch);
        }
    }

    /// <summary>
    /// Disposes the context and releases all associated resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _handle.Dispose();
            _disposed = true;
        }
    }
}
