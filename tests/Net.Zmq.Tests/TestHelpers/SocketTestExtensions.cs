namespace Net.Zmq;

/// <summary>
/// Test helper extension methods for Socket.
/// These methods are deprecated in production code but kept for testing convenience.
/// </summary>
internal static class SocketTestExtensions
{
    /// <summary>
    /// Receives data as a byte array (blocking).
    /// This is a test helper method. Production code should use Recv(Message) or Recv(Span) instead.
    /// </summary>
    public static byte[] RecvBytes(this Socket socket)
    {
        using var msg = new Message();
        socket.Recv(msg, RecvFlags.None);
        return msg.ToArray();
    }

    /// <summary>
    /// Receives data as a byte array (blocking).
    /// This is a test helper method. Production code should use Recv(Message) or Recv(Span) instead.
    /// </summary>
    public static byte[] RecvBytes(this SocketRef socketRef)
    {
        using var msg = new Message();
        msg.Recv(socketRef.Handle, RecvFlags.None);
        return msg.ToArray();
    }

    /// <summary>
    /// Tries to receive data as a byte array (non-blocking).
    /// This is a test helper method. Production code should use TryRecv(Message, out int) instead.
    /// </summary>
    public static bool TryRecvBytes(this Socket socket, out byte[] data)
    {
        using var msg = new Message();
        var result = socket.Recv(msg, RecvFlags.DontWait);
        if (result == -1)
        {
            data = null!;
            return false;
        }
        data = msg.ToArray();
        return true;
    }
}
