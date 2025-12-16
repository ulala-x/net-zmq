using System.Runtime.InteropServices;

namespace Net.Zmq.Core.Native;

/// <summary>
/// ZeroMQ message structure.
/// Must be 64 bytes with pointer-size alignment (8 bytes on 64-bit systems).
/// Using explicit layout to ensure correct size and alignment across platforms.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct ZmqMsg
{
    [FieldOffset(0)] private long _p0;
    [FieldOffset(8)] private long _p1;
    [FieldOffset(16)] private long _p2;
    [FieldOffset(24)] private long _p3;
    [FieldOffset(32)] private long _p4;
    [FieldOffset(40)] private long _p5;
    [FieldOffset(48)] private long _p6;
    [FieldOffset(56)] private long _p7;
}

/// <summary>
/// ZeroMQ poll item structure for Windows.
/// On Windows, zmq_fd_t is:
/// - 64-bit: unsigned __int64 (8 bytes)
/// - 32-bit: unsigned int (4 bytes)
/// Using nuint handles both cases correctly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ZmqPollItemWindows
{
    public nint Socket;
    public nuint Fd;
    public short Events;
    public short Revents;
}

/// <summary>
/// ZeroMQ poll item structure for Unix/macOS.
/// On Unix, zmq_fd_t is int (4 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ZmqPollItemUnix
{
    public nint Socket;
    public int Fd;
    public short Events;
    public short Revents;
}
