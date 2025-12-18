# NetZeroMQ.Benchmarks

NetZeroMQ 성능 벤치마크 도구

## 빠른 시작

```bash
cd benchmarks/NetZeroMQ.Benchmarks

# 전체 벤치마크 실행 (27개 테스트, ~5분)
dotnet run -c Release

# 빠른 테스트 (1회 반복, ~30초)
dotnet run -c Release -- --quick
```

## 실행 옵션

### 벤치마크 실행

```bash
# 전체 실행 (ShortRun: 3 warmup + 3 iterations)
dotnet run -c Release

# 빠른 실행 (Dry: 1 iteration)
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

# 조합 필터
dotnet run -c Release -- --quick --filter "*PushPull*64*Blocking*"
```

### 진단 도구

```bash
# 소켓 연결 테스트
dotnet run -c Release -- --test

# Receive 모드별 동작 확인
dotnet run -c Release -- --mode-test

# 메모리 할당 분석
dotnet run -c Release -- --alloc-test
```

### 벤치마크 목록 확인

```bash
dotnet run -c Release -- --list flat
```

## 벤치마크 구조

### ThroughputBenchmarks

| Parameter | Values |
|-----------|--------|
| **MessageSize** | 64, 1024, 65536 bytes |
| **MessageCount** | 10,000 messages |
| **Mode** | Blocking, NonBlocking, Poller |

**패턴:**
- `PushPull_Throughput` - 단방향 메시지 전송
- `PubSub_Throughput` - Pub/Sub 브로드캐스트
- `RouterRouter_Throughput` - 양방향 라우팅

**총 27개 벤치마크** (3 patterns × 3 sizes × 3 modes)

## 출력 컬럼

| Column | Description |
|--------|-------------|
| **Mean** | 평균 실행 시간 (전체 MessageCount 처리) |
| **Latency** | 메시지당 지연 시간 (Mean / MessageCount) |
| **msg/sec** | 초당 처리량 |
| **Allocated** | 힙 메모리 할당량 |

## Receive 모드 비교

| Mode | Description | Use Case |
|------|-------------|----------|
| **Blocking** | `Recv()` 블로킹 호출 | 단일 소켓, 최고 성능 |
| **NonBlocking** | `TryRecv()` + Sleep + burst | 다중 소켓, 폴링 없이 |
| **Poller** | `Poll()` + `TryRecv()` burst | 다중 소켓, 이벤트 기반 |

## 프로젝트 구조

```
NetZeroMQ.Benchmarks/
├── Program.cs                    # 엔트리 포인트, CLI 옵션 처리
├── Configs/
│   └── BenchmarkConfig.cs        # 커스텀 컬럼 (Latency, msg/sec)
├── Benchmarks/
│   └── ThroughputBenchmarks.cs   # 메인 벤치마크
├── AllocTest.cs                  # 메모리 할당 진단
└── ModeTest.cs                   # Receive 모드 비교 테스트
```

## 예상 결과

### 64 bytes 메시지

| Pattern | Mode | Latency | msg/sec |
|---------|------|---------|---------|
| PushPull | Blocking | ~300 ns | ~3M |
| PushPull | Poller | ~350 ns | ~2.8M |
| PushPull | NonBlocking | ~1.2 μs | ~800K |

### 메모리 할당

| Mode | Allocated |
|------|-----------|
| Blocking | ~900 B |
| NonBlocking | ~900 B |
| Poller | ~1.1 KB |

## 성능 최적화 가이드

### 1. 벤치마크 결과 상세

#### 1.1 송신 전략 비교 (MemoryStrategy, 10,000 messages)

