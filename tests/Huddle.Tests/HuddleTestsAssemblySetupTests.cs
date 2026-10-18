namespace Agency.Huddle.Tests;

/// <summary>
/// Pins that the <c>Huddle.Tests</c> assembly wires its own shared test-process setup: the temp-folder redirect
/// is applied by this assembly's initializer, and its runner file is pinned.
/// </summary>
public sealed class HuddleTestsAssemblySetupTests
{
    /// <summary>The assembly initializer applies the temp-folder redirect before any test runs.</summary>
    [Fact]
    public void ModuleInitializer_AppliedTheTempRedirect()
    {
        Assert.True(TestTempRedirect.HasRun);
    }

    /// <summary>This project has no runner file yet, so the parallelism setting reads as not configured.</summary>
    [Fact]
    public void RunnerConfig_MaxParallelThreads_IsPinned()
    {
        Assert.Null(RunnerConfig.MaxParallelThreads());
    }
}
