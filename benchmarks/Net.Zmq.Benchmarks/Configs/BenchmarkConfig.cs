using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

namespace Net.Zmq.Benchmarks.Configs;

/// <summary>
/// Custom column that displays per-message latency in human-readable format.
/// Calculates latency as Mean / MessageCount for throughput benchmarks.
/// </summary>
public class LatencyColumn : IColumn
{
    public string Id => "Latency";
    public string ColumnName => "Latency";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => -1;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Time;
    public string Legend => "Per-message latency (Mean / MessageCount)";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null)
            return "N/A";

        var meanNs = report.ResultStatistics.Mean;

        // Get MessageCount parameter to calculate per-message latency
        var messageCountParam = benchmarkCase.Parameters.Items
            .FirstOrDefault(p => p.Name == "MessageCount");
        var messageCount = messageCountParam?.Value is int count ? count : 1;

        // Per-message latency = total time / message count
        var latencyNs = meanNs / messageCount;

        return latencyNs switch
        {
            >= 1_000_000_000 => $"{latencyNs / 1_000_000_000:N2} s",
            >= 1_000_000 => $"{latencyNs / 1_000_000:N2} ms",
            >= 1_000 => $"{latencyNs / 1_000:N2} μs",
            _ => $"{latencyNs:N2} ns"
        };
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}

/// <summary>
/// Custom column that displays messages per second (Messages/sec).
/// Automatically detects MessageCount parameter for throughput benchmarks.
/// </summary>
public class MessagesPerSecondColumn : IColumn
{
    public string Id => "MsgPerSec";
    public string ColumnName => "Messages/sec";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Messages processed per second (message throughput)";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null)
            return "N/A";

        var meanNs = report.ResultStatistics.Mean;
        var opsPerSec = 1_000_000_000.0 / meanNs;  // ns to sec conversion

        // Try to get MessageCount parameter to calculate total messages per second
        var messageCountParam = benchmarkCase.Parameters.Items
            .FirstOrDefault(p => p.Name == "MessageCount");
        var messageCount = messageCountParam?.Value is int count ? count : 1;

        var msgPerSec = opsPerSec * messageCount;

        return msgPerSec switch
        {
            >= 1_000_000 => $"{msgPerSec / 1_000_000:N2}M",
            >= 1_000 => $"{msgPerSec / 1_000:N2}K",
            _ => $"{msgPerSec:N2}"
        };
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}

/// <summary>
/// Custom column that displays data throughput (Gbps or GB/s).
/// Calculates throughput as Messages/sec × MessageSize.
/// </summary>
public class DataThroughputColumn : IColumn
{
    public string Id => "DataThroughput";
    public string ColumnName => "Data Throughput";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 1;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Data throughput (Messages/sec × MessageSize)";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null)
            return "N/A";

        var meanNs = report.ResultStatistics.Mean;
        var opsPerSec = 1_000_000_000.0 / meanNs;  // ns to sec conversion

        // Get MessageCount parameter
        var messageCountParam = benchmarkCase.Parameters.Items
            .FirstOrDefault(p => p.Name == "MessageCount");
        var messageCount = messageCountParam?.Value is int count ? count : 1;

        // Get MessageSize parameter
        var messageSizeParam = benchmarkCase.Parameters.Items
            .FirstOrDefault(p => p.Name == "MessageSize");
        var messageSize = messageSizeParam?.Value is int size ? size : 0;

        if (messageSize == 0)
            return "N/A";

        var msgPerSec = opsPerSec * messageCount;
        var bytesPerSec = msgPerSec * messageSize;

        // For 64KB messages, use GB/s; for smaller messages, use Gbps
        if (messageSize >= 65536)
        {
            var gbPerSec = bytesPerSec / 1_073_741_824.0;  // bytes to GB
            return $"{gbPerSec:N2} GB/s";
        }
        else
        {
            var gbps = (bytesPerSec * 8) / 1_000_000_000.0;  // bytes to bits to Gbps
            return $"{gbps:N2} Gbps";
        }
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        => GetValue(summary, benchmarkCase);

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}

/// <summary>
/// Short run configuration for quick testing and development.
/// </summary>
public class ShortRunConfig : ManualConfig
{
    public ShortRunConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(new LatencyColumn());
        AddColumn(new MessagesPerSecondColumn());
        AddColumn(new DataThroughputColumn());
        AddExporter(MarkdownExporter.GitHub);

        AddDiagnoser(MemoryDiagnoser.Default);

        AddJob(Job.ShortRun
            .WithRuntime(CoreRuntime.Core80)
            .WithPlatform(Platform.X64)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(false));
    }
}

/// <summary>
/// Full run configuration for accurate benchmark results.
/// </summary>
public class FullRunConfig : ManualConfig
{
    public FullRunConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(new LatencyColumn());
        AddColumn(new MessagesPerSecondColumn());
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);

        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);

        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core80)
            .WithPlatform(Platform.X64)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(false)
            .WithIterationCount(15)
            .WithWarmupCount(5));
    }
}

/// <summary>
/// Memory-focused configuration for analyzing allocations and GC behavior.
/// </summary>
public class MemoryConfig : ManualConfig
{
    public MemoryConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(new LatencyColumn());
        AddColumn(new MessagesPerSecondColumn());
        AddExporter(MarkdownExporter.GitHub);

        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);

        // Force GC collection between runs for more accurate memory measurements
        AddJob(Job.ShortRun
            .WithRuntime(CoreRuntime.Core80)
            .WithPlatform(Platform.X64)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(true));
    }
}

/// <summary>
/// Clean output configuration with minimal logging for CI/CD environments.
/// </summary>
public class CleanOutputConfig : ManualConfig
{
    public CleanOutputConfig()
    {
        AddLogger(NullLogger.Instance);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddColumn(new LatencyColumn());
        AddColumn(new MessagesPerSecondColumn());
        AddExporter(MarkdownExporter.GitHub);

        AddDiagnoser(MemoryDiagnoser.Default);

        AddJob(Job.ShortRun
            .WithRuntime(CoreRuntime.Core80)
            .WithPlatform(Platform.X64)
            .WithGcServer(true)
            .WithGcConcurrent(true)
            .WithGcForce(false));
    }
}
