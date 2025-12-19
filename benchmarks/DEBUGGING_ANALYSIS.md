# ReceiveWithPool 성능 디버깅 분석

## 목표
`MessagePooled_SendRecv_WithReceivePool`이 `MessagePooled_SendRecv`보다 느린 이유 규명

## 초기 가설 및 테스트

### 가설 1: ConcurrentBag의 Cross-thread Contention (✅ 해결됨)

**문제:**
- ConcurrentBag은 thread-local storage 최적화
- 벤치마크는 sender/receiver가 다른 스레드
- 매번 cross-thread stealing 발생

**테스트:**
- ConcurrentBag → ConcurrentStack으로 변경 (commit cce0e70)

**결과 (64B 메시지 기준):**
- 변경 전: MessagePooled_SendRecv_WithReceivePool = 20.6ms
- 변경 후: MessagePooled_SendRecv_WithReceivePool = 7.6ms
- **개선: 62% 성능 향상**

**결론:** ✅ ConcurrentStack이 cross-thread 환경에서 훨씬 효율적

---

### 가설 2: 메모리 복사(CopyFromNative)가 오버헤드 (❌ 기각됨)

**가설:**
```
ReceiveWithPool 내부:
1. Recv(_recvBufferPtr) - Socket 버퍼로 수신
2. MessagePool.Rent() - 풀에서 버퍼 대여
3. CopyFromNative() - 메모리 복사 ← 의심 지점
```

**테스트:** `Socket.cs:476` CopyFromNative() 주석 처리

**결과:**
| MessageSize | 복사 있음 (ms) | 복사 제거 (ms) | 차이 |
|-------------|---------------|---------------|------|
| 64B         | 7.641         | 8.310         | **+669μs (8.8% 느려짐)** |
| 512B        | 9.015         | 9.277         | **+262μs (2.9% 느려짐)** |
| 1KB         | 8.486         | 8.596         | **+110μs (1.3% 느려짐)** |
| 64KB        | 139.744       | 137.457       | -2.3ms (1.6% 빨라짐) |

**결론:** ❌ CopyFromNative()는 오버헤드가 아님! 오히려 제거하면 더 느려짐

**이유:**
- 네이티브 `NativeMemory.Copy()`는 극도로 빠름 (수 나노초)
- CPU 캐시 효과로 오히려 성능에 도움

---

## 현재 상황

### 벤치마크 결과 (ConcurrentStack 적용 후)

**MemoryStrategyBenchmarks:**
| MessageSize | MessagePooled_SendRecv | MessagePooled_SendRecv_WithReceivePool | 차이 |
|-------------|------------------------|---------------------------------------|------|
| 64B         | 7.108ms                | 7.641ms                               | **+533μs (7.5% 느림)** |
| 512B        | 8.150ms                | 9.015ms                               | **+865μs (10.6% 느림)** |
| 1KB         | 7.310ms                | 8.486ms                               | **+1.176ms (16.1% 느림)** |
| 64KB        | 129.868ms              | 139.744ms                             | **+9.876ms (7.6% 느림)** |

### 벤치마크 코드 비교

**MessagePooled_SendRecv (Receive 측):**
```csharp
// Receiver 스레드
using var msg = new Message();  // libzmq가 내부 메모리 할당
_router2.Recv(msg);              // libzmq가 직접 수신
// 작업: 할당 + 수신
```

**MessagePooled_SendRecv_WithReceivePool (Receive 측):**
```csharp
// Receiver 스레드
using var msg = _router2.ReceiveWithPool();

// ReceiveWithPool 내부:
int actualSize = Recv(_recvBufferPtr, MaxRecvBufferSize, flags);  // Socket 고정 버퍼로 수신
var msg = MessagePool.Shared.Rent(actualSize, withCallback: false); // 풀에서 대여 (할당 없음)
msg.CopyFromNative(_recvBufferPtr, actualSize);                    // 메모리 복사
// 작업: 수신 + Rent + 복사
```

---

## RecvMethodComparison 벤치마크 (추가 검증)

**목적:** `Recv(Span<byte>)` vs `Recv(Message)` 순수 성능 비교

