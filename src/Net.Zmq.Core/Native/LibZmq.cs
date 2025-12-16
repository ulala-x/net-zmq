using System.Runtime.InteropServices;

namespace Net.Zmq.Core.Native;

/// <summary>
/// P/Invoke declarations for libzmq using LibraryImport.
/// </summary>
internal static partial class LibZmq
{
    private const string LibraryName = "libzmq";

    // ========== Error Handling ==========

    /// <summary>
    /// Gets the last error number.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_errno")]
    internal static partial int Errno();

    /// <summary>
    /// Gets the error message for the given error number.
    /// Returns a pointer to a static string - do NOT free this memory.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_strerror")]
    private static partial nint StrerrorPtr(int errnum);

    /// <summary>
    /// Gets the error message for the given error number.
    /// </summary>
    internal static string Strerror(int errnum)
    {
        var ptr = StrerrorPtr(errnum);
        return System.Runtime.InteropServices.Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    // ========== Version ==========

    /// <summary>
    /// Gets the ZeroMQ library version.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_version")]
    internal static partial void Version(out int major, out int minor, out int patch);

    // ========== Context Management ==========

    /// <summary>
    /// Creates a new ZeroMQ context.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_ctx_new")]
    internal static partial nint CtxNew();

    /// <summary>
    /// Terminates a ZeroMQ context.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_ctx_term")]
    internal static partial int CtxTerm(nint context);

    /// <summary>
    /// Shuts down a ZeroMQ context.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_ctx_shutdown")]
    internal static partial int CtxShutdown(nint context);

    /// <summary>
    /// Sets a context option.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_ctx_set")]
    internal static partial int CtxSet(nint context, int option, int optval);

    /// <summary>
    /// Gets a context option.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_ctx_get")]
    internal static partial int CtxGet(nint context, int option);

    // ========== Socket Management ==========

    /// <summary>
    /// Creates a ZeroMQ socket.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_socket")]
    internal static partial nint Socket(nint context, int type);

    /// <summary>
    /// Closes a ZeroMQ socket.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_close")]
    internal static partial int Close(nint socket);

    /// <summary>
    /// Binds a socket to an endpoint.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_bind", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Bind(nint socket, string addr);

    /// <summary>
    /// Connects a socket to an endpoint.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_connect", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Connect(nint socket, string addr);

    /// <summary>
    /// Unbinds a socket from an endpoint.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_unbind", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Unbind(nint socket, string addr);

    /// <summary>
    /// Disconnects a socket from an endpoint.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_disconnect", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Disconnect(nint socket, string addr);

    /// <summary>
    /// Sets a socket option.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_setsockopt")]
    internal static partial int SetSockOpt(nint socket, int option, nint optval, nuint optvallen);

    /// <summary>
    /// Gets a socket option.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_getsockopt")]
    internal static partial int GetSockOpt(nint socket, int option, nint optval, ref nuint optvallen);

    /// <summary>
    /// Sends data on a socket.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_send")]
    internal static partial int Send(nint socket, nint buf, nuint len, int flags);

    /// <summary>
    /// Receives data from a socket.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_recv")]
    internal static partial int Recv(nint socket, nint buf, nuint len, int flags);

    /// <summary>
    /// Monitors socket events.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_socket_monitor", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int SocketMonitor(nint socket, string? addr, int events);

    // ========== Message Management ==========

    /// <summary>
    /// Initializes an empty message.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_init")]
    internal static partial int MsgInit(ref ZmqMsg msg);

    /// <summary>
    /// Initializes a message with a specific size.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_init_size")]
    internal static partial int MsgInitSize(ref ZmqMsg msg, nuint size);

    /// <summary>
    /// Initializes a message with existing data.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_init_data")]
    internal static partial int MsgInitData(ref ZmqMsg msg, nint data, nuint size, nint ffn, nint hint);

    /// <summary>
    /// Sends a message on a socket.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_send")]
    internal static partial int MsgSend(ref ZmqMsg msg, nint socket, int flags);

    /// <summary>
    /// Receives a message from a socket.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_recv")]
    internal static partial int MsgRecv(ref ZmqMsg msg, nint socket, int flags);

    /// <summary>
    /// Closes a message.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_close")]
    internal static partial int MsgClose(ref ZmqMsg msg);

    /// <summary>
    /// Moves message content from source to destination.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_move")]
    internal static partial int MsgMove(ref ZmqMsg dest, ref ZmqMsg src);

    /// <summary>
    /// Copies message content from source to destination.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_copy")]
    internal static partial int MsgCopy(ref ZmqMsg dest, ref ZmqMsg src);

    /// <summary>
    /// Gets a pointer to the message data.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_data")]
    internal static partial nint MsgData(ref ZmqMsg msg);

    /// <summary>
    /// Gets the size of the message.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_size")]
    internal static partial nuint MsgSize(ref ZmqMsg msg);

    /// <summary>
    /// Checks if there are more message parts.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_more")]
    internal static partial int MsgMore(ref ZmqMsg msg);

    /// <summary>
    /// Gets a message property.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_get")]
    internal static partial int MsgGet(ref ZmqMsg msg, int property);

    /// <summary>
    /// Sets a message property.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_set")]
    internal static partial int MsgSet(ref ZmqMsg msg, int property, int optval);

    /// <summary>
    /// Gets a message metadata property.
    /// Returns a pointer to metadata string - do NOT free this memory.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_gets", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint MsgGetsInternal(ref ZmqMsg msg, string property);

    /// <summary>
    /// Gets a message metadata property.
    /// </summary>
    internal static string? MsgGets(ref ZmqMsg msg, string property)
    {
        var ptr = MsgGetsInternal(ref msg, property);
        if (ptr == nint.Zero) return null;
        return Marshal.PtrToStringUTF8(ptr);
    }

    // ========== Message Management (Pointer-based for unmanaged memory) ==========

    /// <summary>
    /// Initializes an empty message using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_init")]
    internal static partial int MsgInitPtr(nint msg);

    /// <summary>
    /// Initializes a message with a specific size using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_init_size")]
    internal static partial int MsgInitSizePtr(nint msg, nuint size);

    /// <summary>
    /// Initializes a message with existing data using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_init_data")]
    internal static partial int MsgInitDataPtr(nint msg, nint data, nuint size, nint ffn, nint hint);

    /// <summary>
    /// Sends a message on a socket using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_send")]
    internal static partial int MsgSendPtr(nint msg, nint socket, int flags);

    /// <summary>
    /// Receives a message from a socket using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_recv")]
    internal static partial int MsgRecvPtr(nint msg, nint socket, int flags);

    /// <summary>
    /// Closes a message using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_close")]
    internal static partial int MsgClosePtr(nint msg);

    /// <summary>
    /// Moves message content from source to destination using raw pointers.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_move")]
    internal static partial int MsgMovePtr(nint dest, nint src);

    /// <summary>
    /// Copies message content from source to destination using raw pointers.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_copy")]
    internal static partial int MsgCopyPtr(nint dest, nint src);

    /// <summary>
    /// Gets a pointer to the message data using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_data")]
    internal static partial nint MsgDataPtr(nint msg);

    /// <summary>
    /// Gets the size of the message using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_size")]
    internal static partial nuint MsgSizePtr(nint msg);

    /// <summary>
    /// Checks if there are more message parts using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_more")]
    internal static partial int MsgMorePtr(nint msg);

    /// <summary>
    /// Gets a message property using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_get")]
    internal static partial int MsgGetPtr(nint msg, int property);

    /// <summary>
    /// Sets a message property using raw pointer.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_set")]
    internal static partial int MsgSetPtr(nint msg, int property, int optval);

    /// <summary>
    /// Gets a message metadata property using raw pointer.
    /// Returns a pointer to metadata string - do NOT free this memory.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_msg_gets", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint MsgGetsPtrInternal(nint msg, string property);

    /// <summary>
    /// Gets a message metadata property using raw pointer.
    /// </summary>
    internal static string? MsgGetsPtr(nint msg, string property)
    {
        var ptr = MsgGetsPtrInternal(msg, property);
        if (ptr == nint.Zero) return null;
        return Marshal.PtrToStringUTF8(ptr);
    }

    // ========== Polling ==========

    /// <summary>
    /// Polls multiple sockets for events (Windows version - pointer based).
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_poll")]
    internal static unsafe partial int PollWindows(ZmqPollItemWindows* items, int nitems, long timeout);

    /// <summary>
    /// Polls multiple sockets for events (Unix version - pointer based).
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_poll")]
    internal static unsafe partial int PollUnix(ZmqPollItemUnix* items, int nitems, long timeout);

    /// <summary>
    /// Polls multiple sockets for events (platform-aware wrapper for pointer-based calls).
    /// </summary>
    internal static unsafe int Poll(void* items, int nitems, long timeout)
    {
        if (OperatingSystem.IsWindows())
            return PollWindows((ZmqPollItemWindows*)items, nitems, timeout);
        else
            return PollUnix((ZmqPollItemUnix*)items, nitems, timeout);
    }

    // ========== Utilities ==========

    /// <summary>
    /// Starts a proxy between frontend and backend sockets.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_proxy")]
    internal static partial int Proxy(nint frontend, nint backend, nint capture);

    /// <summary>
    /// Starts a steerable proxy between frontend and backend sockets.
    /// Control socket can receive PAUSE, RESUME, TERMINATE, and STATISTICS commands.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_proxy_steerable")]
    internal static partial int ProxySteerable(nint frontend, nint backend, nint capture, nint control);

    /// <summary>
    /// Checks if a capability is available.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_has", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Has(string capability);

    /// <summary>
    /// Encodes binary data to Z85 format.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_z85_encode")]
    internal static partial nint Z85Encode(nint dest, nint data, nuint size);

    /// <summary>
    /// Decodes Z85 format to binary data.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_z85_decode")]
    internal static partial nint Z85Decode(nint dest, nint str);

    /// <summary>
    /// Generates a CURVE keypair.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_curve_keypair")]
    internal static partial int CurveKeypair(nint z85PublicKey, nint z85SecretKey);

    /// <summary>
    /// Derives the public key from a secret key.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "zmq_curve_public")]
    internal static partial int CurvePublic(nint z85PublicKey, nint z85SecretKey);
}
