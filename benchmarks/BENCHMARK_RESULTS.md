# NetZeroMQ Benchmark Results

## 테스트 환경

- **OS**: Ubuntu 24.04 LTS (WSL2)
- **CPU**: Intel Core Ultra 7 265K
- **.NET**: 8.0 (Server GC, Concurrent)
- **BenchmarkDotNet**: v0.14.0

---

## Throughput Benchmarks

10,000 메시지 배치 처리 성능 (TCP transport)

### PUSH/PULL (단방향)

| MessageSize | Mode | Mean | Latency | msg/sec | Allocated |
|-------------|------|------|---------|---------|-----------|
| 64 B | Blocking | 3.1 ms | 310 ns | 3.2M | 936 B |
| 64 B | Poller | 3.4 ms | 340 ns | 2.9M | 1.1 KB |
| 64 B | NonBlocking | 12.2 ms | 1.2 μs | 820K | 936 B |
| 1 KB | Blocking | 11.4 ms | 1.1 μs | 880K | 936 B |
| 1 KB | Poller | 10.7 ms | 1.1 μs | 930K | 1.1 KB |
| 1 KB | NonBlocking | 26.0 ms | 2.6 μs | 380K | 936 B |
| 64 KB | Blocking | 129 ms | 12.9 μs | 77K | 936 B |
| 64 KB | Poller | 163 ms | 16.3 μs | 61K | 1.1 KB |
| 64 KB | NonBlocking | 199 ms | 19.9 μs | 50K | 936 B |

### PUB/SUB (브로드캐스트)

| MessageSize | Mode | Mean | Latency | msg/sec | Allocated |
|-------------|------|------|---------|---------|-----------|
| 64 B | Blocking | 3.2 ms | 320 ns | 3.1M | 936 B |
| 64 B | Poller | 3.5 ms | 350 ns | 2.8M | 1.1 KB |
| 64 B | NonBlocking | 12.5 ms | 1.3 μs | 800K | 936 B |

### ROUTER/ROUTER (양방향 라우팅)

| MessageSize | Mode | Mean | Latency | msg/sec | Allocated |
|-------------|------|------|---------|---------|-----------|
| 64 B | Blocking | 5.8 ms | 580 ns | 1.7M | 936 B |
| 64 B | Poller | 6.2 ms | 620 ns | 1.6M | 1.1 KB |
| 64 B | NonBlocking | 14.5 ms | 1.5 μs | 690K | 936 B |

---

## Receive 모드 비교

### 특성

| Mode | 설명 | 장점 | 단점 |
|------|------|------|------|
| **Blocking** | `Recv()` 블로킹 | 최고 성능, 단순함 | 단일 소켓만 |
| **Poller** | `Poll()` + burst `TryRecv()` | 다중 소켓 지원, 이벤트 기반 | 약간의 오버헤드 |
| **NonBlocking** | `TryRecv()` + Sleep | Poll 없이 동작 | 10ms Sleep으로 지연 발생 |

### 성능 순위

1. **Blocking** - 단일 소켓 최적화, OS 레벨 대기
2. **Poller** - Blocking 대비 ~90% 성능, 다중 소켓 가능
3. **NonBlocking** - Blocking 대비 ~25% 성능, Sleep 오버헤드

---

## 메모리 할당

### 모드별 할당량 (10,000 메시지 기준)

| Mode | Allocated | 메시지당 |
|------|-----------|----------|
| Blocking | 936 B | ~0.1 B |
| NonBlocking | 936 B | ~0.1 B |
| Poller | 1,136 B | ~0.1 B |

**Zero-allocation 달성**: ThreadStatic 캐시를 통해 Poll 호출 시 배열 재할당 제거

---

## 권장사항

### 소켓 패턴 선택

| 시나리오 | 권장 패턴 |
|----------|-----------|
| 작업 분배 | PUSH/PULL |
| 이벤트 브로드캐스트 | PUB/SUB |
| 양방향 라우팅 | ROUTER/ROUTER |
| Request-Reply | DEALER/ROUTER (REQ/REP보다 빠름) |

### Receive 모드 선택

| 시나리오 | 권장 모드 |
|----------|-----------|
| 단일 소켓 | Blocking |
| 다중 소켓 대기 | Poller |
| 주기적 폴링 | NonBlocking |

### Transport 선택

| Transport | 용도 | 상대 성능 |
|-----------|------|-----------|
| `inproc://` | 동일 프로세스 | 가장 빠름 |
| `ipc://` | 동일 머신 IPC | 빠름 |
| `tcp://` | 네트워크 통신 | 보통 |

---

## 벤치마크 실행

```bash
cd benchmarks/NetZeroMQ.Benchmarks

# 전체 실행
dotnet run -c Release

# 빠른 테스트
dotnet run -c Release -- --quick

# 특정 패턴
dotnet run -c Release -- --filter "*PushPull*"

# 메모리 진단
dotnet run -c Release -- --alloc-test
```

---

*Last updated: 2025-12-15*
