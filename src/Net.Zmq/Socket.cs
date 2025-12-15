using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Net.Zmq.Core.Native;
using Net.Zmq.Core.SafeHandles;

namespace Net.Zmq;

/// <summary>
/// ZeroMQ socket. Equivalent to cppzmq's socket_t.
/// </summary>
public sealed class Socket : IDisposable
{
    private readonly ZmqSocketHandle _handle;
    private bool _disposed;

    /// <summary>
    /// Creates a new ZMQ socket with the specified type.
    /// </summary>
    /// <param name="context">The context to create the socket in.</param>
    /// <param name="socketType">The type of socket to create.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public Socket(Context context, SocketType socketType)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ptr = LibZmq.Socket(context.Handle, (int)socketType);
        ZmqException.ThrowIfNull(ptr);
        _handle = new ZmqSocketHandle(ptr, true);
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
    /// Gets a non-owning reference to this socket.
    /// </summary>
    public SocketRef Ref => new(Handle);

    /// <summary>
    /// Gets a value indicating whether there are more message parts to receive.
    /// </summary>
    public bool HasMore => GetOption<int>(SocketOption.Rcvmore) != 0;

    #region Bind/Connect/Unbind/Disconnect

    /// <summary>
    /// Binds the socket to an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to bind to.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Bind(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(endpoint);
        var result = LibZmq.Bind(Handle, endpoint);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Connects the socket to an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to connect to.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Connect(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(endpoint);
        var result = LibZmq.Connect(Handle, endpoint);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Unbinds the socket from an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to unbind from.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Unbind(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(endpoint);
        var result = LibZmq.Unbind(Handle, endpoint);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Disconnects the socket from an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to disconnect from.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Disconnect(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(endpoint);
        var result = LibZmq.Disconnect(Handle, endpoint);
        ZmqException.ThrowIfError(result);
    }

    #endregion

    #region Send Methods

    /// <summary>
    /// Sends a byte array on the socket.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="flags">Send flags.</param>
    /// <returns>The number of bytes sent.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Send(byte[] data, SendFlags flags = SendFlags.None)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Send(data.AsSpan(), flags);
    }

    /// <summary>
    /// Sends a span of bytes on the socket.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="flags">Send flags.</param>
    /// <returns>The number of bytes sent.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Send(ReadOnlySpan<byte> data, SendFlags flags = SendFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var result = LibZmq.Send(Handle, (nint)ptr, (nuint)data.Length, (int)flags);
                ZmqException.ThrowIfError(result);
                return result;
            }
        }
    }

    /// <summary>
    /// Sends a UTF-8 string on the socket.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <param name="flags">Send flags.</param>
    /// <returns>The number of bytes sent.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Send(string text, SendFlags flags = SendFlags.None)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Fast path for small strings using stackalloc
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
        if (maxByteCount <= 512)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            var actualByteCount = Encoding.UTF8.GetBytes(text, buffer);
            return Send(buffer.Slice(0, actualByteCount), flags);
        }

        // Slow path for large strings using ArrayPool
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var actualByteCount = Encoding.UTF8.GetBytes(text, rentedBuffer);
            return Send(rentedBuffer.AsSpan(0, actualByteCount), flags);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Sends a message on the socket.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="flags">Send flags.</param>
    /// <returns>The number of bytes sent.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Send(Message message, SendFlags flags = SendFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return message.Send(Handle, flags);
    }

    /// <summary>
    /// Tries to send a byte array on the socket without blocking.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="flags">Send flags (DontWait is added automatically).</param>
    /// <returns>True if the data was sent; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TrySend(byte[] data, SendFlags flags = SendFlags.None)
    {
        ArgumentNullException.ThrowIfNull(data);
        return TrySend(data.AsSpan(), flags);
    }

    /// <summary>
    /// Tries to send a span of bytes on the socket without blocking.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="flags">Send flags (DontWait is added automatically).</param>
    /// <returns>True if the data was sent; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TrySend(ReadOnlySpan<byte> data, SendFlags flags = SendFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        flags |= SendFlags.DontWait;

        unsafe
        {
            fixed (byte* ptr = data)
            {
                var result = LibZmq.Send(Handle, (nint)ptr, (nuint)data.Length, (int)flags);
                if (result == -1)
                {
                    var errno = LibZmq.Errno();
                    if (errno == ZmqConstants.EAGAIN)
                        return false;
                    ZmqException.ThrowIfError(-1);
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Tries to send a UTF-8 string on the socket without blocking.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <param name="flags">Send flags (DontWait is added automatically).</param>
    /// <returns>True if the data was sent; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TrySend(string text, SendFlags flags = SendFlags.None)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Fast path for small strings using stackalloc
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
        if (maxByteCount <= 512)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            var actualByteCount = Encoding.UTF8.GetBytes(text, buffer);
            return TrySend(buffer.Slice(0, actualByteCount), flags);
        }

        // Slow path for large strings using ArrayPool
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var actualByteCount = Encoding.UTF8.GetBytes(text, rentedBuffer);
            return TrySend(rentedBuffer.AsSpan(0, actualByteCount), flags);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    #endregion

    #region Receive Methods

    /// <summary>
    /// Receives data into a byte array.
    /// </summary>
    /// <param name="buffer">The buffer to receive data into.</param>
    /// <param name="flags">Receive flags.</param>
    /// <returns>The number of bytes received.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Recv(byte[] buffer, RecvFlags flags = RecvFlags.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Recv(buffer.AsSpan(), flags);
    }

    /// <summary>
    /// Receives data into a span.
    /// </summary>
    /// <param name="buffer">The buffer to receive data into.</param>
    /// <param name="flags">Receive flags.</param>
    /// <returns>The number of bytes received.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Recv(Span<byte> buffer, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var result = LibZmq.Recv(Handle, (nint)ptr, (nuint)buffer.Length, (int)flags);
                ZmqException.ThrowIfError(result);
                return result;
            }
        }
    }

    /// <summary>
    /// Receives a UTF-8 string.
    /// </summary>
    /// <param name="flags">Receive flags.</param>
    /// <returns>The received string.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public string RecvString(RecvFlags flags = RecvFlags.None)
    {
        var msg = MessagePool.Rent();
        try
        {
            msg.Recv(Handle, flags);
            return msg.ToString();
        }
        finally
        {
            MessagePool.Return(msg);
        }
    }

    /// <summary>
    /// Receives a message.
    /// </summary>
    /// <param name="message">The message to receive into.</param>
    /// <param name="flags">Receive flags.</param>
    /// <returns>The number of bytes received.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int Recv(Message message, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return message.Recv(Handle, flags);
    }

    /// <summary>
    /// Tries to receive data into a byte array without blocking.
    /// </summary>
    /// <param name="buffer">The buffer to receive data into.</param>
    /// <param name="bytesReceived">The number of bytes received.</param>
    /// <param name="flags">Receive flags (DontWait is added automatically).</param>
    /// <returns>True if data was received; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TryRecv(byte[] buffer, out int bytesReceived, RecvFlags flags = RecvFlags.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return TryRecv(buffer.AsSpan(), out bytesReceived, flags);
    }

    /// <summary>
    /// Tries to receive data into a span without blocking.
    /// </summary>
    /// <param name="buffer">The buffer to receive data into.</param>
    /// <param name="bytesReceived">The number of bytes received.</param>
    /// <param name="flags">Receive flags (DontWait is added automatically).</param>
    /// <returns>True if data was received; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TryRecv(Span<byte> buffer, out int bytesReceived, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        flags |= RecvFlags.DontWait;

        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var result = LibZmq.Recv(Handle, (nint)ptr, (nuint)buffer.Length, (int)flags);
                if (result == -1)
                {
                    var errno = LibZmq.Errno();
                    if (errno == ZmqConstants.EAGAIN)
                    {
                        bytesReceived = 0;
                        return false;
                    }
                    ZmqException.ThrowIfError(-1);
                }
                bytesReceived = result;
                return true;
            }
        }
    }

    /// <summary>
    /// Tries to receive a UTF-8 string without blocking.
    /// </summary>
    /// <param name="text">The received string.</param>
    /// <param name="flags">Receive flags (DontWait is added automatically).</param>
    /// <returns>True if data was received; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TryRecvString(out string? text, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        flags |= RecvFlags.DontWait;
        var msg = MessagePool.Rent();
        try
        {
            var result = msg.Recv(Handle, flags);
            text = msg.ToString();
            return true;
        }
        catch (ZmqException ex) when (ex.ErrorNumber == ZmqConstants.EAGAIN)
        {
            text = null;
            return false;
        }
        finally
        {
            MessagePool.Return(msg);
        }
    }

    /// <summary>
    /// Receives data as a byte array.
    /// </summary>
    /// <param name="flags">Receive flags.</param>
    /// <returns>The received data as a byte array.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public byte[] RecvBytes(RecvFlags flags = RecvFlags.None)
    {
        var msg = MessagePool.Rent();
        try
        {
            Recv(msg, flags);
            return msg.ToArray();
        }
        finally
        {
            MessagePool.Return(msg);
        }
    }

    /// <summary>
    /// Tries to receive data as a byte array without blocking.
    /// </summary>
    /// <param name="data">The received data.</param>
    /// <param name="flags">Receive flags (DontWait is added automatically).</param>
    /// <returns>True if data was received; false if the operation would block.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    public bool TryRecvBytes(out byte[]? data, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        flags |= RecvFlags.DontWait;
        var msg = MessagePool.Rent();
        try
        {
            msg.Recv(Handle, flags);
            data = msg.ToArray();
            return true;
        }
        catch (ZmqException ex) when (ex.ErrorNumber == ZmqConstants.EAGAIN)
        {
            data = null;
            return false;
        }
        finally
        {
            MessagePool.Return(msg);
        }
    }

    #endregion

    #region Socket Options

    /// <summary>
    /// Gets an integer socket option.
    /// </summary>
    /// <typeparam name="T">The type of the option value (int or long).</typeparam>
    /// <param name="option">The option to retrieve.</param>
    /// <returns>The option value.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public T GetOption<T>(SocketOption option) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (typeof(T) == typeof(int))
        {
            int value = 0;
            nuint size = sizeof(int);
            unsafe
            {
                var result = LibZmq.GetSockOpt(Handle, (int)option, (nint)(&value), ref size);
                ZmqException.ThrowIfError(result);
            }
            return (T)(object)value;
        }
        else if (typeof(T) == typeof(long))
        {
            long value = 0;
            nuint size = sizeof(long);
            unsafe
            {
                var result = LibZmq.GetSockOpt(Handle, (int)option, (nint)(&value), ref size);
                ZmqException.ThrowIfError(result);
            }
            return (T)(object)value;
        }
        else if (typeof(T) == typeof(nint))
        {
            nint value = 0;
            nuint size = (nuint)nint.Size;
            unsafe
            {
                var result = LibZmq.GetSockOpt(Handle, (int)option, (nint)(&value), ref size);
                ZmqException.ThrowIfError(result);
            }
            return (T)(object)value;
        }
        else
        {
            throw new ArgumentException($"Unsupported type: {typeof(T)}");
        }
    }

    /// <summary>
    /// Gets a byte array socket option.
    /// </summary>
    /// <param name="option">The option to retrieve.</param>
    /// <param name="buffer">The buffer to receive the option value.</param>
    /// <returns>The actual size of the option value.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int GetOption(SocketOption option, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        nuint size = (nuint)buffer.Length;
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var result = LibZmq.GetSockOpt(Handle, (int)option, (nint)ptr, ref size);
                ZmqException.ThrowIfError(result);
            }
        }
        return (int)size;
    }

    /// <summary>
    /// Gets a string socket option.
    /// </summary>
    /// <param name="option">The option to retrieve.</param>
    /// <returns>The option value as a string.</returns>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public string GetOptionString(SocketOption option)
    {
        Span<byte> buffer = stackalloc byte[256];
        ObjectDisposedException.ThrowIf(_disposed, this);

        nuint size = (nuint)buffer.Length;
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var result = LibZmq.GetSockOpt(Handle, (int)option, (nint)ptr, ref size);
                ZmqException.ThrowIfError(result);
            }
        }

        // Exclude null terminator
        var actualSize = (int)size - 1;
        return Encoding.UTF8.GetString(buffer.Slice(0, actualSize));
    }

    /// <summary>
    /// Sets an integer socket option.
    /// </summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SetOption(SocketOption option, int value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            var result = LibZmq.SetSockOpt(Handle, (int)option, (nint)(&value), sizeof(int));
            ZmqException.ThrowIfError(result);
        }
    }

    /// <summary>
    /// Sets a long socket option.
    /// </summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SetOption(SocketOption option, long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            var result = LibZmq.SetSockOpt(Handle, (int)option, (nint)(&value), sizeof(long));
            ZmqException.ThrowIfError(result);
        }
    }

    /// <summary>
    /// Sets a byte array socket option.
    /// </summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SetOption(SocketOption option, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ObjectDisposedException.ThrowIf(_disposed, this);

        unsafe
        {
            fixed (byte* ptr = value)
            {
                var result = LibZmq.SetSockOpt(Handle, (int)option, (nint)ptr, (nuint)value.Length);
                ZmqException.ThrowIfError(result);
            }
        }
    }

    /// <summary>
    /// Sets a string socket option.
    /// </summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SetOption(SocketOption option, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // For empty strings, use the original implementation via byte array
        // Some ZMQ options may not accept zero-length values
        if (value.Length == 0)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            SetOption(option, bytes);
            return;
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path for small strings using stackalloc
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        if (maxByteCount <= 256)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            var actualByteCount = Encoding.UTF8.GetBytes(value, buffer);

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    var result = LibZmq.SetSockOpt(Handle, (int)option, (nint)ptr, (nuint)actualByteCount);
                    ZmqException.ThrowIfError(result);
                }
            }
            return;
        }

        // Slow path for large strings using ArrayPool
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var actualByteCount = Encoding.UTF8.GetBytes(value, rentedBuffer);
            unsafe
            {
                fixed (byte* ptr = rentedBuffer)
                {
                    var result = LibZmq.SetSockOpt(Handle, (int)option, (nint)ptr, (nuint)actualByteCount);
                    ZmqException.ThrowIfError(result);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    #endregion

    #region Subscribe/Unsubscribe

    /// <summary>
    /// Subscribes to all messages (SUB socket).
    /// </summary>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SubscribeAll()
    {
        SetOption(SocketOption.Subscribe, Array.Empty<byte>());
    }

    /// <summary>
    /// Subscribes to messages with a specific prefix (SUB socket).
    /// </summary>
    /// <param name="prefix">The prefix to subscribe to.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Subscribe(byte[] prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        SetOption(SocketOption.Subscribe, prefix);
    }

    /// <summary>
    /// Subscribes to messages with a specific string prefix (SUB socket).
    /// </summary>
    /// <param name="prefix">The prefix to subscribe to.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Subscribe(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        SetOption(SocketOption.Subscribe, prefix);
    }

    /// <summary>
    /// Unsubscribes from all messages (SUB socket).
    /// </summary>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void UnsubscribeAll()
    {
        SetOption(SocketOption.Unsubscribe, Array.Empty<byte>());
    }

    /// <summary>
    /// Unsubscribes from messages with a specific prefix (SUB socket).
    /// </summary>
    /// <param name="prefix">The prefix to unsubscribe from.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Unsubscribe(byte[] prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        SetOption(SocketOption.Unsubscribe, prefix);
    }

    /// <summary>
    /// Unsubscribes from messages with a specific string prefix (SUB socket).
    /// </summary>
    /// <param name="prefix">The prefix to unsubscribe from.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Unsubscribe(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        SetOption(SocketOption.Unsubscribe, prefix);
    }

    #endregion

    #region Monitor

    /// <summary>
    /// Starts monitoring socket events.
    /// </summary>
    /// <param name="endpoint">The inproc endpoint to publish events on. Pass null to stop monitoring.</param>
    /// <param name="events">The events to monitor (default: all events).</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Monitor(string? endpoint, int events = ZmqConstants.ZMQ_EVENT_ALL)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = LibZmq.SocketMonitor(Handle, endpoint, events);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Starts monitoring socket events.
    /// </summary>
    /// <param name="endpoint">The inproc endpoint to publish events on. Pass null to stop monitoring.</param>
    /// <param name="events">The events to monitor (default: all events).</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Monitor(string? endpoint, SocketMonitorEvent events = SocketMonitorEvent.All)
    {
        Monitor(endpoint, (int)events);
    }

    #endregion

    /// <summary>
    /// Disposes the socket and releases all associated resources.
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
