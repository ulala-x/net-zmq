[![English](https://img.shields.io/badge/lang:en-red.svg)](samples.md) [![한국어](https://img.shields.io/badge/lang:한국어-blue.svg)](samples.ko.md)

# 샘플 애플리케이션

Net.Zmq는 다양한 메시징 패턴과 기능을 시연하는 포괄적인 샘플 애플리케이션을 포함합니다. 각 샘플은 완전하고 실행 가능한 애플리케이션입니다.

## 샘플 실행

모든 샘플은 .NET CLI를 사용하여 실행할 수 있습니다:

```bash
# Clone the repository
git clone https://github.com/ulala-x/net-zmq.git
cd net-zmq

# Run a specific sample
dotnet run --project samples/Net.Zmq.Samples.ReqRep
dotnet run --project samples/Net.Zmq.Samples.PubSub
dotnet run --project samples/Net.Zmq.Samples.PushPull
```

---

## Request-Reply (REQ-REP)

클래식 동기식 request-reply 패턴입니다. 클라이언트가 요청을 보내고 응답을 기다립니다.

**소스**: [Net.Zmq.Samples.ReqRep](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.ReqRep)

[!code-csharp[](../samples/Net.Zmq.Samples.ReqRep/Program.cs)]

---

## Publish-Subscribe (PUB-SUB)

토픽 필터링을 사용한 일대다 메시지 배포입니다.

**소스**: [Net.Zmq.Samples.PubSub](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PubSub)

[!code-csharp[](../samples/Net.Zmq.Samples.PubSub/Program.cs)]

---

## Push-Pull 파이프라인

Ventilator-Worker-Sink 패턴을 사용한 부하 분산 작업 배포입니다.

**소스**: [Net.Zmq.Samples.PushPull](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PushPull)

[!code-csharp[](../samples/Net.Zmq.Samples.PushPull/Program.cs)]

---

## Poller

논블로킹 I/O로 여러 소켓 다중화입니다.

**소스**: [Net.Zmq.Samples.Poller](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Poller)

[!code-csharp[](../samples/Net.Zmq.Samples.Poller/Program.cs)]

---

## Router-Dealer 비동기 브로커

클라이언트와 워커 간 메시지를 라우팅하는 브로커를 사용한 비동기 request-reply입니다.

**소스**: [Net.Zmq.Samples.RouterDealer](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.RouterDealer)

[!code-csharp[](../samples/Net.Zmq.Samples.RouterDealer/Program.cs)]

---

## CURVE 보안

ZeroMQ의 CURVE 보안 메커니즘을 사용한 엔드투엔드 암호화입니다.

**소스**: [Net.Zmq.Samples.CurveSecurity](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.CurveSecurity)

[!code-csharp[](../samples/Net.Zmq.Samples.CurveSecurity/Program.cs)]

---

## 소켓 모니터

연결 라이프사이클 추적을 위한 실시간 소켓 이벤트 모니터링입니다.

**소스**: [Net.Zmq.Samples.Monitor](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Monitor)

[!code-csharp[](../samples/Net.Zmq.Samples.Monitor/Program.cs)]

---

## 모든 샘플

| 샘플 | 패턴 | 설명 |
|--------|---------|-------------|
| [ReqRep](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.ReqRep) | REQ-REP | 기본 request-reply |
| [PubSub](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PubSub) | PUB-SUB | 토픽 기반 pub-sub |
| [PushPull](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.PushPull) | PUSH-PULL | 파이프라인 패턴 |
| [Poller](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Poller) | 다중 | 소켓 다중화 |
| [RouterDealer](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.RouterDealer) | ROUTER-DEALER | 비동기 브로커 |
| [RouterToRouter](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.RouterToRouter) | ROUTER-ROUTER | 피어 투 피어 |
| [Pair](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Pair) | PAIR | 스레드 간 |
| [Proxy](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Proxy) | Proxy | 메시지 포워딩 |
| [SteerableProxy](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.SteerableProxy) | Proxy | 제어 가능한 프록시 |
| [CurveSecurity](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.CurveSecurity) | 보안 | CURVE 암호화 |
| [Monitor](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.Monitor) | 모니터링 | 소켓 이벤트 |
| [MultipartExtensions](https://github.com/ulala-x/net-zmq/tree/main/samples/Net.Zmq.Samples.MultipartExtensions) | 다중 파트 | 다중 프레임 메시지 |
