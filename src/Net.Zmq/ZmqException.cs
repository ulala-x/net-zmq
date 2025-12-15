namespace Net.Zmq;

/// <summary>
/// ZeroMQ error. Equivalent to cppzmq's error_t.
/// This is the high-level wrapper that inherits from the core exception.
/// </summary>
public class ZmqException : Core.ZmqException
{
    public ZmqException() : base() { }

    public ZmqException(int errorNumber) : base(errorNumber) { }

    public ZmqException(int errorNumber, string message) : base(errorNumber, message) { }

    internal new static void ThrowIfError(int returnCode)
    {
        if (returnCode == -1)
        {
            throw new ZmqException();
        }
    }

    internal new static void ThrowIfNull(nint ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            throw new ZmqException();
        }
    }
}
