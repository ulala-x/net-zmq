namespace Net.Zmq;

/// <summary>
/// Events for polling operations.
/// </summary>
[Flags]
public enum PollEvents : short
{
    None = 0,
    In = 1,
    Out = 2,
    Err = 4,
    Pri = 8
}
