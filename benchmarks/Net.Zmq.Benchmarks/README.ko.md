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

#### 1.1 메시지 버퍼 전략 비교 (MessageBufferStrategy, 10,000개 메시지)

NetZeroMQ는 4가지 메시지 버퍼 전략을 제공합니다:

1. **ByteArray** (Baseline): 매번 새로운 `byte[]` 할당 - 최대 GC 압력
2. **ArrayPool**: `ArrayPool<byte>.Shared` 재사용 - 최소 GC 압력
3. **Message**: 네이티브 메모리 기반 Message 객체 - 중간 GC 압력
4. **MessageZeroCopy**: `zmq_msg_init_data`를 사용한 진정한 제로카피 - 중간 GC 압력

| 메시지 크기 | ByteArray (Baseline) | ArrayPool | Message | MessageZeroCopy | 우승자 |
|-------------|----------------------|-----------|---------|-----------------|--------|
| **64 bytes** | 2.80 ms (3.57M/s, 1719 KB) | 2.60 ms (3.84M/s, 1.08 KB) | 4.86 ms (2.06M/s, 625 KB) | 5.78 ms (1.73M/s, 625 KB) | **ArrayPool** (8% 빠름, 99.94% GC 감소) |
| **512 bytes** | 6.25 ms (1.60M/s, 10.5 MB) | 6.02 ms (1.66M/s, 1.52 KB) | 6.89 ms (1.45M/s, 625 KB) | 8.04 ms (1.24M/s, 625 KB) | **ArrayPool** (4% 빠름, 99.99% GC 감소) |
| **1 KB** | 9.53 ms (1.05M/s, 20.5 MB) | 8.49 ms (1.18M/s, 2.04 KB) | 9.08 ms (1.10M/s, 625 KB) | 11.49 ms (870K/s, 625 KB) | **ArrayPool** (11% 빠름, 99.99% GC 감소) |
| **64 KB** | 185.5 ms (53.9K/s, 1.25 GB) | 202.2 ms (49.5K/s, 65 KB) | **169.0 ms (59.2K/s, 626 KB)** | **166.5 ms (60.1K/s, 626 KB)** | **MessageZeroCopy** (10% 빠름, 99.95% GC 감소) |
| **131 KB** | 1491 ms (6.71K/s, 2.5 GB, Gen2) | 341 ms (29.3K/s, 129 KB) | 361 ms (27.7K/s, 627 KB) | 373 ms (26.8K/s, 627 KB) | **ArrayPool** (4.4배 빠름, 99.99% GC 감소) |
| **262 KB** | 2423 ms (4.13K/s, 5.0 GB, Gen2) | 720 ms (13.9K/s, 258 KB) | 706 ms (14.2K/s, 626 KB) | 697 ms (14.4K/s, 626 KB) | **MessageZeroCopy** (3.5배 빠름, 99.99% GC 감소) |

**핵심 인사이트:**
- **작은 메시지 (64B)**: ArrayPool이 최고 성능 (8% 빠름, 99.94% GC 감소)
- **중간 메시지 (512B-1KB)**: ArrayPool이 최고 성능 (4-11% 빠름, 99.99% GC 감소)
- **큰 메시지 (64KB)**: MessageZeroCopy가 최고 성능 (10% 빠름, 99.95% GC 감소)
- **대용량 메시지 (131KB-262KB)**: ArrayPool/MessageZeroCopy가 ByteArray 대비 3.5-4.4배 빠름
- **전환점**: 64KB 이상에서 네이티브 전략(Message/MessageZeroCopy)이 우위 확보
- **치명적 문제**: ByteArray는 대용량 메시지에서 Gen2 GC 발생 → 성능 급격히 저하

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

**권장사항**: 기본 `Send(byte[])` 메서드 사용 (내부적으로 최적화됨)

```csharp
// 기존 byte[] 데이터를 그대로 전송
byte[] apiData = await httpClient.GetByteArrayAsync(url);
socket.Send(apiData);
```

**예상 성능**:
- 64B: ~3.57M msg/sec
- 512B: ~1.60M msg/sec
- 1KB: ~1.05M msg/sec
- 64KB: ~53.9K msg/sec

#### 2.2 최대 처리량 시나리오 (예: 로그 수집기, 메트릭 전송)

**시나리오**: 가능한 한 빠르게 대량의 메시지 전송

**권장사항**: 메시지 크기에 따른 전략 선택
- **작은~중간 메시지 (≤1KB)**: ArrayPool 사용
- **큰 메시지 (≥64KB)**: MessageZeroCopy 사용
- **수신**: Message 객체 재사용

```csharp
// 전송자: 작은~중간 메시지 (≤1KB) - ArrayPool 사용
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // 버퍼에 데이터 작성
    WriteData(buffer.AsSpan(0, size));
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// 전송자: 큰 메시지 (≥64KB) - MessageZeroCopy 사용
nint nativePtr = Marshal.AllocHGlobal(size);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, size);
    sourceData.CopyTo(nativeSpan);
}
using var msg = new Message(nativePtr, size, ptr => Marshal.FreeHGlobal(ptr));
socket.Send(msg);

// 수신자: Message 재사용 + 배치 처리
using var recvMsg = new Message();
while (running)
{
    socket.Recv(recvMsg);
    ProcessMessage(recvMsg.Data);
}
```

**예상 성능**:
- 64B: ~3.84M msg/sec (ArrayPool)
- 512B: ~1.66M msg/sec (ArrayPool)
- 1KB: ~1.18M msg/sec (ArrayPool)
- 64KB: ~60.1K msg/sec (MessageZeroCopy)
- 131KB: ~29.3K msg/sec (ArrayPool)
- 262KB: ~14.4K msg/sec (MessageZeroCopy)

