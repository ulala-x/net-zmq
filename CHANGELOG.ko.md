[![English](https://img.shields.io/badge/lang:en-red.svg)](CHANGELOG.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](CHANGELOG.ko.md)

# 변경 이력 (Changelog)

이 프로젝트의 모든 주목할 만한 변경 사항은 이 파일에 문서화됩니다.

형식은 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [유의적 버전](https://semver.org/spec/v2.0.0.html)을 따릅니다.

## [출시 예정]

## [0.1.0] - 2025-12-14

### 추가됨 (Added)
- 초기 릴리스
- 옵션 지원을 포함한 컨텍스트 관리
- 모든 소켓 타입 (REQ, REP, PUB, SUB, PUSH, PULL, DEALER, ROUTER, PAIR, XPUB, XSUB, STREAM)
- Span<byte> 지원을 포함한 메시지 API
- 폴링 지원
- Z85 인코딩/디코딩 유틸리티
- CURVE 키 생성 유틸리티
- 프록시 지원
- SafeHandle 기반 리소스 관리

### 소켓 기능
- Bind/Connect/Unbind/Disconnect
- 여러 오버로드를 사용한 Send/Recv
- 논블로킹 작업을 위한 TrySend/TryRecv
- 완전한 소켓 옵션 지원
- PUB/SUB 패턴을 위한 Subscribe/Unsubscribe

### 플랫폼
- Windows x64, x86, ARM64
- Linux x64, ARM64
- macOS x64, ARM64 (Apple Silicon)

### 핵심 구성 요소
- NetZeroMQ: cppzmq 스타일 인터페이스를 갖춘 고수준 API
- NetZeroMQ.Core: 저수준 P/Invoke 바인딩
- NetZeroMQ.Native: 멀티 플랫폼 지원을 갖춘 네이티브 라이브러리 패키지

### 성능
- Span<byte>를 사용한 제로카피 작업
- SafeHandle을 사용한 효율적인 메모리 관리
- 직접 P/Invoke를 통한 네이티브 성능

### 안전성
- 포괄적인 널 참조 타입 주석
- SafeHandle 기반 리소스 정리
- 스레드 안전 소켓 작업
