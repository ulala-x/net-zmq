[![English](https://img.shields.io/badge/lang:en-red.svg)](CHANGELOG.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](CHANGELOG.ko.md)

# 변경 이력 (Changelog)

이 프로젝트의 모든 주목할 만한 변경 사항은 이 파일에 문서화됩니다.

형식은 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [유의적 버전](https://semver.org/spec/v2.0.0.html)을 따릅니다.

## [출시 예정]

## [0.4.1] - 2025-12-27

### 추가됨 (Added)
- **SetActualDataSize() public API** - 풀링된 메시지에서 Data Span에 직접 쓴 후 실제 데이터 크기 설정 가능

## [0.4.0] - 2025-12-26

### 추가됨 (Added)
- **MessagePool** - 고성능 시나리오를 위한 스레드 로컬 캐시가 포함된 네이티브 메모리 버퍼 재사용
- **MessagePool 벤치마크** - 다른 메모리 전략과의 성능 비교
- **애플리케이션 레벨 에러 코드** - ZmqConstants에 `EBUFFERSMALL` 및 `ESIZEMISMATCH` 상수 추가
- **설명적인 예외 메시지** - 버퍼 검증 에러에 대해 의미 있는 에러 설명 포함

### 변경됨 (Changed)
- **ZmqException 개선** - 버퍼 검증 에러가 errno에 의존하지 않고 특정 에러 코드와 설명 메시지와 함께 throw됨

### 수정됨 (Fixed)
- **벤치마크 공정성** - 정확한 성능 측정을 위해 공유 수신 버퍼 제거

## [0.2.0] - 2025-12-22

### 변경됨 (Changed)
- **Send()가 bool 반환** - 논블로킹 전송의 성공/실패 표시
- **Poller를 인스턴스 기반 설계로 리팩토링** - 제로 할당 폴링
- **MessageBufferStrategyBenchmarks를 순수 블로킹 모드로 변경** - 정확한 측정을 위해

### 추가됨 (Added)
- **TryRecv() 메서드** - 명시적 성공 표시자를 가진 논블로킹 수신
- **PureBlocking 모드** - 정확한 비교를 위한 ReceiveModeBenchmarks에 추가
- **128KB 및 256KB 메시지 크기 테스트** - 벤치마크에 추가
- **한국어 번역** - 모든 문서, 샘플, 템플릿
- **DocFX 문서** - GitHub Pages 배포
- **LOH (Large Object Heap) 영향 분석** - 벤치마크 문서에 추가
- **단일 전략 권장 섹션** - 일관된 성능을 위해 Message 권장

### 제거됨 (Removed)
- **RecvBytes() 및 TryRecvBytes()** - 이중 복사와 GC 압력 유발; `Recv(Span<byte>)` 또는 `Recv(Message)` 사용 권장
- **MessagePool** - 벤치마크 결과에 따라 메시지 버퍼 전략 단순화

### 문서
- 새로운 수신 모드 및 메시지 버퍼 전략으로 벤치마크 결과 업데이트
- 메시지 크기별 메시지 버퍼 전략 선택 가이드 추가
- 주요 발견사항 문서화:
  - 단일 소켓: PureBlocking 권장
  - 다중 소켓: Poller 권장
  - 메시지 버퍼: Message 권장 (GC 프리, 일관된 성능)
  - MessageZeroCopy는 256KB 이상에서 유리

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
