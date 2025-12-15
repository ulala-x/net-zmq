namespace Net.Zmq;

/// <summary>
/// Flags for receive operations.
/// </summary>
[Flags]
public enum RecvFlags
{
    None = 0,
    DontWait = 1
}
