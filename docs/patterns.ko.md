[![English](https://img.shields.io/badge/lang:en-red.svg)](patterns.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](patterns.ko.md)

# 메시징 패턴 (Messaging Patterns)

ZeroMQ는 다양한 통신 시나리오를 위한 여러 내장 메시징 패턴을 제공합니다. 이 가이드는 Net.Zmq가 지원하는 모든 패턴을 실용적인 예제와 함께 다룹니다.

## 개요

| 패턴 | 소켓 | 사용 사례 |
|---------|---------|----------|
| Request-Reply | REQ-REP | 동기식 클라이언트-서버 |
| Publish-Subscribe | PUB-SUB | 일대다 브로드캐스트 |
| Push-Pull | PUSH-PULL | 부하 분산 파이프라인 |
| Router-Dealer | ROUTER-DEALER | 비동기 클라이언트-서버 |
| Pair | PAIR | 독점적 양방향 통신 |

## Request-Reply 패턴 (REQ-REP)

REQ-REP 패턴은 동기식 클라이언트-서버 통신을 구현합니다. 클라이언트는 요청을 보내고 응답을 기다립니다.

### 특징

- **동기식 (Synchronous)**: 클라이언트는 응답을 받을 때까지 대기
- **고정 순서 (Lockstep)**: 송신-수신-송신-수신 순서를 번갈아 수행
- **일대일 (One-to-one)**: 각 요청은 정확히 하나의 응답을 받음

### 예제: 간단한 에코 서버

**서버 (REP)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var server = new Socket(context, SocketType.Rep);
server.Bind("tcp://*:5555");

Console.WriteLine("Echo server started on port 5555");

while (true)
{
    // Wait for request
    var request = server.RecvString();
    Console.WriteLine($"Received: {request}");

    // Send reply
    server.Send($"Echo: {request}");
}
```

**클라이언트 (REQ)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var client = new Socket(context, SocketType.Req);
client.Connect("tcp://localhost:5555");

// Send request
client.Send("Hello World");
Console.WriteLine("Request sent");

// Wait for reply
var reply = client.RecvString();
Console.WriteLine($"Reply: {reply}");
```

### 모범 사례

- 항상 Send()와 RecvString()/RecvBytes()를 짝지어 사용
- 연결 실패를 처리하기 위해 try-catch 사용
- 무한 대기를 방지하기 위해 타임아웃 설정
- 비동기 시나리오에는 DEALER-ROUTER 고려

## Publish-Subscribe 패턴 (PUB-SUB)

PUB-SUB 패턴은 하나의 퍼블리셔에서 여러 구독자로 메시지를 배포합니다. 구독자는 토픽으로 메시지를 필터링합니다.

### 특징

- **일대다 (One-to-many)**: 단일 퍼블리셔, 다수의 구독자
- **토픽 기반 (Topic-based)**: 구독자는 접두사 매칭으로 필터링
- **발사 후 망각 (Fire-and-forget)**: 퍼블리셔는 누가 받는지 모름
- **늦은 합류 문제 (Late joiner problem)**: 구독자는 구독 전 전송된 메시지를 놓침

### 예제: 날씨 업데이트

**퍼블리셔 (PUB)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var publisher = new Socket(context, SocketType.Pub);
publisher.Bind("tcp://*:5556");

Console.WriteLine("Weather publisher started");

var random = new Random();
while (true)
{
    // Generate weather data
    var zipcode = random.Next(10000, 99999);
    var temperature = random.Next(-20, 40);
    var humidity = random.Next(10, 90);

    // Publish with topic (zipcode)
    var update = $"{zipcode} {temperature} {humidity}";
    publisher.Send(update);

    Console.WriteLine($"Published: {update}");
    Thread.Sleep(100);
}
```

**구독자 (SUB)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var subscriber = new Socket(context, SocketType.Sub);
subscriber.Connect("tcp://localhost:5556");

// Subscribe to specific zipcode(s)
subscriber.Subscribe("10001");
subscriber.Subscribe("10002");

Console.WriteLine("Subscribed to zipcodes 10001 and 10002");

while (true)
{
    var update = subscriber.RecvString();
    var parts = update.Split(' ');

    var zipcode = parts[0];
    var temperature = int.Parse(parts[1]);
    var humidity = int.Parse(parts[2]);

    Console.WriteLine($"Zipcode: {zipcode}, Temp: {temperature}°C, Humidity: {humidity}%");
}
```

### 토픽 필터링

토픽은 접두사 매칭을 사용합니다. "A"를 구독하면 "A", "AB", "ABC" 등이 매칭됩니다.

```csharp
// Subscribe to all messages
subscriber.Subscribe("");

// Subscribe to specific topics
subscriber.Subscribe("weather.");
subscriber.Subscribe("stock.AAPL");

// Unsubscribe
subscriber.Unsubscribe("weather.");
```

### 모범 사례

- 메시지를 받기 전에 항상 Subscribe() 호출
- 필터링을 위해 의미 있는 토픽 접두사 사용
- 느린 합류자 문제 고려 (bind/connect 후 sleep 추가)
- 퍼블리셔는 안정적이어야 함 (bind), 구독자는 connect

## Push-Pull 패턴 (파이프라인)

PUSH-PULL 패턴은 워커에게 작업을 배포하는 파이프라인을 생성합니다. 작업은 자동으로 부하 분산됩니다.

### 특징

- **부하 분산 (Load balancing)**: 작업이 워커 간 균등하게 배포
- **공정 큐잉 (Fair queuing)**: 워커가 라운드 로빈으로 작업 수신
- **단방향 (One-way)**: 응답을 보내지 않음
- **안정적 (Reliable)**: 워커가 바쁘면 메시지가 대기열에 저장

### 예제: 병렬 작업 처리

**작업 생산자 (PUSH)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var pusher = new Socket(context, SocketType.Push);
pusher.Bind("tcp://*:5557");

Console.WriteLine("Task producer started");

for (int i = 0; i < 100; i++)
{
    var task = $"Task {i:D3}";
    pusher.Send(task);
    Console.WriteLine($"Sent: {task}");
    Thread.Sleep(10);
}
```

**워커 (PULL)**:
```csharp
using Net.Zmq;

using var context = new Context();
using var puller = new Socket(context, SocketType.Pull);
puller.Connect("tcp://localhost:5557");

var workerId = Environment.ProcessId;
Console.WriteLine($"Worker {workerId} started");

while (true)
{
    var task = puller.RecvString();
    Console.WriteLine($"Worker {workerId} processing: {task}");

    // Simulate work
    Thread.Sleep(Random.Shared.Next(100, 500));

    Console.WriteLine($"Worker {workerId} completed: {task}");
}
```

**결과 수집기 (선택사항)**:

결과를 수집하려면 별도의 PULL 소켓을 사용하세요:

```csharp
// In worker, add a PUSH socket
using var resultPusher = new Socket(context, SocketType.Push);
resultPusher.Connect("tcp://localhost:5558");

// After processing
resultPusher.Send($"Result for {task}");

// Collector
using var resultPuller = new Socket(context, SocketType.Pull);
resultPuller.Bind("tcp://*:5558");

while (true)
{
    var result = resultPuller.RecvString();
    Console.WriteLine($"Collected: {result}");
}
```

### 모범 사례

- 생산자는 bind, 워커는 connect (동적 확장 가능)
- 작업 배포와 결과 수집에 별도의 소켓 사용
- 완전한 파이프라인을 위해 ventilator-worker-sink 패턴 고려
- 느린 워커를 감지하기 위해 큐 크기 모니터링

## Router-Dealer 패턴

ROUTER와 DEALER 소켓은 고급 라우팅 기능을 갖춘 비동기 request-reply를 제공합니다.

### DEALER-DEALER (비동기 Request-Reply)

DEALER 소켓은 응답을 기다리지 않고 여러 요청을 보낼 수 있습니다.

```csharp
// Async server (DEALER)
using var server = new Socket(context, SocketType.Dealer);
server.Bind("tcp://*:5559");

// Async client (DEALER)
using var client = new Socket(context, SocketType.Dealer);
client.Connect("tcp://localhost:5559");

// Client can send multiple requests
client.Send("Request 1");
client.Send("Request 2");
client.Send("Request 3");

// Receive replies (may arrive out of order)
for (int i = 0; i < 3; i++)
{
    var reply = client.RecvString();
    Console.WriteLine($"Reply: {reply}");
}
```

### ROUTER-ROUTER (신원을 가진 피어 투 피어)

ROUTER 소켓은 명시적 라우팅을 위해 신원 프레임을 추가합니다.

```csharp
using System.Text;
using Net.Zmq;

using var context = new Context();
using var peerA = new Socket(context, SocketType.Router);
using var peerB = new Socket(context, SocketType.Router);

// Set explicit identities
peerA.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_A"));
peerB.SetOption(SocketOption.Routing_Id, Encoding.UTF8.GetBytes("PEER_B"));

peerA.Bind("tcp://127.0.0.1:5560");
peerB.Connect("tcp://127.0.0.1:5560");

Thread.Sleep(100); // Allow connection to establish

// Peer B sends to Peer A (first frame = target identity)
peerB.Send(Encoding.UTF8.GetBytes("PEER_A"), SendFlags.SendMore);
peerB.Send("Hello from Peer B!");

// Peer A receives (first frame = sender identity)
var senderId = Encoding.UTF8.GetString(peerA.RecvBytes());
var message = peerA.RecvString();

Console.WriteLine($"From {senderId}: {message}");

// Peer A replies using sender's identity
peerA.Send(Encoding.UTF8.GetBytes(senderId), SendFlags.SendMore);
peerA.Send("Hello back from Peer A!");

// Peer B receives reply
var replyFrom = Encoding.UTF8.GetString(peerB.RecvBytes());
var reply = peerB.RecvString();

Console.WriteLine($"From {replyFrom}: {reply}");
```

### 모범 사례

- ROUTER-ROUTER에는 항상 명시적 신원 설정
- 첫 번째 프레임은 항상 신원 (envelope)
- 다중 프레임 메시지에는 SendFlags.SendMore 사용
- ROUTER는 더 복잡함; 간단한 경우 REQ-REP 또는 DEALER-REP 사용

## Pair 패턴 (PAIR)

PAIR 소켓은 두 엔드포인트 간 독점적 연결을 생성합니다.

### 특징

- **독점적 (Exclusive)**: 두 엔드포인트만 연결 가능
- **양방향 (Bidirectional)**: 양측 모두 송수신 가능
- **라우팅 없음 (No routing)**: 직접 피어 투 피어
- **주로 inproc용 (Mainly for inproc)**: 스레드 통신에 최적

### 예제: 스레드 간 통신

```csharp
using Net.Zmq;

using var context = new Context();

// Thread 1
var thread1 = new Thread(() =>
{
    using var pair = new Socket(context, SocketType.Pair);
    pair.Bind("inproc://pair-example");

    pair.Send("Message from Thread 1");
    var response = pair.RecvString();
    Console.WriteLine($"Thread 1 received: {response}");
});

// Thread 2
var thread2 = new Thread(() =>
{
    using var pair = new Socket(context, SocketType.Pair);
    pair.Connect("inproc://pair-example");

    var message = pair.RecvString();
    Console.WriteLine($"Thread 2 received: {message}");
    pair.Send("Message from Thread 2");
});

thread1.Start();
Thread.Sleep(100); // Ensure bind happens first
thread2.Start();

thread1.Join();
thread2.Join();
```

### 모범 사례

- PAIR는 주로 inproc:// 통신에 사용
- TCP의 경우 REQ-REP 또는 다른 패턴 고려
- bind가 connect보다 먼저 발생하도록 보장
- 복잡한 토폴로지에는 부적합

## 패턴 선택 가이드

사용 사례에 맞는 패턴을 선택하세요:

| 시나리오 | 권장 패턴 |
|----------|-------------------|
| 응답이 있는 클라이언트-서버 | REQ-REP 또는 DEALER-REP |
| 다수의 클라이언트에게 브로드캐스트 | PUB-SUB |
| 워커에게 작업 배포 | PUSH-PULL (파이프라인) |
| 비동기 클라이언트-서버 | DEALER-ROUTER |
| 피어 투 피어 메시징 | ROUTER-ROUTER 또는 PAIR |
| 스레드 간 통신 | PAIR (inproc) |
| 부하 분산 | PUSH-PULL 또는 ROUTER-DEALER |

## 고급: 패턴 조합

복잡한 아키텍처를 위해 패턴을 조합할 수 있습니다:

### Majordomo 패턴 (브로커)

브로커가 클라이언트와 워커 사이에 위치:

```
Client (REQ) → Broker (ROUTER-DEALER) → Worker (REP)
```

### Paranoid Pirate 패턴

하트비트를 사용한 안정적인 request-reply:

```
Client (REQ) → Load Balancer (ROUTER) → Workers (DEALER) with heartbeats
```

이러한 패턴은 [고급 주제](advanced-topics.ko.md) 가이드를 참조하세요.

## 다음 단계

- [API 사용법](api-usage.ko.md)에서 상세한 API 문서 학습
- [고급 주제](advanced-topics.ko.md)에서 복잡한 패턴 탐색
- [API 레퍼런스](../api/index.html)에서 완전한 문서 확인
