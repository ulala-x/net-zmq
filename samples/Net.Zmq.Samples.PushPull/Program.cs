using Net.Zmq;

Console.WriteLine("NetZeroMQ PUSH-PULL Pipeline Pattern Sample");
Console.WriteLine("==========================================");
Console.WriteLine("Demonstrating Ventilator-Worker-Sink Pattern");
Console.WriteLine();

var mode = args.Length > 0 ? args[0].ToLower() : "all";

// Port configuration
const string VentilatorAddress = "tcp://*:5557";
const string SinkAddress = "tcp://*:5558";
const string VentilatorConnectAddress = "tcp://localhost:5557";
const string SinkConnectAddress = "tcp://localhost:5558";

// Workload configuration
const int TaskCount = 100;
const int WorkerCount = 3;

if (mode == "ventilator")
{
    RunVentilator();
    return;
}

if (mode == "worker")
{
    var workerId = args.Length > 1 ? int.Parse(args[1]) : 1;
    RunWorker(workerId);
    return;
}

if (mode == "sink")
{
    RunSink();
    return;
}

if (mode == "all")
{
    Console.WriteLine($"Starting complete pipeline: 1 Ventilator, {WorkerCount} Workers, 1 Sink");
    Console.WriteLine();

    // Start Sink first
    var sinkTask = Task.Run(RunSink);
    Thread.Sleep(500);

    // Start Workers
    var workerTasks = new List<Task>();
    for (int i = 0; i < WorkerCount; i++)
    {
        int workerId = i + 1;
        workerTasks.Add(Task.Run(() => RunWorker(workerId)));
    }
    Thread.Sleep(500);

    // Start Ventilator
    var ventilatorTask = Task.Run(RunVentilator);

    // Wait for all components to complete
    await Task.WhenAll(ventilatorTask);
    await Task.WhenAll(workerTasks);
    await sinkTask;

    Console.WriteLine();
    Console.WriteLine("Pipeline completed successfully!");
}
else
{
    Console.WriteLine($"Unknown mode: {mode}");
    Console.WriteLine("Usage: NetZmq.Samples.PushPull [all|ventilator|worker|sink] [worker-id]");
    Console.WriteLine();
    Console.WriteLine("Modes:");
    Console.WriteLine("  all        - Run complete pipeline (default)");
    Console.WriteLine("  ventilator - Run only the task generator");
    Console.WriteLine("  worker     - Run a single worker (specify worker-id as second argument)");
    Console.WriteLine("  sink       - Run only the result collector");
}

void RunVentilator()
{
    Console.WriteLine("[Ventilator] Starting task generator...");

    using var ctx = new Context();
    using var sender = new Socket(ctx, SocketType.Push);

    sender.SetOption(SocketOption.Linger, 0);
    sender.Bind(VentilatorAddress);
    Console.WriteLine($"[Ventilator] Bound to {VentilatorAddress}");

    // Give workers time to connect
    Thread.Sleep(1000);

    Console.WriteLine($"[Ventilator] Distributing {TaskCount} tasks...");

    // Signal start of batch to sink
    using var signalSocket = new Socket(ctx, SocketType.Push);
    signalSocket.SetOption(SocketOption.Linger, 0);
    signalSocket.Connect(SinkConnectAddress);
    signalSocket.Send("START");

    // Distribute tasks
    int totalWorkload = 0;
    Random random = new Random();

    for (int taskNum = 0; taskNum < TaskCount; taskNum++)
    {
        // Generate random workload (1-100 milliseconds)
        int workload = random.Next(1, 101);
        totalWorkload += workload;

        string message = $"{taskNum}:{workload}";
        sender.Send(message);

        if ((taskNum + 1) % 20 == 0)
        {
            Console.WriteLine($"[Ventilator] Dispatched {taskNum + 1}/{TaskCount} tasks");
        }
    }

    Console.WriteLine($"[Ventilator] All {TaskCount} tasks dispatched");
    Console.WriteLine($"[Ventilator] Total expected workload: {totalWorkload}ms");
    Console.WriteLine($"[Ventilator] Average per task: {totalWorkload / TaskCount}ms");
}

