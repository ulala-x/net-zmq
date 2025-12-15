using System.Collections;
using System.Text;

namespace Net.Zmq;

/// <summary>
/// Container for multipart ZeroMQ messages.
/// Provides convenient methods for building and manipulating multipart messages.
/// Similar to cppzmq's multipart_t.
/// </summary>
/// <remarks>
/// This class is not thread-safe. External synchronization is required if accessed from multiple threads.
/// </remarks>
public sealed class MultipartMessage : IEnumerable<Message>, IDisposable
{
    private readonly List<Message> _frames = new();
    private bool _disposed;

    /// <summary>
    /// Gets the number of message frames in this multipart message.
    /// </summary>
    public int Count => _frames.Count;

    /// <summary>
    /// Gets a value indicating whether this multipart message is empty.
    /// </summary>
    public bool IsEmpty => _frames.Count == 0;

    /// <summary>
    /// Gets the message frame at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the frame to get.</param>
    /// <returns>The message frame at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public Message this[int index]
    {
        get
        {
            ThrowIfDisposed();
            return _frames[index];
        }
    }

    /// <summary>
    /// Gets the first message frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public Message First
    {
        get
        {
            ThrowIfDisposed();
            if (_frames.Count == 0)
                throw new InvalidOperationException("Multipart message is empty");
            return _frames[0];
        }
    }

    /// <summary>
    /// Gets the last message frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public Message Last
    {
        get
        {
            ThrowIfDisposed();
            if (_frames.Count == 0)
                throw new InvalidOperationException("Multipart message is empty");
            return _frames[^1];
        }
    }

    /// <summary>
    /// Adds a message frame to the multipart message.
    /// This method takes ownership of the message.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public void Add(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();
        _frames.Add(message);
    }

    /// <summary>
    /// Creates a new message from the byte array and adds it to the multipart message.
    /// </summary>
    /// <param name="data">The data to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if data is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    /// <exception cref="ZmqException">Thrown if message creation fails.</exception>
    public void Add(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ThrowIfDisposed();
        _frames.Add(new Message(data));
    }

    /// <summary>
    /// Creates a new message from the UTF-8 encoded string and adds it to the multipart message.
    /// </summary>
    /// <param name="text">The text to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if text is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    /// <exception cref="ZmqException">Thrown if message creation fails.</exception>
    public void Add(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposed();
        _frames.Add(new Message(text));
    }

    /// <summary>
    /// Creates a new message from the span and adds it to the multipart message.
    /// </summary>
    /// <param name="data">The data to add.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    /// <exception cref="ZmqException">Thrown if message creation fails.</exception>
    public void Add(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        _frames.Add(new Message(data));
    }

    /// <summary>
    /// Adds an empty delimiter frame to the multipart message.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    /// <exception cref="ZmqException">Thrown if message creation fails.</exception>
    public void AddEmptyFrame()
    {
        ThrowIfDisposed();
        _frames.Add(new Message());
    }

    /// <summary>
    /// Inserts a message frame at the specified index.
    /// This method takes ownership of the message.
    /// </summary>
    /// <param name="index">The zero-based index at which the message should be inserted.</param>
    /// <param name="message">The message to insert.</param>
    /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public void Insert(int index, Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();
        _frames.Insert(index, message);
    }

    /// <summary>
    /// Removes the message frame at the specified index and disposes it.
    /// </summary>
    /// <param name="index">The zero-based index of the frame to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public void RemoveAt(int index)
    {
        ThrowIfDisposed();
        var message = _frames[index];
        _frames.RemoveAt(index);
        message.Dispose();
    }

    /// <summary>
    /// Removes all message frames and disposes them.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public void Clear()
    {
        ThrowIfDisposed();
        foreach (var message in _frames)
        {
            message.Dispose();
        }
        _frames.Clear();
    }

    /// <summary>
    /// Removes and returns the first message frame.
    /// The caller takes ownership of the returned message and is responsible for disposing it.
    /// </summary>
    /// <returns>The first message frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public Message Pop()
    {
        ThrowIfDisposed();
        if (_frames.Count == 0)
            throw new InvalidOperationException("Multipart message is empty");

        var message = _frames[0];
        _frames.RemoveAt(0);
        return message;
    }

    /// <summary>
    /// Removes and returns the last message frame.
    /// The caller takes ownership of the returned message and is responsible for disposing it.
    /// </summary>
    /// <returns>The last message frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public Message PopBack()
    {
        ThrowIfDisposed();
        if (_frames.Count == 0)
            throw new InvalidOperationException("Multipart message is empty");

        var index = _frames.Count - 1;
        var message = _frames[index];
        _frames.RemoveAt(index);
        return message;
    }

    /// <summary>
    /// Gets the data of the first message frame without removing it.
    /// </summary>
    /// <returns>A span containing the data of the first frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public ReadOnlySpan<byte> PeekFirst()
    {
        ThrowIfDisposed();
        if (_frames.Count == 0)
            throw new InvalidOperationException("Multipart message is empty");
        return _frames[0].Data;
    }

    /// <summary>
    /// Gets the data of the last message frame without removing it.
    /// </summary>
    /// <returns>A span containing the data of the last frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public ReadOnlySpan<byte> PeekLast()
    {
        ThrowIfDisposed();
        if (_frames.Count == 0)
            throw new InvalidOperationException("Multipart message is empty");
        return _frames[^1].Data;
    }

    /// <summary>
    /// Gets the UTF-8 decoded string data of the first message frame without removing it.
    /// </summary>
    /// <returns>The string data of the first frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public string PeekFirstString()
    {
        ThrowIfDisposed();
        if (_frames.Count == 0)
            throw new InvalidOperationException("Multipart message is empty");
        return _frames[0].ToString();
    }

    /// <summary>
    /// Gets the UTF-8 decoded string data of the last message frame without removing it.
    /// </summary>
    /// <returns>The string data of the last frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the multipart message is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public string PeekLastString()
    {
        ThrowIfDisposed();
        if (_frames.Count == 0)
            throw new InvalidOperationException("Multipart message is empty");
        return _frames[^1].ToString();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the message frames.
    /// </summary>
    /// <returns>An enumerator for the message frames.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    public IEnumerator<Message> GetEnumerator()
    {
        ThrowIfDisposed();
        return _frames.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the message frames.
    /// </summary>
    /// <returns>An enumerator for the message frames.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the multipart message has been disposed.</exception>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Disposes the multipart message and all contained message frames.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var message in _frames)
        {
            message.Dispose();
        }
        _frames.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MultipartMessage));
    }
}
