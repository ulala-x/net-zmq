# NetZeroMQ 기여하기

[![English](https://img.shields.io/badge/lang-en-red.svg)](CONTRIBUTING.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](CONTRIBUTING.ko.md)

NetZeroMQ에 기여하는 데 관심을 가져주셔서 감사합니다! 이 문서는 기여자를 위한 가이드라인과 정보를 제공합니다.

## 시작하기

### 사전 요구사항

- .NET 8.0 SDK 이상
- Git
- C# IDE (Visual Studio, VS Code with C# extension, 또는 JetBrains Rider)

### 개발 환경 설정

1. GitHub에서 리포지토리를 포크합니다
2. 로컬에 포크를 클론합니다:
   ```bash
   git clone https://github.com/YOUR_USERNAME/netzmq.git
   cd netzmq
   ```
3. 프로젝트를 빌드합니다:
   ```bash
   dotnet build
   ```
4. 테스트를 실행합니다:
   ```bash
   dotnet test
   ```

## 기여 방법

### 버그 리포팅

버그 리포트를 작성하기 전에 중복을 피하기 위해 기존 이슈를 확인해 주세요.

이슈를 제출할 때 다음을 포함해 주세요:
- 명확하고 설명적인 제목
- 문제를 재현하는 단계
- 예상 동작 vs 실제 동작
- 환경 정보 (OS, .NET 버전, NetZeroMQ 버전)
- 관련 코드 스니펫이나 오류 메시지

### 기능 제안

기능 요청은 환영합니다! 다음을 제공해 주세요:
- 기능에 대한 명확한 설명
- 사용 사례 및 이점
- 구현 아이디어 (선택사항)

### 풀 리퀘스트

1. **브랜치 생성** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **변경사항 작성**:
   - 기존 코드 스타일을 따릅니다
   - 공개 API에 XML 문서화 추가
   - 새로운 기능에 대한 테스트 작성
   - 필요시 문서 업데이트

3. **변경사항 테스트**:
   ```bash
   dotnet test
   ```

4. **변경사항 커밋**:
   - 명확하고 설명적인 커밋 메시지 사용
   - 관련 이슈 참조 (예: "Fixes #123")

5. **푸시 및 PR 생성**:
   ```bash
   git push origin feature/your-feature-name
   ```
   그런 다음 GitHub에서 Pull Request를 생성합니다.

## 코드 스타일 가이드라인

### 일반

- 적절한 경우 C# 12 기능 사용
- Microsoft의 [C# 코딩 규칙](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) 준수
- 변수, 메서드, 클래스에 의미 있는 이름 사용

### 문서화

- 모든 공개 타입과 멤버에 XML 문서화 주석 추가
- 주석을 간결하고 최신 상태로 유지
- 도움이 되는 경우 문서에 코드 예제 포함

### 테스트

- 새로운 기능에 대한 단위 테스트 작성
- 높은 테스트 커버리지 목표
- 예상 동작을 설명하는 설명적인 테스트 이름 사용
- assertion에 FluentAssertions 사용

## 프로젝트 구조

```
netzmq/
├── src/
│   ├── NetZeroMQ/           # 고수준 API
│   ├── NetZeroMQ.Core/      # 저수준 P/Invoke 바인딩
│   └── NetZeroMQ.Native/    # 네이티브 라이브러리 패키징
├── tests/
│   ├── NetZeroMQ.Tests/     # 단위 및 통합 테스트
│   └── NetZeroMQ.Core.Tests/
├── examples/             # 예제 프로젝트
└── native/               # 네이티브 라이브러리 바이너리
```

## 질문이 있으신가요?

기여에 관한 질문이 있으시면 언제든지 이슈를 열어주세요.

기여해 주셔서 감사합니다!
