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
    internal readonly nint _msgPtr;
    internal bool _initialized;
    internal bool _disposed;
    internal bool _wasSuccessfullySent;
    internal nint _poolDataPtr = nint.Zero;     // 풀 반환을 위한 네이티브 포인터
    internal int _poolBucketIndex = -1;          // 풀의 버킷 인덱스
    internal int _poolActualSize = -1;           // 실제 할당된 크기

    // 재사용 가능한 Message 전용 필드
    internal bool _isFromPool = false;                  // 풀에서 재사용되는 Message 객체 여부
    internal Action<nint>? _reusableCallback = null;    // 재사용 가능한 콜백
    internal int _callbackExecuted = 0;                 // 콜백 실행 여부 (0 or 1)
    internal GCHandle _callbackHandle;                  // 콜백 GCHandle

    // 새로운 필드: 실제 데이터 크기와 버퍼 크기 추적
    internal int _actualDataSize;  // 실제 데이터 크기 (예: 64 bytes)
    internal int _bufferSize;      // 전체 버퍼 크기 (예: 1024 bytes, 버킷 크기)

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
        _actualDataSize = 0;
        _bufferSize = 0;
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
        _actualDataSize = size;
        _bufferSize = size;
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
        _actualDataSize = size;
        _bufferSize = size;
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
    /// For pooled messages, returns a span of the actual data size, not the buffer size.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public Span<byte> Data
    {
        get
        {
            EnsureInitialized();

            // Pooled 메시지의 경우 _poolDataPtr과 _actualDataSize 사용
            if (_isFromPool && _poolDataPtr != nint.Zero)
            {
                unsafe { return new Span<byte>((void*)_poolDataPtr, _actualDataSize); }
            }

            // 일반 메시지의 경우 기존 방식 사용
            var ptr = LibZmq.MsgDataPtr(_msgPtr);
            var size = (int)LibZmq.MsgSizePtr(_msgPtr);
            unsafe { return new Span<byte>((void*)ptr, size); }
        }
    }

    /// <summary>
    /// Gets the size of the message in bytes.
    /// For pooled messages, returns the actual data size, not the buffer size.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized.</exception>
    public int Size
    {
        get
        {
            EnsureInitialized();

            // Pooled 메시지의 경우 _actualDataSize 반환
            if (_isFromPool && _poolDataPtr != nint.Zero)
            {
                return _actualDataSize;
            }

            // 일반 메시지의 경우 zmq_msg_size 사용
            return (int)LibZmq.MsgSizePtr(_msgPtr);
        }
    }

    /// <summary>
    /// Gets the actual data size in bytes.
    /// For pooled messages, this may be smaller than the buffer size.
    /// </summary>
    public int ActualSize => _isFromPool ? _actualDataSize : Size;

    /// <summary>
    /// Gets the allocated buffer size in bytes.
    /// For pooled messages, this is the bucket size. For regular messages, same as ActualSize.
    /// </summary>
    public int BufferSize => _isFromPool ? _bufferSize : Size;

    /// <summary>
    /// Gets a pointer to the message data.
    /// For internal use only.
    /// </summary>
    internal nint DataPtr
    {
        get
        {
            EnsureInitialized();
            return _isFromPool && _poolDataPtr != nint.Zero ? _poolDataPtr : LibZmq.MsgDataPtr(_msgPtr);
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
    /// Copies data from a native memory pointer directly to the message buffer.
    /// This is an optimized method for ReceiveWithPool that avoids intermediate buffer copies.
    /// </summary>
    /// <param name="sourcePtr">Pointer to the source data buffer.</param>
    /// <param name="size">Size of the data to copy in bytes.</param>
    /// <exception cref="InvalidOperationException">Thrown if the message is not initialized or does not have a pool buffer.</exception>
    internal void CopyFromNative(nint sourcePtr, int size)
    {
        EnsureInitialized();
        if (_poolDataPtr == nint.Zero)
            throw new InvalidOperationException("CopyFromNative requires a message with a pool buffer");

        if (size > _poolActualSize)
            throw new ArgumentException($"Size {size} exceeds pooled buffer capacity {_poolActualSize}");

        unsafe
        {
            // Direct native-to-native memory copy - most efficient
            NativeMemory.Copy((void*)sourcePtr, (void*)_poolDataPtr, (nuint)size);
        }

        // 실제 데이터 크기 설정
        if (_isFromPool)
        {
            _actualDataSize = size;
        }
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

        int result;

        if (_isFromPool && _poolDataPtr != nint.Zero)
        {
            // Pooled 메시지: zmq_send로 실제 크기만 전송
            // 이렇게 하면 버킷 크기(예: 1024)가 아닌 실제 데이터 크기(예: 64)만 전송됨
            result = LibZmq.Send(socket, _poolDataPtr, (nuint)_actualDataSize, (int)flags);
            ZmqException.ThrowIfError(result);
            _wasSuccessfullySent = true;

            // 콜백 호출 (풀에 반환)
            // zmq_send는 zmq_msg_send와 달리 콜백을 자동으로 호출하지 않으므로 수동 호출
            _reusableCallback?.Invoke(nint.Zero);
        }
        else
        {
            // 일반 메시지: zmq_msg_send 사용 (기존 방식)
            result = LibZmq.MsgSendPtr(_msgPtr, socket, (int)flags);
            ZmqException.ThrowIfError(result);
            _wasSuccessfullySent = true;
        }

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
    /// 재사용을 위해 Message 상태를 초기화합니다.
    /// 플래그만 리셋하고 zmq_msg_t는 그대로 재사용합니다 (이미 초기화되어 있음).
    /// </summary>
    internal void PrepareForReuse()
    {
        _disposed = false;
        _wasSuccessfullySent = false;
        Interlocked.Exchange(ref _callbackExecuted, 0);

        // 리셋 시 전체 버퍼 사용 가능하도록 설정
        if (_isFromPool && _poolDataPtr != nint.Zero)
        {
            _actualDataSize = _bufferSize;
        }

        // 중요: zmq_msg_t는 이미 초기화되어 있으므로 재초기화하지 않음!
        // CreatePooledMessage()에서 한 번만 초기화했고, 그대로 재사용
    }

    /// <summary>
    /// Pooled 메시지의 실제 데이터 크기를 설정합니다.
    /// </summary>
    /// <param name="size">실제 데이터 크기</param>
    /// <exception cref="ArgumentException">실제 크기가 버퍼 크기를 초과하는 경우</exception>
    internal void SetActualDataSize(int size)
    {
        if (!_isFromPool)
            throw new InvalidOperationException("SetActualDataSize can only be called on pooled messages");

        if (size > _bufferSize)
            throw new ArgumentException($"Actual size ({size}) cannot exceed buffer size ({_bufferSize})");

        if (size < 0)
            throw new ArgumentException("Size cannot be negative", nameof(size));

        _actualDataSize = size;
    }

    /// <summary>
    /// Message를 풀에 반환합니다 (휴면 상태로 표시).
    /// 실제로는 아무것도 해제하지 않고 disposed 플래그만 설정합니다.
    /// </summary>
    internal void ReturnToPool()
    {
        _disposed = true;
        // 중요: 아무것도 해제하지 않음! 리소스는 유지됨
    }

    /// <summary>
    /// Pooled 메시지를 풀에 반환하지 않고 실제로 dispose합니다.
    /// maxBuffer 초과 시 호출됩니다.
    /// </summary>
    internal void DisposePooledMessage()
    {
        if (!_isFromPool)
            throw new InvalidOperationException("This method is only for pooled messages");

        // GCHandle 해제
        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }

        // zmq_msg_t 닫기
        if (_initialized)
        {
            LibZmq.MsgClosePtr(_msgPtr);
            _initialized = false;
        }

        // 네이티브 메모리 해제
        if (_poolDataPtr != nint.Zero)
        {
            Marshal.FreeHGlobal(_poolDataPtr);
            _poolDataPtr = nint.Zero;
        }

        if (_msgPtr != nint.Zero)
        {
            Marshal.FreeHGlobal(_msgPtr);
        }

        _disposed = true;
    }

    /// <summary>
    /// Disposes the message and releases all associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        // Pool 메시지 (재사용 가능): 콜백만 호출하고 zmq_msg_t는 닫지 않음
        if (_isFromPool)
        {
            _disposed = true;

            // Send()로 보내지지 않은 경우에만 콜백을 수동으로 호출
            // Send()로 보낸 경우는 ZMQ가 자동으로 콜백을 호출함
            if (!_wasSuccessfullySent)
            {
                // 중요: zmq_msg_close() 대신 콜백 직접 호출
                // zmq_msg_t는 초기화된 상태로 유지되어 재사용됨
                _reusableCallback?.Invoke(nint.Zero);
            }

            // _initialized는 true로 유지 - zmq_msg_t는 계속 초기화된 상태
            // zmq_msg_close() 호출하지 않음!

            GC.SuppressFinalize(this);
            return;
        }

        // 일반 메시지: 기존 로직대로 완전히 해제
        _disposed = true;

        if (_initialized)
        {
            // Only close message if it was NOT successfully sent
            // If sent, ZMQ owns it and will invoke the free callback
            // If not sent, we must close it to invoke the free callback
            if (!_wasSuccessfullySent)
            {
                LibZmq.MsgClosePtr(_msgPtr);
            }
            _initialized = false;
        }

        Marshal.FreeHGlobal(_msgPtr);

        GC.SuppressFinalize(this);
    }

    ~Message()
    {
        // Pool 메시지는 Finalizer에서 처리하지 않음 (Dispose에서 GC.SuppressFinalize 호출됨)
        // 만약 Finalizer가 호출되었다면 버그이므로 아무것도 해제하지 않음
        if (_isFromPool)
            return;

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