| Message Size | ArrayPool | ByteArray | Message | MessagePooled | Winner |
|--------------|-----------|-----------|---------|---------------|--------|
| **64 bytes** | 2.45 ms (4,082 K/sec) | 2.49 ms (4,016 K/sec) | 5.24 ms (1,908 K/sec) | 7.19 ms (1,391 K/sec) | **ArrayPool** (40% faster) |
| **512 bytes** | 6.37 ms (1,570 K/sec) | 6.86 ms (1,458 K/sec) | 6.75 ms (1,481 K/sec) | 7.99 ms (1,251 K/sec) | **ArrayPool** (20% faster) |
| **1 KB** | 8.79 ms (1,138 K/sec) | 9.08 ms (1,101 K/sec) | 8.49 ms (1,178 K/sec) | **7.02 ms (1,424 K/sec)** | **MessagePooled** (18% faster) |
| **64 KB** | 149.5 ms (66.9 K/sec) | 147.2 ms (67.9 K/sec) | 120.4 ms (83.1 K/sec) | **120.1 ms (83.2 K/sec)** | **MessagePooled** (20% faster) |

**핵심 인사이트:**
- **작은 메시지 (<1KB)**: ArrayPool이 최고 성능 (관리형 메모리 복사가 효율적)
- **큰 메시지 (≥1KB)**: MessagePooled가 최고 성능 (zero-copy가 복사 오버헤드를 압도)
- **전환점**: 약 1KB 지점에서 성능 우위가 역전됨

#### 1.2 수신 모드 비교 (ReceiveMode, 10,000 messages)

| Message Size | Blocking | Poller | NonBlocking | Winner |
|--------------|----------|--------|-------------|--------|
| **64 bytes** | 2.35 ms (4,255 K/sec) | **1.83 ms (5,464 K/sec)** | 3.34 ms (2,994 K/sec) | **Poller** (22% faster) |
| **512 bytes** | 5.28 ms (1,894 K/sec) | **5.08 ms (1,969 K/sec)** | 6.95 ms (1,439 K/sec) | **Poller** (4% faster) |
| **1 KB** | 8.13 ms (1,230 K/sec) | **7.26 ms (1,377 K/sec)** | 10.00 ms (1,000 K/sec) | **Poller** (11% faster) |
| **64 KB** | **143.2 ms (69.8 K/sec)** | 161.7 ms (61.8 K/sec) | 252.3 ms (39.6 K/sec) | **Blocking** (13% faster) |

**핵심 인사이트:**
- **Poller 모드**: 대부분의 경우 최고 성능 (특히 작은~중간 메시지)
- **Blocking 모드**: 매우 큰 메시지(64KB+)에서 약간 우위
- **NonBlocking 모드**: 모든 경우에서 최악 (Sleep 오버헤드로 인한 지연)

### 2. 시나리오별 권장사항

#### 2.1 외부 할당 메모리 전송 (예: API에서 받은 byte[])

**상황**: 이미 할당된 byte[] 데이터를 전송해야 함

**권장**: `SendOptimized()` 사용 (크기별 자동 최적화)

```csharp
// 자동으로 크기에 따라 ArrayPool 또는 MessagePool 선택
byte[] apiData = await httpClient.GetByteArrayAsync(url);
socket.SendOptimized(apiData);
```

**성능 기대치**:
- 64B-512B: ~1.5-4M msg/sec
- 1KB-64KB: ~70-140K msg/sec

#### 2.2 최대 처리량 시나리오 (예: 로그 수집기, 메트릭 전송)

**상황**: 대량의 메시지를 가능한 빠르게 전송

**권장**: MessagePool + Poller 조합

```csharp
// 송신측: MessagePool 일관 사용 (모든 크기에서 안정적)
using var msg = MessagePool.Shared.Rent(MessageSize.Medium);
Span<byte> buffer = msg.Data;
// buffer에 데이터 작성
socket.Send(ref msg, SendFlags.None);

// 수신측: Poller + Message
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

**성능 기대치**:
- 512B: ~1.9M msg/sec
- 1KB: ~1.4M msg/sec
- 64KB: ~62K msg/sec

#### 2.3 저지연 요구사항 (예: 트레이딩, 실시간 게임)

**상황**: 메시지당 지연 시간 최소화

**권장**: MessagePool + Poller (작은 메시지) / Blocking (큰 메시지)

```csharp
// 작은 메시지 (<64KB): Poller 사용
using var poller = new Poller(1);
int idx = poller.Add(socket, PollEvents.In);

