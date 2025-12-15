using Net.Zmq.Core.Native;

namespace Net.Zmq.Core;

/// <summary>
/// Exception thrown when a ZeroMQ operation fails.
/// </summary>
public class ZmqException : Exception
{
    /// <summary>
    /// Gets the ZeroMQ error number.
    /// </summary>
    public int ErrorNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZmqException"/> class with the current error.
    /// </summary>
    public ZmqException() : this(LibZmq.Errno())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZmqException"/> class with a specific error number.
    /// </summary>
    /// <param name="errorNumber">The ZeroMQ error number.</param>
    public ZmqException(int errorNumber)
        : base(LibZmq.Strerror(errorNumber))
    {
        ErrorNumber = errorNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZmqException"/> class with a specific error number and message.
    /// </summary>
    /// <param name="errorNumber">The ZeroMQ error number.</param>
    /// <param name="message">The error message.</param>
    public ZmqException(int errorNumber, string message)
        : base(message)
    {
        ErrorNumber = errorNumber;
    }

    /// <summary>
    /// Throws a <see cref="ZmqException"/> if the return code indicates an error.
    /// </summary>
    /// <param name="returnCode">The return code from a ZeroMQ function.</param>
    /// <exception cref="ZmqException">Thrown when returnCode is -1.</exception>
    internal static void ThrowIfError(int returnCode)
    {
        if (returnCode == -1)
        {
            throw new ZmqException();
        }
    }

    /// <summary>
    /// Throws a <see cref="ZmqException"/> if the pointer is null.
    /// </summary>
    /// <param name="ptr">The pointer to check.</param>
    /// <exception cref="ZmqException">Thrown when ptr is IntPtr.Zero.</exception>
    internal static void ThrowIfNull(nint ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            throw new ZmqException();
        }
    }
}