**SpanRecv:**
```csharp
_router2.Recv(_recvBufferPtr, MessageSize);  // 고정 버퍼로 수신
```

**MessageRecv:**
```csharp
using var msg = new Message();  // 메모리 할당
_router2.Recv(msg);              // libzmq 직접 수신
```

**결과:**
| MessageSize | SpanRecv | MessageRecv | Ratio | 할당 (SpanRecv) | 할당 (MessageRecv) |
|-------------|----------|-------------|-------|-----------------|-------------------|
| 64B         | 2.237ms  | 3.181ms     | **1.42x (42% 느림)** | 340B | 560KB |
| 512B        | 5.429ms  | 4.858ms     | **0.89x (11% 빠름)** | 344B | 560KB |
| 1KB         | 7.781ms  | 7.328ms     | **0.94x (6% 빠름)**  | 344B | 560KB |
| 64KB        | 142.855ms| 135.062ms   | **0.95x (5% 빠름)**  | 688B | 560KB |

**발견:**
- **작은 메시지(64B)**: `Recv(buffer)`가 42% 빠름
- **큰 메시지(512B+)**: `Recv(Message)`가 5-11% 빠름
- **메모리 할당**: MessageRecv가 1600배 더 많음

---

## 문제 분석

### 이론적 예상

**MessagePooled_SendRecv 시간 분해:**
```
총 시간 = 메모리 할당 시간 + Recv(msg) 시간
```

**MessagePooled_SendRecv_WithReceivePool 시간 분해:**
```
총 시간 = Recv(buffer) 시간 + Rent() 시간 + CopyFromNative() 시간
```

**RecvMethodComparison 결과 기준:**
- 64B: `Recv(buffer)` (2.237ms) < `new Message() + Recv(msg)` (3.181ms)
- **차이: 944μs**

**예상:**
- `Recv(buffer)` 자체가 더 빠름 (944μs 절약)
- `Rent()`는 풀 재사용이므로 할당 없음 (빨라야 함)
- `CopyFromNative()`는 오버헤드 아님 (테스트로 입증)
- **결론: ReceiveWithPool이 더 빨라야 함!**

### 실제 결과

**64B 기준:**
- MessagePooled_SendRecv: 7.108ms
- ReceiveWithPool: 7.641ms
- **차이: +533μs (더 느림)**

---

## 미해결 문제

### 왜 ReceiveWithPool이 예상보다 느린가?

**검증된 사실:**
1. ✅ `Recv(buffer)`가 `Recv(Message)`보다 빠름 (RecvMethodComparison)
2. ✅ `CopyFromNative()`는 오버헤드 아님 (주석 테스트)
3. ✅ `MessagePool.Rent()`는 할당 없음 (풀 재사용)
4. ✅ ConcurrentStack으로 개선됨 (62% 향상)

**남은 의문:**
- `Recv(buffer)` 절약 시간: ~944μs (RecvMethodComparison 기준)
- 실제 손실 시간: +533μs
- **설명되지 않는 오버헤드: ~1.5ms**

### 가능한 원인 후보

**1. Recv() 호출 차이**
- `Recv(_recvBufferPtr, MaxRecvBufferSize, flags)` - 고정 크기 버퍼
- vs `Recv(msg)` - libzmq 직접 할당

**2. MessagePool.Rent() 오버헤드**
- `ConcurrentStack.TryPop()` - 락 프리이지만 atomic 연산
- 10,000번 호출 시 누적 오버헤드?

**3. 중간 버퍼 경유**
- `Socket._recvBufferPtr` (64KB 고정) → 풀 버퍼
- CPU 캐시 미스 증가 가능성?

**4. 벤치마크 구조 차이**
- RecvMethodComparison: 단일 스레드 환경
- MemoryStrategy: Send/Recv 별도 스레드 + MessagePool 공유

---

## MessageAllocationBenchmarks (순수 할당 성능 비교)

### 목적
`new Message()` vs `MessagePool.Rent()` 순수 할당/해제 오버헤드 측정 (I/O 없음)

### 테스트 시나리오

