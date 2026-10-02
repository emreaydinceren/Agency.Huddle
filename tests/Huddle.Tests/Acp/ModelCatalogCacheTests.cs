using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins Spec §6.5's re-keyed caches: <see cref="ModelCatalogProbe"/> caches models per Adapter and
/// effort levels per (Adapter, Model), and a failed probe is never cached. Exercises the real
/// <see cref="ModelCatalogProbe"/> against a <see cref="FakeAdapterProbeRunner"/> standing in for
/// the real process spawn — docs/engineering/rules.md row 35 ("No test may reach the real
/// <see cref="ModelCatalogProbe"/>") forbids a test reaching the real probe, which is exactly what
/// exercising this caching logic required before <see cref="IAdapterProbeRunner"/> was extracted as
/// a seam.
/// </summary>
public sealed class ModelCatalogCacheTests
{
    /// <summary>A second probe for the same Adapter is served from cache, never re-spawned.</summary>
    [Fact]
    public async Task GetAsync_SecondCallSameAdapter_DoesNotReprobe()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], []),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        IReadOnlyList<AgentModelOption> first = await probe.GetAsync("claude", ct);
        IReadOnlyList<AgentModelOption> second = await probe.GetAsync("claude", ct);

        Assert.Single(runner.Calls);
        Assert.Equal(first, second);
    }

    /// <summary>A probe for a different Adapter is not served from another Adapter's cache entry.</summary>
    [Fact]
    public async Task GetAsync_DifferentAdapter_Reprobes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], []),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetAsync("claude", ct);
        await probe.GetAsync("agency", ct);

        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal("node", runner.Calls[0].ProcessOptions.Command);
        Assert.Equal("agency-acp", runner.Calls[1].ProcessOptions.Command);
    }

    /// <summary>
    /// Two spellings of the SAME Adapter — no <c>adapter:</c> at all, and naming the default
    /// Adapter by its own id — must share one cache entry and spawn exactly one process. Keying on
    /// the requested id rather than the resolved profile id would probe twice for what is actually
    /// one Adapter, contradicting Spec §4 P7 ("probe cost follows the Adapter").
    /// </summary>
    [Fact]
    public async Task GetAsync_DefaultAdapterRequestedTwoWays_ProbesOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], []),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetAsync(null, ct);
        await probe.GetAsync("claude", ct);

        Assert.Single(runner.Calls);
    }

    /// <summary>
    /// An unconfigured id that resolves to the default Adapter reuses the default's cache entry
    /// rather than spawning a second process for the same resolved profile.
    /// </summary>
    [Fact]
    public async Task GetAsync_UnconfiguredIdFallsBackToDefault_ReusesDefaultCacheEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], []),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetAsync(null, ct);
        await probe.GetAsync("ghost", ct);

        Assert.Single(runner.Calls);
    }

    /// <summary>
    /// Effort results are cached per (Adapter, Model): varying either dimension re-probes, and
    /// repeating an exact pair is served from cache.
    /// </summary>
    [Fact]
    public async Task GetEffortLevelsAsync_KeyedPerAdapterAndModel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([], [new AgentEffortOption("high", "High", null)]),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetEffortLevelsAsync("claude", "model-a", ct);
        await probe.GetEffortLevelsAsync("agency", "model-a", ct);
        await probe.GetEffortLevelsAsync("claude", "model-b", ct);
        await probe.GetEffortLevelsAsync("claude", "model-a", ct);
        await probe.GetEffortLevelsAsync("agency", "model-a", ct);

        Assert.Equal(3, runner.Calls.Count);
    }

    /// <summary>A failed probe (the runner throwing) is never cached, so a retry probes again.</summary>
    [Fact]
    public async Task GetAsync_FailedProbe_IsNeverCached_RetryProbesAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => throw new IOException("adapter unreachable"),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        IReadOnlyList<AgentModelOption> failed = await probe.GetAsync("claude", ct);
        Assert.Empty(failed);
        Assert.Single(runner.Calls);

        runner.Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], []);
        IReadOnlyList<AgentModelOption> retried = await probe.GetAsync("claude", ct);

        Assert.Single(retried);
        Assert.Equal(2, runner.Calls.Count);
    }

    /// <summary>The probe's cwd is the Teammates ROOT (<c>DataDir/Teammates</c>), never a Persona's own subfolder.</summary>
    [Fact]
    public async Task GetAsync_ProbeCwd_IsTeammatesRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], []),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetAsync("claude", ct);

        Assert.Single(runner.Calls);
        Assert.Equal(Path.Combine(dir.Path, "Teammates"), runner.Calls[0].ProbeCwd);
    }

    /// <summary>The Work Modes share the effort probe: asking for both of one (Adapter, Model) spawns one process, not two.</summary>
    [Fact]
    public async Task GetWorkModesAsync_AfterEffortLevelsForTheSameModel_DoesNotReprobe()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([], [new AgentEffortOption("high", "High", null)])
            {
                Modes = [new AgentModeOption("default", "Manual", null), new AgentModeOption("acceptEdits", "Accept edits", null)],
            },
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetEffortLevelsAsync("claude", "model-a", ct);
        IReadOnlyList<AgentModeOption> modes = await probe.GetWorkModesAsync("claude", "model-a", ct);

        Assert.Single(runner.Calls);
        Assert.Equal(["default", "acceptEdits"], modes.Select(mode => mode.Id));
    }

    /// <summary>The model probe, which runs with no model requested, also answers the default model's Work Modes.</summary>
    [Fact]
    public async Task GetWorkModesAsync_AfterTheModelProbe_ForTheDefaultModel_DoesNotReprobe()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([new AgentModelOption("m1", "Model 1", null)], [])
            {
                Modes = [new AgentModeOption("acceptEdits", "Accept edits", null)],
            },
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        await probe.GetAsync("claude", ct);
        IReadOnlyList<AgentModeOption> modes = await probe.GetWorkModesAsync("claude", null, ct);

        Assert.Single(runner.Calls);
        Assert.Single(modes);
    }

    /// <summary>The hidden modes are filtered out on the way out, and <c>default</c> (Manual) is kept.</summary>
    [Fact]
    public async Task GetWorkModesAsync_FiltersHiddenModesAndKeepsDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => new AdapterProbeOutcome([], [])
            {
                Modes =
                [
                    new AgentModeOption("default", "Manual", null),
                    new AgentModeOption("acceptEdits", "Accept edits", null),
                    new AgentModeOption("plan", "Plan", null),
                    new AgentModeOption("auto", "Auto", null),
                    new AgentModeOption("bypassPermissions", "Bypass permissions", null),
                ],
            },
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        IReadOnlyList<AgentModeOption> modes = await probe.GetWorkModesAsync("claude", null, ct);

        Assert.Equal(["default", "acceptEdits"], modes.Select(mode => mode.Id));
    }

    /// <summary>A successful answer with no modes is a real answer: it is cached, so the card is not re-probed.</summary>
    [Fact]
    public async Task GetWorkModesAsync_SuccessfulEmptyAnswer_IsCached()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new();
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        IReadOnlyList<AgentModeOption> first = await probe.GetWorkModesAsync("claude", null, ct);
        IReadOnlyList<AgentModeOption> second = await probe.GetWorkModesAsync("claude", null, ct);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Single(runner.Calls);
    }

    /// <summary>A failed probe is not cached for modes either, so a retry probes again.</summary>
    [Fact]
    public async Task GetWorkModesAsync_FailedProbe_IsNeverCached_RetryProbesAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new()
        {
            Handler = static (_, _, _) => throw new IOException("adapter unreachable"),
        };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        IReadOnlyList<AgentModeOption> failed = await probe.GetWorkModesAsync("claude", null, ct);
        Assert.Empty(failed);

        runner.Handler = static (_, _, _) => new AdapterProbeOutcome([], []) { Modes = [new AgentModeOption("acceptEdits", "Accept edits", null)] };
        IReadOnlyList<AgentModeOption> retried = await probe.GetWorkModesAsync("claude", null, ct);

        Assert.Single(retried);
        Assert.Equal(2, runner.Calls.Count);
    }

    /// <summary>Builds a <see cref="ModelCatalogProbe"/> over two configured Adapters, "claude" and "agency".</summary>
    /// <param name="dir">The isolated data directory the probe's Work Dir root lives under.</param>
    /// <param name="runner">The fake probe runner standing in for the real process spawn.</param>
    private static ModelCatalogProbe CreateProbe(TempDataDir dir, FakeAdapterProbeRunner runner)
    {
        AcpOptions acp = new()
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "claude", DisplayName = "Claude", Command = "node", Args = ["claude.js"] },
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "agency-acp", Args = ["--flag"] },
            ],
        };
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = dir.Path, Acp = acp });
        AdapterCatalog catalog = new(options);
        AdapterProfileResolver resolver = new(catalog);

        return new ModelCatalogProbe(new(options), NullLoggerFactory.Instance, resolver, runner, new WorkModePolicy(options));
    }
}
