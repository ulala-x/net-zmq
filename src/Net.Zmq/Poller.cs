using System.Runtime.InteropServices;
using Net.Zmq.Core.Native;

namespace Net.Zmq;

/// <summary>
/// Zero-allocation poller for ZeroMQ sockets.
/// Manages native memory directly for optimal performance.
/// </summary>
public sealed class Poller : IDisposable
{
    private readonly int _capacity;
    private readonly Socket?[] _sockets;
    private readonly PollEvents[] _requestedEvents;
    private unsafe void* _nativeItems;
    private int _count;
    private bool _disposed;

    /// <summary>
    /// Creates a new poller with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of sockets that can be polled.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 1.</exception>
    public unsafe Poller(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1");
        }

        _capacity = capacity;
        _sockets = new Socket?[capacity];
        _requestedEvents = new PollEvents[capacity];
        _count = 0;
        _disposed = false;

        // Allocate native memory based on platform
        nuint itemSize = OperatingSystem.IsWindows()
            ? (nuint)sizeof(ZmqPollItemWindows)
            : (nuint)sizeof(ZmqPollItemUnix);

        _nativeItems = NativeMemory.Alloc((nuint)capacity, itemSize);
    }

    /// <summary>
    /// Gets the number of registered sockets.
    /// </summary>
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _count;
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the poller.
    /// </summary>
    public int Capacity
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _capacity;
        }
    }

    /// <summary>
    /// Adds a socket to the poller.
    /// </summary>
    /// <param name="socket">The socket to add.</param>
    /// <param name="events">Events to poll for.</param>
    /// <returns>The index of the socket in the poller.</returns>
    /// <exception cref="ArgumentNullException">Thrown when socket is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the poller is at capacity.</exception>
    public int Add(Socket socket, PollEvents events)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_count >= _capacity)
        {
            throw new InvalidOperationException($"Poller is at capacity ({_capacity} sockets)");
        }

        int index = _count;
        _sockets[index] = socket;
        _requestedEvents[index] = events;

        unsafe
        {
            WriteToNative(index, socket.Handle, events);
        }

        _count++;
        return index;
    }

    /// <summary>
    /// Updates the events to poll for a socket at the specified index.
    /// </summary>
    /// <param name="index">The index of the socket.</param>
    /// <param name="events">New events to poll for.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void Update(int index, PollEvents events)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_count - 1}");
        }

        _requestedEvents[index] = events;

        unsafe
        {
            var socket = _sockets[index];
            WriteToNative(index, socket?.Handle ?? 0, events);
        }
    }

    /// <summary>
    /// Gets the socket at the specified index.
    /// </summary>
    /// <param name="index">The index of the socket.</param>
    /// <returns>The socket at the specified index, or null if none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public Socket? GetSocket(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_count - 1}");
        }

        return _sockets[index];
    }

    /// <summary>
    /// Polls for events on registered sockets.
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (-1 for infinite).</param>
    /// <returns>Number of sockets with events, or -1 on error.</returns>
    public int Poll(long timeout = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_count == 0)
        {
            return 0;
        }

        unsafe
        {
            int result = LibZmq.Poll(_nativeItems, _count, timeout);
            ZmqException.ThrowIfError(result);
            return result;
        }
    }

    /// <summary>
    /// Polls for events with a TimeSpan timeout.
    /// </summary>
    /// <param name="timeout">Timeout as TimeSpan.</param>
    /// <returns>Number of sockets with events, or -1 on error.</returns>
    public int Poll(TimeSpan timeout)
    {
        return Poll((long)timeout.TotalMilliseconds);
    }

    /// <summary>
    /// Returns whether the socket at the index has readable data.
    /// </summary>
    /// <param name="index">The index of the socket.</param>
    /// <returns>True if the socket is readable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public bool IsReadable(int index)
    {
        return (GetReturnedEvents(index) & PollEvents.In) != 0;
    }

    /// <summary>
    /// Returns whether the socket at the index is writable.
    /// </summary>
    /// <param name="index">The index of the socket.</param>
    /// <returns>True if the socket is writable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public bool IsWritable(int index)
    {
        return (GetReturnedEvents(index) & PollEvents.Out) != 0;
    }

    /// <summary>
    /// Returns whether the socket at the index has an error.
    /// </summary>
    /// <param name="index">The index of the socket.</param>
    /// <returns>True if the socket has an error.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public bool HasError(int index)
    {
        return (GetReturnedEvents(index) & PollEvents.Err) != 0;
    }

    /// <summary>
    /// Gets the returned events for the socket at the index.
    /// </summary>
    /// <param name="index">The index of the socket.</param>
    /// <returns>The returned events.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public unsafe PollEvents GetReturnedEvents(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_count - 1}");
        }

        if (OperatingSystem.IsWindows())
        {
            var ptr = (ZmqPollItemWindows*)_nativeItems + index;
            return (PollEvents)ptr->Revents;
        }
        else
        {
            var ptr = (ZmqPollItemUnix*)_nativeItems + index;
            return (PollEvents)ptr->Revents;
        }
    }

    /// <summary>
    /// Clears all registered sockets from the poller, allowing it to be reused.
    /// </summary>
    /// <remarks>
    /// This method resets the internal count to 0 and clears the socket references.
    /// The native memory is not reallocated, making this efficient for reuse scenarios.
    /// This is particularly useful for thread-local cached pollers.
    /// </remarks>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _count = 0;
        Array.Clear(_sockets);
    }

    /// <summary>
    /// Writes socket information to native memory.
    /// </summary>
    private unsafe void WriteToNative(int index, nint socketHandle, PollEvents events)
    {
        if (OperatingSystem.IsWindows())
        {
            var ptr = (ZmqPollItemWindows*)_nativeItems + index;
            ptr->Socket = socketHandle;
            ptr->Fd = 0;
            ptr->Events = (short)events;
            ptr->Revents = 0;
        }
        else
        {
            var ptr = (ZmqPollItemUnix*)_nativeItems + index;
            ptr->Socket = socketHandle;
            ptr->Fd = 0;
            ptr->Events = (short)events;
            ptr->Revents = 0;
        }
    }

    /// <summary>
    /// Releases all resources used by the poller.
    /// </summary>
    public unsafe void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_nativeItems != null)
        {
            NativeMemory.Free(_nativeItems);
            _nativeItems = null;
        }

        _disposed = true;
    }
}
