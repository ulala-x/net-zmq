namespace Net.Zmq;

/// <summary>
/// Flags for send operations.
/// </summary>
[Flags]
public enum SendFlags
{
    None = 0,
    DontWait = 1,
    SendMore = 2
}
