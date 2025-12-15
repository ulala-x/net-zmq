# NetZeroMQ PUSH-PULL Pipeline Pattern Sample

This sample demonstrates the classic **Ventilator-Worker-Sink** pattern using ZeroMQ's PUSH-PULL sockets.

## Pattern Overview

The PUSH-PULL pattern creates a pipeline for distributing work across multiple parallel workers:

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

## Components

### 1. Ventilator (Task Generator)
- Creates and distributes tasks to workers
- Uses **PUSH** socket bound to `tcp://*:5557`
- Generates 100 tasks with random workloads (1-100ms)
- Load balances automatically across connected workers

### 2. Workers (Task Processors)
- Process tasks in parallel
- Use **PULL** socket to receive tasks from ventilator
- Use **PUSH** socket to send results to sink
- Default: 3 workers running concurrently
- Each worker tracks tasks processed and total workload

### 3. Sink (Result Collector)
- Collects and aggregates results from all workers
- Uses **PULL** socket bound to `tcp://*:5558`
- Displays statistics including:
  - Total processing time
  - Load distribution across workers
  - Per-worker task counts and workloads

## Key Features

### Automatic Load Balancing
ZeroMQ's PUSH-PULL sockets provide automatic round-robin load balancing across connected workers. Tasks are distributed evenly without manual coordination.

### Parallel Processing
Workers process tasks concurrently, demonstrating the pipeline's ability to scale horizontally.

### Asynchronous Flow
The pipeline operates asynchronously - the ventilator doesn't wait for results, workers process independently, and the sink collects results as they arrive.

## Usage

### Run Complete Pipeline
```bash
dotnet run
# or
dotnet run all
```

Runs all components in a single process with 3 workers.

### Run Components Separately

**Terminal 1 - Start Sink:**
```bash
dotnet run sink
```

**Terminal 2, 3, 4 - Start Workers:**
```bash
dotnet run worker 1
dotnet run worker 2
dotnet run worker 3
```

**Terminal 5 - Start Ventilator:**
```bash
dotnet run ventilator
```

## Sample Output

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

## ZeroMQ Concepts Demonstrated

### PUSH-PULL Pattern
- **PUSH**: Fair-queued distribution to downstream PULL sockets
- **PULL**: Fair-queued collection from upstream PUSH sockets
- Unidirectional data flow
- Automatic load balancing

### Socket Options
- **Linger (0)**: Don't wait for pending messages on socket close
- **ReceiveTimeout**: Timeout for blocking receive operations

### Pipeline Architecture
- **Separation of Concerns**: Ventilator, workers, and sink are independent
- **Scalability**: Easy to add more workers for increased throughput
- **Fault Tolerance**: Workers can fail independently without affecting the pipeline

## Configuration

You can modify these constants in `Program.cs`:

```csharp
const int TaskCount = 100;      // Total number of tasks to distribute
const int WorkerCount = 3;      // Number of parallel workers
```

## Network Ports

- **5557**: Ventilator PUSH socket (task distribution)
- **5558**: Sink PULL socket (result collection)

## Notes

- Workers connect to both the ventilator and sink
- The ventilator and sink bind to their respective ports
- ZeroMQ handles all queuing and message delivery
- The pattern demonstrates one-way data flow (no replies)
- Load balancing is automatic and transparent

## Related Patterns

- **Request-Reply**: See `NetZeroMQ.Samples.ReqRep` for synchronous request-response
- **Publish-Subscribe**: See `NetZeroMQ.Samples.PubSub` for one-to-many distribution