#### 2.3 낮은 지연시간 요구사항 (예: 트레이딩, 실시간 게이밍)

**시나리오**: 메시지당 지연시간 최소화

**권장사항**: ArrayPool 전송 + Message 수신

```csharp
// 전송: ArrayPool로 최소 지연시간 달성
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    WriteData(buffer.AsSpan(0, size));
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// 수신: Message 객체 재사용
using var msg = new Message();
socket.Recv(msg);
ProcessMessage(msg.Data);
```

**예상 지연시간**:
- 64B: ~260 ns (ArrayPool)
- 512B: ~602 ns (ArrayPool)
- 1KB: ~849 ns (ArrayPool)
- 64KB: ~16.7 μs (MessageZeroCopy)

### 3. 성능 체크리스트

#### DO (권장):

- **전송**:
  - 작은~중간 메시지 (≤1KB) → `ArrayPool<byte>.Shared` 사용
  - 큰 메시지 (≥64KB) → `MessageZeroCopy` 사용
  - 메시지 크기에 따라 적절한 전략 선택
- **수신**:
  - Message 객체 재사용 (`using var msg = new Message()`)
  - 단일 Message 객체로 반복 수신
  - `msg.Data`를 통한 제로카피 접근
- **일반**:
  - `using` 키워드로 리소스 정리 보장
  - 버퍼 재사용으로 GC 압력 최소화
  - ArrayPool 사용 시 반드시 Return 호출

#### DON'T (비권장):

- **전송**:
  - 작은~중간 메시지에 Message/MessageZeroCopy 사용 (ArrayPool이 최대 11% 빠름)
  - 대용량 메시지에 ByteArray 사용 (Gen2 GC 발생, 최대 4.4배 느림)
  - ArrayPool 버퍼를 Return하지 않음 (메모리 누수)
- **수신**:
  - 수신할 때마다 새 Message 할당 (GC 압력 증가)
  - 수신할 때마다 새 byte[] 할당 (대용량에서 Gen2 GC 발생)
- **일반**:
  - Message 객체 Dispose 누락
  - 적절한 전략 선택 없이 단일 방식만 고집

### 4. 의사결정 흐름

```
전송 전략 선택:
├─ 메시지 크기 ≤ 1KB?
│  └─ YES → ArrayPool 사용 (최고 성능, 최소 GC)
│     └─ var buf = ArrayPool<byte>.Shared.Rent(size)
│        socket.Send(buf.AsSpan(0, size))
│        ArrayPool<byte>.Shared.Return(buf)
│
└─ NO (≥64KB) → MessageZeroCopy 사용 (제로카피, 최소 GC)
   └─ nint ptr = Marshal.AllocHGlobal(size)
      using var msg = new Message(ptr, size, p => Marshal.FreeHGlobal(p))
      socket.Send(msg)

수신 전략:
└─ Message 객체 재사용 (모든 크기에 최적)
   └─ using var msg = new Message()
      while (running)
      {
          socket.Recv(msg)
          ProcessMessage(msg.Data)
      }

최적 조합: ArrayPool (≤1KB) 또는 MessageZeroCopy (≥64KB) + Message 재사용
```

### 5. 예상 성능 개선률

기존 코드에 최적화를 적용할 때 예상되는 개선률:

| 개선 항목 | 이전 | 이후 | 개선률 |
|----------|------|------|--------|
| **작은 메시지 전송** (64B) | ByteArray (2.80ms) | ArrayPool (2.60ms) | **+8%** 처리량, **-99.94%** 할당량 |
| **중간 메시지 전송** (512B-1KB) | ByteArray | ArrayPool | **+4-11%** 처리량, **-99.99%** 할당량 |
| **큰 메시지 전송** (64KB) | ByteArray (185ms) | MessageZeroCopy (167ms) | **+10%** 처리량, **-99.95%** 할당량 |
| **대용량 메시지 전송** (131KB) | ByteArray (1491ms, Gen2 GC) | ArrayPool (341ms) | **+4.4배** 처리량, **-99.99%** 할당량 |
| **대용량 메시지 전송** (262KB) | ByteArray (2423ms, Gen2 GC) | MessageZeroCopy (697ms) | **+3.5배** 처리량, **-99.99%** 할당량 |

**전체 개선 예시** (131KB 메시지, ByteArray → ArrayPool):
- 실행 시간: 1491 ms → 341 ms (**4.4배 빠름**)
- 처리량: 6.71K/sec → 29.3K/sec (**4.4배 증가**)
- 메모리: 2.5 GB (Gen2 GC 발생) → 129 KB (**-99.99%**)
- GC 압력: Gen2 컬렉션 발생 → Gen0/Gen1/Gen2 모두 0
- **전체 효과: 극적인 성능 개선 + GC 압력 완전 제거**

### 6. 추가 성능 권장사항

- **메시지 크기별 전략**:
  - ≤1KB: ArrayPool (최고 성능, 최소 GC)
  - ≥64KB: MessageZeroCopy (제로카피, 최소 GC)
  - 중간 크기는 벤치마크로 확인 필요
- **수신 최적화**:
  - Message 객체 재사용으로 GC 압력 최소화
  - `msg.Data`를 통한 제로카피 접근
- **GC 최적화**:
  - ArrayPool 사용 시 반드시 Return 호출
  - 대용량 메시지는 절대 byte[] 직접 할당 금지 (Gen2 GC 유발)
- **벤치마크 결과 활용**:
  - 실제 워크로드에서 메시지 크기 분포 측정
  - 크기별 최적 전략 선택으로 최대 4.4배 성능 향상 가능
