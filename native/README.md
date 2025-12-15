# Native Binaries

이 디렉토리는 libzmq 네이티브 바이너리를 포함합니다.

## 디렉토리 구조

```
native/
├── build/
│   └── NetZeroMQ.Native.targets    # MSBuild targets (자동 복사 설정)
└── runtimes/
    ├── win-x64/native/          # Windows x64
    │   └── libzmq.dll
    ├── win-x86/native/          # Windows x86
    │   └── libzmq.dll
    ├── win-arm64/native/        # Windows ARM64
    │   └── libzmq.dll
    ├── linux-x64/native/        # Linux x64
    │   └── libzmq.so.5
    ├── linux-arm64/native/      # Linux ARM64
    │   └── libzmq.so.5
    ├── osx-x64/native/          # macOS x64 (Intel)
    │   └── libzmq.5.dylib
    └── osx-arm64/native/        # macOS ARM64 (Apple Silicon)
        └── libzmq.5.dylib
```

## 바이너리 소스

Native 바이너리는 [libzmq-native](https://github.com/ulala-x/libzmq-native)에서 빌드됩니다.
libsodium이 정적으로 링크되어 단일 파일로 배포됩니다.

### 지원 플랫폼

| 플랫폼 | 아키텍처 | Runtime ID | 라이브러리 파일 |
|--------|----------|------------|----------------|
| Windows | x64 | win-x64 | libzmq.dll |
| Windows | x86 | win-x86 | libzmq.dll |
| Windows | ARM64 | win-arm64 | libzmq.dll |
| Linux | x64 | linux-x64 | libzmq.so.5 |
| Linux | ARM64 | linux-arm64 | libzmq.so.5 |
| macOS | x64 (Intel) | osx-x64 | libzmq.5.dylib |
| macOS | ARM64 (Apple Silicon) | osx-arm64 | libzmq.5.dylib |

## 사용법

### NuGet 패키지 참조

NetZeroMQ.Native NuGet 패키지를 참조하면 자동으로 적절한 네이티브 라이브러리가 출력 디렉토리에 복사됩니다.

```xml
<ItemGroup>
  <PackageReference Include="NetZeroMQ.Native" Version="0.1.0" />
</ItemGroup>
```

### 자동 복사 메커니즘

`NetZeroMQ.Native.targets` 파일이 다음을 자동으로 처리합니다:

1. **RuntimeIdentifier 감지**: 현재 빌드 환경의 RID를 자동으로 감지
2. **플랫폼별 라이브러리 선택**: 해당 RID에 맞는 네이티브 라이브러리 선택
3. **출력 디렉토리 복사**: 선택된 라이브러리를 빌드 출력 디렉토리에 자동 복사

### 디버깅 정보

빌드 시 다음과 같은 진단 정보를 확인할 수 있습니다 (Verbosity: detailed):

```
NetZeroMQ.Native: RuntimeIdentifier = win-x64
NetZeroMQ.Native: Platform = x64
NetZeroMQ.Native: OS = Windows_NT
NetZeroMQ.Native: Native library name = libzmq.dll
```

## 네이티브 바이너리 업데이트

새로운 libzmq 버전으로 업데이트하려면:

1. [libzmq-native](https://github.com/ulala-x/libzmq-native) 리포지토리에서 최신 바이너리 빌드
2. 각 플랫폼별 바이너리를 해당 `runtimes/{rid}/native/` 디렉토리에 복사
3. NetZeroMQ.Native 패키지 버전 업데이트
4. NuGet 패키지 재생성 및 배포

## 라이선스

libzmq는 MPL-2.0 라이선스를 따릅니다.
자세한 내용은 [ZeroMQ 라이선스](https://github.com/zeromq/libzmq/blob/master/LICENSE)를 참조하세요.
