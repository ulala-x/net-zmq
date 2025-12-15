using System.Buffers;
using Net.Zmq.Core.Native;

namespace Net.Zmq;

/// <summary>
/// Polling item for zmq_poll.
/// </summary>
public struct PollItem
{
    public Socket? Socket { get; set; }
    public nint FileDescriptor { get; set; }
    public PollEvents Events { get; set; }
    public PollEvents ReturnedEvents { get; internal set; }

    public PollItem(Socket socket, PollEvents events)
    {
        Socket = socket;
        FileDescriptor = 0;
        Events = events;
        ReturnedEvents = PollEvents.None;
    }

    public PollItem(nint fd, PollEvents events)
    {
        Socket = null;
        FileDescriptor = fd;
        Events = events;
        ReturnedEvents = PollEvents.None;
    }

    public readonly bool IsReadable => (ReturnedEvents & PollEvents.In) != 0;
    public readonly bool IsWritable => (ReturnedEvents & PollEvents.Out) != 0;
    public readonly bool HasError => (ReturnedEvents & PollEvents.Err) != 0;
}

/// <summary>
/// Static polling functions. Equivalent to cppzmq's poll() functions.
/// </summary>
public static class Poller
{
    private const int StackAllocThreshold = 16;

    // Thread-local cached array for single-socket polling (zero allocation)
    [ThreadStatic]
    private static ZmqPollItem[]? _singlePollItem;

    /// <summary>
    /// Polls on multiple items.
    /// </summary>
    public static int Poll(Span<PollItem> items, long timeout = -1)
    {
        if (items.Length == 0)
        {
            return 0;
        }

        // Use ArrayPool to avoid repeated allocations
        var rentedArray = ArrayPool<ZmqPollItem>.Shared.Rent(items.Length);

        try
        {
            // Convert PollItem to ZmqPollItem
            for (int i = 0; i < items.Length; i++)
            {
                rentedArray[i] = new ZmqPollItem
                {
                    Socket = items[i].Socket?.Handle ?? IntPtr.Zero,
                    Fd = items[i].FileDescriptor,
                    Events = (short)items[i].Events,
                    Revents = 0
                };
            }

            // Perform the poll operation
            var result = LibZmq.Poll(rentedArray, items.Length, timeout);
            ZmqException.ThrowIfError(result);

            // Copy back the results
            for (int i = 0; i < items.Length; i++)
            {
                items[i].ReturnedEvents = (PollEvents)rentedArray[i].Revents;
            }

            return result;
        }
        finally
        {
            ArrayPool<ZmqPollItem>.Shared.Return(rentedArray);
        }
    }

    public static int Poll(Span<PollItem> items, TimeSpan timeout)
        => Poll(items, (long)timeout.TotalMilliseconds);

    public static bool Poll(Socket socket, PollEvents events, long timeout = -1)
    {
        // Use thread-local cached array (zero allocation after first call per thread)
        _singlePollItem ??= new ZmqPollItem[1];

        _singlePollItem[0] = new ZmqPollItem
        {
            Socket = socket.Handle,
            Fd = 0,
            Events = (short)events,
            Revents = 0
        };

        var result = LibZmq.Poll(_singlePollItem, 1, timeout);
        ZmqException.ThrowIfError(result);

        return result > 0 && _singlePollItem[0].Revents != 0;
    }
}
