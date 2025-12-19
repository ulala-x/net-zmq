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
    private nint _recvBufferPtr = nint.Zero;
    private const int MaxRecvBufferSize = 4 * 1024 * 1024;  // 4 MB

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
        _recvBufferPtr = Marshal.AllocHGlobal(MaxRecvBufferSize);
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
    /// <param name="flags">Send flags. If DontWait flag is set and socket would block (EAGAIN), returns -1 instead of throwing.</param>
    /// <returns>
    /// The number of bytes sent, or -1 if DontWait flag was set and socket would block (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public int Send(ReadOnlySpan<byte> data, SendFlags flags = SendFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var result = LibZmq.Send(Handle, (nint)ptr, (nuint)data.Length, (int)flags);
                if (result == -1)
                {
                    var errno = LibZmq.Errno();
                    // Only suppress EAGAIN if DontWait flag is set
                    if (errno == ZmqConstants.EAGAIN && (flags & SendFlags.DontWait) != 0)
                        return -1;

                    // For all other errors, or EAGAIN without DontWait, throw
                    ZmqException.ThrowIfError(-1);
                }
                return result;
            }
        }
    }

    /// <summary>
    /// Sends data from a native memory buffer to the socket.
    /// This method is optimized for native memory and avoids the pinning overhead of managed memory.
    /// </summary>
    /// <param name="data">Pointer to the native memory buffer containing the data to send.</param>
    /// <param name="size">Size of the data in bytes.</param>
    /// <param name="flags">Send flags. If DontWait flag is set and socket would block (EAGAIN), returns -1 instead of throwing.</param>
    /// <returns>
    /// The number of bytes sent, or -1 if DontWait flag was set and socket would block (EAGAIN).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is negative.</exception>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public int Send(nint data, int size, SendFlags flags = SendFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        var result = LibZmq.Send(Handle, data, (nuint)size, (int)flags);
        if (result == -1)
        {
            var errno = LibZmq.Errno();
            // Only suppress EAGAIN if DontWait flag is set
            if (errno == ZmqConstants.EAGAIN && (flags & SendFlags.DontWait) != 0)
                return -1;

            // For all other errors, or EAGAIN without DontWait, throw
            ZmqException.ThrowIfError(-1);
        }
        return result;
    }

    /// <summary>
    /// Sends a UTF-8 string on the socket.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <param name="flags">Send flags. If DontWait flag is set and socket would block (EAGAIN), returns -1 instead of throwing.</param>
    /// <returns>
    /// The number of bytes sent, or -1 if DontWait flag was set and socket would block (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
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

    #endregion

    #region Receive Methods

    /// <summary>
    /// Receives data into a byte array.
    /// </summary>
    /// <param name="buffer">The buffer to receive data into.</param>
    /// <param name="flags">Receive flags. If DontWait flag is set and no message is available (EAGAIN), returns -1 instead of throwing.</param>
    /// <returns>
    /// The number of bytes received, or -1 if DontWait flag was set and no message is available (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public int Recv(byte[] buffer, RecvFlags flags = RecvFlags.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Recv(buffer.AsSpan(), flags);
    }

    /// <summary>
    /// Receives data into a span.
    /// </summary>
    /// <param name="buffer">The buffer to receive data into.</param>
    /// <param name="flags">Receive flags. If DontWait flag is set and no message is available (EAGAIN), returns -1 instead of throwing.</param>
    /// <returns>
    /// The number of bytes received, or -1 if DontWait flag was set and no message is available (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public int Recv(Span<byte> buffer, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var result = LibZmq.Recv(Handle, (nint)ptr, (nuint)buffer.Length, (int)flags);
                if (result == -1)
                {
                    var errno = LibZmq.Errno();
                    // Only suppress EAGAIN if DontWait flag is set
                    if (errno == ZmqConstants.EAGAIN && (flags & RecvFlags.DontWait) != 0)
                        return -1;

                    // For all other errors, or EAGAIN without DontWait, throw
                    ZmqException.ThrowIfError(-1);
                }
                return result;
            }
        }
    }

    /// <summary>
    /// Receives data from the socket into a native memory buffer.
    /// This method is optimized for native memory and avoids the pinning overhead of managed memory.
    /// </summary>
    /// <param name="buffer">Pointer to the native memory buffer.</param>
    /// <param name="size">Size of the buffer in bytes.</param>
    /// <param name="flags">Receive flags.</param>
    /// <returns>
    /// The number of bytes received, or -1 if the operation would block (only when DontWait flag is set).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is negative.</exception>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public int Recv(nint buffer, int size, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        var result = LibZmq.Recv(Handle, buffer, (nuint)size, (int)flags);
        if (result == -1)
        {
            var errno = LibZmq.Errno();
            // Only suppress EAGAIN if DontWait flag is set
            if (errno == ZmqConstants.EAGAIN && (flags & RecvFlags.DontWait) != 0)
                return -1;

            // For all other errors, or EAGAIN without DontWait, throw
            ZmqException.ThrowIfError(-1);
        }
        return result;
    }

    /// <summary>
    /// Receives a UTF-8 string.
    /// </summary>
    /// <param name="flags">Receive flags. If DontWait flag is set and no message is available (EAGAIN), returns null instead of throwing.</param>
    /// <returns>
    /// The received string, or null if DontWait flag was set and no message is available (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public string? RecvString(RecvFlags flags = RecvFlags.None)
    {
        using var msg = new Message();
        try
        {
            msg.Recv(Handle, flags);
            return msg.ToString();
        }
        catch (ZmqException ex) when (ex.ErrorNumber == ZmqConstants.EAGAIN && (flags & RecvFlags.DontWait) != 0)
        {
            return null;
        }
    }

    /// <summary>
    /// Receives a message.
    /// </summary>
    /// <param name="message">The message to receive into.</param>
    /// <param name="flags">Receive flags. If DontWait flag is set and no message is available (EAGAIN), returns -1 instead of throwing.</param>
    /// <returns>
    /// The number of bytes received, or -1 if DontWait flag was set and no message is available (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public int Recv(Message message, RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            return message.Recv(Handle, flags);
        }
        catch (ZmqException ex) when (ex.ErrorNumber == ZmqConstants.EAGAIN && (flags & RecvFlags.DontWait) != 0)
        {
            return -1;
        }
    }

    /// <summary>
    /// Receives data as a byte array.
    /// </summary>
    /// <param name="flags">Receive flags. If DontWait flag is set and no message is available (EAGAIN), returns null instead of throwing.</param>
    /// <returns>
    /// The received data as a byte array, or null if DontWait flag was set and no message is available (EAGAIN).
    /// </returns>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    public byte[]? RecvBytes(RecvFlags flags = RecvFlags.None)
    {
        using var msg = new Message();
        var result = Recv(msg, flags);
        if (result == -1)
            return null;
        return msg.ToArray();
    }

    /// <summary>
    /// MessagePool을 사용하여 최적화된 메모리 관리로 메시지를 수신합니다.
    /// 재사용 수신 버퍼를 사용하여 할당 오버헤드를 최소화합니다.
    /// </summary>
    /// <param name="flags">수신 플래그. If DontWait flag is set and no message is available (EAGAIN), returns null instead of throwing.</param>
    /// <returns>
    /// 풀링된 Message, or null if DontWait flag was set and no message is available (EAGAIN).
    /// 메시지는 Send()를 통해 전송 시 ZMQ callback으로 자동 반환됩니다.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Socket이 Dispose됨</exception>
    /// <exception cref="ZmqException">
    /// Thrown if the operation fails with an error other than EAGAIN.
    /// For blocking mode (without DontWait flag), EAGAIN also throws an exception as it indicates an abnormal state.
    /// </exception>
    /// <example>
    /// <code>
    /// // 케이스 1: 수신 후 다른 소켓으로 전송
    /// using (var msg = socket.ReceiveWithPool())
    /// {
    ///     otherSocket.Send(msg);  // ZMQ가 자동 반환
    /// }
    ///
    /// // 케이스 2: DontWait 플래그로 논블로킹 수신
    /// var msg = socket.ReceiveWithPool(RecvFlags.DontWait);
    /// if (msg == null)
    /// {
    ///     // No message available
    /// }
    /// else
    /// {
    ///     using (msg)
    ///     {
    ///         otherSocket.Send(msg);
    ///     }
    /// }
    /// </code>
    /// </example>
    public Message? ReceiveWithPool(RecvFlags flags = RecvFlags.None)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int actualSize = Recv(_recvBufferPtr, MaxRecvBufferSize, flags);
        if (actualSize == -1)
            return null;

        var msg = MessagePool.Shared.Rent(actualSize);
        msg.CopyFromNative(_recvBufferPtr, actualSize);
        return msg;
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
            if (_recvBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(_recvBufferPtr);
                _recvBufferPtr = nint.Zero;
            }
            _disposed = true;
        }
    }
}
