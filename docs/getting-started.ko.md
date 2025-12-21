# 시작하기

[![English](https://img.shields.io/badge/lang-en-red.svg)](getting-started.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](getting-started.ko.md)

Net.Zmq에 오신 것을 환영합니다! 이 가이드는 ZeroMQ를 위한 현대적인 .NET 8+ 바인딩인 Net.Zmq를 시작하는 데 도움을 드립니다.

## 설치

NuGet 패키지 관리자를 통해 Net.Zmq를 설치하세요:

### .NET CLI 사용

```bash
dotnet add package Net.Zmq
```

### 패키지 관리자 콘솔 사용

```powershell
Install-Package Net.Zmq
```

### Visual Studio 사용

1. 솔루션 탐색기에서 프로젝트를 마우스 오른쪽 버튼으로 클릭
2. "NuGet 패키지 관리" 선택
3. "Net.Zmq" 검색
4. "설치" 클릭

## 요구사항

- **.NET 8.0 이상**: Net.Zmq는 최신 .NET을 위해 빌드되었습니다
- **네이티브 libzmq 라이브러리**: Net.Zmq.Native 패키지 종속성을 통해 자동으로 포함됩니다

## 첫 번째 Net.Zmq 애플리케이션

기본 개념을 이해하기 위해 간단한 요청-응답 애플리케이션을 만들어 보겠습니다.

### 1. 서버 생성

서버는 들어오는 요청을 수신하고 응답을 보냅니다.

```csharp
using Net.Zmq;

// ZeroMQ 컨텍스트 생성
using var context = new Context();

// REP (Reply) 소켓 생성
using var server = new Socket(context, SocketType.Rep);

// TCP 엔드포인트에 바인딩
server.Bind("tcp://*:5555");

Console.WriteLine("Server is listening on port 5555...");

while (true)
{
    // 요청 대기
    var request = server.RecvString();
    Console.WriteLine($"Received: {request}");

    // 응답 전송
    server.Send("World");
}
```

### 2. 클라이언트 생성

클라이언트는 요청을 보내고 응답을 기다립니다.

```csharp
using Net.Zmq;

// ZeroMQ 컨텍스트 생성
using var context = new Context();

// REQ (Request) 소켓 생성
using var client = new Socket(context, SocketType.Req);

// 서버에 연결
client.Connect("tcp://localhost:5555");

// 요청 전송
client.Send("Hello");
Console.WriteLine("Sent: Hello");

// 응답 대기
var reply = client.RecvString();
Console.WriteLine($"Received: {reply}");
```

### 3. 애플리케이션 실행

1. 먼저 서버 애플리케이션 시작
2. 클라이언트 애플리케이션 실행
3. 서버 콘솔에 "Hello"가, 클라이언트 콘솔에 "World"가 표시됩니다

## 기본 개념

### Context

`Context`는 단일 프로세스의 모든 소켓을 위한 컨테이너입니다. I/O 스레드와 내부 리소스를 관리합니다.

```csharp
// 기본 컨텍스트 (I/O 스레드 1개, 최대 소켓 1024개)
using var context = new Context();

// 커스텀 컨텍스트
using var context = new Context(ioThreads: 2, maxSockets: 2048);
```

**모범 사례**: 애플리케이션당 하나의 컨텍스트를 사용하세요. 여러 컨텍스트를 생성하는 것은 거의 필요하지 않습니다.

### Socket

`Socket`은 메시지를 송수신하는 엔드포인트입니다. 각 소켓은 동작을 정의하는 타입을 가집니다.

```csharp
// 소켓 생성
using var socket = new Socket(context, SocketType.Rep);

// 엔드포인트에 바인딩 (서버 측)
socket.Bind("tcp://*:5555");

// 엔드포인트에 연결 (클라이언트 측)
socket.Connect("tcp://localhost:5555");
```

### Message

메시지는 소켓 간에 전송되는 데이터 단위입니다. Net.Zmq는 메시지를 다루는 여러 방법을 제공합니다:

```csharp
// 문자열 전송
socket.Send("Hello World");

// 바이트 전송
byte[] data = [1, 2, 3, 4, 5];
socket.Send(data);

// 고급 시나리오를 위한 Message 객체 사용
using var message = new Message("Hello");
socket.Send(ref message, SendFlags.None);

// 문자열 수신
string text = socket.RecvString();

// 바이트 수신
byte[] received = socket.RecvBytes();
```

### 엔드포인트

Net.Zmq는 여러 전송 프로토콜을 지원합니다:

| 전송 | 형식 | 설명 |
|------|------|------|
| TCP | `tcp://hostname:port` | 네트워크 통신 |
| IPC | `ipc:///tmp/socket` | 프로세스 간 통신 (Unix domain socket) |
| In-Process | `inproc://name` | 프로세스 내 통신 (가장 빠름) |
| PGM/EPGM | `pgm://interface;multicast` | 안정적인 멀티캐스트 |

**예제**:
```csharp
socket.Bind("tcp://*:5555");              // 모든 인터페이스에서 TCP
socket.Connect("tcp://192.168.1.100:5555"); // 특정 호스트로 TCP
socket.Bind("ipc:///tmp/my-socket");       // Unix domain socket
socket.Bind("inproc://my-queue");          // 프로세스 내
```

## 소켓 패턴

Net.Zmq는 여러 메시징 패턴을 지원합니다. 가장 일반적인 패턴은 다음과 같습니다:

### Request-Reply (REQ-REP)

동기식 클라이언트-서버 패턴입니다. 클라이언트가 요청을 보내면 서버가 응답을 보냅니다.

```csharp
// 서버
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");
var request = server.RecvString();
server.Send("Response");

// 클라이언트
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");
client.Send("Request");
var reply = client.RecvString();
```

### Publish-Subscribe (PUB-SUB)

일대다 배포 패턴입니다. 퍼블리셔가 모든 서브스크라이버에게 메시지를 보냅니다.

```csharp
// 퍼블리셔
using var publisher = new Socket(context, SocketType.Pub);
publisher.Bind("tcp://*:5556");
publisher.Send("topic data");

// 서브스크라이버
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");
subscriber.Subscribe("topic");
var message = subscriber.RecvString();
```

### Push-Pull (Pipeline)

로드 밸런싱된 작업 배포 패턴입니다. 작업이 워커들 사이에 분산됩니다.

```csharp
// Push (프로듀서)
using var pusher = new Socket(context, SocketType.Push);
pusher.Bind("tcp://*:5557");
pusher.Send("work item");

// Pull (워커)
using var puller = new Socket(context, SocketType.Pull);
puller.Connect("tcp://localhost:5557");
var work = puller.RecvString();
```

모든 패턴에 대한 자세한 정보는 [메시징 패턴](patterns.ko.md) 가이드를 참조하세요.

## 리소스 관리

Net.Zmq는 적절한 리소스 정리를 위해 `IDisposable`을 사용합니다. 항상 `using` 문을 사용하세요:

```csharp
// 올바른 방법: using 문이 적절한 정리를 보장
using var context = new Context();
using var socket = new Socket(context, SocketType.Rep);

// 또한 올바른 방법: 명시적 dispose
var context = new Context();
try
{
    var socket = new Socket(context, SocketType.Rep);
    // 소켓 사용...
}
finally
{
    context.Dispose();
}
```

## 오류 처리

Net.Zmq는 오류 시 예외를 발생시킵니다. 항상 적절하게 처리하세요:

```csharp
try
{
    socket.Bind("tcp://*:5555");
    var message = socket.RecvString();
}
catch (ZmqException ex)
{
    Console.WriteLine($"ZMQ Error: {ex.ErrorCode} - {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## 다음 단계

- [메시징 패턴](patterns.ko.md)에 대해 자세히 알아보기
- 고급 기능을 위한 [API 사용 가이드](api-usage.ko.md) 살펴보기
- 성능 튜닝 및 모범 사례를 위한 [고급 주제](advanced-topics.ko.md) 확인하기
- 완전한 문서는 [API 레퍼런스](../api/index.html)에서 찾아보기

## 추가 리소스

- [ZeroMQ 가이드](https://zguide.zeromq.org/) - 공식 ZeroMQ 가이드
- [GitHub 리포지토리](https://github.com/ulala-x/net-zmq) - 소스 코드 및 예제
- [NuGet 패키지](https://www.nuget.org/packages/Net.Zmq) - 최신 릴리스
