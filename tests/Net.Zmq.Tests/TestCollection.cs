using Xunit;

namespace Net.Zmq.Tests;

/// <summary>
/// All ZMQ tests must run sequentially to avoid context conflicts.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialTestCollection : ICollectionFixture<ZmqCleanupFixture>
{
}

/// <summary>
/// Fixture that helps with cleanup between test runs to avoid native resource exhaustion.
/// </summary>
public class ZmqCleanupFixture : IDisposable
{
    public ZmqCleanupFixture()
    {
        // Force garbage collection at the start of the collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public void Dispose()
    {
        // Force garbage collection at the end of the collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Thread.Sleep(100); // Small delay for native cleanup
    }
}
