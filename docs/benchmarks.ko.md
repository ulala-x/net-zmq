[![English](https://img.shields.io/badge/lang-en-red.svg)](benchmarks.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](benchmarks.ko.md)

# Net.Zmq 성능 벤치마크

이 문서는 수신 모드 비교 및 메모리 전략 평가에 중점을 둔 Net.Zmq의 포괄적인 성능 벤치마크 결과를 포함합니다.

## 요약

Net.Zmq는 다양한 성능 요구사항과 아키텍처 패턴을 수용하기 위해 여러 수신 모드와 메모리 전략을 제공합니다. 이 벤치마크 제품군은 다음을 평가합니다:

- **수신 모드 (Receive Modes)**: Blocking, NonBlocking, Poller 기반 메시지 수신
- **메모리 전략 (Memory Strategies)**: ByteArray, ArrayPool, Message, MessageZeroCopy 접근 방식
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

#### Blocking 모드 - I/O 블로킹 패턴

**API**: `socket.Recv()`

**내부 메커니즘**:
1. `recv()` 시스템 콜 호출, 사용자 공간에서 커널 공간으로 전환
2. 스레드가 커널의 대기 큐에서 슬립 상태 진입
3. 데이터 도착 시 → 네트워크 하드웨어가 인터럽트 트리거
4. 커널이 스레드를 준비 큐로 이동
5. 스케줄러가 스레드를 깨우고 실행 재개

**특징**:
- 결정적인 대기로 가장 간단한 구현
- **대기 중 CPU 사용량: 0%** (스레드가 커널에서 슬립)
- 커널이 필요할 때 정확히 스레드를 깨움
- 소켓당 하나의 스레드 필요

#### Poller 모드 - 리액터 패턴 (I/O 멀티플렉싱)

**API**: `zmq_poll()`

**내부 메커니즘**:
1. 내부적으로 OS 멀티플렉싱 API를 사용하는 `zmq_poll(sockets, timeout)` 호출:
   - Linux: `epoll_wait()`
   - BSD/macOS: `kqueue()`
   - Windows: `select()` 또는 IOCP
2. 커널이 여러 소켓을 동시에 모니터링
3. 소켓 이벤트 발생 시 → 커널이 즉시 제어 반환
4. 이벤트가 준비된 소켓을 표시

**특징**:
- 단일 스레드로 여러 소켓을 모니터링하는 이벤트 기반 아키텍처
- **대기 중 CPU 사용량: 0%** (커널 레벨 블로킹)
- 커널이 하드웨어 인터럽트를 사용하여 이벤트를 효율적으로 감지
- 폴링 인프라를 위한 약간 더 많은 메모리 오버헤드

#### NonBlocking 모드 - 폴링 패턴 (바쁜 대기)

**API**: `socket.TryRecv()`

**내부 메커니즘**:
1. 사용자 공간에서 반복 루프
2. `TryRecv()`가 메시지 확인 (사용 가능하지 않으면 내부적으로 `EAGAIN`/`EWOULDBLOCK` 반환)
3. 메시지가 없으면 즉시 `false` 반환
4. 사용자 코드가 재시도 전에 `Thread.Sleep(1ms)` 호출
5. 커널 지원 없이 루프 계속

**특징**:
- **커널 레벨 대기 없음** - 모든 폴링이 사용자 공간에서 발생
- `Thread.Sleep(1ms)`가 CPU 사용량을 줄이지만 지연 오버헤드 추가 (1.3-1.7배 느림)
- **프로덕션에 권장하지 않음** 성능이 좋지 않음

#### Blocking과 Poller가 효율적인 이유

| 모드 | 대기 위치 | 깨우기 메커니즘 | CPU (유휴) | 효율성 |
|------|-----------------|----------------|------------|------------|
| **Blocking** | 커널 공간 | 커널 인터럽트 | 0% | ✓ 단일 소켓에 최적 |
| **Poller** | 커널 공간 | 커널 (epoll/kqueue) | 0% | ✓ 다중 소켓에 최적 |
| **NonBlocking** | 사용자 공간 | 없음 (지속적 폴링) | 낮음 (Sleep 1ms) | ✗ 성능 좋지 않음 |

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
| **Blocking** | 2.187 ms | 218.7 ns | 4.57M | 2.34 Gbps | 336 B | 1.00x |
| **Poller** | 2.311 ms | 231.1 ns | 4.33M | 2.22 Gbps | 456 B | 1.06x |
| NonBlocking (Sleep 1ms) | 3.783 ms | 378.3 ns | 2.64M | 1.35 Gbps | 336 B | 1.73x |

#### 512바이트 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 4.902 ms | 490.2 ns | 2.04M | 8.36 Gbps | 336 B | 1.00x |
| **Poller** | 4.718 ms | 471.8 ns | 2.12M | 8.68 Gbps | 456 B | 0.96x |
| NonBlocking (Sleep 1ms) | 6.137 ms | 613.7 ns | 1.63M | 6.67 Gbps | 336 B | 1.25x |

#### 1024바이트 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 7.541 ms | 754.1 ns | 1.33M | 10.82 Gbps | 336 B | 1.00x |
| **Poller** | 7.737 ms | 773.7 ns | 1.29M | 10.53 Gbps | 456 B | 1.03x |
| NonBlocking (Sleep 1ms) | 9.661 ms | 966.1 ns | 1.04M | 8.44 Gbps | 336 B | 1.28x |

#### 65KB 메시지

| 모드 | Mean | 지연 | Messages/sec | Data Throughput | Allocated | Ratio |
|------|------|---------|--------------|-----------------|-----------|-------|
| **Blocking** | 139.915 ms | 13.99 μs | 71.47K | 4.37 GB/s | 664 B | 1.00x |
| **Poller** | 141.733 ms | 14.17 μs | 70.56K | 4.31 GB/s | 640 B | 1.01x |
| NonBlocking (Sleep 1ms) | 260.014 ms | 26.00 μs | 38.46K | 2.35 GB/s | 1736 B | 1.86x |

### 성능 분석

**Blocking vs Poller**: 성능은 모든 메시지 크기에서 거의 동일합니다(96-106% 상대 성능). 작은 메시지(64B-1KB)의 경우 Poller가 Blocking보다 0-4% 빠릅니다. 큰 메시지(65KB)의 경우 Blocking이 Poller보다 1% 빠릅니다. 두 모드 모두 메시지가 도착할 때 스레드를 효율적으로 깨우는 커널 레벨 대기 메커니즘을 사용합니다. Poller는 폴링 인프라로 인해 약간 더 많은 메모리(10K 메시지당 456-640바이트 vs 336-664바이트)를 할당하지만, 실제로는 차이가 무시할 만합니다.

**NonBlocking 성능**: `Thread.Sleep(1ms)`를 사용한 NonBlocking 모드는 다음 이유로 Blocking 및 Poller 모드보다 일관되게 느립니다(1.25-1.86배 느림):
1. `TryRecv()`를 사용한 사용자 공간 폴링은 커널 레벨 블로킹에 비해 오버헤드 발생
2. Thread.Sleep()은 최소 1ms 슬립 간격으로도 지연 추가
3. Blocking과 Poller 모드는 메시지가 도착할 때 즉시 스레드를 깨우는 효율적인 커널 메커니즘(`recv()` 시스템 콜 및 `zmq_poll()`)을 사용

**메시지 크기 영향**: Sleep 오버헤드는 작은 메시지(64B)에서 가장 두드러지며 NonBlocking이 1.73배 느리고, 큰 메시지(65KB)의 경우 1.86배 느립니다.

**권장사항**: NonBlocking 모드는 성능이 좋지 않아(25-86% 느림) 프로덕션 사용에 권장되지 않습니다. 대부분의 시나리오에 Poller(가장 간단한 API와 최고의 전반적인 성능) 또는 단일 소켓 애플리케이션에 Blocking을 사용하세요.

### 수신 모드 선택 고려사항

수신 모드를 선택할 때 다음을 고려하세요:

**권장 접근 방식**:
- **단일 소켓**: 단순성과 최고의 성능을 위해 **Blocking** 모드 사용
- **다중 소켓**: 단일 스레드로 여러 소켓을 모니터링하려면 **Poller** 모드 사용
- 두 모드 모두 최적의 CPU 효율성(유휴 시 0%)과 낮은 지연 제공

**NonBlocking 모드 제한사항**:
- 성능이 좋지 않아(Blocking/Poller보다 1.2-2.4배 느림) **프로덕션에 권장되지 않음**
- Thread.Sleep(1ms)가 지연 오버헤드 추가
- Blocking 또는 Poller를 사용할 수 없는 기존 폴링 루프와 통합해야 하는 경우에만 NonBlocking 고려

**성능 특성**:
- Blocking과 Poller는 유사한 성능 제공(대부분의 경우 5% 이내)
- 두 모드 모두 메시지가 도착할 때 즉시 스레드를 깨우는 커널 레벨 대기 사용
- NonBlocking은 본질적으로 덜 효율적인 사용자 공간 폴링 사용

## 메모리 전략 벤치마크

### 각 전략의 작동 방식

**ByteArray (`new byte[]`)**: 각 메시지에 대해 새 바이트 배열을 할당합니다. 간단하고 직관적이지만 메시지 크기와 빈도에 비례하는 가비지 컬렉션 압력을 생성합니다.

**ArrayPool (`ArrayPool<byte>.Shared`)**: 공유 풀에서 버퍼를 대여하고 사용 후 반환합니다. 메모리를 재사용하여 GC 할당을 줄이지만, 수동 반환 관리가 필요합니다.

**Message (`zmq_msg_t`)**: libzmq의 네이티브 메시지 구조를 사용하며 내부적으로 메모리를 관리합니다. .NET 래퍼는 필요에 따라 네이티브와 관리 메모리 간에 데이터를 마샬링합니다.

**MessageZeroCopy (`Marshal.AllocHGlobal`)**: 언매니지드 메모리를 직접 할당하고 프리 콜백을 통해 libzmq에 소유권을 전달합니다. 제로카피 시맨틱을 제공하지만 신중한 라이프사이클 관리가 필요합니다.

### 메모리 벤치마크 메트릭 이해

[표준 벤치마크 메트릭](#벤치마크-메트릭-이해) 외에도 메모리 전략 벤치마크에는 다음이 포함됩니다:

| 열 | 설명 |
|--------|-------------|
| **Gen0** | 벤치마크 중 세대 0 가비지 컬렉션 수 (낮을수록 좋음) |
| **Gen1** | 세대 1 가비지 컬렉션 수 (큰 할당에만 나타남) |

**GC 영향**: 높은 Gen0/Gen1 값은 더 많은 GC 압력을 나타내며, 성능 저하 및 예측할 수 없는 지연 스파이크를 유발할 수 있습니다. 대시(-)는 컬렉션이 발생하지 않았음을 의미합니다.

### 성능 결과

모든 테스트는 수신에 Poller 모드를 사용합니다.

#### 64바이트 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 2.428 ms | 242.8 ns | 4.12M | 2.11 Gbps | - | 1.85 KB | 0.99x |
| **Message** | 4.279 ms | 427.9 ns | 2.34M | 1.20 Gbps | - | 168.54 KB | 1.76x |
| **MessageZeroCopy** | 5.917 ms | 591.7 ns | 1.69M | 0.87 Gbps | - | 168.61 KB | 2.43x |
| **ByteArray** | 2.438 ms | 243.8 ns | 4.10M | 2.10 Gbps | 9.77 | 9860.2 KB | 1.00x |

#### 512바이트 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 6.376 ms | 637.6 ns | 1.57M | 6.43 Gbps | - | 2.04 KB | 0.95x |
| **Message** | 8.187 ms | 818.7 ns | 1.22M | 5.01 Gbps | - | 168.72 KB | 1.22x |
| **ByteArray** | 6.707 ms | 670.7 ns | 1.49M | 6.11 Gbps | 48.83 | 50017.99 KB | 1.00x |
| **MessageZeroCopy** | 13.372 ms | 1.34 μs | 748K | 3.07 Gbps | - | 168.80 KB | 1.99x |

#### 1024바이트 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|-----------|-------|
| **ArrayPool** | 9.021 ms | 902.1 ns | 1.11M | 9.05 Gbps | - | 2.24 KB | 1.01x |
| **Message** | 9.739 ms | 973.9 ns | 1.03M | 8.39 Gbps | - | 168.92 KB | 1.09x |
| **ByteArray** | 8.973 ms | 897.3 ns | 1.11M | 9.10 Gbps | 97.66 | 100033.11 KB | 1.00x |
| **MessageZeroCopy** | 14.612 ms | 1.46 μs | 684K | 5.60 Gbps | - | 169.01 KB | 1.63x |

#### 65KB 메시지

| 전략 | Mean | 지연 | Messages/sec | Data Throughput | Gen0 | Gen1 | Allocated | Ratio |
|----------|------|---------|--------------|-----------------|------|------|-----------|-------|
| **Message** | 119.164 ms | 11.92 μs | 83.93K | 5.13 GB/s | - | - | 171.47 KB | 0.84x |
| **MessageZeroCopy** | 124.720 ms | 12.47 μs | 80.18K | 4.90 GB/s | - | - | 171.56 KB | 0.88x |
| **ArrayPool** | 142.814 ms | 14.28 μs | 70.02K | 4.28 GB/s | - | - | 4.78 KB | 1.01x |
| **ByteArray** | 141.652 ms | 14.17 μs | 70.60K | 4.31 GB/s | 3906.25 | 781.25 | 4001252.47 KB | 1.00x |

### 성능 및 GC 분석

**작은 메시지 (64B)**: ArrayPool이 최고의 성능(2.428 ms, 4.12M msg/sec)과 거의 제로 GC 압력(1.85 KB 할당)을 제공합니다. ByteArray는 속도가 비슷하지만(2.438 ms, 4.10M msg/sec) 상당한 GC 압력(9.77 Gen0 컬렉션, 9860.2 KB 할당)을 생성합니다. Message (4.279 ms, 1.76배 느림)와 MessageZeroCopy (5.917 ms, 2.43배 느림)는 작은 페이로드에 대해 네이티브 interop 비용이 비례적으로 높아 상당한 오버헤드를 나타냅니다.

**중간 메시지 (512B)**: ArrayPool이 가장 빠르며(6.376 ms, 1.57M msg/sec, 0.95x) 최소 할당(2.04 KB)을 보입니다. ByteArray는 약간 느리지만(6.707 ms, 1.49M msg/sec) GC 압력이 증가합니다(48.83 Gen0 컬렉션, 50 MB 할당). Message (8.187 ms, 1.22x)는 합리적으로 수행되고 MessageZeroCopy (13.372 ms, 1.99x)는 예상치 못한 오버헤드를 나타냅니다.

**중대형 메시지 (1KB)**: ArrayPool (9.021 ms, 1.11M msg/sec, 1.01x)과 ByteArray (8.973 ms, 1.11M msg/sec, 1.00x) 간 성능이 수렴하며, ByteArray는 이제 상당한 GC 압력(97.66 Gen0 컬렉션, 100 MB 할당)을 나타냅니다. Message (9.739 ms, 1.09x)가 경쟁력을 갖추고 MessageZeroCopy (14.612 ms, 1.63x)는 여전히 뒤처집니다.

**큰 메시지 (65KB)**: 네이티브 전략이 지배적 - Message가 최고의 성능(119.164 ms, 83.93K msg/sec, 0.84x = 기준선보다 16% 빠름)을 달성하고 MessageZeroCopy (124.720 ms, 80.18K msg/sec, 0.88x = 12% 빠름)가 뒤따릅니다. ArrayPool (142.814 ms, 70.02K msg/sec, 1.01x)과 ByteArray (141.652 ms, 70.60K msg/sec, 1.00x)는 속도가 비슷하지만 ByteArray는 대규모 GC 압력(3906 Gen0 + 781 Gen1 컬렉션, 4 GB 할당)을 트리거합니다.

**GC 패턴 전환**: ArrayPool과 네이티브 전략은 모든 메시지 크기에서 제로 GC 컬렉션을 유지합니다. ByteArray는 기하급수적 GC 압력 증가를 나타냅니다: 64B에서 9.77 Gen0 → 512B에서 48.83 Gen0 → 1KB에서 97.66 Gen0 → 65KB에서 3906 Gen0 + 781 Gen1.

**메모리 할당**: ArrayPool은 예외적인 효율성(모든 크기에서 1.85-4.78 KB 총 할당 - ByteArray 대비 99.8-99.99% 감소)을 보여줍니다. Message와 MessageZeroCopy는 메시지 크기에 관계없이 일관된 ~170 KB 할당 유지(65KB에서 ByteArray 대비 99.95% 감소).

### 메모리 전략 선택 고려사항

메모리 전략을 선택할 때 다음을 고려하세요:

**메시지 크기 기반 권장사항**:
- **작은 메시지 (≤512B)**: **`ArrayPool<byte>.Shared`** 사용 - 가장 빠른 성능(ByteArray보다 1-5% 빠름)과 99.8-99.99% 적은 할당
- **큰 메시지 (≥64KB)**: **`Message`** 또는 **`MessageZeroCopy`** 사용 - 12-16% 빠르고 99.95% 적은 할당
- **전환 영역 (1KB)**: ArrayPool과 Message 모두 유사하게 수행; 코드 단순성 vs GC 요구사항에 따라 선택

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

**MessageZeroCopy 사용 패턴**:
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
- GC 일시 중지에 민감한 애플리케이션은 ArrayPool(작은 메시지) 또는 MessageZeroCopy(큰 메시지)를 선호해야 함
- 드문 메시징이나 작은 메시지가 있는 애플리케이션은 ByteArray가 허용 가능할 수 있음
- 고처리량 애플리케이션은 GC 프리 전략(ArrayPool, Message, MessageZeroCopy)의 이점

**코드 복잡성**:
- **ByteArray**: 자동 메모리 관리로 가장 간단한 구현
- **ArrayPool**: 명시적 Rent/Return 호출 및 버퍼 라이프사이클 추적 필요
- **Message**: 적당한 복잡도로 네이티브 통합
- **MessageZeroCopy**: 언매니지드 메모리 관리 및 프리 콜백 필요

**성능 트레이드오프**:
- **작은 메시지 (≤ 512B)**: 관리 전략(ByteArray, ArrayPool)이 더 낮은 오버헤드 보유
- **큰 메시지 (> 512B)**: MessageZeroCopy가 제로카피 시맨틱을 통해 최적의 성능 제공
- **일관성**: GC 프리 전략(ArrayPool, MessageZeroCopy)이 더 예측 가능한 타이밍 제공

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

# Run only memory strategy benchmarks
dotnet run -c Release --filter "*MemoryStrategyBenchmarks*"

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
- NonBlocking 모드는 10ms 슬립 간격 사용; 다른 슬립 값은 다른 결과를 생성

### 결과 해석

성능 비율은 각 테스트 카테고리 내에서 1.00x가 기준선(가장 느림)인 상대 성능을 나타냅니다. 낮은 평균 시간과 높은 처리량은 더 나은 성능을 나타냅니다. 할당된 메모리와 GC 컬렉션은 메모리 관리 효율성을 나타냅니다.

벤치마크는 절대적인 "최선"의 선택이 아닌 다양한 접근 방식의 성능 특성을 반영합니다. 선택은 특정 애플리케이션 요구사항, 메시지 패턴 및 아키텍처 제약 조건에 따라 다릅니다.

## 전체 벤치마크 데이터

전체 BenchmarkDotNet 출력은 다음을 참조하세요:
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.ReceiveModeBenchmarks-report-github.md`
- `benchmarks/Net.Zmq.Benchmarks/BenchmarkDotNet.Artifacts/results/Net.Zmq.Benchmarks.Benchmarks.MemoryStrategyBenchmarks-report-github.md`
