[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

# NetZeroMQ ROUTER-DEALER 비동기 브로커 샘플

이 샘플은 NetZeroMQ를 사용한 ZeroMQ Router-Dealer 비동기 브로커 패턴을 보여줍니다.

## 패턴 개요

비동기 브로커 패턴은 여러 클라이언트의 요청을 여러 워커에게 비동기적으로 라우팅하는 부하 분산 메시지 브로커를 제공합니다.

### 아키텍처

```
Clients (DEALER)  →  Broker (ROUTER-ROUTER)  →  Workers (DEALER)
                         Frontend | Backend
```

- **프론트엔드 (ROUTER)**: 클라이언트로부터 요청을 수신합니다
- **백엔드 (ROUTER)**: 워커에게 작업을 분배합니다
- **클라이언트 (DEALER)**: 비동기적으로 요청을 보내고 응답을 수신합니다
- **워커 (DEALER)**: 요청을 처리하고 응답을 보냅니다

## 주요 패턴

### 1. 명시적 라우팅 ID
클라이언트와 워커 모두 명시적 라우팅 ID를 설정합니다:
```csharp
socket.SetOption(SocketOption.Routing_Id, "client-1");
```

### 2. 멀티파트 메시지 형식

**클라이언트에서 브로커로 (DEALER → ROUTER)**:
```
[빈 프레임]
[요청 데이터]
```

**브로커가 클라이언트로부터 수신 (ROUTER가 식별자 추가)**:
```
[클라이언트 식별자]
[빈 프레임]
[요청 데이터]
```

**브로커에서 워커로 (ROUTER → DEALER)**:
```
[워커 식별자]
[빈 프레임]
[클라이언트 식별자]
[빈 프레임]
[요청 데이터]
```

**워커가 브로커로부터 수신 (DEALER가 식별자 제거)**:
```
[빈 프레임]
[클라이언트 식별자]
[빈 프레임]
[요청 데이터]
```

### 3. 비동기 요청/응답
REQ-REP와 달리 DEALER 소켓은:
- 엄격한 요청-응답 순서를 강제하지 않습니다
- 응답을 기다리지 않고 여러 요청을 보낼 수 있습니다
- 진정한 비동기 통신을 가능하게 합니다

### 4. 부하 분산
브로커는 사용 가능한 워커의 큐를 유지하고 가장 최근에 사용되지 않은 워커에게 요청을 라우팅합니다.

## 샘플 실행

### 사전 요구 사항
이 샘플은 네이티브 libzmq 라이브러리가 필요합니다. 개발을 위해 `libzmq.dll`을 출력 디렉토리에 복사하세요:

```bash
# Windows (PowerShell)
Copy-Item native\runtimes\win-x64\native\libzmq.dll samples\NetZeroMQ.Samples.RouterDealer\bin\Debug\net8.0\

# Linux
cp native/runtimes/linux-x64/native/libzmq.so samples/NetZeroMQ.Samples.RouterDealer/bin/Debug/net8.0/

# macOS
cp native/runtimes/osx-x64/native/libzmq.dylib samples/NetZeroMQ.Samples.RouterDealer/bin/Debug/net8.0/
```

### 빌드 및 실행

```bash
# 빌드
dotnet build

# 실행
dotnet run
```

### 예상 출력

```
NetZeroMQ ROUTER-DEALER Async Broker Sample
==========================================

[Broker] Starting...
[Broker] Frontend listening on tcp://*:5555
[Broker] Backend listening on tcp://*:5556
[Broker] Polling started...
[worker-1] Starting...
[worker-2] Starting...
[worker-1] Connected to broker
[worker-2] Connected to broker
[Broker] Worker worker-1 is ready
[Broker] Worker worker-2 is ready
[client-1] Starting...
[client-2] Starting...
[client-1] Connected to broker
[client-2] Connected to broker
[client-1] Sent: Request #1 from client-1
[Broker] Client client-1 -> Request: Request #1 from client-1
[Broker] Routed to Worker worker-1 for Client client-1
[worker-1] Processing request from client-1: Request #1 from client-1
[worker-1] Sent reply to client-1: Processed by worker-1
[Broker] Worker worker-1 -> Client client-1: Processed by worker-1
[client-1] Received: Processed by worker-1
...
```

## 코드 하이라이트

### 브로커 구현
```csharp
// 프론트엔드와 백엔드 모두에 대해 ROUTER 소켓 생성
using var frontend = new Socket(ctx, SocketType.Router);
using var backend = new Socket(ctx, SocketType.Router);

frontend.Bind("tcp://*:5555");  // 클라이언트용
backend.Bind("tcp://*:5556");   // 워커용

// Poller 생성 및 두 소켓 추가
using var poller = new Poller(capacity: 2);
int frontendIdx = poller.Add(frontend, PollEvents.In);
int backendIdx = poller.Add(backend, PollEvents.In);

while (true)
{
    poller.Poll(timeout: 100);

    if (poller.IsReadable(frontendIdx))
    {
        // 클라이언트 요청 처리
    }

    if (poller.IsReadable(backendIdx))
    {
        // 워커 응답 또는 READY 메시지 처리
    }
}
```

### 클라이언트 구현
```csharp
using var socket = new Socket(ctx, SocketType.Dealer);
socket.SetOption(SocketOption.Routing_Id, "client-1");
socket.Connect("tcp://localhost:5555");

// 요청 전송 (DEALER가 빈 프레임 추가)
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("Request data", SendFlags.None);

// 응답 수신
var empty = RecvBytes(socket);
var reply = socket.RecvString();
```

### 워커 구현
```csharp
using var socket = new Socket(ctx, SocketType.Dealer);
socket.SetOption(SocketOption.Routing_Id, "worker-1");
socket.Connect("tcp://localhost:5556");

// READY 메시지 전송
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("READY", SendFlags.SendMore);
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("READY", SendFlags.None);

// 요청 수신
var empty1 = RecvBytes(socket);
var clientId = RecvBytes(socket);
var empty2 = RecvBytes(socket);
var request = RecvBytes(socket);

// 처리 후 응답 전송
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send(clientId, SendFlags.SendMore);
socket.Send(Array.Empty<byte>(), SendFlags.SendMore);
socket.Send("Reply data", SendFlags.None);
```

## 사용 사례

이 패턴은 다음과 같은 경우에 이상적입니다:
- 부하 분산 요청 처리
- 분산 작업 큐
- 마이크로서비스 아키텍처
- 비동기 RPC 시스템
- 워커 풀 관리

## 참고 자료

- [ZeroMQ 가이드 - 고급 요청-응답 패턴](http://zguide.zeromq.org/page:all#Advanced-Request-Reply-Patterns)
- [ROUTER 소켓 유형](http://api.zeromq.org/4-3:zmq-socket#toc17)
- [DEALER 소켓 유형](http://api.zeromq.org/4-3:zmq-socket#toc15)
