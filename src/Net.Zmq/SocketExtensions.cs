using System.Text;

namespace Net.Zmq;

/// <summary>
/// Extension methods for Socket to handle multipart messages.
/// </summary>
public static class SocketExtensions
{
    #region SendMultipart

    /// <summary>
    /// Sends a multipart message on the socket.
    /// All frames except the last are sent with SendMore flag.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="message">The multipart message to send.</param>
    /// <exception cref="ArgumentNullException">Thrown if socket or message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the message is empty.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public static void SendMultipart(this Socket socket, MultipartMessage message)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(message);

        if (message.IsEmpty)
            throw new InvalidOperationException("Cannot send empty multipart message");

        var count = message.Count;
        for (int i = 0; i < count; i++)
        {
            var flags = (i < count - 1) ? SendFlags.SendMore : SendFlags.None;
            socket.Send(message[i], flags);
        }
    }

    /// <summary>
    /// Sends a collection of byte arrays as a multipart message.
    /// All frames except the last are sent with SendMore flag.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="frames">The collection of byte arrays to send.</param>
    /// <exception cref="ArgumentNullException">Thrown if socket or frames is null.</exception>
    /// <exception cref="ArgumentException">Thrown if frames is empty or contains null values.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public static void SendMultipart(this Socket socket, IEnumerable<byte[]> frames)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(frames);

        var frameList = frames.ToList();
        if (frameList.Count == 0)
            throw new ArgumentException("Frame collection cannot be empty", nameof(frames));

        if (frameList.Any(f => f == null))
            throw new ArgumentException("Frame collection cannot contain null values", nameof(frames));

        var count = frameList.Count;
        for (int i = 0; i < count; i++)
        {
            var flags = (i < count - 1) ? SendFlags.SendMore : SendFlags.None;
            socket.Send(frameList[i], flags);
        }
    }

    /// <summary>
    /// Sends an array of strings as a multipart message.
    /// Each string is UTF-8 encoded before sending.
    /// All frames except the last are sent with SendMore flag.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="frames">The array of strings to send.</param>
    /// <exception cref="ArgumentNullException">Thrown if socket or frames is null.</exception>
    /// <exception cref="ArgumentException">Thrown if frames is empty or contains null values.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public static void SendMultipart(this Socket socket, params string[] frames)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Length == 0)
            throw new ArgumentException("Frame array cannot be empty", nameof(frames));

        if (frames.Any(f => f == null))
            throw new ArgumentException("Frame array cannot contain null values", nameof(frames));

        var count = frames.Length;
        for (int i = 0; i < count; i++)
        {
            var flags = (i < count - 1) ? SendFlags.SendMore : SendFlags.None;
            socket.Send(frames[i], flags);
        }
    }

    /// <summary>
    /// Sends a collection of Message objects as a multipart message.
    /// All frames except the last are sent with SendMore flag.
    /// </summary>
    /// <param name="socket">The socket to send on.</param>
    /// <param name="messages">The collection of messages to send.</param>
    /// <exception cref="ArgumentNullException">Thrown if socket or messages is null.</exception>
    /// <exception cref="ArgumentException">Thrown if messages is empty or contains null values.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public static void SendMultipart(this Socket socket, IEnumerable<Message> messages)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages.ToList();
        if (messageList.Count == 0)
            throw new ArgumentException("Message collection cannot be empty", nameof(messages));

        if (messageList.Any(m => m == null))
            throw new ArgumentException("Message collection cannot contain null values", nameof(messages));

        var count = messageList.Count;
        for (int i = 0; i < count; i++)
        {
            var flags = (i < count - 1) ? SendFlags.SendMore : SendFlags.None;
            socket.Send(messageList[i], flags);
        }
    }

    #endregion

    #region RecvMultipart

    /// <summary>
    /// Receives a complete multipart message from the socket.
    /// This method blocks until all parts of the message are received.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <returns>A MultipartMessage containing all received frames.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket is null.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    /// <remarks>
    /// The returned MultipartMessage must be disposed by the caller to free resources.
    /// </remarks>
    public static MultipartMessage RecvMultipart(this Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var multipart = new MultipartMessage();
        try
        {
            do
            {
                var msg = new Message();
                try
                {
                    socket.Recv(msg);
                    multipart.Add(msg);
                }
                catch
                {
                    // If receive fails, dispose the message and rethrow
                    msg.Dispose();
                    throw;
                }
            }
            while (socket.HasMore);

            return multipart;
        }
        catch
        {
            // If anything goes wrong, dispose the multipart message and rethrow
            multipart.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Tries to receive a complete multipart message from the socket without blocking.
    /// </summary>
    /// <param name="socket">The socket to receive from.</param>
    /// <param name="message">The received multipart message, or null if no message is available.</param>
    /// <returns>True if a message was received; false if the operation would block.</returns>
    /// <exception cref="ArgumentNullException">Thrown if socket is null.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails with an error other than EAGAIN.</exception>
    /// <remarks>
    /// If this method returns true, the returned MultipartMessage must be disposed by the caller.
    /// Once the first frame is received, all subsequent frames are expected to be immediately available.
    /// </remarks>
    public static bool TryRecvMultipart(this Socket socket, out MultipartMessage? message)
    {
        ArgumentNullException.ThrowIfNull(socket);

        message = null;

        // Try to receive the first frame without blocking
        var firstFrame = socket.RecvBytes(RecvFlags.DontWait);
        if (firstFrame == null)
        {
            return false;
        }

        // First frame received successfully, now receive remaining frames
        var multipart = new MultipartMessage();
        try
        {
            // Add the first frame
            multipart.Add(firstFrame);

            // Receive remaining frames (they should be available immediately)
            while (socket.HasMore)
            {
                var msg = new Message();
                try
                {
                    socket.Recv(msg);
                    multipart.Add(msg);
                }
                catch
                {
                    // If receive fails, dispose the message and rethrow
                    msg.Dispose();
                    throw;
                }
            }

            message = multipart;
            return true;
        }
        catch
        {
            // If anything goes wrong after receiving the first frame, dispose and rethrow
            multipart.Dispose();
            throw;
        }
    }

    #endregion
}