void RunWorker(int workerId)
{
    Console.WriteLine($"[Worker-{workerId}] Starting...");

    using var ctx = new Context();

    // Socket to receive tasks from ventilator
    using var receiver = new Socket(ctx, SocketType.Pull);
    receiver.SetOption(SocketOption.Linger, 0);
    receiver.Connect(VentilatorConnectAddress);

    // Socket to send results to sink
    using var sender = new Socket(ctx, SocketType.Push);
    sender.SetOption(SocketOption.Linger, 0);
    sender.Connect(SinkConnectAddress);

    Console.WriteLine($"[Worker-{workerId}] Connected and ready for tasks");

    int tasksProcessed = 0;
    int totalWorkload = 0;

    try
    {
        // Set receive timeout to detect when work is done
        receiver.SetOption(SocketOption.Rcvtimeo, 3000);

        while (true)
        {
            try
            {
                // Receive task from ventilator
                string message = receiver.RecvString();
                var parts = message.Split(':');
                int taskNum = int.Parse(parts[0]);
                int workload = int.Parse(parts[1]);

                // Simulate processing time
                Thread.Sleep(workload);

                tasksProcessed++;
                totalWorkload += workload;

                // Send result to sink
                string result = $"{workerId}:{taskNum}:{workload}";
                sender.Send(result);

                if (tasksProcessed % 10 == 0)
                {
                    Console.WriteLine($"[Worker-{workerId}] Processed {tasksProcessed} tasks (current: task#{taskNum}, {workload}ms)");
                }
            }
            catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN - timeout
            {
                Console.WriteLine($"[Worker-{workerId}] No more tasks available (timeout)");
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Worker-{workerId}] Error: {ex.Message}");
    }

    Console.WriteLine($"[Worker-{workerId}] Completed {tasksProcessed} tasks, total workload: {totalWorkload}ms");
}

void RunSink()
{
    Console.WriteLine("[Sink] Starting result collector...");

    using var ctx = new Context();
    using var receiver = new Socket(ctx, SocketType.Pull);

    receiver.SetOption(SocketOption.Linger, 0);
    receiver.Bind(SinkAddress);
    Console.WriteLine($"[Sink] Bound to {SinkAddress}");

    // Wait for start signal
    Console.WriteLine("[Sink] Waiting for batch start signal...");
    string startSignal = receiver.RecvString();
    if (startSignal != "START")
    {
        Console.WriteLine($"[Sink] Unexpected signal: {startSignal}");
        return;
    }

    Console.WriteLine("[Sink] Batch started, collecting results...");
    DateTime startTime = DateTime.Now;

    // Process results
    int resultsReceived = 0;
    Dictionary<int, int> workerStats = new Dictionary<int, int>();
    Dictionary<int, int> workerWorkload = new Dictionary<int, int>();

    try
    {
        // Set timeout to detect completion
        receiver.SetOption(SocketOption.Rcvtimeo, 5000);

        while (resultsReceived < TaskCount)
        {
            try
            {
                string result = receiver.RecvString();
                var parts = result.Split(':');
                int workerId = int.Parse(parts[0]);
                int taskNum = int.Parse(parts[1]);
                int workload = int.Parse(parts[2]);

                resultsReceived++;

                // Update statistics
                if (!workerStats.ContainsKey(workerId))
                {
                    workerStats[workerId] = 0;
                    workerWorkload[workerId] = 0;
                }
                workerStats[workerId]++;
                workerWorkload[workerId] += workload;

                if (resultsReceived % 20 == 0)
                {
                    Console.WriteLine($"[Sink] Received {resultsReceived}/{TaskCount} results");
                }
            }
            catch (ZmqException ex) when (ex.ErrorNumber == 11) // EAGAIN - timeout
            {
                Console.WriteLine($"[Sink] Timeout waiting for results. Received {resultsReceived}/{TaskCount}");
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Sink] Error: {ex.Message}");
    }

    DateTime endTime = DateTime.Now;
    TimeSpan elapsed = endTime - startTime;

    // Display final statistics
    Console.WriteLine();
    Console.WriteLine("[Sink] ========== Pipeline Statistics ==========");
    Console.WriteLine($"[Sink] Total results received: {resultsReceived}/{TaskCount}");
    Console.WriteLine($"[Sink] Total elapsed time: {elapsed.TotalMilliseconds:F2}ms");
    Console.WriteLine();
    Console.WriteLine("[Sink] Worker Load Distribution:");

    foreach (var kvp in workerStats.OrderBy(x => x.Key))
    {
        int workerId = kvp.Key;
        int taskCount = kvp.Value;
        int totalWorkload = workerWorkload[workerId];
        double percentage = (taskCount / (double)resultsReceived) * 100;

        Console.WriteLine($"[Sink]   Worker-{workerId}: {taskCount} tasks ({percentage:F1}%), {totalWorkload}ms workload");
    }

    Console.WriteLine("[Sink] =============================================");
    Console.WriteLine("[Sink] Done");
}
