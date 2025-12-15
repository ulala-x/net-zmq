namespace Net.Zmq;

/// <summary>
/// Non-owning reference to a ZMQ socket. Equivalent to cppzmq's socket_ref.
/// </summary>
public readonly struct SocketRef : IEquatable<SocketRef>
{
    private readonly nint _handle;

    internal SocketRef(nint handle)
    {
        _handle = handle;
    }

    public static SocketRef FromHandle(nint handle) => new(handle);

    internal nint Handle => _handle;

    public bool IsValid => _handle != IntPtr.Zero;

    public bool Equals(SocketRef other) => _handle == other._handle;

    public override bool Equals(object? obj) => obj is SocketRef other && Equals(other);

    public override int GetHashCode() => _handle.GetHashCode();

    public static bool operator ==(SocketRef left, SocketRef right) => left.Equals(right);

    public static bool operator !=(SocketRef left, SocketRef right) => !left.Equals(right);
}
