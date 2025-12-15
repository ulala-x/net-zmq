using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Net.Zmq.Core.Native;

namespace Net.Zmq;

/// <summary>
/// ZeroMQ message. Equivalent to cppzmq's message_t.
/// Uses unmanaged memory for zmq_msg_t to avoid GC relocation issues on Linux/macOS.
/// </summary>
public sealed class Message : IDisposable
{
    private const int ZmqMsgSize = 64;
    private readonly nint _msgPtr;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes an empty message.
    /// </summary>
    public Message()
    {
        _msgPtr = Marshal.AllocHGlobal(ZmqMsgSize);
        ClearMemory();
        var result = LibZmq.MsgInitPtr(_msgPtr);
        ZmqException.ThrowIfError(result);
        _initialized = true;
    }

    /// <summary>
    /// Initializes a message with a specific size.
    /// </summary>
    /// <param name="size">The size of the message in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is negative.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public Message(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        _msgPtr = Marshal.AllocHGlobal(ZmqMsgSize);
        ClearMemory();
        var result = LibZmq.MsgInitSizePtr(_msgPtr, (nuint)size);
        ZmqException.ThrowIfError(result);
        _initialized = true;
    }

    private void ClearMemory()
    {
        // Use NativeMemory.Clear for safe cross-platform memory zeroing
        unsafe
        {
            NativeMemory.Clear((void*)_msgPtr, ZmqMsgSize);
        }
    }

    /// <summary>
    /// Initializes a message with data from a byte span.
    /// </summary>
    /// <param name="data">The data to initialize the message with.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public Message(ReadOnlySpan<byte> data) : this(data.Length)
    {
        data.CopyTo(Data);
    }

    /// <summary>
    /// Initializes a message with UTF-8 encoded string data.
    /// </summary>
    /// <param name="text">The text to initialize the message with.</param>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public Message(string text) : this(Encoding.UTF8.GetBytes(text)) { }

    /// <summary>
    /// Initializes a message with external data buffer (zero-copy).
    /// The freeCallback will be called when ZeroMQ is done with the data.
    /// </summary>
    /// <param name="data">Pointer to the external data buffer.</param>
    /// <param name="size">Size of the data buffer in bytes.</param>
    /// <param name="freeCallback">Optional callback to be invoked when ZeroMQ releases the data.
    /// The callback receives the pointer to the data buffer that can be freed.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is negative.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public Message(nint data, int size, Action<nint>? freeCallback = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        _msgPtr = Marshal.AllocHGlobal(ZmqMsgSize);
        ClearMemory();

        nint ffnPtr = nint.Zero;
        nint hintPtr = nint.Zero;

        if (freeCallback != null)
        {
            // Allocate GCHandle to keep the callback alive
            var handle = GCHandle.Alloc(freeCallback);
            hintPtr = GCHandle.ToIntPtr(handle);
            unsafe
            {
                ffnPtr = (nint)FreeCallbackFunctionPointer;
            }
        }

        var result = LibZmq.MsgInitDataPtr(_msgPtr, data, (nuint)size, ffnPtr, hintPtr);
        if (result != 0)
        {
            // If initialization failed and we allocated a handle, free it
            if (hintPtr != nint.Zero)
            {
                var handle = GCHandle.FromIntPtr(hintPtr);
                handle.Free();
            }
            Marshal.FreeHGlobal(_msgPtr);
            ZmqException.ThrowIfError(result);
        }

        _initialized = true;
    }

    /// <summary>
    /// Unmanaged callback function pointer for zmq_msg_init_data.
    /// Uses UnmanagedCallersOnly for modern .NET P/Invoke.
    /// </summary>
    private static readonly unsafe delegate* unmanaged[Cdecl]<nint, nint, void> FreeCallbackFunctionPointer = &FreeCallbackImpl;

    /// <summary>
    /// Implementation of the unmanaged free callback.
    /// This is called by ZeroMQ when it's done with the message data.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void FreeCallbackImpl(nint data, nint hint)
    {
        if (hint == nint.Zero)
            return;

        try
        {
            // Retrieve the Action<nint> callback from the GCHandle
            var handle = GCHandle.FromIntPtr(hint);
            var callback = handle.Target as Action<nint>;

            // Invoke the user's callback with the data pointer
            callback?.Invoke(data);

            // Free the GCHandle
            handle.Free();
        }
        catch
        {
            // Swallow exceptions in unmanaged callback to prevent corrupting ZeroMQ's state
            // In production code, you might want to log this error
        }
    }

    /// <summary>
    /// Gets a span to the message data.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public Span<byte> Data
    {
        get
        {
            EnsureInitialized();
            var ptr = LibZmq.MsgDataPtr(_msgPtr);
            var size = (int)LibZmq.MsgSizePtr(_msgPtr);
            unsafe { return new Span<byte>((void*)ptr, size); }
        }
    }

    /// <summary>
    /// Gets the size of the message in bytes.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public int Size
    {
        get
        {
            EnsureInitialized();
            return (int)LibZmq.MsgSizePtr(_msgPtr);
        }
    }

    /// <summary>
    /// Gets a value indicating whether more message parts follow.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public bool More
    {
        get
        {
            EnsureInitialized();
            return LibZmq.MsgMorePtr(_msgPtr) != 0;
        }
    }

    /// <summary>
    /// Gets a message property value.
    /// </summary>
    /// <param name="property">The property to retrieve.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public int GetProperty(MessageProperty property)
    {
        EnsureInitialized();
        var result = LibZmq.MsgGetPtr(_msgPtr, (int)property);
        if (result == -1) ZmqException.ThrowIfError(-1);
        return result;
    }

    /// <summary>
    /// Sets a message property value.
    /// </summary>
    /// <param name="property">The property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void SetProperty(MessageProperty property, int value)
    {
        EnsureInitialized();
        var result = LibZmq.MsgSetPtr(_msgPtr, (int)property, value);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Gets a message metadata property.
    /// </summary>
    /// <param name="property">The metadata property name.</param>
    /// <returns>The metadata property value, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public string? GetMetadata(string property)
    {
        EnsureInitialized();
        return LibZmq.MsgGetsPtr(_msgPtr, property);
    }

    /// <summary>
    /// Converts the message data to a byte array.
    /// </summary>
    /// <returns>A byte array containing the message data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public byte[] ToArray() => Data.ToArray();

    /// <summary>
    /// Converts the message data to a UTF-8 string.
    /// </summary>
    /// <returns>A string containing the message data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public override string ToString() => Encoding.UTF8.GetString(Data);

    /// <summary>
    /// Rebuilds the message as an empty message.
    /// </summary>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Rebuild()
    {
        if (_initialized) LibZmq.MsgClosePtr(_msgPtr);
        ClearMemory();
        var result = LibZmq.MsgInitPtr(_msgPtr);
        ZmqException.ThrowIfError(result);
        _initialized = true;
    }

    /// <summary>
    /// Rebuilds the message with a specific size.
    /// </summary>
    /// <param name="size">The new size of the message in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is negative.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Rebuild(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (_initialized) LibZmq.MsgClosePtr(_msgPtr);
        ClearMemory();
        var result = LibZmq.MsgInitSizePtr(_msgPtr, (nuint)size);
        ZmqException.ThrowIfError(result);
        _initialized = true;
    }

    /// <summary>
    /// Moves the content from the source message to this message.
    /// </summary>
    /// <param name="source">The source message.</param>
    /// <exception cref="InvalidOperationException">Thrown if either message is not initialized.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Move(Message source)
    {
        EnsureInitialized();
        source.EnsureInitialized();
        var result = LibZmq.MsgMovePtr(_msgPtr, source._msgPtr);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Copies the content from the source message to this message.
    /// </summary>
    /// <param name="source">The source message.</param>
    /// <exception cref="InvalidOperationException">Thrown if either message is not initialized.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    public void Copy(Message source)
    {
        EnsureInitialized();
        source.EnsureInitialized();
        var result = LibZmq.MsgCopyPtr(_msgPtr, source._msgPtr);
        ZmqException.ThrowIfError(result);
    }

    /// <summary>
    /// Sends the message on a socket.
    /// </summary>
    /// <param name="socket">The socket handle.</param>
    /// <param name="flags">Send flags.</param>
    /// <returns>The number of bytes sent.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    internal int Send(nint socket, SendFlags flags)
    {
        EnsureInitialized();
        var result = LibZmq.MsgSendPtr(_msgPtr, socket, (int)flags);
        ZmqException.ThrowIfError(result);
        return result;
    }

    /// <summary>
    /// Receives a message from a socket.
    /// </summary>
    /// <param name="socket">The socket handle.</param>
    /// <param name="flags">Receive flags.</param>
    /// <returns>The number of bytes received.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    /// <exception cref="ZmqException">Thrown if the operation fails.</exception>
    internal int Recv(nint socket, RecvFlags flags)
    {
        EnsureInitialized();
        var result = LibZmq.MsgRecvPtr(_msgPtr, socket, (int)flags);
        if (result == -1)
        {
            // On error with EAGAIN, the message remains valid and initialized.
            // For other errors, the message may be in an undefined state.
            // We'll just throw without touching the message - it's still initialized
            // and can be safely closed later.
            throw new ZmqException();
        }
        return result;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Message not initialized");
    }

    /// <summary>
    /// Disposes the message and releases all associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_initialized)
        {
            LibZmq.MsgClosePtr(_msgPtr);
            _initialized = false;
        }

        Marshal.FreeHGlobal(_msgPtr);
        GC.SuppressFinalize(this);
    }

    ~Message()
    {
        // Only free unmanaged memory if not already disposed
        if (!_disposed)
        {
            // We must call zmq_msg_close if the message was initialized,
            // otherwise we leak ZMQ's internal resources
            if (_initialized)
            {
                LibZmq.MsgClosePtr(_msgPtr);
            }
            Marshal.FreeHGlobal(_msgPtr);
        }
    }
}
