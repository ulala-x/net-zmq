using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Net.Zmq.Benchmarks.Configs;

namespace Net.Zmq.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Check for --test flag to run quick diagnostic
        if (args.Contains("--test"))
        {
            RunDiagnostic();
            return;
        }

        // Check for --mode-test flag to verify Blocking vs NonBlocking
        if (args.Contains("--mode-test"))
        {
            ModeTest.Run();
            return;
        }

        // Check for --alloc-test flag to check memory allocation
        if (args.Contains("--alloc-test"))
        {
            AllocTest.Run();
            return;
        }

        // Check for --quick flag for fast Dry run mode
        bool isQuick = args.Contains("--quick");
        var filteredArgs = args.Where(arg => arg != "--quick").ToArray();

        // Create config with appropriate logger and job
        // Default: ConsoleLogger + ShortRun (verbose, 3 warmup + 3 iterations)
        // --quick: ConsoleLogger + Dry (verbose, 1 iteration for fast testing)
        var job = isQuick
            ? Job.Dry
            : Job.ShortRun;

        var config = ManualConfig.CreateEmpty()
            .AddLogger(ConsoleLogger.Default)
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddColumn(new LatencyColumn())
            .AddColumn(new MessagesPerSecondColumn())
            .AddExporter(MarkdownExporter.GitHub)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(job
                .WithRuntime(CoreRuntime.Core80)
                .WithPlatform(Platform.X64)
                .WithGcServer(true)
                .WithGcConcurrent(true));

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(filteredArgs, config);
    }

    private static void RunDiagnostic()
    {
        Console.WriteLine("=== Running Diagnostic Tests ===\n");

        TestPushPull();
        TestPubSub();
        TestReqRep();
        TestRouterRouter();

        Console.WriteLine("\n=== All tests completed! ===");
    }

    private static void TestPushPull()
    {
        Console.Write("Testing PUSH/PULL... ");
        try
        {
            using var ctx = new Net.Zmq.Context();
            using var pull = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Pull);
            using var push = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Push);

            pull.SetOption(Net.Zmq.SocketOption.Rcvtimeo, 3000);
            pull.SetOption(Net.Zmq.SocketOption.Linger, 0);
            pull.Bind("tcp://127.0.0.1:15580");

            push.SetOption(Net.Zmq.SocketOption.Linger, 0);
            push.Connect("tcp://127.0.0.1:15580");

            Thread.Sleep(100);

            var data = new byte[] { 1, 2, 3 };
            push.Send(data);

            using var msg = new Net.Zmq.Message(16);
            pull.Recv(msg);

            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
        }
    }

    private static void TestPubSub()
    {
        Console.Write("Testing PUB/SUB... ");
        try
        {
            using var ctx = new Net.Zmq.Context();
            using var pub = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Pub);
            using var sub = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Sub);

            pub.SetOption(Net.Zmq.SocketOption.Linger, 0);
            pub.Bind("tcp://127.0.0.1:15581");

            sub.SetOption(Net.Zmq.SocketOption.Rcvtimeo, 3000);
            sub.SetOption(Net.Zmq.SocketOption.Linger, 0);
            sub.Subscribe("");
            sub.Connect("tcp://127.0.0.1:15581");

            Thread.Sleep(200); // PUB/SUB needs more time

            var data = new byte[] { 1, 2, 3 };
            pub.Send(data);

            using var msg = new Net.Zmq.Message(16);
            sub.Recv(msg);

            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
        }
    }

    private static void TestReqRep()
    {
        Console.Write("Testing REQ/REP... ");
        try
        {
            using var ctx = new Net.Zmq.Context();
            using var rep = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Rep);
            using var req = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Req);

            rep.SetOption(Net.Zmq.SocketOption.Rcvtimeo, 3000);
            rep.SetOption(Net.Zmq.SocketOption.Linger, 0);
            rep.Bind("tcp://127.0.0.1:15582");

            req.SetOption(Net.Zmq.SocketOption.Rcvtimeo, 3000);
            req.SetOption(Net.Zmq.SocketOption.Linger, 0);
            req.Connect("tcp://127.0.0.1:15582");

            Thread.Sleep(100);

            var data = new byte[] { 1, 2, 3 };

            // REQ sends first
            req.Send(data);

            // REP receives
            using var msg = new Net.Zmq.Message(16);
            rep.Recv(msg);

            // REP replies
            rep.Send(data);

            // REQ receives reply
            msg.Rebuild(16);
            req.Recv(msg);

            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
        }
    }

    private static void TestRouterRouter()
    {
        Console.Write("Testing ROUTER/ROUTER... ");
        try
        {
            using var ctx = new Net.Zmq.Context();
            using var router1 = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Router);
            using var router2 = new Net.Zmq.Socket(ctx, Net.Zmq.SocketType.Router);

            var router1Id = System.Text.Encoding.UTF8.GetBytes("r1");
            var router2Id = System.Text.Encoding.UTF8.GetBytes("r2");

            router1.SetOption(Net.Zmq.SocketOption.Routing_Id, router1Id);
            router1.SetOption(Net.Zmq.SocketOption.Rcvtimeo, 3000);
            router1.SetOption(Net.Zmq.SocketOption.Linger, 0);
            router1.Bind("tcp://127.0.0.1:15583");

            router2.SetOption(Net.Zmq.SocketOption.Routing_Id, router2Id);
            router2.SetOption(Net.Zmq.SocketOption.Rcvtimeo, 3000);
            router2.SetOption(Net.Zmq.SocketOption.Linger, 0);
            router2.Connect("tcp://127.0.0.1:15583");

            Thread.Sleep(100);

            // Router2 sends to Router1 first (handshake)
            router2.Send(router1Id, Net.Zmq.SendFlags.SendMore);
            router2.Send(new byte[] { 1, 2, 3 });

            // Router1 receives (learns Router2's identity)
            using var msg = new Net.Zmq.Message(64);
            router1.Recv(msg); // sender identity
            msg.Rebuild(64);
            router1.Recv(msg); // payload

            // Now Router1 can send to Router2
            router1.Send(router2Id, Net.Zmq.SendFlags.SendMore);
            router1.Send(new byte[] { 4, 5, 6 });

            // Router2 receives
            msg.Rebuild(64);
            router2.Recv(msg); // sender identity
            msg.Rebuild(64);
            router2.Recv(msg); // payload

            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
        }
    }
}
