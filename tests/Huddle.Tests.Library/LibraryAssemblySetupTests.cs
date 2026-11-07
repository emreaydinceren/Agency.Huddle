namespace Agency.Huddle.Tests;

/// <summary>
/// Pins that the <c>Huddle.Tests.Library</c> assembly wires its own shared test-process setup: the temp-folder redirect
/// is applied by this assembly's initializer, and its runner file is pinned.
/// </summary>
public sealed class LibraryAssemblySetupTests
{
    /// <summary>The assembly initializer applies the temp-folder redirect before any test runs.</summary>
    [Fact]
    public void ModuleInitializer_AppliedTheTempRedirect()
    {
        Assert.True(TestTempRedirect.HasRun);
    }

    /// <summary>The assembly initializer raises the thread pool's minimum, so tests blocked in a bUnit wait cannot hold every thread the work they wait for needs.</summary>
    [Fact]
    public void ModuleInitializer_RaisedTheThreadPoolMinimum()
    {
        ThreadPool.GetMinThreads(out int workers, out int _);

        Assert.True(workers >= TestThreadPool.RaisedMinimum, $"Minimum workers {workers}, expected at least {TestThreadPool.RaisedMinimum}.");
    }

    /// <summary>The runner file pins parallelism to one thread per core, matching the other test projects.</summary>
    [Fact]
    public void RunnerConfig_MaxParallelThreads_IsPinned()
    {
        Assert.Equal("1x", RunnerConfig.MaxParallelThreads());
    }
}
