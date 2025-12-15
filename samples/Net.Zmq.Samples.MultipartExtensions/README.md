# Multipart Extensions Sample

This sample demonstrates the `SocketExtensions` class which provides convenient extension methods for sending and receiving multipart messages in ZeroMQ.

## Overview

Multipart messages are a fundamental concept in ZeroMQ, allowing you to send multiple frames as a single atomic unit. The `SocketExtensions` class simplifies working with multipart messages by providing high-level methods that handle the `SendMore` flag management and frame iteration automatically.

## Extension Methods

### SendMultipart Overloads

1. **SendMultipart(MultipartMessage)**
   - Send a complete `MultipartMessage` container
   - Automatically applies `SendMore` flag to all frames except the last

2. **SendMultipart(IEnumerable<byte[]>)**
   - Send a collection of byte arrays as multipart
   - Useful for binary protocols

3. **SendMultipart(params string[])**
   - Send string frames as multipart
   - Each string is UTF-8 encoded automatically
   - Most convenient for simple text-based protocols

4. **SendMultipart(IEnumerable<Message>)**
   - Send a collection of `Message` objects as multipart
   - Provides fine-grained control over each frame

### RecvMultipart Methods

1. **RecvMultipart()**
   - Blocking receive of complete multipart message
   - Returns a `MultipartMessage` containing all frames
   - Simplifies receiving by handling the `HasMore` loop

2. **TryRecvMultipart(out MultipartMessage?)**
   - Non-blocking receive of multipart message
   - Returns `false` if no message is available (would block)
   - Returns `true` with the complete message if available

## Examples in This Sample

### Example 1: String Params (Simplest)
```csharp
sender.SendMultipart("Header", "Body", "Footer");
```

### Example 2: Binary Frames
```csharp
var frames = new List<byte[]>
{
    new byte[] { 0x01, 0x02, 0x03 },
    new byte[] { 0x04, 0x05 }
};
sender.SendMultipart(frames);
```

### Example 3: MultipartMessage Container
```csharp
using var message = new MultipartMessage();
message.Add("Command");
message.AddEmptyFrame(); // Delimiter
message.Add(binaryData);
sender.SendMultipart(message);
```

### Example 4: Blocking Receive
```csharp
using var received = receiver.RecvMultipart();
for (int i = 0; i < received.Count; i++)
{
    Console.WriteLine(received[i].ToString());
}
```

### Example 5: Non-Blocking Receive
```csharp
if (receiver.TryRecvMultipart(out var message))
{
    // Message available
    using (message)
    {
        ProcessMessage(message);
    }
}
else
{
    // Would block, no message available
}
```

### Example 6: Router-Dealer Pattern
Demonstrates how extension methods simplify envelope handling in Router-Dealer patterns.

## Benefits of Extension Methods

1. **Simplified Code**: No need to manually track `SendMore` flags
2. **Fewer Errors**: Automatic handling of the last frame (no `SendMore`)
3. **Cleaner API**: Single method call instead of loops
4. **Better Readability**: Intent is clearer with `SendMultipart()`
5. **Resource Safety**: `RecvMultipart()` returns a disposable container

## Traditional Approach vs Extension Methods

### Without Extensions
```csharp
// Sending
sender.Send("Frame1", SendFlags.SendMore);
sender.Send("Frame2", SendFlags.SendMore);
sender.Send("Frame3", SendFlags.None); // Don't forget to remove SendMore!

// Receiving
var frames = new List<byte[]>();
do
{
    frames.Add(receiver.RecvBytes());
} while (receiver.HasMore);
```

### With Extensions
```csharp
// Sending
sender.SendMultipart("Frame1", "Frame2", "Frame3");

// Receiving
using var message = receiver.RecvMultipart();
// All frames are in message[0], message[1], message[2]
```

## Running the Sample

```bash
cd samples/NetZeroMQ.Samples.MultipartExtensions
dotnet run
```

## Key Concepts

1. **Multipart Atomicity**: All frames of a multipart message are delivered together or not at all
2. **SendMore Flag**: Indicates more frames follow in the current message
3. **HasMore Property**: Indicates whether the current frame has more parts following
4. **Empty Frames**: Often used as delimiters (e.g., in REQ/REP envelope pattern)

## Common Use Cases

- **Protocol Headers**: Separate metadata from payload
- **Routing Envelopes**: Router sockets add identity frames
- **Structured Messages**: Command + parameters structure
- **Binary Protocols**: Mix text and binary data
- **Message Delimiting**: Use empty frames as separators

## Memory Management

When using `RecvMultipart()` or `TryRecvMultipart()`, always dispose the returned `MultipartMessage`:

```csharp
using var message = receiver.RecvMultipart();
// Use message...
// Automatically disposed at end of scope
```

This ensures all underlying `Message` objects are properly released.
