namespace Agency.Huddle.Tests;

/// <summary>
/// Pins that the <c>Huddle.Tests.Hosted.Gateway</c> assembly wires its own shared test-process setup: the temp-folder redirect
/// is applied by this assembly's initializer, and its runner file is pinned.
/// </summary>
public sealed class HostedGatewayAssemblySetupTests
{
    /// <summary>The assembly initializer applies the temp-folder redirect before any test runs.</summary>
    [Fact]
    public void ModuleInitializer_AppliedTheTempRedirect()
    {
        Assert.True(TestTempRedirect.HasRun);
    }

    /// <summary>The runner file pins parallelism to two threads because every test boots the app.</summary>
    [Fact]
    public void RunnerConfig_MaxParallelThreads_IsPinned()
    {
        Assert.Equal("2", RunnerConfig.MaxParallelThreads());
    }
}
