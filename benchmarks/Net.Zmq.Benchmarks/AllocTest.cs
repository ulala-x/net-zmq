using System;
using System.Threading;
using Net.Zmq;

namespace Net.Zmq.Benchmarks;

public static class AllocTest
{
    public static void Run()
    {
        var messageSize = 65536;
        var messageCount = 1000;

        Console.WriteLine($"=== Memory Allocation Test (msg={messageSize}, count={messageCount}) ===\n");

        using var ctx = new Context();
        using var push = new Socket(ctx, SocketType.Push);
        using var pull = new Socket(ctx, SocketType.Pull);

        pull.SetOption(SocketOption.Rcvhwm, 0);
        push.SetOption(SocketOption.Sndhwm, 0);
        pull.Bind("tcp://127.0.0.1:0");
        var endpoint = pull.GetOptionString(Net.Zmq.SocketOption.Last_Endpoint);
        push.Connect(endpoint);
        Thread.Sleep(50);

        var sendData = new byte[messageSize];
        var recvBuffer = new byte[messageSize];

        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // === Blocking mode ===
        var beforeBlocking = GC.GetTotalAllocatedBytes(precise: true);
        
        var blockingThread = new Thread(() =>
        {
            for (int i = 0; i < messageCount; i++)
                pull.Recv(recvBuffer);
        });
        blockingThread.Start();
        for (int i = 0; i < messageCount; i++)
            push.Send(sendData);
        blockingThread.Join();
        
        var afterBlocking = GC.GetTotalAllocatedBytes(precise: true);
        Console.WriteLine($"Blocking:    {afterBlocking - beforeBlocking,10:N0} bytes");

        Thread.Sleep(100);
        GC.Collect();

        // === NonBlocking mode ===
        var beforeNonBlocking = GC.GetTotalAllocatedBytes(precise: true);
        
        var nonBlockingThread = new Thread(() =>
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
                    Thread.Sleep(1);
                }
            }
        });
        nonBlockingThread.Start();
        for (int i = 0; i < messageCount; i++)
            push.Send(sendData);
        nonBlockingThread.Join();
        
        var afterNonBlocking = GC.GetTotalAllocatedBytes(precise: true);
        Console.WriteLine($"NonBlocking: {afterNonBlocking - beforeNonBlocking,10:N0} bytes");

        Thread.Sleep(100);
        GC.Collect();

        // === Poller mode ===
        var beforePoller = GC.GetTotalAllocatedBytes(precise: true);
        int pollCount = 0;

        var pollerThread = new Thread(() =>
        {
            using var poller = new Poller(1);
            int idx = poller.Add(pull, PollEvents.In);
            int received = 0;
            while (received < messageCount)
            {
                poller.Poll(-1);
                pollCount++;
                while (received < messageCount && pull.TryRecv(recvBuffer, out _))
                    received++;
            }
        });
        pollerThread.Start();
        for (int i = 0; i < messageCount; i++)
            push.Send(sendData);
        pollerThread.Join();
        
        var afterPoller = GC.GetTotalAllocatedBytes(precise: true);
        Console.WriteLine($"Poller:      {afterPoller - beforePoller,10:N0} bytes (poll count: {pollCount})");
        
        if (pollCount > 0)
            Console.WriteLine($"Per-poll:    {(afterPoller - beforePoller) / pollCount,10:N0} bytes");
    }
}
