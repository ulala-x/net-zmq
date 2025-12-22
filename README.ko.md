# Net.Zmq

[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

[![Build and Test](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Zmq.svg)](https://www.nuget.org/packages/Net.Zmq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Documentation](https://img.shields.io/badge/docs-online-blue.svg)](https://ulala-x.github.io/net-zmq/)
[![Changelog](https://img.shields.io/badge/changelog-v0.2.0-green.svg)](CHANGELOG.md)

cppzmq 스타일 API를 제공하는 현대적인 .NET 8+ ZeroMQ (libzmq) 바인딩입니다.

## 주요 기능

- **모던 .NET**: `[LibraryImport]` 소스 생성기를 사용하는 .NET 8.0+ 빌드 (런타임 마샬링 오버헤드 없음)
- **cppzmq 스타일**: C++에서 넘어온 개발자에게 익숙한 API
- **타입 안전성**: 강타입 소켓 옵션, 메시지 속성, 열거형
- **크로스 플랫폼**: Windows, Linux, macOS (x64, ARM64) 지원
- **안전한 기본값**: SafeHandle 기반 리소스 관리

## 설치

```bash
dotnet add package Net.Zmq
```

## 빠른 시작

### REQ-REP 패턴

```csharp
using Net.Zmq;

// 서버
using var ctx = new Context();
using var server = new Socket(ctx, SocketType.Rep);
server.Bind("tcp://*:5555");

var request = server.RecvString();
server.Send("World");

// 클라이언트
using var client = new Socket(ctx, SocketType.Req);
client.Connect("tcp://localhost:5555");
client.Send("Hello");
var reply = client.RecvString();
```

### PUB-SUB 패턴

```csharp
using Net.Zmq;

// 퍼블리셔
using var ctx = new Context();
using var pub = new Socket(ctx, SocketType.Pub);
pub.Bind("tcp://*:5556");
pub.Send("topic1 Hello subscribers!");

// 서브스크라이버
using var sub = new Socket(ctx, SocketType.Sub);
sub.Connect("tcp://localhost:5556");
sub.Subscribe("topic1");
var message = sub.RecvString();
```

### Router-to-Router 패턴

```csharp
using System.Text;
using Net.Zmq;

using var ctx = new Context();
using var peerA = new Socket(ctx, SocketType.Router);
using var peerB = new Socket(ctx, SocketType.Router);

// Router-to-Router를 위한 명시적 식별자 설정
peerA.SetOption(SocketOption.Routing_Id, "PEER_A"u8.ToArray());
peerB.SetOption(SocketOption.Routing_Id, "PEER_B"u8.ToArray());

peerA.Bind("tcp://127.0.0.1:5555");
peerB.Connect("tcp://127.0.0.1:5555");

// Peer B가 Peer A로 전송 (첫 번째 프레임 = 대상 식별자)
peerB.Send("PEER_A"u8, SendFlags.SendMore);
peerB.Send("Hello from Peer B!");

// Peer A가 수신 (첫 번째 프레임 = 발신자 식별자)
Span<byte> idBuffer = stackalloc byte[64];
int idLen = peerA.Recv(idBuffer);
var senderId = idBuffer[..idLen];
var message = peerA.RecvString();

// Peer A가 발신자의 식별자를 사용하여 응답
peerA.Send(senderId, SendFlags.SendMore);
peerA.Send("Hello back from Peer A!");
```

### 폴링

```csharp
using Net.Zmq;

// Poller 인스턴스 생성
using var poller = new Poller(capacity: 2);

// 소켓 추가 및 인덱스 저장
int idx1 = poller.Add(socket1, PollEvents.In);
int idx2 = poller.Add(socket2, PollEvents.In);

// 이벤트 폴링
if (poller.Poll(timeout: 1000) > 0)
{
    if (poller.IsReadable(idx1)) { /* socket1 처리 */ }
    if (poller.IsReadable(idx2)) { /* socket2 처리 */ }
}
```

### Message API

```csharp
using Net.Zmq;

// 메시지 생성 및 전송
using var msg = new Message("Hello World");
socket.Send(msg);

// 메시지 수신
using var reply = new Message();
socket.Recv(reply);
Console.WriteLine(reply.ToString());
```

## 소켓 타입

| 타입 | 설명 |
|------|------|
| `SocketType.Req` | Request 소켓 (클라이언트) |
| `SocketType.Rep` | Reply 소켓 (서버) |
| `SocketType.Pub` | Publish 소켓 |
| `SocketType.Sub` | Subscribe 소켓 |
| `SocketType.Push` | Push 소켓 (파이프라인) |
| `SocketType.Pull` | Pull 소켓 (파이프라인) |
| `SocketType.Dealer` | 비동기 요청 |
| `SocketType.Router` | 비동기 응답 |
| `SocketType.Pair` | 독점 페어 |

## API 레퍼런스

### Context

```csharp
var ctx = new Context();                           // 기본값
var ctx = new Context(ioThreads: 2, maxSockets: 1024);  // 커스텀

ctx.SetOption(ContextOption.IoThreads, 4);
var threads = ctx.GetOption(ContextOption.IoThreads);

var (major, minor, patch) = Context.Version;       // ZMQ 버전 가져오기
bool hasCurve = Context.Has("curve");              // 기능 확인
```

### Socket

```csharp
var socket = new Socket(ctx, SocketType.Req);

// 연결
socket.Bind("tcp://*:5555");
socket.Connect("tcp://localhost:5555");
socket.Unbind("tcp://*:5555");
socket.Disconnect("tcp://localhost:5555");

// 전송
socket.Send("Hello");
socket.Send(byteArray);
socket.Send(message, SendFlags.SendMore);
bool sent = socket.Send(data, SendFlags.DontWait); // 블록되면 false 반환

// 수신
string str = socket.RecvString();
int bytesRead = socket.Recv(buffer);
socket.Recv(message);
bool received = socket.TryRecvString(out string result);
bool gotData = socket.TryRecv(buffer, out int size);

// 옵션
socket.SetOption(SocketOption.Linger, 0);
int linger = socket.GetOption<int>(SocketOption.Linger);
```

## 성능

### 권장 방식

**메모리 전략: `Message` 사용**
- 모든 메시지 크기에서 일관된 성능
- GC 프리 (네이티브 메모리)
- 128KB 이상에서 최대 6배 빠름
- .NET Large Object Heap 문제 회피

**수신 모드:**
- 단일 소켓 → Blocking
- 다중 소켓 → `Poller`

```csharp
// 권장: Blocking 수신 + Message
using var msg = new Message();
socket.Recv(msg);
// msg.Data 처리
```

세부 최적화 옵션과 상세 벤치마크는 [docs/benchmarks.ko.md](docs/benchmarks.ko.md)를 참조하세요.

## 지원 플랫폼

| OS | 아키텍처 |
|----|---------|
| Windows | x64, ARM64 |
| Linux | x64, ARM64 |
| macOS | x64, ARM64 |

## 문서

완전한 API 문서는 [https://ulala-x.github.io/net-zmq/](https://ulala-x.github.io/net-zmq/)에서 확인하실 수 있습니다.

문서 내용:
- 모든 클래스와 메서드의 API 레퍼런스
- 사용 예제 및 패턴
- 성능 벤치마크
- 플랫폼별 가이드

## 요구사항

- .NET 8.0 이상
- 네이티브 libzmq 라이브러리 (Net.Zmq.Native 패키지를 통해 자동으로 제공됨)

## 라이선스

MIT License - 자세한 내용은 [LICENSE](LICENSE)를 참조하세요.

## 관련 프로젝트

- [libzmq](https://github.com/zeromq/libzmq) - ZeroMQ 코어 라이브러리
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ 바인딩 (API 영감)
- [libzmq-native](https://github.com/ulala-x/libzmq-native) - 네이티브 바이너리
