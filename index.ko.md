[![English](https://img.shields.io/badge/lang:en-red.svg)](index.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](index.ko.md)

# Net.Zmq

[![Build and Test](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml/badge.svg)](https://github.com/ulala-x/net-zmq/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Zmq.svg)](https://www.nuget.org/packages/Net.Zmq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

cppzmq 스타일 API를 갖춘 현대적인 .NET 8+ ZeroMQ (libzmq) 바인딩입니다.

## 기능

- **현대적인 .NET**: `[LibraryImport]` 소스 생성기를 사용하여 .NET 8.0+ 용으로 구축
- **고성능**: 4.95M 메시지/초 처리량, 202ns 지연
- **타입 안전**: 강력한 타입의 소켓 옵션, 메시지 속성 및 열거형
- **크로스 플랫폼**: Windows, Linux, macOS (x64, ARM64)
- **기본적으로 안전**: SafeHandle 기반 리소스 관리
- **cppzmq 스타일**: C++ 개발자에게 익숙한 API

## 빠른 시작

### 설치

```bash
dotnet add package Net.Zmq
```

### Request-Reply 예제

```csharp
using Net.Zmq;

using var context = new Context();

// Server
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

// Client
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");

// Communication
client.Send("Hello");
var request = server.RecvString();  // "Hello"
server.Send("World");
var reply = client.RecvString();    // "World"
```

### Publish-Subscribe 예제

```csharp
using Net.Zmq;

using var context = new Context();

// Publisher
using var publisher = new Socket(context, SocketType.Pub);
publisher.Bind("tcp://*:5556");
publisher.Send("weather.tokyo 25°C");

// Subscriber
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");
subscriber.Subscribe("weather.");
var update = subscriber.RecvString();  // "weather.tokyo 25°C"
```

## 문서

완전한 문서는 **[https://ulala-x.github.io/net-zmq/](https://ulala-x.github.io/net-zmq/)**에서 확인할 수 있습니다.

### 빠른 링크

- **[시작하기](docs/getting-started.ko.md)** - 설치 및 기본 개념
- **[메시징 패턴](docs/patterns.ko.md)** - REQ-REP, PUB-SUB, PUSH-PULL 등
- **[API 사용 가이드](docs/api-usage.ko.md)** - 상세한 API 문서
- **[고급 주제](docs/advanced-topics.ko.md)** - 성능 튜닝 및 모범 사례
- **[API 레퍼런스](api/index.html)** - 완전한 API 문서

## 성능

Net.Zmq는 탁월한 성능을 제공합니다:

| 메시지 크기 | 처리량 | 지연 | 패턴 |
|--------------|------------|---------|---------|
| 64바이트 | 4.95M/초 | 202ns | PUSH/PULL |
| 1 KB | 1.36M/초 | 736ns | PUB/SUB |
| 64 KB | 73.47K/초 | 13.61μs | ROUTER/ROUTER |

**테스트 환경**: Intel Core Ultra 7 265K, .NET 8.0.22, Ubuntu 24.04.3 LTS

자세한 벤치마크는 [BENCHMARKS.md](https://github.com/ulala-x/net-zmq/blob/main/BENCHMARKS.md)를 참조하세요.

## 지원 플랫폼

| OS | 아키텍처 | 상태 |
|----|--------------|--------|
| Windows | x64, ARM64 | ✅ 지원됨 |
| Linux | x64, ARM64 | ✅ 지원됨 |
| macOS | x64, ARM64 | ✅ 지원됨 |

## 소켓 타입

Net.Zmq는 모든 ZeroMQ 소켓 타입을 지원합니다:

| 타입 | 설명 | 패턴 |
|------|-------------|---------|
| `Req` | Request | 클라이언트-서버 |
| `Rep` | Reply | 클라이언트-서버 |
| `Pub` | Publish | Pub-Sub |
| `Sub` | Subscribe | Pub-Sub |
| `Push` | Push | 파이프라인 |
| `Pull` | Pull | 파이프라인 |
| `Dealer` | 비동기 Request | 고급 |
| `Router` | 비동기 Reply | 고급 |
| `Pair` | 독점적 Pair | 피어 투 피어 |

## 요구사항

- **.NET 8.0 이상**
- **libzmq 네이티브 라이브러리** (Net.Zmq.Native 패키지를 통해 자동으로 포함됨)

## 기여

기여를 환영합니다! 자세한 내용은 [기여 가이드](https://github.com/ulala-x/net-zmq/blob/main/CONTRIBUTING.md)를 참조하세요.

## 라이선스

Net.Zmq는 [MIT 라이선스](https://github.com/ulala-x/net-zmq/blob/main/LICENSE)에 따라 라이선스가 부여됩니다.

## 관련 프로젝트

- [libzmq](https://github.com/zeromq/libzmq) - ZeroMQ 핵심 라이브러리
- [cppzmq](https://github.com/zeromq/cppzmq) - C++ 바인딩 (API 영감)
- [libzmq-native](https://github.com/ulala-x/libzmq-native) - Net.Zmq용 네이티브 바이너리
