# Net.Zmq 문서

[![English](https://img.shields.io/badge/lang-en-red.svg)](index.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](index.ko.md)

Net.Zmq에 오신 것을 환영합니다! cppzmq 스타일 API를 제공하는 현대적인 .NET 8+ ZeroMQ 바인딩으로, 분산 애플리케이션을 위한 고성능 메시지 큐잉을 제공합니다.

## Net.Zmq란?

Net.Zmq는 ZeroMQ (libzmq)를 위한 .NET 래퍼로, 분산 시스템 구축을 위한 깔끔하고 타입 안전한 API를 제공합니다. ZeroMQ의 강력함과 소스 생성기 및 SafeHandle과 같은 최신 .NET 기능을 결합합니다.

### 주요 기능

- **모던 .NET**: `[LibraryImport]` 소스 생성기를 사용하는 .NET 8.0+ 빌드
- **고성능**: 초당 4.95M 메시지 처리량, 202ns 레이턴시
- **타입 안전성**: 강타입 소켓 옵션 및 열거형
- **크로스 플랫폼**: Windows, Linux, macOS (x64, ARM64)
- **안전한 기본값**: SafeHandle 기반 리소스 관리
- **cppzmq 스타일**: C++ 개발자에게 익숙한 API

## 빠른 시작

NuGet을 통해 설치:

```bash
dotnet add package Net.Zmq
```

간단한 요청-응답 예제:

```csharp
using Net.Zmq;

using var context = new Context();

// 서버
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

// 클라이언트
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");

// 통신
client.Send("Hello");
var request = server.RecvString();  // "Hello"
server.Send("World");
var reply = client.RecvString();    // "World"
```

## 문서 가이드

### 초보자용

처음부터 Net.Zmq를 배우려면 다음 가이드를 시작하세요:

- **[시작하기](getting-started.ko.md)** - 설치, 기본 개념, 첫 번째 애플리케이션
- **[메시징 패턴](patterns.ko.md)** - REQ-REP, PUB-SUB, PUSH-PULL 등

### 개발자용

API 및 고급 사용법에 대해 심층적으로 알아보기:

- **[API 사용 가이드](api-usage.ko.md)** - Context, Socket, Message, Poller에 대한 자세한 가이드
- **[고급 주제](advanced-topics.ko.md)** - 성능 튜닝, 모범 사례, 보안, 문제 해결

### 레퍼런스

- **[API 레퍼런스](../api/index.html)** - 모든 클래스와 메서드에 대한 완전한 API 문서

## 핵심 개념

### Context

Context는 ZeroMQ 리소스 (I/O 스레드, 소켓)를 관리합니다. 애플리케이션당 하나를 생성하세요:

```csharp
using var context = new Context();
```

### 소켓 타입

Net.Zmq는 모든 ZeroMQ 소켓 타입을 지원합니다:

| 타입 | 설명 | 사용 사례 |
|------|------|----------|
| REQ | Request | 클라이언트-서버의 클라이언트 |
| REP | Reply | 클라이언트-서버의 서버 |
| PUB | Publish | 발행-구독의 퍼블리셔 |
| SUB | Subscribe | 발행-구독의 서브스크라이버 |
| PUSH | Push | 파이프라인의 프로듀서 |
| PULL | Pull | 파이프라인의 컨슈머 |
| DEALER | Dealer | 비동기 요청 |
| ROUTER | Router | 라우팅을 사용한 비동기 응답 |
| PAIR | Pair | 독점 피어 투 피어 |

### 메시징 패턴

사용 사례에 적합한 패턴을 선택하세요:

- **Request-Reply (REQ-REP)**: 동기식 클라이언트-서버 통신
- **Publish-Subscribe (PUB-SUB)**: 일대다 메시지 배포
- **Push-Pull (Pipeline)**: 로드 밸런싱된 작업 배포
- **Router-Dealer**: 고급 라우팅을 사용한 비동기 요청-응답
- **Pair**: 독점 양방향 연결

자세한 예제는 [메시징 패턴](patterns.ko.md) 가이드를 참조하세요.

## 성능

Net.Zmq는 뛰어난 성능을 제공합니다:

| 메시지 크기 | 처리량 | 레이턴시 | 패턴 |
|------------|--------|---------|-------|
| 64 bytes | 4.95M/sec | 202ns | PUSH/PULL |
| 1 KB | 1.36M/sec | 736ns | PUB/SUB |
| 64 KB | 73.47K/sec | 13.61μs | ROUTER/ROUTER |

**테스트 환경**: Intel Core Ultra 7 265K, .NET 8.0.22, Ubuntu 24.04.3 LTS

포괄적인 성능 메트릭은 [benchmarks.ko.md](benchmarks.ko.md)를 참조하세요.

## 플랫폼 지원

| OS | 아키텍처 | 상태 |
|----|---------|------|
| Windows | x64, ARM64 | ✅ 지원 |
| Linux | x64, ARM64 | ✅ 지원 |
| macOS | x64, ARM64 | ✅ 지원 |

## 요구사항

- **.NET 8.0 이상**
- **libzmq 네이티브 라이브러리** (Net.Zmq.Native 패키지를 통해 자동으로 포함됨)

## 추가 리소스

### 프로젝트 링크

- [GitHub 리포지토리](https://github.com/ulala-x/net-zmq) - 소스 코드, 이슈, 토론
- [NuGet 패키지](https://www.nuget.org/packages/Net.Zmq) - 최신 릴리스
- [변경 이력](https://github.com/ulala-x/net-zmq/blob/main/CHANGELOG.md) - 릴리스 히스토리

### ZeroMQ 리소스

- [ZeroMQ 가이드](https://zguide.zeromq.org/) - ZeroMQ 패턴에 대한 포괄적인 가이드
- [libzmq 문서](https://libzmq.readthedocs.io/) - 코어 라이브러리 문서
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ 바인딩 (API 영감)

### 커뮤니티

- [기여 가이드](https://github.com/ulala-x/net-zmq/blob/main/CONTRIBUTING.ko.md) - 기여 방법
- [GitHub 토론](https://github.com/ulala-x/net-zmq/discussions) - 질문하고 아이디어 공유
- [GitHub 이슈](https://github.com/ulala-x/net-zmq/issues) - 버그 리포트 및 기능 요청

## 라이선스

Net.Zmq는 [MIT 라이선스](https://github.com/ulala-x/net-zmq/blob/main/LICENSE)에 따라 공개된 오픈소스 소프트웨어입니다.
