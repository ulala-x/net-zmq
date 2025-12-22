[![English](https://img.shields.io/badge/lang:en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](README.ko.md)

# NetZeroMQ.Benchmarks

NetZeroMQ 성능 벤치마크 도구

## 빠른 시작

```bash
cd benchmarks/NetZeroMQ.Benchmarks

# 전체 벤치마크 실행 (27개 테스트, ~5분 소요)
dotnet run -c Release

# 빠른 테스트 (1회 반복, ~30초 소요)
dotnet run -c Release -- --quick
```

## 실행 옵션

### 벤치마크 실행

```bash
# 전체 실행 (ShortRun: 3회 워밍업 + 3회 반복)
dotnet run -c Release

# 빠른 실행 (Dry: 1회 반복)
dotnet run -c Release -- --quick

# 특정 패턴만 실행
dotnet run -c Release -- --filter "*PushPull*"
dotnet run -c Release -- --filter "*PubSub*"
dotnet run -c Release -- --filter "*RouterRouter*"

# 특정 모드만 실행
dotnet run -c Release -- --filter "*Blocking*"
dotnet run -c Release -- --filter "*Poller*"

# 특정 메시지 크기만 실행
dotnet run -c Release -- --filter "*64*"      # 64 bytes
dotnet run -c Release -- --filter "*1024*"    # 1 KB
dotnet run -c Release -- --filter "*65536*"   # 64 KB

# 필터 조합
dotnet run -c Release -- --quick --filter "*PushPull*64*Blocking*"
```

### 진단 도구

```bash
# 소켓 연결 테스트
dotnet run -c Release -- --test

# 수신 모드 동작 확인
dotnet run -c Release -- --mode-test

# 메모리 할당 분석
dotnet run -c Release -- --alloc-test
```

### 사용 가능한 벤치마크 목록

```bash
dotnet run -c Release -- --list flat
```

## 벤치마크 구조

### ThroughputBenchmarks

| 파라미터 | 값 |
|----------|-----|
| **MessageSize** | 64, 1024, 65536 bytes |
| **MessageCount** | 10,000개 메시지 |
| **Mode** | Blocking, NonBlocking, Poller |

**패턴:**
- `PushPull_Throughput` - 단방향 메시지 전송
- `PubSub_Throughput` - Pub/Sub 브로드캐스트
- `RouterRouter_Throughput` - 양방향 라우팅

**총 27개 벤치마크** (3개 패턴 × 3개 크기 × 3개 모드)

## 출력 컬럼

| 컬럼 | 설명 |
|------|------|
| **Mean** | 평균 실행 시간 (전체 MessageCount 처리) |
| **Latency** | 메시지당 지연 시간 (Mean / MessageCount) |
| **msg/sec** | 초당 처리량 |
| **Allocated** | 힙 메모리 할당량 |

## 수신 모드 (Receive Mode) 비교

| 모드 | 설명 | 사용 사례 |
|------|------|-----------|
| **Blocking** | `Recv()` 블로킹 호출 | 단일 소켓, 최고 성능 |
| **NonBlocking** | `TryRecv()` + Sleep + 버스트 | 여러 소켓, 폴링 없이 |
| **Poller** | `Poll()` + `TryRecv()` 버스트 | 여러 소켓, 이벤트 기반 |

## 프로젝트 구조

```
NetZeroMQ.Benchmarks/
├── Program.cs                    # 진입점, CLI 옵션 처리
├── Configs/
│   └── BenchmarkConfig.cs        # 커스텀 컬럼 (Latency, msg/sec)
├── Benchmarks/
│   └── ThroughputBenchmarks.cs   # 메인 벤치마크
├── AllocTest.cs                  # 메모리 할당 진단
└── ModeTest.cs                   # 수신 모드 비교 테스트
```

## 예상 결과

### 64 bytes 메시지

| 패턴 | 모드 | 지연시간 | msg/sec |
|------|------|----------|---------|
| PushPull | Blocking | ~300 ns | ~3M |
| PushPull | Poller | ~350 ns | ~2.8M |
| PushPull | NonBlocking | ~1.2 μs | ~800K |

### 메모리 할당

| 모드 | 할당량 |
|------|--------|
| Blocking | ~900 B |
| NonBlocking | ~900 B |
| Poller | ~1.1 KB |

## 성능 최적화 가이드

### 1. 상세 벤치마크 결과

#### 1.1 전송 전략 비교 (MessageBufferStrategy, 10,000개 메시지)

| 메시지 크기 | ArrayPool | ByteArray | Message | MessageZeroCopy | 우승자 |
|-------------|-----------|-----------|---------|-----------------|--------|
| **64 bytes** | 2.43 ms (4,120 K/sec) | 2.44 ms (4,100 K/sec) | 4.28 ms (2,340 K/sec) | 5.92 ms (1,690 K/sec) | **ArrayPool** (1% 빠름, 99.98% GC 감소) |
| **512 bytes** | 6.38 ms (1,570 K/sec) | 6.71 ms (1,490 K/sec) | 8.19 ms (1,220 K/sec) | 13.37 ms (748 K/sec) | **ArrayPool** (5% 빠름, 99.99% GC 감소) |
| **1 KB** | 9.02 ms (1,110 K/sec) | 8.97 ms (1,110 K/sec) | 9.74 ms (1,030 K/sec) | 14.61 ms (684 K/sec) | **ByteArray** (0.5% 빠름, 대량 GC) |
| **64 KB** | 142.8 ms (70.0 K/sec) | 141.7 ms (70.6 K/sec) | **119.2 ms (83.9 K/sec)** | 124.7 ms (80.2 K/sec) | **Message** (16% 빠름, 99.95% GC 감소) |

**핵심 인사이트:**
- **작은 메시지 (≤512B)**: ArrayPool이 최고 성능 제공 (1-5% 빠르며 99.98-99.99% GC 감소)
- **중간 메시지 (1KB)**: 유사한 성능이지만 ByteArray는 대량 GC 발생 (100MB 할당)
- **큰 메시지 (≥64KB)**: Message가 최고 성능 제공 (16% 빠르며 99.95% GC 감소)
- **전환점**: 64KB에서 네이티브 전략 (Message/MessageZeroCopy)이 12-16% 우위 확보

#### 1.2 수신 모드 비교 (ReceiveMode, 10,000개 메시지)

| 메시지 크기 | Blocking | Poller | NonBlocking | 우승자 |
|-------------|----------|--------|-------------|--------|
| **64 bytes** | **2.19 ms (4,570 K/sec)** | 2.31 ms (4,330 K/sec) | 3.78 ms (2,640 K/sec) | **Blocking** (기준선, Poller 6% 느림) |
| **512 bytes** | 4.90 ms (2,040 K/sec) | **4.72 ms (2,120 K/sec)** | 6.14 ms (1,630 K/sec) | **Poller** (4% 빠름) |
| **1 KB** | 7.54 ms (1,330 K/sec) | **7.74 ms (1,290 K/sec)** | 9.66 ms (1,040 K/sec) | **Blocking** (기준선, Poller 3% 느림) |
| **64 KB** | **139.9 ms (71.5 K/sec)** | 141.7 ms (70.6 K/sec) | 260.0 ms (38.5 K/sec) | **Blocking** (기준선, Poller 1% 느림) |

**핵심 인사이트:**
- **Blocking vs Poller**: 거의 동일한 성능 (96-106% 범위, 0-6% 차이)
- **Poller 모드**: 512B에서만 4% 우위, 나머지는 Blocking과 거의 동일
- **NonBlocking 모드**: 모든 경우에서 최악 (Sleep 오버헤드로 25-73% 느림)
- **권장사항**: Poller 사용 (성능 동일하면서 다중 소켓 지원 및 일관된 API)

### 2. 시나리오별 권장사항

#### 2.1 외부 할당 메모리 전송 (예: API에서 받은 byte[])

**시나리오**: 이미 할당된 byte[] 데이터를 전송해야 하는 경우

**권장사항**: `SendOptimized()` 사용 (크기별 자동 최적화)

```csharp
// 크기에 따라 ArrayPool 또는 MessagePool 자동 선택
byte[] apiData = await httpClient.GetByteArrayAsync(url);
socket.SendOptimized(apiData);
```

**예상 성능**:
- 64B: ~4.1M msg/sec (ArrayPool)
- 512B: ~1.6M msg/sec (ArrayPool)
- 1KB: ~1.1M msg/sec (ArrayPool)
- 64KB: ~84K msg/sec (Message)

#### 2.2 최대 처리량 시나리오 (예: 로그 수집기, 메트릭 전송)

**시나리오**: 가능한 한 빠르게 대량의 메시지 전송

**권장사항**: ArrayPool (≤512B) / MessageZeroCopy (>512B) + Poller 조합

```csharp
// 전송자: 크기별 전략 선택
// 작은 메시지 (≤512B): ArrayPool
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // 버퍼에 데이터 작성
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// 큰 메시지 (>512B): MessageZeroCopy
nint nativePtr = Marshal.AllocHGlobal(size);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, size);
    sourceData.CopyTo(nativeSpan);
}
using var msg = new Message(nativePtr, size, ptr => Marshal.FreeHGlobal(ptr));
socket.Send(msg);

// 수신자: Poller + Message
using var poller = new Poller(capacity: 10);
int idx = poller.Add(socket, PollEvents.In);

using var recvMsg = new Message();
while (running)
{
    if (poller.Poll(timeout: 100) > 0)
    {
        while (poller.IsReadable(idx))
        {
            socket.Recv(ref recvMsg, RecvFlags.None);
            ProcessMessage(recvMsg.Data);
        }
    }
}
```

**예상 성능**:
- 512B: ~2.1M msg/sec (ArrayPool + Poller)
- 1KB: ~1.3M msg/sec (ArrayPool + Poller)
- 64KB: ~71K msg/sec (Message + Blocking)

#### 2.3 낮은 지연시간 요구사항 (예: 트레이딩, 실시간 게이밍)

**시나리오**: 메시지당 지연시간 최소화

**권장사항**: Poller (작은 메시지) / Blocking (큰 메시지)

```csharp
// 작은 메시지 (<64KB): Poller 사용
using var poller = new Poller(1);
int idx = poller.Add(socket, PollEvents.In);

using var msg = new Message();
if (poller.Poll(timeout: 1) > 0 && poller.IsReadable(idx))
{
    socket.Recv(ref msg, RecvFlags.None);
    // 지연시간: 64B ~231ns, 512B ~472ns, 1KB ~774ns
}

// 큰 메시지 (≥64KB): Blocking 사용
socket.Recv(ref msg, RecvFlags.None);
// 지연시간: 64KB ~14.0μs
```

### 3. 성능 체크리스트

#### DO (권장):

- **전송**:
  - 작은 메시지 (≤512B) → `ArrayPool<byte>.Shared` 사용
  - 큰 메시지 (>512B) → `MessageZeroCopy` 사용
  - 메시지 크기에 따라 적절한 전략 선택
- **수신**:
  - Poller 모드 우선 (대부분의 경우 최적)
  - Message 객체 재사용 (using var msg = new Message())
  - 배치 처리 (Poller.IsReadable() 루프)
- **일반**:
  - using 키워드로 리소스 정리 보장
  - 버퍼 재사용으로 GC 압력 최소화

#### DON'T (비권장):

- **전송**:
  - 작은 메시지에 MessageZeroCopy 강제 사용 (ArrayPool이 더 빠름)
  - 큰 메시지에 ByteArray 사용 (GC 압력 증가)
- **수신**:
  - NonBlocking 모드 사용 (Sleep 오버헤드로 최악의 성능)
  - 수신할 때마다 새 Message 할당 (GC 압력)
  - Poller 없이 TryRecv() 루프 (CPU 낭비)
- **일반**:
  - Message 객체 수동 Dispose 누락
  - 단일 메시지만 처리하고 대기 (배치 기회 놓침)

### 4. 의사결정 흐름

```
전송 전략 선택:
├─ 메시지 크기 ≤ 512B?
│  └─ YES → ArrayPool 사용 (최고 성능)
│     └─ ArrayPool<byte>.Shared.Rent(size)
│
└─ NO → MessageZeroCopy 사용 (제로카피)
   └─ Marshal.AllocHGlobal + Message(ptr, size, freeCallback)

수신 모드 선택:
├─ 메시지 크기 < 64KB?
│  └─ YES → Poller 모드 권장 (Blocking과 거의 동일, 0-6% 차이)
│     └─ Poller + Message + 배치 처리
│
└─ NO → Blocking 또는 Poller 모두 허용 (1% 차이)
   └─ Poller 권장 (일관된 API, 다중 소켓 지원)

권장사항: ArrayPool (≤512B) / Message (≥64KB) + Poller
```

### 5. 예상 성능 개선률

기존 코드에 최적화를 적용할 때 예상되는 개선률:

| 개선 항목 | 이전 | 이후 | 개선률 |
|----------|------|------|--------|
| **작은 메시지 전송** (64B-512B) | ByteArray/Message | ArrayPool | **+1-5%** 처리량, **-99.98%** 할당량 |
| **큰 메시지 전송** (≥64KB) | ByteArray/ArrayPool | Message | **+16%** 처리량, **-99.95%** 할당량 |
| **수신 모드** (모든 크기) | NonBlocking | Poller | **+25-73%** 처리량 |
| **수신 모드** (64KB) | NonBlocking | Blocking | **+86%** 처리량 |
| **메모리 할당** (64KB) | ByteArray | Message | **-99.95%** 할당량 (4GB → 171KB) |

**전체 개선 예시** (512B 메시지, 기존 ByteArray+NonBlocking → 최적 ArrayPool+Poller):
- 전송: 1,490 K/sec → 1,570 K/sec (+5%)
- 수신: 1,630 K/sec → 2,120 K/sec (+30%)
- 메모리: 50MB → 2KB (-99.99%)
- **전체 파이프라인: ~35% 처리량 개선 + 대량 GC 감소**

### 6. 추가 성능 권장사항

- **단일 소켓**: Blocking 또는 Poller 모드 (유사한 성능)
- **여러 소켓**: Poller 모드 필수 (효율적인 이벤트 대기)
- **버스트 처리**: Poller.IsReadable() 루프로 사용 가능한 모든 메시지 처리
- **버퍼 재사용**: Message 객체 재사용으로 GC 압력 최소화