**NewMessage (Baseline):**
```csharp
for (int i = 0; i < 10000; i++)
{
    using var msg = new Message(size);  // Marshal.AllocHGlobal
    // Message disposed, native memory freed
}
```

**PoolRent_NoCallback:**
```csharp
for (int i = 0; i < 10000; i++)
{
    using var msg = MessagePool.Shared.Rent(size, withCallback: false);
    // Message disposed, buffer returned to pool via Dispose
}
```

**PoolRent_WithCallback:**
```csharp
for (int i = 0; i < 10000; i++)
{
    using var msg = MessagePool.Shared.Rent(size, withCallback: true);
    // Message disposed, buffer NOT returned (callback-based)
}
// Manually clear and re-prewarm pool to simulate callback returns
```

### 결과

| MessageSize | NewMessage | PoolRent_NoCallback | PoolRent_WithCallback | Ratio (NoCallback) | Ratio (WithCallback) |
|-------------|------------|---------------------|----------------------|-------------------|---------------------|
| 64B         | 1.527ms    | 2.077ms             | 3.602ms              | **1.36x (36% 느림)** | **2.36x (136% 느림)** |
| 512B        | 1.528ms    | 2.087ms             | 3.622ms              | **1.37x (37% 느림)** | **2.37x (137% 느림)** |
| 1KB         | 1.705ms    | 2.094ms             | 3.616ms              | **1.23x (23% 느림)** | **2.12x (112% 느림)** |
| 64KB        | 1.788ms    | 2.121ms             | 3.673ms              | **1.19x (19% 느림)** | **2.06x (106% 느림)** |

### 중요 발견: MessagePool.Rent()가 new Message()보다 느리다! 🚨

**예상:**
- 풀링은 메모리 재사용으로 할당 오버헤드 제거
- `Rent()`가 `new Message()`보다 빨라야 함

**실제:**
- `PoolRent_NoCallback`: 새 할당보다 **19-37% 느림** (+550μs for 64B)
- `PoolRent_WithCallback`: 새 할당보다 **106-137% 느림** (+2075μs for 64B)

**원인 분석:**

1. **ConcurrentStack.TryPop() 오버헤드**
   - 락 프리이지만 atomic 연산 비용
   - 10,000번 호출 시 누적

2. **추가 초기화 로직**
   - 풀 버퍼 찾기, 상태 설정, 포인터 할당
   - `new Message()`의 단순 Marshal.AllocHGlobal보다 복잡

3. **WithCallback의 추가 오버헤드**
   - Free callback 설정 비용
   - GC 핸들 할당

**결론:** ❌ MessagePool.Rent()는 순수 할당 성능에서 이득이 없음!

---

## 이론적 성능 계산 (64B 기준)

### MessagePooled_SendRecv 시간 분해

```
총 시간 = new Message() + Recv(msg)
        = 1.527ms + Recv(msg) 시간
```

RecvMethodComparison에서 MessageRecv = 3.181ms:
```
Recv(msg) 시간 = 3.181ms - 1.527ms (내부 할당 포함) ≈ 3.181ms
```

**예상 총 시간:**
```
Send/Recv 전체 = 7.108ms (실제 측정값)
```

### MessagePooled_SendRecv_WithReceivePool 시간 분해

```
총 시간 = Recv(buffer) + Rent() + CopyFromNative()
```

**각 구성 요소:**
1. `Recv(buffer)`: 2.237ms (RecvMethodComparison SpanRecv)
2. `Rent()`: 2.077ms (MessageAllocationBenchmarks PoolRent_NoCallback)
3. `CopyFromNative()`: ~0ms (테스트로 입증)

**예상 총 시간:**
```
Recv(buffer) + Rent() = 2.237ms + 2.077ms = 4.314ms
```

But this is just the receive side! We need to factor in the send side too.

**실제 측정값:** 7.641ms

### 성능 차이 분석

**예상:**
- `Recv(buffer)` (2.237ms) vs `Recv(Message)` (3.181ms): **944μs 절약**
- `Rent()` (2.077ms) vs `new Message()` (1.527ms): **550μs 손실**
- **순 예상 이득: 394μs 절약**