using var msg = new Message();
if (poller.Poll(timeout: 1) > 0 && poller.IsReadable(idx))
{
    socket.Recv(ref msg, RecvFlags.None);
    // 지연시간: 64B ~183ns, 1KB ~726ns
}

// 큰 메시지 (≥64KB): Blocking 사용
socket.Recv(ref msg, RecvFlags.None);
// 지연시간: 64KB ~14.3μs
```

### 3. 성능 체크리스트

#### DO:

- **송신**:
  - 외부 메모리 → `SendOptimized()` 사용
  - 최대 성능 → `MessagePool.Shared.Rent()` 일관 사용
  - 메시지 크기별 적절한 `MessageSize` enum 선택
- **수신**:
  - Poller 모드 우선 고려 (대부분 최적)
  - Message 객체 재사용 (using var msg = new Message())
  - Batch 처리 (Poller.IsReadable() 루프)
- **일반**:
  - Prewarm으로 초기 할당 비용 제거
  - using 키워드로 자원 해제 보장

#### DON'T:

- **송신**:
  - 작은 메시지에 MessagePool 강제 사용 (ArrayPool이 더 빠름)
  - MessagePool.Rent() 후 전송하지 않고 Dispose (메모리 누수)
- **수신**:
  - NonBlocking 모드 사용 (Sleep 오버헤드로 최악의 성능)
  - 매 수신마다 새 Message 할당 (GC 압력)
  - Poller 없이 TryRecv() 루프 (CPU 낭비)
- **일반**:
  - Message 객체 수동 Dispose 누락
  - 단일 메시지만 처리하고 대기 (batch 기회 놓침)

### 4. 의사결정 플로우

```
송신 전략 선택:
├─ 외부 할당 메모리(byte[]) 전송?
│  └─ YES → SendOptimized() 사용
│     └─ 자동으로 크기별 최적 전략 선택
│
└─ NO → 직접 메시지 생성
   ├─ 크기 < 1KB?
   │  └─ YES → ArrayPool 고려 (40% 빠름)
   │     └─ 하지만 MessagePool 사용 권장 (일관성)
   │
   └─ NO → MessagePool 사용 (18-23% 빠름)
      └─ MessagePool.Shared.Rent(size)

수신 모드 선택:
├─ 메시지 크기 < 64KB?
│  └─ YES → Poller 모드 (4-22% 빠름)
│     └─ Poller + Message + batch 처리
│
└─ NO → Blocking 모드 고려 (13% 빠름)
   └─ 하지만 Poller도 충분히 빠름 (일관성)

추천: Poller + Message (모든 크기에서 안정적)
```

### 5. 예상 성능 개선 수치

기존 코드에서 최적화 적용 시 예상 개선율:

| 개선 항목 | 이전 | 이후 | 개선율 |
|----------|------|------|--------|
| **작은 메시지 송신** (64B-512B) | ByteArray/Message | SendOptimized (ArrayPool) | **+40%** throughput |
| **큰 메시지 송신** (1KB-64KB) | ByteArray/ArrayPool | MessagePool | **+18-23%** throughput |
| **수신 모드** (all sizes) | NonBlocking | Poller | **+27-82%** throughput |
| **수신 모드** (64KB) | Poller | Blocking | **+13%** throughput |
| **메모리 할당** | 매번 new Message() | Message 재사용 | **-95%** allocations |

**종합 개선 예시** (512B 메시지, 기존 ByteArray+NonBlocking → 최적 ArrayPool+Poller):
- 송신: 1,458 K/sec → 1,570 K/sec (+8%)
- 수신: 1,439 K/sec → 1,969 K/sec (+37%)
- **전체 파이프라인: +22% throughput**

### 6. 추가 성능 권장사항

- **단일 소켓**: Blocking 또는 Poller 모드 (비슷한 성능)
- **다중 소켓**: Poller 모드 필수 (효율적 이벤트 대기)
- **초기화**: `MessagePool.Shared.Prewarm()` 호출로 워밍업
- **버스트 처리**: Poller.IsReadable() 루프로 가용 메시지 모두 처리
