[![English](https://img.shields.io/badge/lang:en-red.svg)](advanced-topics.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](advanced-topics.ko.md)

# 고급 주제

이 가이드는 성능 최적화, 모범 사례, 보안, 문제 해결을 포함한 Net.Zmq의 고급 주제를 다룹니다.

## 성능 최적화

Net.Zmq는 뛰어난 성능을 제공하지만, 최적의 결과를 달성하려면 적절한 구성이 필수적입니다.

### 성능 지표

Net.Zmq가 달성한 성능:

- **최대 처리량 (Peak Throughput)**: 4.95M 메시지/초 (PUSH/PULL, 64B)
- **초저지연 (Ultra-Low Latency)**: 메시지당 202ns
- **메모리 효율성 (Memory Efficient)**: 10K 메시지당 441B 할당

자세한 성능 지표는 [BENCHMARKS.md](https://github.com/ulala-x/net-zmq/blob/main/BENCHMARKS.md)를 참조하세요.

### 수신 모드

Net.Zmq는 각기 다른 성능 특성과 사용 사례를 가진 세 가지 수신 모드를 제공합니다.

#### 각 모드의 작동 방식

**Blocking 모드**: 호출 스레드는 메시지가 도착할 때까지 `Recv()`에서 블로킹됩니다. 스레드는 대기하는 동안 운영 체제 스케줄러에게 양보하여 최소한의 CPU 리소스를 소비합니다. 이는 결정적인 대기 동작을 가진 가장 간단한 접근 방식입니다.

**NonBlocking 모드**: 애플리케이션은 메시지를 폴링하기 위해 `TryRecv()`를 반복적으로 호출합니다. 메시지가 즉시 사용 가능하지 않을 때 스레드는 일반적으로 재시도하기 전에 짧은 간격(예: 10ms) 동안 슬립합니다. 이는 스레드 블로킹을 방지하지만 슬립 간격으로 인해 지연이 발생합니다.

**Poller 모드**: 내부적으로 `zmq_poll()`을 사용하는 이벤트 기반 수신입니다. 애플리케이션은 바쁜 대기나 개별 소켓 블로킹 없이 소켓 이벤트를 기다립니다. 이 모드는 단일 스레드로 여러 소켓을 효율적으로 처리하고 반응적인 이벤트 알림을 제공합니다.

#### 사용 예제

Blocking 모드는 가장 간단한 구현을 제공합니다:

```csharp
using var context = new Context();
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Blocks until message arrives
var buffer = new byte[1024];
int size = socket.Recv(buffer);
ProcessMessage(buffer.AsSpan(0, size));
```

NonBlocking 모드는 폴링 루프와 통합됩니다:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

var buffer = new byte[1024];
while (running)
{
    if (socket.TryRecv(buffer, out int size))
    {
        ProcessMessage(buffer.AsSpan(0, size));
    }
    else
    {
        Thread.Sleep(10); // Wait before retry
    }
}
```

Poller 모드는 여러 소켓을 지원합니다:

```csharp
using var socket1 = new Socket(context, SocketType.Pull);
using var socket2 = new Socket(context, SocketType.Pull);
socket1.Connect("tcp://localhost:5555");
socket2.Connect("tcp://localhost:5556");

using var poller = new Poller(2);
poller.Add(socket1, PollEvents.In);
poller.Add(socket2, PollEvents.In);

var buffer = new byte[1024];
while (running)
{
    int eventCount = poller.Poll(1000); // 1 second timeout

    if (eventCount > 0)
    {
        if (socket1.TryRecv(buffer, out int size))
        {
            ProcessMessage1(buffer.AsSpan(0, size));
        }

        if (socket2.TryRecv(buffer, out size))
        {
            ProcessMessage2(buffer.AsSpan(0, size));
        }
    }
}
```

#### 성능 특성

동시 송신자 및 수신자를 사용한 ROUTER-to-ROUTER 패턴에서 벤치마크 (10,000 메시지, Intel Core Ultra 7 265K):

**64바이트 메시지**:
- Blocking: 2.187 ms (4.57M msg/sec, 218.7 ns 지연)
- Poller: 2.311 ms (4.33M msg/sec, 231.1 ns 지연)
- NonBlocking: 3.783 ms (2.64M msg/sec, 378.3 ns 지연)

**512바이트 메시지**:
- Poller: 4.718 ms (2.12M msg/sec, 471.8 ns 지연)
- Blocking: 4.902 ms (2.04M msg/sec, 490.2 ns 지연)
- NonBlocking: 6.137 ms (1.63M msg/sec, 613.7 ns 지연)

**1024바이트 메시지**:
- Blocking: 7.541 ms (1.33M msg/sec, 754.1 ns 지연)
- Poller: 7.737 ms (1.29M msg/sec, 773.7 ns 지연)
- NonBlocking: 9.661 ms (1.04M msg/sec, 966.1 ns 지연)

**65KB 메시지**:
- Blocking: 139.915 ms (71.47K msg/sec, 13.99 μs 지연)
- Poller: 141.733 ms (70.56K msg/sec, 14.17 μs 지연)
- NonBlocking: 260.014 ms (38.46K msg/sec, 26.00 μs 지연)

Blocking과 Poller 모드는 모든 메시지 크기에서 거의 동일한 성능(96-106% 상대 성능)을 제공합니다. Poller는 폴링 인프라를 위해 약간 더 많은 메모리(10K 메시지당 456-640바이트 vs 336-664바이트)를 할당하지만, 실제로는 차이가 무시할 만합니다. NonBlocking 모드는 메시지가 즉시 사용 가능하지 않을 때 슬립 오버헤드로 인해 일관되게 느립니다(1.25-1.86배 느림).

#### 선택 고려사항

**단일 소켓 애플리케이션**:
- 스레드 블로킹이 허용되는 경우 Blocking 모드는 간단한 구현 제공
- Poller 모드는 유사한 성능으로 이벤트 기반 아키텍처 제공
- NonBlocking 모드는 기존 폴링 루프와의 통합 가능

**다중 소켓 애플리케이션**:
- Poller 모드는 단일 스레드로 여러 소켓 모니터링
- Blocking 모드는 소켓당 하나의 스레드 필요
- NonBlocking 모드는 더 높은 지연으로 여러 소켓 서비스 가능

**지연 요구사항**:
- Blocking과 Poller 모드는 서브 마이크로초 지연 달성 (64바이트 메시지에 대해 218-231 ns)
- NonBlocking 모드는 슬립 간격으로 인한 오버헤드 추가 (64바이트 메시지에 대해 378 ns)

**스레드 관리**:
- Blocking 모드는 소켓에 스레드를 전담
- Poller 모드는 하나의 스레드가 여러 소켓을 서비스하도록 허용
- NonBlocking 모드는 애플리케이션 이벤트 루프와 통합

### 메시지 버퍼 전략

Net.Zmq는 송수신 작업을 위한 여러 메시지 버퍼 관리 전략을 지원하며, 각각 다른 성능 및 가비지 컬렉션 특성을 가집니다.

#### 각 전략의 작동 방식

**ByteArray**: 각 메시지에 대해 새 바이트 배열(`new byte[]`)을 할당합니다. 간단하고 자동적인 메모리 관리를 제공하지만 메시지 크기와 빈도에 비례하는 가비지 컬렉션 압력을 생성합니다.

**ArrayPool**: `ArrayPool<byte>.Shared`에서 버퍼를 대여하고 사용 후 반환합니다. 공유 풀에서 메모리를 재사용하여 GC 할당을 줄이지만, 수동 대여/반환 라이프사이클 관리가 필요합니다.

**Message**: libzmq의 네이티브 메시지 구조(`zmq_msg_t`)를 사용하며 내부적으로 메모리를 관리합니다. .NET 래퍼는 필요에 따라 네이티브와 관리 메모리 간에 데이터를 마샬링합니다. 이 접근 방식은 네이티브 메모리 관리를 활용합니다.

**MessageZeroCopy**: 언매니지드 메모리를 직접 할당(`Marshal.AllocHGlobal`)하고 프리 콜백을 통해 libzmq에 소유권을 전달합니다. 관리 메모리를 완전히 피함으로써 진정한 제로카피 시맨틱을 제공하지만, 신중한 라이프사이클 관리가 필요합니다.

**MessagePool**: cppzmq에는 없는 Net.Zmq만의 독점 기능으로, 네이티브 메모리 버퍼를 풀링하고 재사용하여 GC 압력을 제거하고 고성능 메시징을 가능하게 합니다. 수동 `Return()` 호출이 필요한 ArrayPool과 달리, MessagePool은 전송이 완료되면 ZeroMQ의 프리 콜백을 통해 버퍼를 자동으로 풀에 반환합니다.

#### 사용 예제

ByteArray 접근 방식은 표준 .NET 배열을 사용합니다:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Allocate new buffer for each receive
var buffer = new byte[1024];
int size = socket.Recv(buffer);

// Create output buffer for external delivery
var output = new byte[size];
buffer.AsSpan(0, size).CopyTo(output);
DeliverMessage(output);
```

ArrayPool 접근 방식은 버퍼를 재사용합니다:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Receive into fixed buffer
var recvBuffer = new byte[1024];
int size = socket.Recv(recvBuffer);

// Rent buffer from pool for external delivery
var output = ArrayPool<byte>.Shared.Rent(size);
try
{
    recvBuffer.AsSpan(0, size).CopyTo(output);
    DeliverMessage(output.AsSpan(0, size));
}
finally
{
    ArrayPool<byte>.Shared.Return(output);
}
```

Message 접근 방식은 네이티브 메모리를 사용합니다:

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// Receive into native message
using var message = new Message();
socket.Recv(message);

// Access data directly without copying
ProcessMessage(message.Data); // ReadOnlySpan<byte>
```

MessageZeroCopy 전송 접근 방식:

```csharp
using var socket = new Socket(context, SocketType.Push);
socket.Connect("tcp://localhost:5555");

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

MessagePool 전송 접근 방식:

```csharp
using var socket = new Socket(context, SocketType.Push);
socket.Connect("tcp://localhost:5555");

// 기본 사용법: 지정된 크기의 버퍼 대여
var msg = MessagePool.Shared.Rent(1024);
socket.Send(msg);  // 전송 완료 시 자동으로 풀에 반환됨

// 데이터와 함께 대여: 데이터를 풀링된 버퍼에 복사
var data = new byte[1024];
var msg = MessagePool.Shared.Rent(data);
socket.Send(msg);

// 예측 가능한 성능을 위한 풀 프리워밍
MessagePool.Shared.Prewarm(MessageSize.K1, 500);  // 500개의 1KB 버퍼를 미리 할당
```

MessagePool 수신 접근 방식 (크기 선행 프로토콜):

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// 송신자 측: 메시지에 크기를 접두사로 붙임
var payload = new byte[8192];
socket.Send(BitConverter.GetBytes(payload.Length), SendFlags.SendMore);
socket.Send(payload);

// 수신자 측: 먼저 크기를 읽은 다음 풀링된 버퍼로 수신
var sizeBuffer = new byte[4];
socket.Recv(sizeBuffer);
int expectedSize = BitConverter.ToInt32(sizeBuffer);

var msg = MessagePool.Shared.Rent(expectedSize);
socket.Recv(msg, expectedSize);  // 알려진 크기로 수신
ProcessMessage(msg.Data);
```

MessagePool 수신 접근 방식 (고정 크기 메시지):

```csharp
using var socket = new Socket(context, SocketType.Pull);
socket.Connect("tcp://localhost:5555");

// 메시지 크기가 고정되어 있고 알려져 있을 때
const int FixedMessageSize = 1024;
var msg = MessagePool.Shared.Rent(FixedMessageSize);
socket.Recv(msg, FixedMessageSize);
ProcessMessage(msg.Data);
```

#### 성능 및 GC 특성

Poller 모드를 사용한 ROUTER-to-ROUTER 패턴에서 벤치마크 (10,000 메시지, Intel Core Ultra 7 265K):

**64바이트 메시지**:
- ArrayPool: 2.428 ms (4.12M msg/sec), 0 GC, 1.85 KB 할당
- ByteArray: 2.438 ms (4.10M msg/sec), 9.77 Gen0, 9860.2 KB 할당
- Message: 4.279 ms (2.34M msg/sec), 0 GC, 168.54 KB 할당
- MessageZeroCopy: 5.917 ms (1.69M msg/sec), 0 GC, 168.61 KB 할당

**512바이트 메시지**:
- ArrayPool: 6.376 ms (1.57M msg/sec), 0 GC, 2.04 KB 할당
- ByteArray: 6.707 ms (1.49M msg/sec), 48.83 Gen0, 50017.99 KB 할당
- Message: 8.187 ms (1.22M msg/sec), 0 GC, 168.72 KB 할당
- MessageZeroCopy: 13.372 ms (748K msg/sec), 0 GC, 168.80 KB 할당

**1024바이트 메시지**:
- ArrayPool: 9.021 ms (1.11M msg/sec), 0 GC, 2.24 KB 할당
- ByteArray: 8.973 ms (1.11M msg/sec), 97.66 Gen0, 100033.11 KB 할당
- Message: 9.739 ms (1.03M msg/sec), 0 GC, 168.92 KB 할당
- MessageZeroCopy: 14.612 ms (684K msg/sec), 0 GC, 169.01 KB 할당

**65KB 메시지**:
- Message: 119.164 ms (83.93K msg/sec), 0 GC, 171.47 KB 할당
- MessageZeroCopy: 124.720 ms (80.18K msg/sec), 0 GC, 171.56 KB 할당
- ArrayPool: 142.814 ms (70.02K msg/sec), 0 GC, 4.78 KB 할당
- ByteArray: 141.652 ms (70.60K msg/sec), 3906 Gen0 + 781 Gen1, 4001252.47 KB 할당

#### 메시지 크기별 GC 압력

벤치마크 데이터에서 최소에서 심각한 GC 압력으로의 전환이 명확하게 보입니다:

- **64B**: ByteArray는 9.77 Gen0 컬렉션 표시 (관리 가능)
- **512B**: ByteArray는 48.83 Gen0 컬렉션 표시 (증가하는 압력)
- **1KB**: ByteArray는 97.66 Gen0 컬렉션 표시 (상당한 압력)
- **65KB**: ByteArray는 3906 Gen0 + 781 Gen1 컬렉션 표시 (심각한 압력)

ArrayPool, Message, MessageZeroCopy는 메시지 크기에 관계없이 제로 GC 컬렉션을 유지하여 GC에 민감한 애플리케이션에 대한 효과를 보여줍니다.

#### MessagePool: Net.Zmq 독점 기능

MessagePool은 cppzmq나 다른 ZeroMQ 바인딩에는 없는 Net.Zmq만의 고유 기능으로, 고성능 GC 프리 메시징을 위한 풀링된 네이티브 메모리 버퍼를 제공합니다.

**아키텍처: 2단계 캐싱 시스템**

MessagePool은 최적의 성능을 위해 정교한 2단계 캐싱 아키텍처를 사용합니다:

- **1단계 - 스레드 로컬 캐시**: 각 스레드는 버킷당 8개의 버퍼를 가진 락 프리 캐시를 유지하여 락 경합을 최소화
- **2단계 - 공유 풀**: 스레드 로컬 캐시가 소진되었을 때 전역 풀 역할을 하는 스레드 안전 `ConcurrentStack`
- **19개 크기 버킷**: 16바이트에서 4MB까지 2의 거듭제곱 크기로 구성된 버퍼로 효율적인 할당

**주요 장점**

1. **제로 GC 압력**: 네이티브 메모리 버퍼를 재사용하여 Gen0/Gen1/Gen2 컬렉션 제거
2. **자동 반환**: 수동 `Return()` 호출이 필요한 ArrayPool과 달리, MessagePool은 전송 완료 시 ZeroMQ의 프리 콜백을 통해 자동으로 버퍼 반환
3. **완벽한 ZeroMQ 통합**: ZeroMQ의 제로카피 아키텍처와 완벽하게 통합
4. **락 프리 고속 경로**: 스레드 로컬 캐시는 고처리량 시나리오에서 락 프리 액세스 제공
5. **뛰어난 성능**: 1KB 메시지에서 ByteArray보다 12% 빠르며, 128KB 메시지에서 3.4배 빠름

**성능 비교**

PUSH-to-PULL 패턴에서 벤치마크 (10,000 메시지, Intel Core Ultra 7 265K):

| 메시지 크기 | MessagePool | ByteArray | ArrayPool | ByteArray 대비 속도 향상 |
|-------------|-------------|-----------|-----------|------------------------|
| 64B         | 1.881 ms    | 1.775 ms  | 1.793 ms  | 0.95x (5% 느림)        |
| 1KB         | 5.314 ms    | 6.048 ms  | 5.361 ms  | 1.14x (12% 빠름)       |
| 128KB       | 342.125 ms  | 1159.675 ms | 367.083 ms | 3.39x (239% 빠름)    |
| 256KB       | 708.083 ms  | 2399.208 ms | 719.708 ms | 3.39x (239% 빠름)    |

**GC 컬렉션 (10,000 메시지)**

| 메시지 크기 | MessagePool | ByteArray | ArrayPool |
|-------------|-------------|-----------|-----------|
| 64B         | 0 GC        | 10 Gen0   | 0 GC      |
| 1KB         | 0 GC        | 98 Gen0   | 0 GC      |
| 128KB       | 0 GC        | 9766 Gen0 + 9765 Gen1 + 7 Gen2 | 0 GC |
| 256KB       | 0 GC        | 19531 Gen0 + 19530 Gen1 + 13 Gen2 | 0 GC |

**트레이드오프 및 제약사항**

*메모리 관련:*
- 네이티브 메모리 점유: 풀링된 버퍼는 관리되는 힙이 아닌 네이티브 메모리에 상주
- 잠재적인 메모리 오버헤드: 버킷당 최대 `MaxBuffers × BucketSize`만큼의 네이티브 메모리 점유 가능
- GC 대상이 아니지만 프로세스 메모리에 포함됨

*성능 특성:*
- 작은 메시지 (64B): 콜백 오버헤드로 인해 ByteArray보다 약간 느림 (~5%)
- 중간 메시지 (1KB-128KB): ByteArray보다 크게 빠름 (12-239%)
- 대부분의 크기에서 ArrayPool과 비슷하지만, 자동 반환이라는 추가 이점 제공

*수신 제약사항 (중요):*
- **수신 작업 시 메시지 크기를 미리 알아야 함**
- 수신 전에 적절한 크기의 버퍼를 대여해야 함
- 두 가지 일반적인 해결 방법:
  1. **크기 선행 프로토콜**: 선행 프레임에서 메시지 크기 전송
  2. **고정 크기 메시지**: 미리 정의된 상수 메시지 크기 사용
- 프로토콜 지원 없이 동적 크기 메시지 수신에는 부적합

**MessagePool 사용 시기**

**권장: 가능한 모든 경우에 MessagePool 사용**

MessagePool은 제로 GC 압력, 자동 버퍼 반환, 높은 성능으로 인해 대부분의 시나리오에서 기본 선택이어야 합니다.

**전송 시:**
- 전송할 때는 항상 MessagePool 사용 - 더 나은 성능과 제로 GC 압력 제공
- 중대형 메시지(>1KB)에서 특히 유리
- 작은 메시지(64B)에서도 5% 오버헤드는 GC 이점에 비해 무시할 수 있음

**수신 시:**
- 메시지 크기를 알 수 있을 때 MessagePool 사용:
  - **크기 선행 프로토콜**: `[size][payload]`를 멀티파트 메시지로 전송
  - **고정 크기 메시지**: 상수 메시지 크기 사용
- 크기를 알 수 없고 예측할 수 없을 때만 `Message`로 폴백

**실제로는 크기를 거의 항상 알 수 있음:**
- ZeroMQ 멀티파트 메시지는 크기 선행을 쉽게 만듦: `socket.Send(size, SendFlags.SendMore); socket.Send(payload);`
- 4바이트 크기 접두사 전송 오버헤드는 GC 절감에 비해 미미함
- 대부분의 실제 프로토콜은 이미 메시지 프레이밍이나 크기 정보를 포함

**대안: 크기를 정말 알 수 없을 때**
- 사전에 크기를 알 수 없는 경우 수신에 `Message` 사용
- ArrayPool 벤치마크조차 고정 길이 메시지를 가정함
- 고정 버퍼로 읽고 복사하는 방식은 복사 오버헤드로 인해 더 느림
- 실제로 이러한 시나리오는 드뭄 - 대부분의 프로토콜은 크기 표시를 지원

**사용 모범 사례**

```csharp
// 권장: 가변 크기 메시지를 위한 크기 선행 프로토콜
// 송신자
var payload = GeneratePayload();
socket.Send(BitConverter.GetBytes(payload.Length), SendFlags.SendMore);
socket.Send(MessagePool.Shared.Rent(payload));

// 수신자
var sizeBuffer = new byte[4];
socket.Recv(sizeBuffer);
int size = BitConverter.ToInt32(sizeBuffer);
var msg = MessagePool.Shared.Rent(size);
socket.Recv(msg, size);

// 권장: 고정 크기 메시지
const int MessageSize = 1024;
var msg = MessagePool.Shared.Rent(MessageSize);
socket.Send(msg);

// 선택사항: 일관된 성능을 위한 풀 프리워밍
MessagePool.Shared.Prewarm(MessageSize.K1, 100);  // 100개의 1KB 버퍼 미리 할당
```

#### 선택 고려사항

**빠른 참조: 각 전략을 언제 사용할지**

대부분의 애플리케이션은 이 결정 트리를 따르세요:

1. **기본 선택: MessagePool**
   - 메시지 크기를 알 수 있거나 전달할 수 있는 모든 시나리오에서 사용
   - 제로 GC, 자동 반환, 최고의 전체 성능 제공
   - 수신에는 크기 선행 프로토콜 또는 고정 크기 메시지 필요

2. **크기를 알 수 없을 때: Message**
   - 메시지 크기를 사전에 결정할 수 없을 때만 사용
   - 제로 GC이지만 중대형 메시지에서 MessagePool보다 느림

3. **레거시 또는 간단한 코드: ByteArray**
   - GC 압력이 허용되고 단순성이 중요할 때만 사용
   - 고처리량 또는 대용량 메시지 시나리오에서는 피해야 함

4. **수동 제어: ArrayPool**
   - 버퍼 라이프사이클에 대한 명시적 제어가 필요할 때 사용
   - 수동 Return() 호출 필요
   - 자동 반환 때문에 일반적으로 MessagePool이 선호됨

5. **고급 제로카피: MessageZeroCopy**
   - 맞춤형 메모리 관리 시나리오에서 사용
   - 대부분의 사용 사례는 MessagePool로 더 잘 처리됨

**상세 고려사항**

**메시지 크기 분포**:
- **작은 메시지 (≤64B)**: ArrayPool/ByteArray가 약간 유리 (~5%) 하지만 콜백 오버헤드가 낮지만 MessagePool의 GC 이점이 이를 압도
- **중간 메시지 (1KB-64KB)**: MessagePool이 가장 빠름 (ByteArray보다 12% 빠름), 제로 GC
- **큰 메시지 (≥128KB)**: MessagePool이 압도적 (ByteArray보다 3.4배 빠름), 제로 GC
- ByteArray는 메시지 크기가 증가함에 따라 기하급수적으로 증가하는 GC 압력 생성
- MessagePool, ArrayPool, Message는 메시지 크기에 관계없이 제로 GC 압력 유지

**GC 민감도**:
- GC 일시 중지에 민감한 애플리케이션은 MessagePool, ArrayPool, Message 또는 MessageZeroCopy 사용해야 함
- 자동 버퍼 관리를 위해 MessagePool 선호
- 가변 메시지 크기의 고처리량 애플리케이션은 MessagePool에서 가장 큰 이점
- ByteArray는 드문 메시징이 있거나 GC 압력이 허용 가능한 애플리케이션에서만 허용

**코드 복잡성**:
- ByteArray: 자동 메모리 관리로 가장 간단한 구현
- **MessagePool: 자동 반환이 있는 간단한 API** - 대부분의 사용 사례에 권장
- ArrayPool: 명시적 Rent/Return 호출 및 버퍼 라이프사이클 추적 필요
- Message: 적당한 복잡도로 네이티브 통합
- MessageZeroCopy: 언매니지드 메모리 관리 및 프리 콜백 필요

**프로토콜 요구사항**:
- **MessagePool 수신은 메시지 크기를 알아야 함:**
  - 크기 선행 프로토콜 사용: `Send([size][payload])`
  - 또는 고정 크기 메시지 사용
  - 대부분의 실제 프로토콜은 이미 이를 지원
- 다른 전략은 수신에 이 제약이 없음

**성능 요구사항**:
- **최고의 전체 성능: MessagePool** (크기를 알 수 있을 때)
  - 1KB 메시지에서 12% 빠르고, 128KB 메시지에서 ByteArray보다 3.4배 빠름
  - 제로 GC 압력
  - 자동 버퍼 반환
- 크기를 알 수 없을 때: Message가 적당한 성능으로 제로 GC 제공
- ByteArray는 단순성이 가장 중요하고 GC 압력이 허용 가능한 경우에만 적합

### I/O 스레드

워크로드에 따라 I/O 스레드를 구성하세요:

```csharp
// Default: 1 I/O thread (suitable for most applications)
using var context = new Context();

// High-throughput: 2-4 I/O threads
using var context = new Context(ioThreads: 4);

// Rule of thumb: 1 thread per 4 CPU cores
var cores = Environment.ProcessorCount;
var threads = Math.Max(1, cores / 4);
using var context = new Context(ioThreads: threads);
```

**가이드라인**:
- 1 스레드: 대부분의 애플리케이션에 충분
- 2-4 스레드: 고처리량 애플리케이션
- 더 많은 스레드: 프로파일링에서 I/O 병목 현상이 나타나는 경우에만

### 고수위 마크 (High Water Marks, HWM)

고수위 마크로 메시지 큐잉을 제어하세요:

```csharp
using var socket = new Socket(context, SocketType.Pub);

// Set send high water mark (default: 1000)
socket.SetOption(SocketOption.Sndhwm, 10000);

// Set receive high water mark
socket.SetOption(SocketOption.Rcvhwm, 10000);

// For low-latency, use smaller HWM
socket.SetOption(SocketOption.Sndhwm, 100);
```

**영향**:
- 높은 HWM: 더 많은 메모리, 더 나은 버스트 처리
- 낮은 HWM: 더 적은 메모리, 더 빠른 배압
- 기본값 (1000): 대부분의 경우 좋은 균형

### 메시지 배치 처리

높은 처리량을 위해 메시지를 배치로 전송하세요:

```csharp
using var socket = new Socket(context, SocketType.Push);
socket.Connect("tcp://localhost:5555");

// Batch sending
for (int i = 0; i < 10000; i++)
{
    socket.Send($"Message {i}", SendFlags.DontWait);
}

// Or use multi-part for logical batches
for (int batch = 0; batch < 100; batch++)
{
    for (int i = 0; i < 99; i++)
    {
        socket.Send($"Item {i}", SendFlags.SendMore);
    }
    socket.Send("Last item"); // Final frame
}
```

### 버퍼 크기

처리량을 위해 커널 소켓 버퍼를 조정하세요:

```csharp
using var socket = new Socket(context, SocketType.Push);

// Increase send buffer (default: OS-dependent)
socket.SetOption(SocketOption.Sndbuf, 256 * 1024); // 256KB

// Increase receive buffer
socket.SetOption(SocketOption.Rcvbuf, 256 * 1024);

// For ultra-high throughput
socket.SetOption(SocketOption.Sndbuf, 1024 * 1024); // 1MB
socket.SetOption(SocketOption.Rcvbuf, 1024 * 1024);
```

### Linger 시간

소켓 종료 동작을 구성하세요:

```csharp
using var socket = new Socket(context, SocketType.Push);

// Wait up to 1 second for messages to send on close
socket.SetOption(SocketOption.Linger, 1000);

// Discard pending messages immediately (not recommended)
socket.SetOption(SocketOption.Linger, 0);

// Wait indefinitely (default: -1)
socket.SetOption(SocketOption.Linger, -1);
```

**권장사항**:
- 개발: 0 (빠른 종료)
- 프로덕션: 1000-5000 (우아한 종료)
- 중요 데이터: -1 (모든 메시지 대기)

### 메시지 크기 최적화

적절한 메시지 크기를 선택하세요:

```csharp
// Small messages (< 1KB): Best throughput
socket.Send("Small payload");

// Medium messages (1KB - 64KB): Good balance
var data = new byte[8192]; // 8KB
socket.Send(data);

// Large messages (> 64KB): Lower throughput but efficient
var largeData = new byte[1024 * 1024]; // 1MB
socket.Send(largeData);
```

**크기별 성능**:
- 64B: 4.95M msg/sec
- 1KB: 1.36M msg/sec
- 64KB: 73K msg/sec

### 제로카피 작업

제로카피를 위해 Message API 사용:

```csharp
// Traditional: Creates copy
var data = socket.RecvBytes();
ProcessData(data);

// Zero-copy: No allocation
using var message = new Message();
socket.Recv(ref message, RecvFlags.None);
ProcessData(message.Data); // ReadOnlySpan<byte>
```

### 전송 선택

사용 사례에 맞는 전송을 선택하세요:

| 전송 | 성능 | 사용 사례 |
|-----------|-------------|----------|
| `inproc://` | 가장 빠름 | 동일 프로세스, 스레드 간 |
| `ipc://` | 빠름 | 동일 머신, 프로세스 간 |
| `tcp://` | 좋음 | 네트워크 통신 |
| `pgm://` | 가변 | 신뢰할 수 있는 멀티캐스트 |

```csharp
// Fastest: inproc (memory copy only)
socket.Bind("inproc://fast-queue");

// Fast: IPC (Unix domain socket)
socket.Bind("ipc:///tmp/my-socket");

// Network: TCP
socket.Bind("tcp://*:5555");
```

## 모범 사례

### 컨텍스트 관리

```csharp
// ✅ 올바름: 애플리케이션당 하나의 컨텍스트
using var context = new Context();
using var socket1 = new Socket(context, SocketType.Req);
using var socket2 = new Socket(context, SocketType.Rep);

// ❌ 잘못됨: 여러 컨텍스트
using var context1 = new Context();
using var context2 = new Context(); // 낭비
```

### 소켓 라이프사이클

```csharp
// ✅ 올바름: 항상 'using' 사용
using var socket = new Socket(context, SocketType.Rep);
socket.Bind("tcp://*:5555");
// Socket automatically disposed

// ❌ 잘못됨: 폐기 누락
var socket = new Socket(context, SocketType.Rep);
socket.Bind("tcp://*:5555");
// Resource leak!

// ✅ 올바름: 수동 폐기
var socket = new Socket(context, SocketType.Rep);
try
{
    socket.Bind("tcp://*:5555");
    // Use socket...
}
finally
{
    socket.Dispose();
}
```

### 오류 처리

```csharp
// ✅ 올바름: 포괄적인 오류 처리
try
{
    using var socket = new Socket(context, SocketType.Rep);
    socket.Bind("tcp://*:5555");

    while (true)
    {
        try
        {
            var msg = socket.RecvString();
            socket.Send(ProcessMessage(msg));
        }
        catch (ZmqException ex) when (ex.ErrorCode == ErrorCode.EAGAIN)
        {
            // Timeout, continue
            continue;
        }
    }
}
catch (ZmqException ex)
{
    Console.WriteLine($"ZMQ Error: {ex.ErrorCode} - {ex.Message}");
}

// ❌ 잘못됨: 모든 예외 삼키기
try
{
    var msg = socket.RecvString();
}
catch
{
    // Silent failure - bad!
}
```

### Bind vs Connect

```csharp
// ✅ 올바름: 안정적인 엔드포인트는 bind, 동적 엔드포인트는 connect
// Server (stable)
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

// Clients (dynamic)
using var client1 = new Socket(context, SocketType.Req);
client1.Connect("tcp://server:5555");

// ✅ 올바름: 동적 확장 허용
// Broker binds (stable)
broker.Bind("tcp://*:5555");

// Workers connect (can scale up/down)
worker1.Connect("tcp://broker:5555");
worker2.Connect("tcp://broker:5555");
```

### 패턴별 관행

#### REQ-REP

```csharp
// ✅ 올바름: 엄격한 송수신 순서
client.Send("Request");
var reply = client.RecvString();

// ❌ 잘못됨: 순서 잘못됨
client.Send("Request 1");
client.Send("Request 2"); // Error! Must receive first
```

#### PUB-SUB

```csharp
// ✅ 올바름: 느린 합류자 처리
publisher.Bind("tcp://*:5556");
Thread.Sleep(100); // Allow subscribers to connect

// ✅ 올바름: 항상 구독
subscriber.Subscribe("topic");
var msg = subscriber.RecvString();

// ❌ 잘못됨: 구독 누락
var msg = subscriber.RecvString(); // Will never receive!
```

#### PUSH-PULL

```csharp
// ✅ 올바름: 생산자 bind, 워커 connect
producer.Bind("tcp://*:5557");
worker.Connect("tcp://localhost:5557");

// ✅ 올바름: 워커는 동적으로 확장 가능
worker1.Connect("tcp://localhost:5557");
worker2.Connect("tcp://localhost:5557");
```

## 스레딩 및 동시성

### 스레드 안전성

ZeroMQ 소켓은 **스레드 안전하지 않습니다**. 각 소켓은 하나의 스레드에서만 사용해야 합니다.

```csharp
// ❌ 잘못됨: 스레드 간 소켓 공유
using var socket = new Socket(context, SocketType.Push);

var thread1 = new Thread(() => socket.Send("From thread 1"));
var thread2 = new Thread(() => socket.Send("From thread 2"));
// RACE CONDITION!

// ✅ 올바름: 스레드당 하나의 소켓
var thread1 = new Thread(() =>
{
    using var socket = new Socket(context, SocketType.Push);
    socket.Connect("tcp://localhost:5555");
    socket.Send("From thread 1");
});

var thread2 = new Thread(() =>
{
    using var socket = new Socket(context, SocketType.Push);
    socket.Connect("tcp://localhost:5555");
    socket.Send("From thread 2");
});
```

### 스레드 간 통신

스레드 조정을 위해 inproc://와 PAIR 소켓 사용:

```csharp
using var context = new Context();

var thread1 = new Thread(() =>
{
    using var socket = new Socket(context, SocketType.Pair);
    socket.Bind("inproc://thread-comm");

    socket.Send("Hello from thread 1");
    var reply = socket.RecvString();
    Console.WriteLine($"Thread 1 received: {reply}");
});

var thread2 = new Thread(() =>
{
    Thread.Sleep(100); // Ensure bind happens first
    using var socket = new Socket(context, SocketType.Pair);
    socket.Connect("inproc://thread-comm");

    var msg = socket.RecvString();
    Console.WriteLine($"Thread 2 received: {msg}");
    socket.Send("Hello from thread 2");
});

thread1.Start();
thread2.Start();
thread1.Join();
thread2.Join();
```

### 작업 기반 비동기 패턴

블로킹 작업을 작업으로 래핑:

```csharp
using var context = new Context();
using var socket = new Socket(context, SocketType.Rep);
socket.Bind("tcp://*:5555");

// Async receive
var receiveTask = Task.Run(() =>
{
    return socket.RecvString();
});

// Wait with timeout
if (await Task.WhenAny(receiveTask, Task.Delay(5000)) == receiveTask)
{
    var message = await receiveTask;
    Console.WriteLine($"Received: {message}");
}
else
{
    Console.WriteLine("Timeout");
}
```

## 보안

### CURVE 인증

CURVE로 암호화 활성화:

```csharp
// Generate key pairs (do this once, store securely)
var (serverPublic, serverSecret) = GenerateCurveKeyPair();
var (clientPublic, clientSecret) = GenerateCurveKeyPair();

// Server
using var server = new Socket(context, SocketType.Rep);
server.SetOption(SocketOption.CurveServer, true);
server.SetOption(SocketOption.CurveSecretkey, serverSecret);
server.Bind("tcp://*:5555");

// Client
using var client = new Socket(context, SocketType.Req);
client.SetOption(SocketOption.CurveServerkey, serverPublic);
client.SetOption(SocketOption.CurvePublickey, clientPublic);
client.SetOption(SocketOption.CurveSecretkey, clientSecret);
client.Connect("tcp://localhost:5555");
```

**참고**: CURVE가 사용 가능한지 확인:
```csharp
bool hasCurve = Context.Has("curve");
if (!hasCurve)
{
    Console.WriteLine("CURVE not available in this ZMQ build");
}
```

## 모니터링 및 진단

### 로깅

사용자 정의 로깅 구현:

```csharp
public class ZmqLogger
{
    public static void LogSend(Socket socket, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SEND: {message}");
    }

    public static void LogRecv(Socket socket, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RECV: {message}");
    }
}

// Usage
var message = "Hello";
ZmqLogger.LogSend(socket, message);
socket.Send(message);
```

## 문제 해결

### 일반적인 문제

#### 연결 거부

```csharp
// Problem: Server not running or wrong address
client.Connect("tcp://localhost:5555"); // Throws or hangs

// Solution: Verify server is running and address is correct
// Check with: netstat -an | grep 5555
```

#### 주소가 이미 사용 중

```csharp
// Problem: Port already bound
socket.Bind("tcp://*:5555"); // Throws ZmqException

// Solution: Use different port or stop conflicting process
socket.Bind("tcp://*:5556");
```

#### 메시지가 수신되지 않음 (PUB-SUB)

```csharp
// Problem: No subscription or slow joiner
subscriber.Connect("tcp://localhost:5556");
var msg = subscriber.RecvString(); // Never receives

// Solution: Add subscription and delay
subscriber.Subscribe("");
Thread.Sleep(100); // Allow connection to establish
```

#### 닫을 때 소켓이 멈춤

```csharp
// Problem: Default linger waits indefinitely
socket.Dispose(); // Hangs if messages pending

// Solution: Set linger time
socket.SetOption(SocketOption.Linger, 1000); // Wait max 1 second
socket.Dispose();
```

#### 높은 메모리 사용량

```csharp
// Problem: High water marks too large
socket.SetOption(SocketOption.SendHwm, 1000000); // 1M messages!

// Solution: Reduce HWM or implement backpressure
socket.SetOption(SocketOption.SendHwm, 1000);
```

### 디버깅 팁

#### 상세 로깅 활성화

```csharp
public static class ZmqDebug
{
    public static void DumpSocketInfo(Socket socket)
    {
        var type = socket.GetOption<int>(SocketOption.Type);
        var rcvMore = socket.GetOption<bool>(SocketOption.RcvMore);
        var events = socket.GetOption<int>(SocketOption.Events);

        Console.WriteLine($"Socket Type: {type}");
        Console.WriteLine($"RcvMore: {rcvMore}");
        Console.WriteLine($"Events: {events}");
    }
}
```

#### ZeroMQ 버전 확인

```csharp
var (major, minor, patch) = Context.Version;
Console.WriteLine($"ZeroMQ Version: {major}.{minor}.{patch}");

// Check capabilities
Console.WriteLine($"CURVE: {Context.Has("curve")}");
Console.WriteLine($"DRAFT: {Context.Has("draft")}");
```

#### 연결 테스트

```csharp
public static bool TestConnection(string endpoint, int timeoutMs = 5000)
{
    try
    {
        using var context = new Context();
        using var socket = new Socket(context, SocketType.Req);

        socket.SetOption(SocketOption.SendTimeout, timeoutMs);
        socket.SetOption(SocketOption.RcvTimeout, timeoutMs);

        socket.Connect(endpoint);
        socket.Send("PING");

        var reply = socket.RecvString();
        return reply == "PONG";
    }
    catch
    {
        return false;
    }
}
```

## 플랫폼별 고려사항

### Windows

- 모든 시나리오에 TCP가 잘 작동
- IPC (Unix 도메인 소켓) 사용 불가
- 프로세스 간에 명명된 파이프 또는 TCP 사용

### Linux

- 프로세스 간에 IPC 선호 (TCP보다 빠름)
- 네트워크 통신에 TCP
- 부하 분산을 위해 `SO_REUSEPORT` 고려

### macOS

- Linux와 유사
- IPC 사용 가능하며 프로세스 간에 권장
- 네트워크 통신에 TCP

## 마이그레이션 가이드

### NetMQ에서

NetMQ 사용자는 Net.Zmq가 익숙하지만 몇 가지 차이점이 있음을 알 수 있습니다:

| NetMQ | Net.Zmq |
|-------|---------|
| `using (var socket = new RequestSocket())` | `using var socket = new Socket(ctx, SocketType.Req)` |
| `socket.SendFrame("msg")` | `socket.Send("msg")` |
| `var msg = socket.ReceiveFrameString()` | `var msg = socket.RecvString()` |
| `NetMQMessage` | `SendFlags.SendMore`를 사용한 다중 파트 |

### pyzmq에서

Python ZeroMQ 사용자는 유사한 패턴을 찾을 수 있습니다:

| pyzmq | Net.Zmq |
|-------|---------|
| `ctx = zmq.Context()` | `var ctx = new Context()` |
| `sock = ctx.socket(zmq.REQ)` | `var sock = new Socket(ctx, SocketType.Req)` |
| `sock.send_string("msg")` | `sock.Send("msg")` |
| `msg = sock.recv_string()` | `var msg = sock.RecvString()` |

## 다음 단계

- [시작하기](getting-started.ko.md)에서 기본 사항 검토
- [메시징 패턴](patterns.ko.md)에서 패턴 세부 사항 학습
- [API 사용법](api-usage.ko.md)에서 API 문서 탐색
- [API 레퍼런스](../api/index.html)에서 완전한 API 문서 확인
- [ZeroMQ 가이드](https://zguide.zeromq.org/)에서 아키텍처 패턴 읽기
