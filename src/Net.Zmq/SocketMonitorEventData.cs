using System.Buffers.Binary;

namespace Net.Zmq;

/// <summary>
/// Represents ZeroMQ socket monitor event data parsed from monitor event messages.
/// Socket monitor events are delivered as two-frame messages:
/// - Frame 1: 6 bytes (event: uint16 LE + value: int32 LE)
/// - Frame 2: endpoint address string
/// </summary>
public readonly record struct SocketMonitorEventData
{
    /// <summary>
    /// Gets the type of the socket monitor event.
    /// </summary>
    public SocketMonitorEvent Event { get; }

    /// <summary>
    /// Gets the event value. This is typically a file descriptor for connection events
    /// or an error code (errno) for failure events.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets the endpoint address associated with the event.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketMonitorEventData"/> struct.
    /// </summary>
    /// <param name="event">The socket monitor event type.</param>
    /// <param name="value">The event value (file descriptor or error code).</param>
    /// <param name="address">The endpoint address.</param>
    public SocketMonitorEventData(SocketMonitorEvent @event, int value, string address)
    {
        Event = @event;
        Value = value;
        Address = address;
    }

    /// <summary>
    /// Parses a socket monitor event from the event frame bytes and endpoint address.
    /// </summary>
    /// <param name="eventFrame">The 6-byte event frame containing event (uint16 LE) and value (int32 LE).</param>
    /// <param name="address">The endpoint address string from the second frame.</param>
    /// <returns>A <see cref="SocketMonitorEventData"/> instance containing the parsed event data.</returns>
    /// <exception cref="ArgumentException">Thrown if eventFrame is not exactly 6 bytes.</exception>
    /// <exception cref="ArgumentNullException">Thrown if address is null.</exception>
    public static SocketMonitorEventData Parse(ReadOnlySpan<byte> eventFrame, string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (eventFrame.Length != 6)
            throw new ArgumentException("Event frame must be exactly 6 bytes (2 bytes for event + 4 bytes for value).", nameof(eventFrame));

        // Read event as uint16 LE from bytes 0-1
        var eventValue = BinaryPrimitives.ReadUInt16LittleEndian(eventFrame);

        // Read value as int32 LE from bytes 2-5
        var value = BinaryPrimitives.ReadInt32LittleEndian(eventFrame.Slice(2));

        return new SocketMonitorEventData((SocketMonitorEvent)eventValue, value, address);
    }

    /// <summary>
    /// Parses a socket monitor event from the event frame byte array and endpoint address.
    /// </summary>
    /// <param name="eventFrame">The 6-byte event frame containing event (uint16 LE) and value (int32 LE).</param>
    /// <param name="address">The endpoint address string from the second frame.</param>
    /// <returns>A <see cref="SocketMonitorEventData"/> instance containing the parsed event data.</returns>
    /// <exception cref="ArgumentNullException">Thrown if eventFrame or address is null.</exception>
    /// <exception cref="ArgumentException">Thrown if eventFrame is not exactly 6 bytes.</exception>
    public static SocketMonitorEventData Parse(byte[] eventFrame, string address)
    {
        ArgumentNullException.ThrowIfNull(eventFrame);
        return Parse(eventFrame.AsSpan(), address);
    }
}
