using System.Text;

namespace Net.Zmq;

/// <summary>
/// Async extension methods for Socket operations.
/// Uses polling to provide async/await support for ZeroMQ sockets.
/// </summary>
/// <remarks>
/// ZeroMQ sockets are not compatible with .NET's native async I/O model.
/// These methods use polling with short timeouts to provide async behavior
/// without blocking the calling thread.
/// </remarks>
public static class SocketAsyncExtensions
{
    /// <summary>
    /// Default poll interval in milliseconds for async operations.
    /// This interval is used between poll attempts to avoid busy-waiting.
    /// </summary>
    private const int DefaultPollIntervalMs = 10;

    #region Send Async

    /// <summary>
    /// Asynchronously sends a byte array on the socket.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the number of bytes sent.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket or data is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// It will continue polling until the socket is ready to send or the operation is cancelled.
    /// </remarks>
    public static ValueTask<int> SendAsync(this Socket socket, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(data);

        return SendAsync(socket, data.AsMemory(), cancellationToken);
    }

    /// <summary>
    /// Asynchronously sends a ReadOnlyMemory of bytes on the socket.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the number of bytes sent.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// It will continue polling until the socket is ready to send or the operation is cancelled.
    /// </remarks>
    public static async ValueTask<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        // Fast path: Try non-blocking send first
        if (socket.TrySend(data.Span, SendFlags.None))
        {
            return data.Length;
        }

        // Slow path: Poll until ready
        return await Task.Run(() =>
        {
            var pollItems = new PollItem[1];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pollItems[0] = new PollItem(socket, PollEvents.Out);

                // Poll with short timeout to check if socket is ready to send
                if (Poller.Poll(pollItems, DefaultPollIntervalMs) > 0 && pollItems[0].IsWritable)
                {
                    return socket.Send(data.Span);
                }

                // Small delay to avoid busy-waiting
                Thread.Sleep(1);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously sends a UTF-8 string on the socket.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="text">The text to send.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the number of bytes sent.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket or text is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// It will continue polling until the socket is ready to send or the operation is cancelled.
    /// The string is UTF-8 encoded before sending.
    /// </remarks>
    public static async ValueTask<int> SendAsync(this Socket socket, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(text);

        // Fast path: Try non-blocking send first
        if (socket.TrySend(text, SendFlags.None))
        {
            return Encoding.UTF8.GetByteCount(text);
        }

        // Slow path: Poll until ready
        return await Task.Run(() =>
        {
            var pollItems = new PollItem[1];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pollItems[0] = new PollItem(socket, PollEvents.Out);

                // Poll with short timeout to check if socket is ready to send
                if (Poller.Poll(pollItems, DefaultPollIntervalMs) > 0 && pollItems[0].IsWritable)
                {
                    return socket.Send(text);
                }

                // Small delay to avoid busy-waiting
                Thread.Sleep(1);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Receive Async

    /// <summary>
    /// Asynchronously receives data as a byte array from the socket.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous receive operation. The task result contains the received data.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// It will continue polling until the socket has data available or the operation is cancelled.
    /// </remarks>
    public static async ValueTask<byte[]> RecvBytesAsync(this Socket socket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        // Fast path: Try non-blocking receive first
        if (socket.TryRecvBytes(out var data))
        {
            return data!;
        }

        // Slow path: Poll until ready
        return await Task.Run(() =>
        {
            var pollItems = new PollItem[1];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pollItems[0] = new PollItem(socket, PollEvents.In);

                // Poll with short timeout to check if socket has data available
                if (Poller.Poll(pollItems, DefaultPollIntervalMs) > 0 && pollItems[0].IsReadable)
                {
                    return socket.RecvBytes();
                }

                // Small delay to avoid busy-waiting
                Thread.Sleep(1);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously receives a UTF-8 string from the socket.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous receive operation. The task result contains the received string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// It will continue polling until the socket has data available or the operation is cancelled.
    /// The received data is UTF-8 decoded into a string.
    /// </remarks>
    public static async ValueTask<string> RecvStringAsync(this Socket socket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        // Fast path: Try non-blocking receive first
        if (socket.TryRecvString(out var text))
        {
            return text!;
        }

        // Slow path: Poll until ready
        return await Task.Run(() =>
        {
            var pollItems = new PollItem[1];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pollItems[0] = new PollItem(socket, PollEvents.In);

                // Poll with short timeout to check if socket has data available
                if (Poller.Poll(pollItems, DefaultPollIntervalMs) > 0 && pollItems[0].IsReadable)
                {
                    return socket.RecvString();
                }

                // Small delay to avoid busy-waiting
                Thread.Sleep(1);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Multipart Async

    /// <summary>
    /// Asynchronously sends a multipart message on the socket.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="message">The multipart message to send.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket or message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the message is empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// All frames except the last are sent with the SendMore flag.
    /// The operation is atomic - if any frame fails to send, the entire operation fails.
    /// </remarks>
    public static async ValueTask SendMultipartAsync(this Socket socket, MultipartMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(message);

        if (message.IsEmpty)
            throw new InvalidOperationException("Cannot send empty multipart message");

        await Task.Run(() =>
        {
            var pollItems = new PollItem[1];
            var count = message.Count;

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var flags = (i < count - 1) ? SendFlags.SendMore : SendFlags.None;
                var frame = message[i];

                // Try non-blocking send first
                bool sent = false;
                unsafe
                {
                    fixed (byte* ptr = frame.Data)
                    {
                        var sendFlags = flags | SendFlags.DontWait;
                        var result = Core.Native.LibZmq.Send(socket.Ref.Handle, (nint)ptr, (nuint)frame.Size, (int)sendFlags);
                        if (result != -1)
                        {
                            sent = true;
                        }
                        else
                        {
                            var errno = Core.Native.LibZmq.Errno();
                            if (errno != Core.Native.ZmqConstants.EAGAIN)
                            {
                                ZmqException.ThrowIfError(-1);
                            }
                        }
                    }
                }

                // If non-blocking send failed, poll until ready
                if (!sent)
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        pollItems[0] = new PollItem(socket, PollEvents.Out);

                        // Poll with short timeout
                        if (Poller.Poll(pollItems, DefaultPollIntervalMs) > 0 && pollItems[0].IsWritable)
                        {
                            socket.Send(frame, flags);
                            break;
                        }

                        // Small delay to avoid busy-waiting
                        Thread.Sleep(1);
                    }
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously receives a complete multipart message from the socket.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous receive operation. The task result contains the received multipart message.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// This method uses polling internally to avoid blocking the calling thread.
    /// It will continue polling until the socket has data available or the operation is cancelled.
    /// The returned MultipartMessage must be disposed by the caller to free resources.
    /// Once the first frame is received, all subsequent frames are expected to be immediately available.
    /// </remarks>
    public static async ValueTask<MultipartMessage> RecvMultipartAsync(this Socket socket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        // Fast path: Try non-blocking receive first
        if (socket.TryRecvMultipart(out var multipart))
        {
            return multipart!;
        }

        // Slow path: Poll until first frame is ready
        return await Task.Run(() =>
        {
            var pollItems = new PollItem[1];

            // Wait for the first frame
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pollItems[0] = new PollItem(socket, PollEvents.In);

                // Poll with short timeout
                if (Poller.Poll(pollItems, DefaultPollIntervalMs) > 0 && pollItems[0].IsReadable)
                {
                    // First frame is available, receive the complete multipart message
                    // (remaining frames should be immediately available)
                    return socket.RecvMultipart();
                }

                // Small delay to avoid busy-waiting
                Thread.Sleep(1);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
