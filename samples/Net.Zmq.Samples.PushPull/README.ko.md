[![English](https://img.shields.io/badge/lang-en-red.svg)](README.md) [![한국어](https://img.shields.io/badge/lang-ko-blue.svg)](README.ko.md)

# NetZeroMQ PUSH-PULL 파이프라인 패턴 샘플

이 샘플은 ZeroMQ의 PUSH-PULL 소켓을 사용한 전형적인 **Ventilator-Worker-Sink** 패턴을 보여줍니다.

## 패턴 개요

PUSH-PULL 패턴은 여러 병렬 워커에게 작업을 분산하는 파이프라인을 생성합니다:

```
┌────────────┐
│ Ventilator │ (PUSH)
│  Port 5557 │
└─────┬──────┘
      │
      ├─────────┬─────────┬─────────┐
      ▼         ▼         ▼         ▼
   ┌────────┬────────┬────────┬────────┐
   │Worker-1│Worker-2│Worker-3│Worker-N│ (PULL → PUSH)
   └────┬───┴────┬───┴────┬───┴────┬───┘
        │        │        │        │
        └────────┴────────┴────────┘
                 │
                 ▼
            ┌────────┐
            │  Sink  │ (PULL)
            │Port 5558│
            └────────┘
```

## 구성 요소

### 1. Ventilator (작업 생성기)
- 워커에게 작업을 생성하고 배포합니다
- `tcp://*:5557`에 바인드된 **PUSH** 소켓을 사용합니다
- 무작위 작업량(1-100ms)으로 100개의 작업을 생성합니다
- 연결된 워커들에게 자동으로 부하 분산을 수행합니다

### 2. Workers (작업 처리기)
- 병렬로 작업을 처리합니다
- Ventilator로부터 작업을 수신하기 위해 **PULL** 소켓을 사용합니다
- Sink로 결과를 전송하기 위해 **PUSH** 소켓을 사용합니다
- 기본값: 3개의 워커가 동시에 실행됩니다
- 각 워커는 처리한 작업 수와 총 작업량을 추적합니다

### 3. Sink (결과 수집기)
- 모든 워커로부터 결과를 수집하고 집계합니다
- `tcp://*:5558`에 바인드된 **PULL** 소켓을 사용합니다
- 다음 통계를 표시합니다:
  - 총 처리 시간
  - 워커들 간의 부하 분산
  - 워커별 작업 수와 작업량

## 주요 기능

### 자동 부하 분산
ZeroMQ의 PUSH-PULL 소켓은 연결된 워커들에게 자동으로 라운드 로빈 부하 분산을 제공합니다. 작업은 수동 조정 없이 균등하게 분산됩니다.

### 병렬 처리
워커들은 작업을 동시에 처리하여 파이프라인의 수평적 확장 능력을 보여줍니다.

### 비동기 흐름
파이프라인은 비동기적으로 작동합니다. Ventilator는 결과를 기다리지 않고, 워커들은 독립적으로 처리하며, Sink는 결과가 도착하는 대로 수집합니다.

## 사용법

### 전체 파이프라인 실행
```bash
dotnet run
# 또는
dotnet run all
```

단일 프로세스에서 3개의 워커와 함께 모든 구성 요소를 실행합니다.

### 구성 요소 개별 실행

**터미널 1 - Sink 시작:**
```bash
dotnet run sink
```

**터미널 2, 3, 4 - Workers 시작:**
```bash
dotnet run worker 1
dotnet run worker 2
dotnet run worker 3
```

**터미널 5 - Ventilator 시작:**
```bash
dotnet run ventilator
```

## 샘플 출력

```
NetZeroMQ PUSH-PULL Pipeline Pattern Sample
==========================================
Demonstrating Ventilator-Worker-Sink Pattern

Starting complete pipeline: 1 Ventilator, 3 Workers, 1 Sink

[Sink] Starting result collector...
[Sink] Bound to tcp://*:5558
[Sink] Waiting for batch start signal...
[Worker-1] Starting...
[Worker-1] Connected and ready for tasks
[Worker-2] Starting...
[Worker-2] Connected and ready for tasks
[Worker-3] Starting...
[Worker-3] Connected and ready for tasks
[Ventilator] Starting task generator...
[Ventilator] Bound to tcp://*:5557
[Sink] Batch started, collecting results...
[Ventilator] Distributing 100 tasks...
[Ventilator] Dispatched 20/100 tasks
[Worker-1] Processed 10 tasks (current: task#27, 45ms)
[Worker-2] Processed 10 tasks (current: task#28, 67ms)
[Ventilator] Dispatched 40/100 tasks
[Worker-3] Processed 10 tasks (current: task#29, 23ms)
[Sink] Received 20/100 results
...
[Ventilator] All 100 tasks dispatched
[Ventilator] Total expected workload: 5234ms
[Ventilator] Average per task: 52ms

[Sink] ========== Pipeline Statistics ==========
[Sink] Total results received: 100/100
[Sink] Total elapsed time: 1847.32ms
[Sink]
[Sink] Worker Load Distribution:
[Sink]   Worker-1: 34 tasks (34.0%), 1789ms workload
[Sink]   Worker-2: 33 tasks (33.0%), 1723ms workload
[Sink]   Worker-3: 33 tasks (33.0%), 1722ms workload
[Sink] =============================================
```

## ZeroMQ 개념

### PUSH-PULL 패턴
- **PUSH**: 하위 PULL 소켓들에게 공정하게 큐잉하여 분배합니다
- **PULL**: 상위 PUSH 소켓들로부터 공정하게 큐잉하여 수집합니다
- 단방향 데이터 흐름
- 자동 부하 분산

### 소켓 옵션
- **Linger (0)**: 소켓 종료 시 대기 중인 메시지를 기다리지 않습니다
- **ReceiveTimeout**: 블로킹 수신 작업의 타임아웃 설정입니다

### 파이프라인 아키텍처
- **관심사의 분리**: Ventilator, Workers, Sink는 독립적입니다
- **확장성**: 처리량 증가를 위해 워커를 쉽게 추가할 수 있습니다
- **장애 허용**: 워커들은 파이프라인에 영향을 주지 않고 독립적으로 실패할 수 있습니다

## 설정

`Program.cs`에서 다음 상수를 수정할 수 있습니다:

```csharp
const int TaskCount = 100;      // 분배할 총 작업 수
const int WorkerCount = 3;      // 병렬 워커 수
```

## 네트워크 포트

- **5557**: Ventilator PUSH 소켓 (작업 분배)
- **5558**: Sink PULL 소켓 (결과 수집)

## 참고 사항

- 워커는 Ventilator와 Sink 모두에 연결합니다
- Ventilator와 Sink는 각각의 포트에 바인드합니다
- ZeroMQ가 모든 큐잉과 메시지 전달을 처리합니다
- 이 패턴은 단방향 데이터 흐름을 보여줍니다 (응답 없음)
- 부하 분산은 자동이며 투명합니다

## 관련 패턴

- **Request-Reply**: 동기 요청-응답은 `NetZeroMQ.Samples.ReqRep`를 참조하세요
- **Publish-Subscribe**: 일대다 배포는 `NetZeroMQ.Samples.PubSub`를 참조하세요
