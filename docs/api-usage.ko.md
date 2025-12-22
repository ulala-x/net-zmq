[![English](https://img.shields.io/badge/lang:en-red.svg)](api-usage.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](api-usage.ko.md)

# API 사용 가이드

이 가이드는 Net.Zmq의 핵심 API 클래스인 Context, Socket, Message, Poller를 사용하는 방법에 대한 상세한 문서를 제공합니다.

## Context

`Context` 클래스는 I/O 스레드와 소켓을 포함한 ZeroMQ 리소스를 관리합니다. 일반적으로 애플리케이션당 하나의 컨텍스트를 생성합니다.

### 컨텍스트 생성

```csharp
using Net.Zmq;

// Default context (1 I/O thread, 1024 max sockets)
using var context = new Context();

// Custom context with specific settings
using var context = new Context(ioThreads: 2, maxSockets: 2048);
```

### 컨텍스트 옵션

`SetOption` 및 `GetOption` 메서드를 사용하여 컨텍스트를 구성할 수 있습니다:

```csharp
using var context = new Context();

// Set I/O threads (must be set before creating sockets)
context.SetOption(ContextOption.IoThreads, 4);

// Set maximum number of sockets
context.SetOption(ContextOption.MaxSockets, 512);

// Set maximum message size (0 = unlimited)
context.SetOption(ContextOption.MaxMsgsz, 1024 * 1024); // 1MB

// Get current values
var ioThreads = context.GetOption(ContextOption.IoThreads);
var maxSockets = context.GetOption(ContextOption.MaxSockets);

Console.WriteLine($"I/O Threads: {ioThreads}, Max Sockets: {maxSockets}");
```

### 사용 가능한 컨텍스트 옵션

| 옵션 | 타입 | 설명 |
|--------|------|-------------|
| `IoThreads` | int | I/O 스레드 수 (기본값: 1) |
| `MaxSockets` | int | 최대 소켓 수 (기본값: 1024) |
| `MaxMsgsz` | int | 최대 메시지 크기 (바이트, 0 = 무제한) |
| `SocketLimit` | int | 설정 가능한 최대 소켓 값 |
| `Ipv6` | bool | IPv6 지원 활성화 |
| `Blocky` | bool | 블로킹 종료 동작 사용 |
| `ThreadPriority` | int | 스레드 스케줄링 우선순위 |
| `ThreadSchedPolicy` | int | 스레드 스케줄링 정책 |

### ZeroMQ 버전 및 기능

```csharp
// Get ZeroMQ library version
var (major, minor, patch) = Context.Version;
Console.WriteLine($"ZeroMQ Version: {major}.{minor}.{patch}");

// Check if a capability is supported
bool hasCurve = Context.Has("curve");      // Encryption support
bool hasDraft = Context.Has("draft");       // Draft API support
bool hasGssapi = Context.Has("gssapi");     // GSSAPI auth support

Console.WriteLine($"CURVE encryption: {hasCurve}");
```

### 리소스 관리

사용이 끝나면 항상 컨텍스트를 폐기하세요:

```csharp
// Using statement (recommended)
using var context = new Context();

// Manual disposal
var context = new Context();
try
{
    // Use context...
}
finally
{
    context.Dispose();
}
```

## Socket

`Socket` 클래스는 메시지를 송수신하기 위한 ZeroMQ 소켓 엔드포인트를 나타냅니다.

### 소켓 생성

```csharp
using var context = new Context();

// Create different socket types
using var req = new Socket(context, SocketType.Req);      // Request
using var rep = new Socket(context, SocketType.Rep);      // Reply
using var pub = new Socket(context, SocketType.Pub);      // Publish
using var sub = new Socket(context, SocketType.Sub);      // Subscribe
using var push = new Socket(context, SocketType.Push);    // Push
using var pull = new Socket(context, SocketType.Pull);    // Pull
using var dealer = new Socket(context, SocketType.Dealer); // Dealer
using var router = new Socket(context, SocketType.Router); // Router
using var pair = new Socket(context, SocketType.Pair);    // Pair
```

### 연결 및 바인딩

```csharp
using var socket = new Socket(context, SocketType.Rep);

// Bind (server-side, accepts connections)
socket.Bind("tcp://*:5555");                    // All interfaces
socket.Bind("tcp://192.168.1.100:5555");        // Specific interface
socket.Bind("ipc:///tmp/my-socket");            // Unix domain socket
socket.Bind("inproc://my-endpoint");            // In-process

// Connect (client-side, initiates connection)
socket.Connect("tcp://localhost:5555");
socket.Connect("tcp://192.168.1.100:5555");

// Unbind and disconnect
socket.Unbind("tcp://*:5555");
socket.Disconnect("tcp://localhost:5555");
```

### 메시지 전송

Net.Zmq는 메시지를 전송하기 위한 여러 메서드를 제공합니다:

#### 문자열 전송

```csharp
// Simple string send
socket.Send("Hello World");

// Send with encoding
socket.Send("안녕하세요", Encoding.UTF8);

// Non-blocking send
bool sent = socket.Send("Hello", SendFlags.DontWait);
if (sent)
{
    Console.WriteLine("Message sent successfully");
}
```

#### 바이트 배열 전송

```csharp
// Send byte array
byte[] data = [1, 2, 3, 4, 5];
socket.Send(data);

// Non-blocking send
bool sent = socket.Send(data, SendFlags.DontWait); // false if would block
```

#### 다중 파트 메시지 전송

```csharp
// Send multi-part message
socket.Send("Header", SendFlags.SendMore);
socket.Send("Body", SendFlags.SendMore);
socket.Send("Footer"); // Last frame without SendMore

// With bytes
socket.Send(headerBytes, SendFlags.SendMore);
socket.Send(bodyBytes);
```

#### 전송 플래그

| 플래그 | 설명 |
|------|-------------|
| `None` | 블로킹 전송 |
| `DontWait` | 논블로킹 전송 |
| `SendMore` | 추가 메시지 프레임이 따라옴 |

### 메시지 수신

#### 문자열 수신

```csharp
// Blocking receive
string message = socket.RecvString();

// With encoding
string message = socket.RecvString(Encoding.UTF8);

// Non-blocking receive
bool received = socket.TryRecvString(out string result);
if (received)
{
    Console.WriteLine($"Received: {result}");
}
```

#### 바이트 수신

```csharp
// Receive into new array
byte[] data = socket.RecvBytes();

// Receive into existing buffer
byte[] buffer = new byte[1024];
int bytesReceived = socket.Recv(buffer);
Console.WriteLine($"Received {bytesReceived} bytes");

// Non-blocking receive
bool received = socket.TryRecvBytes(out byte[] result);
if (received)
{
    Console.WriteLine($"Received {result.Length} bytes");
}
```

#### 다중 파트 메시지 수신

```csharp
// Check if more frames are available
var part1 = socket.RecvString();
bool hasMore = socket.GetOption<bool>(SocketOption.RcvMore);

if (hasMore)
{
    var part2 = socket.RecvString();
}

// Receive all parts
var parts = new List<string>();
do
{
    parts.Add(socket.RecvString());
} while (socket.GetOption<bool>(SocketOption.RcvMore));
```

#### 수신 플래그

| 플래그 | 설명 |
|------|-------------|
| `None` | 블로킹 수신 |
| `DontWait` | 논블로킹 수신 |

### 소켓 옵션

옵션을 사용하여 소켓 동작을 구성합니다:

```csharp
using var socket = new Socket(context, SocketType.Rep);

// Set options
socket.SetOption(SocketOption.Linger, 1000);           // Linger time (ms)
socket.SetOption(SocketOption.Sndhwm, 1000);           // Send high water mark
socket.SetOption(SocketOption.Rcvhwm, 1000);           // Receive high water mark
socket.SetOption(SocketOption.Sndtimeo, 5000);         // Send timeout (ms)
socket.SetOption(SocketOption.Rcvtimeo, 5000);         // Receive timeout (ms)
socket.SetOption(SocketOption.Sndbuf, 131072);         // Send buffer size
socket.SetOption(SocketOption.Rcvbuf, 131072);         // Receive buffer size

// Get options
int linger = socket.GetOption<int>(SocketOption.Linger);
int sendHwm = socket.GetOption<int>(SocketOption.Sndhwm);

Console.WriteLine($"Linger: {linger}ms, Send HWM: {sendHwm}");
```

### 일반적인 소켓 옵션

| 옵션 | 타입 | 설명 |
|--------|------|-------------|
| `Linger` | int | 닫을 때 대기 중인 메시지를 기다리는 시간 (ms) |
| `Sndhwm` | int | 아웃바운드 메시지의 고수위 마크 (High Water Mark) |
| `Rcvhwm` | int | 인바운드 메시지의 고수위 마크 |
| `Sndtimeo` | int | 전송 타임아웃 (밀리초) |
| `Rcvtimeo` | int | 수신 타임아웃 (밀리초) |
| `Sndbuf` | int | 커널 전송 버퍼 크기 |
| `Rcvbuf` | int | 커널 수신 버퍼 크기 |
| `Routing_Id` | byte[] | ROUTER 소켓의 소켓 신원 |
| `RcvMore` | bool | 추가 메시지 프레임 사용 가능 여부 |

### Subscribe/Unsubscribe (SUB 소켓 전용)

```csharp
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");

// Subscribe to topics
subscriber.Subscribe("weather.");
subscriber.Subscribe("stock.AAPL");
subscriber.Subscribe("");  // All messages

// Unsubscribe
subscriber.Unsubscribe("weather.");
```

## Message

`Message` 클래스는 메시지 프레임에 대한 저수준 제어를 제공합니다.

### 메시지 생성

```csharp
using Net.Zmq;

// Empty message
using var msg1 = new Message();

// From string
using var msg2 = new Message("Hello World");

// From byte array
byte[] data = [1, 2, 3, 4, 5];
using var msg3 = new Message(data);

// With specific size
using var msg4 = new Message(1024); // Allocates 1KB
```

### 메시지 속성

```csharp
using var message = new Message("Hello");

// Get message data as span
ReadOnlySpan<byte> data = message.Data;

// Get size
int size = message.Size;

// Convert to string
string text = message.ToString();
string utf8Text = message.ToString(Encoding.UTF8);

// Get byte array
byte[] bytes = message.ToByteArray();

// Check if more frames follow
bool hasMore = message.More;
```

### 메시지 전송

```csharp
using var message = new Message("Hello World");

// Send message (note: ref keyword required)
socket.Send(ref message, SendFlags.None);

// Multi-part send
using var header = new Message("Header");
using var body = new Message("Body");

socket.Send(ref header, SendFlags.SendMore);
socket.Send(ref body, SendFlags.None);
```

### 메시지 수신

```csharp
using var message = new Message();

// Receive into message
socket.Recv(ref message, RecvFlags.None);

// Process message
Console.WriteLine($"Size: {message.Size}");
Console.WriteLine($"Content: {message.ToString()}");

// Non-blocking receive
using var msg = new Message();
bool received = socket.TryRecv(ref msg);
if (received)
{
    Console.WriteLine($"Received: {msg.ToString()}");
}
```

### 메시지 메타데이터

```csharp
using var message = new Message();
socket.Recv(ref message);

// Get metadata property (e.g., for ZMTP 3.0 properties)
string? property = message.Gets("Property-Name");
if (property != null)
{
    Console.WriteLine($"Property: {property}");
}
```

## Poller

`Poller` 클래스는 인스턴스 기반 API를 사용하여 여러 소켓에 대한 I/O 이벤트 다중화를 가능하게 합니다.

### Poller 생성

```csharp
using Net.Zmq;

// Create a Poller with specified capacity (maximum number of sockets)
using var poller = new Poller(capacity: 2);
```

### 기본 폴링

```csharp
using Net.Zmq;

using var context = new Context();
using var socket1 = new Socket(context, SocketType.Pull);
using var socket2 = new Socket(context, SocketType.Pull);

socket1.Bind("tcp://*:5555");
socket2.Bind("tcp://*:5556");

// Create Poller and add sockets
using var poller = new Poller(capacity: 2);
int idx1 = poller.Add(socket1, PollEvents.In);
int idx2 = poller.Add(socket2, PollEvents.In);

// Poll with 1 second timeout
while (true)
{
    int ready = poller.Poll(timeout: 1000);

    if (ready > 0)
    {
        // Check which sockets are ready using their indices
        if (poller.IsReadable(idx1))
        {
            var msg = socket1.RecvString();
            Console.WriteLine($"Socket 1: {msg}");
        }

        if (poller.IsReadable(idx2))
        {
            var msg = socket2.RecvString();
            Console.WriteLine($"Socket 2: {msg}");
        }
    }
    else
    {
        Console.WriteLine("Poll timeout");
    }
}
```

### 폴 이벤트

```csharp
using var poller = new Poller(capacity: 4);

// Add sockets with different event types
int idx1 = poller.Add(socket1, PollEvents.In);                    // Read events
int idx2 = poller.Add(socket2, PollEvents.Out);                   // Write events
int idx3 = poller.Add(socket3, PollEvents.In | PollEvents.Out);   // Both
int idx4 = poller.Add(socket4, PollEvents.Err);                   // Error events

int ready = poller.Poll(timeout: 1000);

// Check event types using socket indices
if (poller.IsReadable(idx1)) { /* Handle read */ }
if (poller.IsWritable(idx2)) { /* Handle write */ }
if (poller.IsReadable(idx3) || poller.IsWritable(idx3)) { /* Handle both */ }
if (poller.HasError(idx4)) { /* Handle error */ }
```

### 이벤트 업데이트

```csharp
using var poller = new Poller(capacity: 2);

// Add socket with initial events
int idx = poller.Add(socket, PollEvents.In);

// Update events for existing socket
poller.Update(idx, PollEvents.In | PollEvents.Out);

// Later, change to write-only
poller.Update(idx, PollEvents.Out);
```

### 폴 타임아웃

```csharp
using var poller = new Poller(capacity: 2);
poller.Add(socket1, PollEvents.In);
poller.Add(socket2, PollEvents.In);

// Block indefinitely until event occurs
poller.Poll(timeout: -1);

// Return immediately (non-blocking)
poller.Poll(timeout: 0);

// Wait up to 5 seconds
poller.Poll(timeout: 5000);
```

### Poller 정리 및 재사용

```csharp
using var poller = new Poller(capacity: 2);

// Add sockets
int idx1 = poller.Add(socket1, PollEvents.In);
int idx2 = poller.Add(socket2, PollEvents.In);

// Use poller...
poller.Poll(timeout: 1000);

// Clear all registered sockets
poller.Clear();

// Add new sockets (reusing the same Poller instance)
int idx3 = poller.Add(socket3, PollEvents.In);
int idx4 = poller.Add(socket4, PollEvents.In);
```

### 고급 폴링 예제

```csharp
using Net.Zmq;

using var context = new Context();

// Create multiple sockets
using var receiver = new Socket(context, SocketType.Pull);
using var sender = new Socket(context, SocketType.Push);
using var control = new Socket(context, SocketType.Pair);

receiver.Bind("tcp://*:5555");
sender.Connect("tcp://localhost:5556");
control.Bind("inproc://control");

// Create Poller and add sockets
using var poller = new Poller(capacity: 2);
int receiverIdx = poller.Add(receiver, PollEvents.In);
int controlIdx = poller.Add(control, PollEvents.In);

bool running = true;
while (running)
{
    // Poll with 100ms timeout
    int ready = poller.Poll(timeout: 100);

    if (ready > 0)
    {
        // Handle incoming messages
        if (poller.IsReadable(receiverIdx))
        {
            var msg = receiver.RecvString();
            Console.WriteLine($"Received: {msg}");

            // Forward to sender
            sender.Send($"Processed: {msg}");
        }

        // Handle control messages
        if (poller.IsReadable(controlIdx))
        {
            var cmd = control.RecvString();
            if (cmd == "STOP")
            {
                running = false;
            }
        }
    }
}
```

### Poller API 레퍼런스

| 메서드 | 설명 |
|--------|-------------|
| `Poller(int capacity)` | 지정된 최대 소켓 용량으로 Poller 생성 |
| `Add(Socket, PollEvents)` | 폴러에 소켓을 추가하고 인덱스 반환 |
| `Update(int index, PollEvents)` | 주어진 인덱스의 소켓에 대한 폴 이벤트 업데이트 |
| `Poll(long timeout)` | 등록된 소켓의 이벤트 대기 (타임아웃 밀리초, -1 = 무한대) |
| `IsReadable(int index)` | 주어진 인덱스의 소켓이 읽기 가능한지 확인 |
| `IsWritable(int index)` | 주어진 인덱스의 소켓이 쓰기 가능한지 확인 |
| `HasError(int index)` | 주어진 인덱스의 소켓에 오류가 있는지 확인 |
| `Clear()` | 폴러에서 등록된 모든 소켓 제거 |
| `Dispose()` | 폴러가 사용하는 리소스 해제 |

### PollEvents 플래그

| 플래그 | 설명 |
|------|-------------|
| `None` | 이벤트 없음 |
| `In` | 소켓이 읽기 가능 (수신 메시지 사용 가능) |
| `Out` | 소켓이 쓰기 가능 (블로킹 없이 메시지 전송 가능) |
| `Err` | 소켓에 오류 조건 발생 |

## 오류 처리

Net.Zmq는 오류에 대해 예외를 발생시킵니다:

```csharp
using Net.Zmq;

try
{
    using var context = new Context();
    using var socket = new Socket(context, SocketType.Rep);

    socket.Bind("tcp://*:5555");
    var message = socket.RecvString();
}
catch (ZmqException ex)
{
    Console.WriteLine($"ZMQ Error {ex.ErrorCode}: {ex.Message}");

    // Common error codes
    if (ex.ErrorCode == ErrorCode.EADDRINUSE)
    {
        Console.WriteLine("Address already in use");
    }
    else if (ex.ErrorCode == ErrorCode.EAGAIN)
    {
        Console.WriteLine("Resource temporarily unavailable");
    }
}
catch (ObjectDisposedException)
{
    Console.WriteLine("Socket or context already disposed");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## 모범 사례

### Context

- 애플리케이션당 하나의 컨텍스트 생성
- 모든 소켓이 닫힌 후에만 컨텍스트 폐기
- CPU 코어 기반으로 I/O 스레드 설정 (일반적으로 코어 4개당 1개)

### Socket

- 자동 폐기를 위해 항상 `using` 문 사용
- 무한 블로킹을 방지하기 위해 타임아웃 설정
- 메모리 문제를 방지하기 위해 고수위 마크 구성
- 패턴에 적합한 소켓 타입 사용

### Message

- 간단한 경우 문자열/바이트 메서드 사용
- 제로카피 시나리오에는 Message 클래스 사용
- 항상 메시지를 명시적으로 폐기
- 큰 메시지 데이터를 불필요하게 복사하지 않음

### Poller

- 적절한 용량으로 Poller 인스턴스 생성
- 이벤트 확인을 위해 Add()가 반환한 소켓 인덱스 저장
- 다중 소켓 I/O 다중화에 폴링 사용
- 합리적인 타임아웃 값 설정 (-1은 무한대, 0은 논블로킹, 양수는 타임아웃)
- 모든 가능한 이벤트 처리 (IsReadable, IsWritable, HasError)
- 소켓을 제거하고 다시 추가하지 않고 Update()를 사용하여 이벤트 변경
- 리셋하고 동일한 Poller 인스턴스를 재사용하려면 Clear() 호출
- 사용이 끝나면 항상 Poller 인스턴스 폐기

## 다음 단계

- [메시징 패턴](patterns.ko.md)에서 실용적인 예제 탐색
- [고급 주제](advanced-topics.ko.md)에서 성능 최적화 읽기
- [API 레퍼런스](../api/index.html)에서 완전한 문서 탐색
