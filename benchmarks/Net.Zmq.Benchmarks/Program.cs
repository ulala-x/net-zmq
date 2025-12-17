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
            .AddColumn(new DataThroughputColumn())
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
        TestRouterRouter();
        Console.WriteLine("\n=== All tests completed! ===");
    }

    private static void TestRouterRouter()
    {
        Console.Write("Testing ROUTER/ROUTER... ");
        try
        {
            using var ctx = new Context();
            using var router1 = new Socket(ctx, SocketType.Router);
            using var router2 = new Socket(ctx, SocketType.Router);

            var router1Id = "r1"u8.ToArray();
            var router2Id = "r2"u8.ToArray();

            router1.SetOption(SocketOption.Routing_Id, router1Id);
            router1.SetOption(SocketOption.Rcvtimeo, 3000);
            router1.SetOption(SocketOption.Linger, 0);
            router1.Bind("tcp://127.0.0.1:15583");

            router2.SetOption(SocketOption.Routing_Id, router2Id);
            router2.SetOption(SocketOption.Rcvtimeo, 3000);
            router2.SetOption(SocketOption.Linger, 0);
            router2.Connect("tcp://127.0.0.1:15583");

            Thread.Sleep(100);

            // Router2 sends to Router1 first (handshake)
            router2.Send(router1Id, SendFlags.SendMore);
            router2.Send(new byte[] { 1, 2, 3 });

            // Router1 receives (learns Router2's identity)
            using var msg = new Message(64);
            router1.Recv(msg); // sender identity
            msg.Rebuild(64);
            router1.Recv(msg); // payload

            // Now Router1 can send to Router2
            router1.Send(router2Id, SendFlags.SendMore);
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
