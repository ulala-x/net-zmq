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

## 성능 권장사항

- **단일 소켓**: Blocking 모드 사용 (최고 성능)
- **다중 소켓**: Poller 모드 사용 (효율적 이벤트 대기)
- **큰 메시지**: 모든 모드에서 비슷한 성능
