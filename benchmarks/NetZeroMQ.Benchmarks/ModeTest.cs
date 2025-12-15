using NetZeroMQ;

namespace NetZeroMQ.Benchmarks;

public static class ModeTest
{
    public static void Run()
    {
        Console.WriteLine("=== Mode Comparison Test ===\n");

        var messageSize = 1024;
        var messageCount = 10000;
        var messageData = new byte[messageSize];

        using var ctx = new Context();
        using var pull = new Socket(ctx, SocketType.Pull);
        using var push = new Socket(ctx, SocketType.Push);

        pull.SetOption(SocketOption.Sndhwm, 0);
        pull.SetOption(SocketOption.Rcvhwm, 0);
        pull.SetOption(SocketOption.Linger, 0);
        pull.Bind("tcp://127.0.0.1:0");

        var endpoint = pull.GetOptionString(SocketOption.Last_Endpoint);

        push.SetOption(SocketOption.Sndhwm, 0);
        push.SetOption(SocketOption.Rcvhwm, 0);
        push.SetOption(SocketOption.Linger, 0);
        push.Connect(endpoint);

        Thread.Sleep(100);

        var recvBuffer = new byte[messageSize];
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ========== Test 1: Blocking Recv ==========
        Console.WriteLine("1. BLOCKING mode (Recv)...");
        sw.Restart();

        var blockingRecvThread = new Thread(() =>
        {
            using var msg = new Message(messageSize);
            for (int i = 0; i < messageCount; i++)
            {
                msg.Rebuild(messageSize);
                pull.Recv(msg);
            }
        });
        blockingRecvThread.Start();

        for (int i = 0; i < messageCount; i++)
            push.Send(messageData);

        blockingRecvThread.Join();
        sw.Stop();

        var blockingTime = sw.ElapsedMilliseconds;
        Console.WriteLine($"   {blockingTime}ms, {messageCount * 1000.0 / blockingTime:N0} msg/sec\n");

        Thread.Sleep(100);

        // ========== Test 2: NonBlocking + Sleep ==========
        Console.WriteLine("2. NON-BLOCKING mode (TryRecv + Sleep 10ms)...");
        int sleepCount = 0;
        sw.Restart();

        var nonBlockingRecvThread = new Thread(() =>
        {
            int received = 0;
            while (received < messageCount)
            {
                if (pull.TryRecv(recvBuffer, out _))
                {
                    received++;
                    while (received < messageCount && pull.TryRecv(recvBuffer, out _))
                        received++;
                }
                else
                {
                    sleepCount++;
                    Thread.Sleep(10);
                }
            }
        });
        nonBlockingRecvThread.Start();

        for (int i = 0; i < messageCount; i++)
            push.Send(messageData);

        nonBlockingRecvThread.Join();
        sw.Stop();

        var nonBlockingTime = sw.ElapsedMilliseconds;
        Console.WriteLine($"   {nonBlockingTime}ms, {messageCount * 1000.0 / nonBlockingTime:N0} msg/sec (sleep count: {sleepCount})\n");

        Thread.Sleep(100);

        // ========== Test 3: Poller Blocking ==========
        Console.WriteLine("3. POLLER mode (Poll blocking + TryRecv burst)...");
        int pollCount = 0;
        sw.Restart();

        var pollerRecvThread = new Thread(() =>
        {
            int received = 0;
            while (received < messageCount)
            {
                Poller.Poll(pull, PollEvents.In, -1);  // Block until event
                pollCount++;

                // Burst receive all available messages
                while (received < messageCount && pull.TryRecv(recvBuffer, out _))
                    received++;
            }
        });
        pollerRecvThread.Start();

        for (int i = 0; i < messageCount; i++)
            push.Send(messageData);

        pollerRecvThread.Join();
        sw.Stop();

        var pollerTime = sw.ElapsedMilliseconds;
        Console.WriteLine($"   {pollerTime}ms, {messageCount * 1000.0 / pollerTime:N0} msg/sec (poll count: {pollCount})\n");

        // ========== Summary ==========
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"  Blocking:    {blockingTime,5}ms  ({messageCount * 1000.0 / blockingTime,10:N0} msg/sec)");
        Console.WriteLine($"  NonBlocking: {nonBlockingTime,5}ms  ({messageCount * 1000.0 / nonBlockingTime,10:N0} msg/sec)");
        Console.WriteLine($"  Poller:      {pollerTime,5}ms  ({messageCount * 1000.0 / pollerTime,10:N0} msg/sec)");
    }
}
