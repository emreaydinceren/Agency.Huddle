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

    /// <summary>The probe's cwd is the Agents ROOT (<c>DataDir/Agents</c>), never a Persona's own subfolder.</summary>
    [Fact]
    public async Task GetAsync_ProbeCwd_IsAgentsRoot()
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
        Assert.Equal(Path.Combine(dir.Path, "Agents"), runner.Calls[0].ProbeCwd);
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

    /// <summary>An outcome whose models and efforts have display names and whose session reported the given current values.</summary>
    private static AdapterProbeOutcome Outcome(string? currentModel, string? currentEffort) =>
        new(
            [new AgentModelOption("opus", "Opus 5.5", null), new AgentModelOption("sonnet", "Sonnet", null)],
            [new AgentEffortOption("default", "Default", null), new AgentEffortOption("high", "High", null), new AgentEffortOption("low", "Low", null)])
        {
            CurrentModel = currentModel,
            CurrentEffort = currentEffort,
        };

    /// <summary>Peeking an empty cache knows nothing but a chosen Claude alias's name, and never reaches the probe runner.</summary>
    [Fact]
    public void PeekRuntime_EmptyCache_ReturnsOnlyTheAliasNameAndDoesNotProbe()
    {
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new();
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        RuntimeNames names = probe.PeekRuntime("claude", null, null);
        RuntimeNames chosen = probe.PeekRuntime("claude", "opus", "high");

        Assert.Equal(default, names);
        Assert.Equal(new RuntimeNames("Opus", null), chosen);
        Assert.Empty(runner.Calls);
    }

    /// <summary>The alias names belong to the Claude Adapter only: another Adapter's "sonnet" stays unknown.</summary>
    [Fact]
    public void PeekRuntime_EmptyCacheNonClaudeAdapter_AliasIsUnknown()
    {
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new();
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        RuntimeNames names = probe.PeekRuntime("agency", "sonnet", null);

        Assert.Null(names.Model);
        Assert.Empty(runner.Calls);
    }

    /// <summary>A Claude model id that is not an alias stays unknown on an empty cache.</summary>
    [Fact]
    public void PeekRuntime_EmptyCacheUnknownModelId_IsUnknown()
    {
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new();
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        RuntimeNames names = probe.PeekRuntime("claude", "claude-sonnet-x", null);

        Assert.Null(names.Model);
    }

    /// <summary>Once a catalog is cached, its name wins over the alias name.</summary>
    [Fact]
    public async Task PeekRuntime_CachedCatalog_NameWinsOverTheAlias()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", "opus", null);

        Assert.Equal("Opus 5.5", names.Model);
    }

    /// <summary>Once a list is cached, an id missing from it stays unknown: there is no alias fallback.</summary>
    [Fact]
    public async Task PeekRuntime_CachedCatalogWithoutTheId_IsUnknownDespiteTheAlias()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", "haiku", null);

        Assert.Null(names.Model);
    }

    /// <summary>An unnamed Adapter resolves to the default Claude Adapter, so its alias reads by name.</summary>
    [Fact]
    public void PeekRuntime_EmptyCacheUnnamedAdapter_UsesTheDefaultAdaptersAlias()
    {
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new();
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);

        RuntimeNames names = probe.PeekRuntime(null, "sonnet", null);

        Assert.Equal("Sonnet", names.Model);
    }

    /// <summary>The null-model probe caches the Adapter's defaults, which peek reports by display name.</summary>
    [Fact]
    public async Task PeekRuntime_AfterDefaultProbe_NamesTheAdapterDefaults()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", null, null);

        Assert.Equal(new RuntimeNames("Opus 5.5", "High"), names);
        Assert.Single(runner.Calls);
    }

    /// <summary>Peek resolves the Adapter the way the probe does, so the default Adapter's cache answers for an unnamed one.</summary>
    [Fact]
    public async Task PeekRuntime_UnnamedAdapter_UsesTheDefaultAdaptersCache()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime(null, null, null);

        Assert.Equal("Opus 5.5", names.Model);
    }

    /// <summary>The adapter's "default" effort sentinel names no level, so the default Effort is unknown.</summary>
    [Fact]
    public async Task PeekRuntime_EffortSentinel_YieldsUnknownEffort()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "default") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", null, null);

        Assert.Equal(new RuntimeNames("Opus 5.5", null), names);
    }

    /// <summary>A chosen model id and effort id map to their display names from the cached lists.</summary>
    [Fact]
    public async Task PeekRuntime_ChosenIds_MapToDisplayNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);
        await probe.GetEffortLevelsAsync("claude", "sonnet", ct);

        RuntimeNames names = probe.PeekRuntime("claude", "sonnet", "low");

        Assert.Equal(new RuntimeNames("Sonnet", "Low"), names);
    }

    /// <summary>A chosen id the cached lists do not contain stays unknown rather than being invented.</summary>
    [Fact]
    public async Task PeekRuntime_ChosenIdNotInCache_IsUnknown()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", "retired-model", "Low");

        Assert.Equal(default, names);
    }

    /// <summary>The default Effort is only known for the default model, so choosing a model leaves it unknown.</summary>
    [Fact]
    public async Task PeekRuntime_ModelChosenEffortNot_DefaultEffortIsUnknown()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "high") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", "sonnet", null);

        Assert.Equal(new RuntimeNames("Sonnet", null), names);
    }

    /// <summary>A failed probe leaves no defaults behind, so peek still knows nothing.</summary>
    [Fact]
    public async Task PeekRuntime_AfterFailedProbe_ReturnsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => throw new IOException("adapter unreachable") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetAsync("claude", ct);

        RuntimeNames names = probe.PeekRuntime("claude", null, null);

        Assert.Equal(default, names);
    }

    /// <summary>The effort-and-modes probe for the default model also records the Adapter's defaults.</summary>
    [Fact]
    public async Task PeekRuntime_AfterDefaultModelEffortProbe_NamesTheDefaultEffort()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeAdapterProbeRunner runner = new() { Handler = static (_, _, _) => ModelCatalogCacheTests.Outcome("opus", "low") };
        using ModelCatalogProbe probe = ModelCatalogCacheTests.CreateProbe(dir, runner);
        await probe.GetEffortLevelsAsync("claude", null, ct);

        RuntimeNames names = probe.PeekRuntime("claude", null, null);

        Assert.Equal("Low", names.Effort);
        Assert.Equal("opus", names.Model);
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
        IOptions<TeamOptions> options = dir.Options(opts => opts.Acp = acp);
        AdapterCatalog catalog = new(options);
        AdapterProfileResolver resolver = new(catalog);

        return new ModelCatalogProbe(new(options), NullLoggerFactory.Instance, resolver, runner, new WorkModePolicy(options));
    }
}
