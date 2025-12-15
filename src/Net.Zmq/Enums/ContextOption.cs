namespace Net.Zmq;

/// <summary>
/// ZeroMQ context options.
/// </summary>
public enum ContextOption
{
    IoThreads = 1,
    MaxSockets = 2,
    SocketLimit = 3,
    ThreadPriority = 3,
    ThreadSchedPolicy = 4,
    MaxMsgSize = 5,
    MsgTSize = 6,
    ThreadAffinityCpuAdd = 7,
    ThreadAffinityCpuRemove = 8,
    ThreadNamePrefix = 9
}
