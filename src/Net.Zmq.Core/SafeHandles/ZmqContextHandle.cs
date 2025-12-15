using System.Runtime.InteropServices;
using Net.Zmq.Core.Native;

namespace Net.Zmq.Core.SafeHandles;

/// <summary>
/// Safe handle for ZeroMQ context resources.
/// </summary>
public sealed class ZmqContextHandle : SafeHandle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZmqContextHandle"/> class.
    /// </summary>
    public ZmqContextHandle() : base(IntPtr.Zero, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZmqContextHandle"/> class with an existing handle.
    /// </summary>
    /// <param name="existingHandle">The existing handle value.</param>
    /// <param name="ownsHandle">true if the handle should be released when this instance is disposed; otherwise, false.</param>
    public ZmqContextHandle(nint existingHandle, bool ownsHandle)
        : base(existingHandle, ownsHandle)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the handle is invalid.
    /// </summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Releases the ZeroMQ context handle.
    /// </summary>
    /// <returns>true if the handle was released successfully; otherwise, false.</returns>
    protected override bool ReleaseHandle()
    {
        if (handle != IntPtr.Zero)
        {
            return LibZmq.CtxTerm(handle) == 0;
        }
        return true;
    }
}
