namespace Agency.Huddle.Tests;

/// <summary>
/// Gives a test process more thread pool threads to start with. A project runs one test per core, and a test blocked in
/// a synchronous bUnit wait holds a pool thread, so the pool's default minimum (one per core) can be used up entirely by
/// tests waiting for work that itself needs a pool thread. Under load the pool adds threads too slowly to help: a queued
/// item was measured waiting 12 seconds, past the 10-second wait budget, and the failure landed on whichever test was waiting.
/// </summary>
internal static class TestThreadPool
{
    /// <summary>How many pool threads per core may start at once. Threads are created only when work needs them.</summary>
    private const int ThreadsPerCore = 4;

    /// <summary>The minimum worker count <see cref="Raise"/> asks for on this machine.</summary>
    public static int RaisedMinimum => Environment.ProcessorCount * ThreadsPerCore;

    /// <summary>Raises the pool's minimum worker count, never lowering it and leaving the I/O count alone.</summary>
    public static void Raise()
    {
        ThreadPool.GetMinThreads(out int workers, out int completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers, RaisedMinimum), completionPorts);
    }
}