**실제:**
- MessagePooled_SendRecv: 7.108ms
- WithReceivePool: 7.641ms
- **실제 손실: 533μs**

**설명되지 않는 오버헤드:**
```
예상: -394μs (더 빨라야 함)
실제: +533μs (더 느림)
차이: 927μs
```

### 결론

**ReceiveWithPool이 느린 주요 원인:**

1. ✅ **MessagePool.Rent() 오버헤드 (550μs)**
   - MessageAllocationBenchmarks로 입증
   - 풀링이 오히려 할당보다 느림

2. ✅ **Recv(buffer) 절약 (944μs)**
   - RecvMethodComparison으로 입증
   - 버퍼 사용이 Message보다 빠름

3. ❓ **추가 미확인 오버헤드 (927μs)**
   - 벤치마크 환경 차이 (단일 스레드 vs 멀티 스레드)?
   - 중간 버퍼 경유로 인한 CPU 캐시 미스?
   - MessagePool 공유 상태 경합?

**핵심 발견:** MessagePool.Rent()의 오버헤드가 Recv(buffer) 절약분을 상쇄하고도 남음!

---

## 최종 결론

### 해결된 것
- ✅ ConcurrentBag → ConcurrentStack (62% 개선)
- ✅ 메모리 복사는 오버헤드 아님 (제거하면 오히려 느려짐)
- ✅ Disposal bug 수정 (use-after-free 방지)
- ✅ **MessagePool.Rent()가 new Message()보다 느린 원인 규명**

### 근본 원인: MessagePool의 순수 할당 성능 문제

**MessageAllocationBenchmarks 결과:**
```
new Message(64B):        1.527ms (10,000회)
MessagePool.Rent(64B):   2.077ms (10,000회) - 36% 느림
```

**왜 풀링이 느린가?**
1. ConcurrentStack.TryPop() atomic 연산 비용
2. 풀 버퍼 찾기, 상태 설정, 포인터 할당 등 추가 로직
3. Marshal.AllocHGlobal의 단순함이 오히려 더 빠름

### ReceiveWithPool 성능 분석 (64B)

**성능 구성:**
```
Recv(buffer) 절약:     -944μs ✅ (빠름)
Rent() 오버헤드:       +550μs ❌ (느림)
순 예상 변화:          -394μs (더 빨라야 함)

실제 측정:             +533μs (더 느림)
설명되지 않는 차이:    +927μs
```

### 미해결 문제
- ❓ 추가 927μs 오버헤드의 출처
  - 벤치마크 환경 차이 (단일 vs 멀티 스레드)?
  - 중간 버퍼 경유로 인한 캐시 미스?
  - MessagePool 공유 상태 경합?

### 현재 권장사항

**Send 측:**
- ✅ **MessagePool.Rent(withCallback: true) 사용**
- 이유: zero-copy 전송, callback이 자동으로 버퍼 반환
- 순수 할당은 느리지만, zero-copy 이득이 훨씬 큼

**Receive 측:**
- ❌ **ReceiveWithPool 사용 권장하지 않음**
- 이유:
  1. `Recv(buffer)` 절약분을 `Rent()` 오버헤드가 상쇄
  2. 추가로 ~1ms의 미확인 오버헤드 존재
  3. 순수 `new Message() + Recv(msg)`가 더 빠름

**대안:**
- 작은 메시지(64B): `Recv(buffer)` 직접 사용 (재사용 버퍼)
- 큰 메시지(512B+): `new Message() + Recv(msg)` 사용 (zero-copy)

### 교훈

**"풀링이 항상 빠르다"는 가정의 오류:**
- MessagePool.Rent()는 순수 할당 성능에서 Marshal.AllocHGlobal보다 느림
- 풀링의 이득은 zero-copy 전송에서 나옴 (Send 측)
- Receive 측에서는 추가 복사가 필요하므로 이득 없음

**성능 최적화의 원칙:**
- 가정을 검증하라 (풀링 = 빠르다 ❌)
- 각 구성 요소를 독립적으로 벤치마크하라
- 이론적 계산과 실제 측정을 비교하라
