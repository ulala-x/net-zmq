[![English](https://img.shields.io/badge/lang:en-red.svg)](benchmarks.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](benchmarks.ko.md)

# Net.Zmq 성능 벤치마크

이 문서는 수신 모드 비교 및 메시지 버퍼 전략 평가에 중점을 둔 Net.Zmq의 포괄적인 성능 벤치마크 결과를 포함합니다.

## 요약

Net.Zmq는 다양한 성능 요구사항과 아키텍처 패턴을 수용하기 위해 여러 수신 모드와 메시지 버퍼 전략을 제공합니다. 이 벤치마크 제품군은 다음을 평가합니다:

- **수신 모드 (Receive Modes)**: PureBlocking, BlockingWithBatch, NonBlocking, Poller 기반 메시지 수신
- **메시지 버퍼 전략 (Message Buffer Strategies)**: ByteArray, ArrayPool, Message, MessageZeroCopy 접근 방식
- **메시지 크기 (Message Sizes)**: 64바이트(작음), 512바이트, 1024바이트, 65KB(큼)

### 테스트 환경

| 구성 요소 | 사양 |
|-----------|--------------|
| **CPU** | Intel Core Ultra 7 265K (20 코어) |
| **OS** | Ubuntu 24.04.3 LTS (Noble Numbat) |
| **런타임** | .NET 8.0.22 (8.0.2225.52707) |
| **JIT** | X64 RyuJIT AVX2 |
| **벤치마크 도구** | BenchmarkDotNet v0.14.0 |

### 벤치마크 구성

- **작업 (Job)**: ShortRun
- **플랫폼 (Platform)**: X64
- **반복 횟수 (Iteration Count)**: 3
- **워밍업 횟수 (Warmup Count)**: 3
- **실행 횟수 (Launch Count)**: 1
- **메시지 수 (Message Count)**: 테스트당 10,000 메시지
- **전송 (Transport)**: tcp://127.0.0.1 (로컬호스트 루프백)
- **패턴 (Pattern)**: ROUTER-to-ROUTER (수신 모드 테스트용)

## 수신 모드 벤치마크

### 각 모드의 작동 방식

벤치마크는 4가지 다른 수신 전략을 비교합니다:

#### 1. PureBlocking: 순수 블로킹 Recv() 패턴

**구현**:
```csharp
for (int n = 0; n < MessageCount; n++)
{
    socket.Recv(buffer);  // 각 메시지마다 블로킹 대기
}
```

**특징**:
- 모든 메시지가 블로킹 `recv()` 시스템 콜 필요
- 결정적인 대기로 가장 간단한 구현
- **대기 중 CPU 사용량: 0%** (스레드가 커널에서 슬립)
- 소켓당 하나의 스레드 필요

#### 2. BlockingWithBatch: 블로킹 + 배치 하이브리드 패턴

**구현**:
```csharp
while (n < MessageCount)
{
    // 첫 메시지: 블로킹 대기
    socket.Recv(buffer);
    n++;

    // 사용 가능한 메시지 배치 수신
    while (n < MessageCount && socket.Recv(buffer, RecvFlags.DontWait) != -1)
    {
        n++;
    }
}
```

**특징**:
- 첫 메시지는 블로킹하고, 그 후 사용 가능한 모든 메시지 드레인
- 여러 메시지가 큐에 있을 때 배치 처리로 시스템 콜 오버헤드 감소
- **첫 메시지 대기 중 CPU 사용량: 0%**
- 버스티한 트래픽의 고처리량 시나리오에 최적

#### 3. NonBlocking: DontWait + Sleep(1ms) + 배치 패턴

**구현**:
```csharp
while (n < MessageCount)
{
    while (n < MessageCount && socket.Recv(buffer, RecvFlags.DontWait) != -1)
    {
        n++;
    }
    Thread.Sleep(1);  // CPU 사용량을 줄이기 위해 1ms 슬립
}
```

**특징**:
- **커널 레벨 블로킹 없음** - 모든 폴링이 사용자 공간에서 발생
- `Thread.Sleep(1ms)`가 CPU 사용량을 줄이지만 지연 오버헤드 추가
- 사용 가능한 메시지를 배치 처리하지만, Sleep()이 최소 1ms 지연 추가
- **상대적으로 성능 저하** (블로킹보다 1.3-1.7배 느림)

#### 4. Poller: Poll(-1) + 배치 패턴 (I/O 멀티플렉싱)

**구현**:
```csharp
while (n < MessageCount)
{
    poller.Poll(-1);  // 소켓 이벤트에 대한 블로킹 대기

    // 사용 가능한 모든 메시지 배치 수신
    while (n < MessageCount && socket.Recv(buffer, RecvFlags.DontWait) != -1)
    {
        n++;
    }
}
```

**특징**:
- OS 멀티플렉싱 API(epoll/kqueue/IOCP)를 사용하여 소켓 이벤트 대기
- **대기 중 CPU 사용량: 0%** (커널 레벨 블로킹)
- 깨어난 후 사용 가능한 모든 메시지 배치 처리
- 단일 스레드로 여러 소켓을 모니터링하는 데 이상적

#### Blocking과 Poller가 효율적인 이유

| 모드 | 대기 위치 | 깨우기 메커니즘 | CPU (유휴) | 효율성 |
|------|-----------------|----------------|------------|------------|
| **Blocking** | 커널 공간 | 커널 인터럽트 | 0% | ✓ 단일 소켓에 최적 |
| **Poller** | 커널 공간 | 커널 (epoll/kqueue) | 0% | ✓ 다중 소켓에 최적 |
| **NonBlocking** | 사용자 공간 | 없음 (지속적 폴링) | 낮음 (Sleep 1ms) | ✗ 상대적 성능 저하 |
 

**핵심 통찰**: Blocking과 Poller는 대기를 커널에 위임하며, 커널은:
- 하드웨어 인터럽트를 사용하여 데이터 도착을 즉시 감지
- 이벤트 발생까지 스레드를 슬립 상태로 유지 (0% CPU)
- 필요한 정확한 순간에 스레드를 깨움

NonBlocking은 이러한 커널 지원이 부족하여 Thread.Sleep()으로 지연 오버헤드를 추가하는 사용자 공간에서 지속적인 확인을 강제합니다.

### 벤치마크 메트릭 이해

벤치마크 결과에는 다음 열이 포함됩니다:

| 열 | 설명 |
|--------|-------------|
| **Mean** | 모든 메시지를 송수신하는 평균 실행 시간 (낮을수록 좋음) |
| **Error** | 평균의 표준 오차 (통계적 오차 범위) |
| **StdDev** | 측정 변동성을 나타내는 표준 편차 |
| **Ratio** | 기준선과 비교한 성능 비율 (1.00x = 기준선, 높을수록 느림) |
| **Latency** | `Mean / MessageCount`로 계산된 메시지당 지연 시간 |
| **Messages/sec** | 메시지 처리량 - 초당 처리된 메시지 수 |
| **Data Throughput** | 실제 네트워크 대역폭 (작은 메시지는 Gbps, 큰 메시지는 GB/s) |
| **Allocated** | 벤치마크 중 할당된 총 메모리 |
| **Gen0/Gen1** | 가비지 컬렉션 주기 수 (낮을수록 좋음) |

**결과 읽는 방법**: 낮은 Mean 시간과 높은 Messages/sec는 더 나은 성능을 나타냅니다. Ratio는 1.00x가 각 카테고리에서 가장 느린 방법(일반적으로)인 기준선인 상대 성능을 나타냅니다.

### 성능 결과

모든 테스트는 동시 송신자 및 수신자를 사용한 ROUTER-to-ROUTER 패턴을 사용합니다.

#### 64바이트 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 2.418 ms | 241.8 ns | 4.14M | 2.12 Gbps | 340 B | 1.00x |
| **BlockingWithBatch** | 2.374 ms | 237.4 ns | 4.21M | 2.16 Gbps | 340 B | 0.98x |
| **Poller** | 2.380 ms | 238.0 ns | 4.20M | 2.15 Gbps | 460 B | 0.98x |
| NonBlocking (Sleep 1ms) | 3.468 ms | 346.8 ns | 2.88M | 1.48 Gbps | 339 B | 1.43x |

#### 512바이트 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 5.289 ms | 528.9 ns | 1.89M | 7.74 Gbps | 344 B | 1.00x |
| **BlockingWithBatch** | 5.493 ms | 549.3 ns | 1.82M | 7.46 Gbps | 344 B | 1.04x |
| **Poller** | 5.318 ms | 531.8 ns | 1.88M | 7.70 Gbps | 467 B | 1.01x |
| NonBlocking (Sleep 1ms) | 6.819 ms | 681.9 ns | 1.47M | 6.01 Gbps | 344 B | 1.29x |

#### 1024바이트 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 8.263 ms | 826.3 ns | 1.21M | 9.91 Gbps | 352 B | 1.00x |
| **BlockingWithBatch** | 8.066 ms | 806.6 ns | 1.24M | 10.16 Gbps | 352 B | 0.98x |
| **Poller** | 8.367 ms | 836.7 ns | 1.20M | 9.79 Gbps | 472 B | 1.01x |
| NonBlocking (Sleep 1ms) | 10.220 ms | 1.02 μs | 978.46K | 8.02 Gbps | 352 B | 1.24x |

#### 65KB 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **PureBlocking** | 148.122 ms | 14.81 μs | 67.51K | 4.12 GB/s | 352 B | 1.00x |
| **BlockingWithBatch** | 143.933 ms | 14.39 μs | 69.48K | 4.24 GB/s | 688 B | 0.97x |
| **Poller** | 144.763 ms | 14.48 μs | 69.08K | 4.22 GB/s | 640 B | 0.98x |
| NonBlocking (Sleep 1ms) | 359.381 ms | 35.94 μs | 27.83K | 1.70 GB/s | 1360 B | 2.43x |

### 성능 분석

**PureBlocking, BlockingWithBatch, Poller 비교**: 세 가지 블로킹 기반 모드의 성능은 모든 메시지 크기에서 거의 동일합니다(97-104% 상대 성능). 작은 메시지(64B)에서 BlockingWithBatch와 Poller가 PureBlocking보다 약 2% 빠릅니다. 중간 크기 메시지(512B-1KB)에서는 세 모드가 거의 동등한 성능을 보이며, 큰 메시지(65KB)에서 BlockingWithBatch가 3% 빠릅니다. 모든 블로킹 기반 모드는 메시지가 도착할 때 스레드를 효율적으로 깨우는 커널 레벨 대기 메커니즘을 사용합니다.

**NonBlocking 성능**: `Thread.Sleep(1ms)`를 사용한 NonBlocking 모드는 다음 이유로 블로킹 기반 모드보다 일관되게 느립니다(1.24-2.43배 느림):
1. `Recv(RecvFlags.DontWait)`를 사용한 사용자 공간 폴링은 커널 레벨 블로킹에 비해 오버헤드 발생
2. Thread.Sleep()은 최소 1ms 슬립 간격으로도 지연 추가
3. 블로킹 기반 모드는 메시지가 도착할 때 즉시 스레드를 깨우는 효율적인 커널 메커니즘(`recv()` 시스템 콜 및 `zmq_poll()`)을 사용

**메시지 크기 영향**: Sleep 오버헤드는 큰 메시지에서 더 두드러집니다. 작은 메시지(64B)에서 NonBlocking이 1.43배 느리고, 큰 메시지(65KB)의 경우 2.43배 느립니다.

**권장사항**: NonBlocking 모드는 상대적으로 느리므로(24-143% 느림) 굳이 사용할 필요가 없습니다. 대부분의 시나리오에 Poller(다중 소켓 지원과 최고의 전반적인 성능) 또는 단일 소켓 애플리케이션에 PureBlocking/BlockingWithBatch를 사용하세요.

### 수신 모드 선택 고려사항

수신 모드를 선택할 때 다음을 고려하세요:

**권장 접근 방식**:
- **단일 소켓**: 단순성과 결정적인 동작을 위해 **PureBlocking** 모드 사용
  - 최소한의 코드 복잡도로 가장 간단한 구현
  - 예측 가능한 동작: 메시지당 하나의 시스템 콜
  - 메시지 도착이 예측 불가능하거나 드문 시나리오에 이상적

- **다중 소켓**: 단일 스레드로 여러 소켓을 모니터링하려면 **Poller** 모드 사용
  - 많은 소켓으로 확장 가능한 이벤트 기반 아키텍처
  - 커널이 소켓에 데이터가 있을 때 효율적으로 스레드를 깨움
  - 처리량 효율성을 위해 사용 가능한 메시지를 배치 처리

**전략별 성능 특성**:

1. **PureBlocking**: 단일 소켓, 낮은-중간 처리량 시나리오에 최적
   - 메시지당 하나의 블로킹 시스템 콜
   - 배치 처리 오버헤드나 복잡성 없음
   - 예측 가능하고 결정적인 지연

2. **BlockingWithBatch**: 고처리량, 버스티한 트래픽 시나리오에 최적
   - 블로킹 대기와 기회적 배치 처리를 결합
   - 메시지가 버스트로 도착할 때 시스템 콜 오버헤드 감소
   - 송신자가 수신자가 처리할 수 있는 것보다 빠르게 메시지를 생성할 때 최적

3. **NonBlocking**: **권장하지 않음** - 상대적으로 성능 저하
   - 블로킹 전략보다 1.3-1.7배 느림
   - Thread.Sleep(1ms)가 배치당 최소 지연 추가
   - 기존 폴링 루프와 통합해야 하는 경우에만 사용

4. **Poller**: 다중 소켓 시나리오에 최적
   - 단일 스레드가 여러 소켓을 효율적으로 모니터링
   - 유휴 CPU 사용량 제로의 커널 레벨 이벤트 알림
   - 깨어난 후 메시지를 배치 처리하여 처리량 효율성

**일반 권장사항**:
- **단일 소켓 + 예측 가능한 트래픽**: **PureBlocking** 사용
- **단일 소켓 + 버스티한 트래픽**: **BlockingWithBatch** 사용 (구현된 경우)
- **다중 소켓**: 항상 **Poller** 사용
- **피해야 할 것**: NonBlocking 모드 (Sleep 오버헤드로 1.3-1.7배 느림)

## 메시지 버퍼 전략 벤치마크

### 각 전략의 작동 방식

**ByteArray (`new byte[]`)**: 각 메시지에 대해 새 바이트 배열을 할당합니다. 간단하고 직관적이지만 메시지 크기와 빈도에 비례하는 가비지 컬렉션 압력을 생성합니다.

**ArrayPool (`ArrayPool<byte>.Shared`)**: 공유 풀에서 버퍼를 대여하고 사용 후 반환합니다. 메모리를 재사용하여 GC 할당을 줄이지만, 수동 반환 관리가 필요합니다.

**Message (`zmq_msg_t`)**: libzmq의 네이티브 메시지 구조를 사용하며 내부적으로 메모리를 관리합니다. .NET 래퍼는 필요에 따라 네이티브와 관리 메모리 간에 데이터를 마샬링합니다.

**MessageZeroCopy (`Marshal.AllocHGlobal`)**: 언매니지드 메모리를 직접 할당하고 프리 콜백을 통해 libzmq에 소유권을 전달합니다. 제로카피 시맨틱을 제공하지만 신중한 라이프사이클 관리가 필요합니다.

**MessagePool (`MessagePool.Shared`)**: 네이티브 메모리 버퍼를 풀링하여 재사용합니다. 스레드-로컬 캐시와 공유 풀을 2단계로 구성하여 높은 성능과 낮은 경합을 제공합니다. ZeroMQ의 free callback을 통해 자동으로 풀에 반환되므로 수동 반환이 필요 없습니다. **MessagePooled_WithReceivePool**은 송신과 수신 모두에서 풀을 사용하여 최소한의 메모리 할당을 달성합니다.

### 메시지 버퍼 벤치마크 메트릭 이해

[표준 벤치마크 메트릭](#벤치마크-메트릭-이해) 외에도 메시지 버퍼 전략 벤치마크에는 다음이 포함됩니다:

| 열 | 설명 |
|--------|-------------|
| **Gen0** | 벤치마크 중 세대 0 가비지 컬렉션 수 (낮을수록 좋음) |
| **Gen1** | 세대 1 가비지 컬렉션 수 (큰 할당에만 나타남) |

**GC 영향**: 높은 Gen0/Gen1 값은 더 많은 GC 압력을 나타내며, 성능 저하 및 예측할 수 없는 지연 스파이크를 유발할 수 있습니다. 대시(-)는 컬렉션이 발생하지 않았음을 의미합니다.

### 성능 결과

모든 테스트는 수신에 PureBlocking 모드를 사용합니다.

#### 64바이트 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 2.409 ms | 240.89 ns | 4.15M | 2.13 Gbps | 3.91 | 1719.08 KB | 1.00x |
| **ArrayPool** | 2.723 ms | 272.34 ns | 3.67M | 1.88 Gbps | - | 1.08 KB | 1.13x |
| **Message** | 4.859 ms | 485.86 ns | 2.06M | 1.05 Gbps | - | 1406.58 KB | 2.02x |
| **MessageZeroCopy** | 5.926 ms | 592.64 ns | 1.69M | 0.86 Gbps | - | 1406.58 KB | 2.46x |
| **MessagePool** | 4.034 ms | 403.41 ns | 2.48M | 1.27 Gbps | - | 703.46 KB | 1.67x |
| **MessagePool+RecvPool** | 2.855 ms | 285.45 ns | 3.50M | 1.79 Gbps | - | 2.74 KB | 1.18x |

#### 512바이트 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 5.708 ms | 570.83 ns | 1.75M | 7.18 Gbps | 23.44 | 10469.09 KB | 1.00x |
| **ArrayPool** | 5.355 ms | 535.52 ns | 1.87M | 7.65 Gbps | - | 1.52 KB | 0.94x |
| **Message** | 6.258 ms | 625.85 ns | 1.60M | 6.54 Gbps | - | 1406.59 KB | 1.10x |
| **MessageZeroCopy** | 6.886 ms | 688.59 ns | 1.45M | 5.95 Gbps | - | 1406.59 KB | 1.21x |
| **MessagePool** | 5.376 ms | 537.58 ns | 1.86M | 7.62 Gbps | - | 703.46 KB | 0.94x |
| **MessagePool+RecvPool** | 5.478 ms | 547.77 ns | 1.83M | 7.48 Gbps | - | 2.74 KB | 0.96x |

#### 1024바이트 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ByteArray** | 8.637 ms | 863.68 ns | 1.16M | 9.48 Gbps | 46.88 | 20469.09 KB | 1.00x |
| **ArrayPool** | 7.820 ms | 782.02 ns | 1.28M | 10.48 Gbps | - | 2.04 KB | 0.91x |
| **Message** | 8.495 ms | 849.46 ns | 1.18M | 9.64 Gbps | - | 1406.59 KB | 0.98x |
| **MessageZeroCopy** | 10.678 ms | 1.07 μs | 936.47K | 7.67 Gbps | - | 1406.59 KB | 1.24x |
| **MessagePool** | 7.616 ms | 761.57 ns | 1.31M | 10.76 Gbps | - | 703.46 KB | 0.88x |
| **MessagePool+RecvPool** | 7.888 ms | 788.80 ns | 1.27M | 10.39 Gbps | - | 2.75 KB | 0.91x |

#### 65KB 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **ByteArray** | 170.14 ms | 17.01 μs | 58.77K | 3.59 GB/s | 3333.33 | 666.67 | 1280469.54 KB | 1.00x |
| **ArrayPool** | 160.17 ms | 16.02 μs | 62.44K | 3.81 GB/s | - | - | 65.29 KB | 0.94x |
| **Message** | 175.25 ms | 17.52 μs | 57.06K | 3.48 GB/s | - | - | 1406.82 KB | 1.03x |
| **MessageZeroCopy** | 164.90 ms | 16.49 μs | 60.64K | 3.70 GB/s | - | - | 1407.04 KB | 0.97x |
| **MessagePool** | 169.32 ms | 16.93 μs | 59.06K | 3.60 GB/s | - | - | 703.90 KB | 1.00x |
| **MessagePool+RecvPool** | 180.06 ms | 18.01 μs | 55.54K | 3.39 GB/s | - | - | 3.03 KB | 1.06x |

#### 128KB 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Gen1 | Gen2 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|------|-----------|-------|
| **ByteArray** | 1,259 ms | 125.91 μs | 7.94K | 0.97 GB/s | 51000 | 51000 | 51000 | 2.5 GB | 1.00x |
| **ArrayPool** | 342.74 ms | 34.27 μs | 29.18K | 3.56 GB/s | - | - | - | 129.48 KB | 0.27x |
| **Message** | 375.43 ms | 37.54 μs | 26.64K | 3.25 GB/s | - | - | - | 1407.95 KB | 0.30x |
| **MessageZeroCopy** | 361.82 ms | 36.18 μs | 27.64K | 3.37 GB/s | - | - | - | 1407.95 KB | 0.29x |
| **MessagePool** | 367.43 ms | 36.74 μs | 27.22K | 3.32 GB/s | - | - | - | 704.83 KB | 0.29x |
| **MessagePool+RecvPool** | 394.45 ms | 39.44 μs | 25.35K | 3.09 GB/s | - | - | - | 3.29 KB | 0.31x |

#### 256KB 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Gen1 | Gen2 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|------|-----------|-------|
| **ByteArray** | 2,485 ms | 248.52 μs | 4.02K | 0.98 GB/s | 100000 | 100000 | 100000 | 5 GB | 1.00x |
| **ArrayPool** | 719.49 ms | 71.95 μs | 13.90K | 3.39 GB/s | - | - | - | 257.70 KB | 0.29x |
| **Message** | 698.36 ms | 69.84 μs | 14.32K | 3.50 GB/s | - | - | - | 1407.95 KB | 0.28x |
| **MessageZeroCopy** | 716.22 ms | 71.62 μs | 13.96K | 3.41 GB/s | - | - | - | 1407.95 KB | 0.29x |
| **MessagePool** | 708.05 ms | 70.80 μs | 14.12K | 3.45 GB/s | - | - | - | 704.83 KB | 0.28x |
| **MessagePool+RecvPool** | 709.37 ms | 70.94 μs | 14.10K | 3.44 GB/s | - | - | - | 3.95 KB | 0.29x |

### 성능 및 GC 분석

**작은 메시지 (64B)**: ByteArray (2.409 ms, 4.15M msg/sec, 1.00x)가 가장 빠르며, **MessagePool+RecvPool** (2.855 ms, 3.50M msg/sec, 1.18x)이 GC-프리로 근접한 성능을 보입니다. ByteArray는 GC 압력(3.91 Gen0, 1719 KB 할당)을 생성하는 반면, MessagePool+RecvPool은 단 2.74 KB만 할당합니다. Message (4.859 ms, 2.02x)와 MessageZeroCopy (5.926 ms, 2.46x)는 네이티브 interop 비용으로 상당한 오버헤드를 보입니다.

**중간 메시지 (512B)**: **ArrayPool** (5.355 ms, 0.94x)과 **MessagePool** (5.376 ms, 0.94x)이 거의 동일하게 가장 빠릅니다. MessagePool+RecvPool (5.478 ms, 0.96x)도 경쟁력 있는 성능에 최소 할당(2.74 KB)을 제공합니다. ByteArray (5.708 ms)는 GC 압력(23.44 Gen0, 10.5 MB 할당)이 증가합니다.

**중대형 메시지 (1KB)**: **MessagePool** (7.616 ms, 0.88x)이 가장 빠르며, ArrayPool (7.820 ms, 0.91x)과 MessagePool+RecvPool (7.888 ms, 0.91x)이 뒤따릅니다. MessagePool은 네이티브 메모리 재사용으로 ArrayPool보다 3% 빠른 성능과 함께 GC-프리를 달성합니다.

**큰 메시지 (65KB)**: **ArrayPool** (160.17 ms, 0.94x)이 가장 빠르며, MessageZeroCopy (164.90 ms, 0.97x)와 MessagePool (169.32 ms, 1.00x)이 비슷한 성능을 보입니다. ByteArray (170.14 ms)는 대규모 GC 압력(3333 Gen0, 1.25 GB 할당)을 트리거합니다. MessagePool+RecvPool (180.06 ms, 1.06x)은 약간 느리지만 가장 적은 메모리(3.03 KB)를 할당합니다.

**매우 큰 메시지 (128KB)**: ByteArray가 극심한 GC 압력(Gen0/1/2 각 51,000번, 2.5GB 할당)으로 1,259ms가 걸립니다. **ArrayPool** (342.74 ms, 0.27x)이 가장 빠르며 3.7배 빠릅니다. MessageZeroCopy (361.82 ms, 0.29x), MessagePool (367.43 ms, 0.29x), Message (375.43 ms, 0.30x)도 모두 3배 이상 빠릅니다. MessagePool+RecvPool (394.45 ms, 0.31x)은 가장 적은 메모리(3.29 KB)를 할당합니다.

**초대형 메시지 (256KB)**: ByteArray는 GC 압력이 더 심해져(Gen0/1/2 각 100,000번, 5GB 할당) 2,485ms가 걸립니다. **Message** (698.36 ms, 0.28x)가 가장 빠르며 3.6배 빠릅니다. MessagePool (708.05 ms, 0.28x), MessagePool+RecvPool (709.37 ms, 0.29x), MessageZeroCopy (716.22 ms, 0.29x), ArrayPool (719.49 ms, 0.29x) 모두 유사한 성능을 보입니다.

**MessagePool의 장점**:
- **자동 반환**: ZeroMQ free callback을 통해 송신 완료 시 자동으로 풀에 반환되어 수동 관리 불필요
- **스레드-로컬 캐시**: 2단계 풀링(스레드-로컬 + 공유 풀)으로 높은 성능과 낮은 경합
- **1KB 이상에서 최고 성능**: 중간 크기 메시지에서 ArrayPool보다 빠름
- **최소 메모리 할당**: MessagePool+RecvPool 사용 시 모든 크기에서 3KB 미만 할당

**Large Object Heap (LOH) 영향**: .NET에서 85KB 이상 객체는 LOH에 할당되어 GC 비용이 급증. 128KB/256KB에서 ByteArray의 성능이 급격히 나빠지는 이유. MessagePool을 포함한 네이티브 전략은 이 문제를 완전히 회피.

**GC 패턴 전환**: ArrayPool, MessagePool 및 네이티브 전략은 모든 메시지 크기에서 제로 GC 컬렉션을 유지합니다. ByteArray는 기하급수적 GC 압력 증가를 나타냅니다: 64B에서 3.91 Gen0 → 512B에서 23.44 Gen0 → 1KB에서 46.88 Gen0 → 65KB에서 3333 Gen0 → 128KB에서 51,000 Gen0/1/2 → 256KB에서 100,000 Gen0/1/2.

**메모리 할당**: MessagePool+RecvPool이 가장 효율적(모든 크기에서 2.74-3.95 KB 총 할당)입니다. ArrayPool (1.08-258 KB)과 MessagePool (703-705 KB)도 ByteArray 대비 99% 이상 감소를 보여줍니다.

### 메시지 버퍼 전략 선택 고려사항

메시지 버퍼 전략을 선택할 때 다음을 고려하세요:

**메시지 크기 기반 권장사항**:
- **작은 메시지 (≤512B)**: **`ArrayPool<byte>.Shared`** 또는 **`MessagePool`** - ByteArray와 동등한 성능, GC 프리
- **중간 메시지 (1KB)**: **`MessagePool`** - ArrayPool보다 3% 빠르고 자동 반환
- **큰 메시지 (≥65KB)**: **`ArrayPool`** 또는 **`MessagePool`** - 유사한 성능, GC 프리
- **매우 큰 메시지 (≥128KB)**: **`ArrayPool`** 또는 **`MessagePool`** - ByteArray 대비 3배 이상 빠름
- **최소 메모리 할당 필요시**: **`MessagePool+RecvPool`** - 모든 크기에서 3KB 미만 할당

**단일 전략 권장 (One-Size-Fits-All)**:

메시지 크기가 다양하거나 예측하기 어려운 경우, **`MessagePool`을 권장**합니다:

1. **일관된 성능**: 모든 메시지 크기에서 예측 가능한 성능 제공
2. **자동 반환**: ZeroMQ free callback을 통해 자동으로 풀에 반환, 수동 관리 불필요
3. **GC 프리**: 네이티브 메모리 풀링으로 GC 압력 제로
4. **스레드-로컬 캐시**: 2단계 풀링으로 높은 성능과 낮은 경합
5. **LOH 회피**: 128KB+ 메시지에서 .NET LOH 문제 완전 회피
6. **충분한 소형 메시지 성능**: 64B에서도 초당 248만 메시지 처리 가능

ArrayPool은 작은 메시지에서 약간 빠르지만 수동 반환 관리가 필요합니다. MessagePool은 ArrayPool과 비슷한 성능에 자동 반환을 제공하므로, 단일 전략을 선택해야 한다면 **MessagePool이 더 안전한 선택**입니다.

**ArrayPool 사용 패턴**:
```csharp
using Net.Zmq;
using System.Buffers;

// Rent buffer from pool
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Fill buffer with data
    socket.Send(buffer.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

**MessagePool 사용 패턴** (권장):
```csharp
using Net.Zmq;

// 송신: 풀에서 메시지를 빌려서 전송 (자동 반환)
var sendMsg = MessagePool.Shared.Rent(dataSize);
sourceData.CopyTo(sendMsg.Data);
socket.Send(sendMsg);  // ZeroMQ free callback을 통해 자동 반환

// 수신: 풀에서 메시지를 빌려서 수신
var recvMsg = MessagePool.Shared.Rent(expectedSize);
socket.Recv(recvMsg, expectedSize);
// 데이터 처리...
recvMsg.Dispose();  // 풀에 반환
```

**MessageZeroCopy 사용 패턴** (특수한 경우):
```csharp
using Net.Zmq;
using System.Runtime.InteropServices;

// Allocate unmanaged memory
nint nativePtr = Marshal.AllocHGlobal(dataSize);
unsafe
{
    var nativeSpan = new Span<byte>((void*)nativePtr, dataSize);
    sourceData.CopyTo(nativeSpan);
}

// Transfer ownership to libzmq
using var message = new Message(nativePtr, dataSize, ptr =>
{
    Marshal.FreeHGlobal(ptr); // Called when libzmq is done
});

socket.Send(message);
```

**GC 민감도**:
- GC 일시 중지에 민감한 애플리케이션은 ArrayPool(작은 메시지) 또는 Message(큰 메시지)를 선호해야 함
- 드문 메시징이나 작은 메시지가 있는 애플리케이션은 ByteArray가 허용 가능할 수 있음
- 고처리량 애플리케이션은 GC 프리 전략(ArrayPool, Message)의 이점

**코드 복잡성**:
- **ByteArray**: 자동 메모리 관리로 가장 간단한 구현
- **ArrayPool**: 명시적 Rent/Return 호출 및 버퍼 라이프사이클 추적 필요
- **Message**: 적당한 복잡도로 네이티브 통합
- **MessageZeroCopy**: 언매니지드 메모리 관리 및 프리 콜백 필요

**성능 트레이드오프**:
- **작은 메시지 (≤ 512B)**: 관리 전략(ByteArray, ArrayPool)이 더 낮은 오버헤드 보유
- **큰 메시지 (≥ 512B)**: Message가 최적의 성능 제공
- **일관성**: GC 프리 전략(ArrayPool, Message)이 더 예측 가능한 타이밍 제공

## 벤치마크 실행

이러한 벤치마크를 직접 실행하려면:

```bash
cd benchmarks/Net.Zmq.Benchmarks
dotnet run -c Release
```

특정 벤치마크의 경우:

```bash
# Run only receive mode benchmarks
dotnet run -c Release --filter "*ReceiveModeBenchmarks*"

# Run only message buffer strategy benchmarks
dotnet run -c Release --filter "*MessageBufferStrategyBenchmarks*"

# Run specific message size
dotnet run -c Release --filter "*MessageSize=64*"
```

## 참고사항

### 측정 환경

- 모든 벤치마크는 `tcp://127.0.0.1` 전송(로컬호스트 루프백) 사용
- 동시 모드는 현실적인 생산자/소비자 시나리오 시뮬레이션
- 결과는 워밍업 후 정상 상태 성능 표시
- BenchmarkDotNet의 ShortRun 작업은 감소된 런타임으로 통계적으로 유효한 측정 제공

### 제한사항 및 고려사항

- `tcp://127.0.0.1` 루프백 전송이 사용됨; 실제 네트워크 성능은 네트워크 인프라에 따라 다름
- 실제 프로덕션 성능은 네트워크 특성, 메시지 패턴 및 시스템 부하에 따라 다름
- GC 측정은 벤치마크 워크로드를 반영; 애플리케이션 GC 동작은 전체 힙 활동에 따라 다름
- 지연 측정에는 10K 메시지에 대한 송신 및 수신 작업 모두 포함
- NonBlocking 모드는 1ms 슬립 간격 사용; 다른 슬립 값은 다른 결과를 생성

### 결과 해석

성능 비율은 각 테스트 카테고리 내에서 1.00x가 기준선(가장 느림)인 상대 성능을 나타냅니다. 낮은 평균 시간과 높은 처리량은 더 나은 성능을 나타냅니다. 할당된 메모리와 GC 컬렉션은 메모리 관리 효율성을 나타냅니다.

벤치마크는 절대적인 "최선"의 선택이 아닌 다양한 접근 방식의 성능 특성을 반영합니다. 선택은 특정 애플리케이션 요구사항, 메시지 패턴 및 아키텍처 제약 조건에 따라 다릅니다.

## 전체 벤치마크 데이터

전체 BenchmarkDotNet 출력은 다음을 참조하세요:
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.ReceiveModeBenchmarks-report-github.md`
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.MessageBufferStrategyBenchmarks-report-github.md`
