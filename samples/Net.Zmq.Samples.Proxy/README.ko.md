[![English](https://img.shields.io/badge/lang-en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](README.ko.md)

# NetZeroMQ XPub-XSub 프록시 패턴 샘플

이 샘플은 NetZeroMQ를 사용한 ZeroMQ XPub-XSub 프록시 패턴을 보여줍니다.

## 아키텍처

```
Publishers --> XSub (Frontend) --> Proxy --> XPub (Backend) --> Subscribers
```

## 구성 요소

### 프록시
- **프론트엔드 (XSub)**: `tcp://*:5559`에서 퍼블리셔로부터 메시지를 수신합니다
- **백엔드 (XPub)**: `tcp://*:5560`에서 구독자에게 메시지를 전달합니다
- 프론트엔드에서 백엔드로 메시지를 전달합니다
- 백엔드에서 프론트엔드로 구독 정보를 전달합니다

### 퍼블리셔
- **Publisher-1**: "weather" 토픽 메시지를 발행합니다
- **Publisher-2**: "sports" 토픽 메시지를 발행합니다
- 모두 프록시의 XSub 소켓(`tcp://localhost:5559`)에 연결합니다

### 구독자
- **Subscriber-1**: "weather" 토픽을 구독합니다
- **Subscriber-2**: "sports" 토픽을 구독합니다
- **Subscriber-3**: "weather"와 "sports" 두 토픽을 모두 구독합니다
- 모두 프록시의 XPub 소켓(`tcp://localhost:5560`)에 연결합니다

## 주요 기능

1. **토픽 기반 필터링**: 구독자는 자신이 구독한 토픽과 일치하는 메시지만 수신합니다
2. **동적 구독**: 구독 정보는 런타임에 처리됩니다
3. **다중 퍼블리셔**: 여러 퍼블리셔가 동일한 프록시로 메시지를 전송할 수 있습니다
4. **다중 구독자**: 여러 구독자가 동일한 프록시로부터 메시지를 수신할 수 있습니다
5. **내장 프록시**: `Proxy.Start()` 유틸리티를 사용하여 자동으로 메시지를 전달합니다

## 샘플 실행

```bash
cd samples/NetZeroMQ.Samples.Proxy
dotnet run
```

## 예상 출력

이 샘플은 다음 작업을 수행합니다:
1. XPub-XSub 프록시를 시작합니다
2. 서로 다른 토픽을 발행하는 두 개의 퍼블리셔를 시작합니다
3. 서로 다른 토픽을 구독하는 세 개의 구독자를 시작합니다
4. 어떤 구독자가 어떤 메시지를 수신하는지 메시지 흐름을 표시합니다
5. 마지막에 구독 해제를 시연합니다

## ZeroMQ 프록시 패턴의 장점

- **분리**: 퍼블리셔와 구독자는 서로에 대해 알 필요가 없습니다
- **확장성**: 퍼블리셔나 구독자를 쉽게 추가할 수 있습니다
- **중앙화**: 모니터링과 관리를 위한 단일 지점을 제공합니다
- **유연성**: 필터링, 로깅 또는 캡처 소켓을 추가할 수 있습니다

## 기술 세부 사항

### 소켓 유형
- **XSub**: 확장 구독자 소켓 (Extended Subscriber Socket, 프록시 프론트엔드에서 사용)
- **XPub**: 확장 퍼블리셔 소켓 (Extended Publisher Socket, 프록시 백엔드에서 사용)

### XPub-XSub를 사용하는 이유
- 표준 PUB-SUB 소켓은 프록시 패턴에서 사용할 수 없습니다
- XPUB는 구독 메시지를 상위 스트림으로 전달합니다
- XSUB는 하위 스트림으로부터 구독 메시지를 수신합니다
- 이를 통해 프록시를 통한 동적 토픽 필터링이 가능합니다

## 코드 구조

- **RunProxy()**: XSub 및 XPub 소켓을 생성하고 내장 프록시를 시작합니다
- **RunPublisher()**: 프록시 프론트엔드에 연결하여 토픽 메시지를 발행합니다
- **RunSubscriber()**: 프록시 백엔드에 연결하여 토픽을 구독하고 메시지를 수신합니다
