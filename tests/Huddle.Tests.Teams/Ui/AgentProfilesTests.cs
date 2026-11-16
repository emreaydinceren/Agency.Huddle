using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="AgentProfiles"/> turns a saved Agent into the data a profile card draws: who is a
/// Team's manager, who is a member of it, and what state the status dot shows.
/// </summary>
public sealed class AgentProfilesTests
{
    /// <summary>A name no Agent has yields null, so raw pipe clients and demo agents get no popover.</summary>
    [Fact]
    public async Task BuildAsync_UnknownName_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Nobody", null, ct);

        Assert.Null(view);
    }

    /// <summary>An Agent with an Adapter, Model and Effort chosen shows the Adapter's display name and the stored ids.</summary>
    [Fact]
    public async Task BuildAsync_ChosenRuntime_ReadsAdapterModelAndEffort()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", adapter: "codex", model: "gpt-5", effort: "high");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(new ProfileRuntime("Codex", "gpt-5", "high"), view.Profile.Runtime);
    }

    /// <summary>A selection with no Persona behind it (a Build a team draft) reads from the cache alone: the Adapter's display name, the cached names, and no probe.</summary>
    [Fact]
    public void DescribeRuntime_Selection_ReadsTheCacheWithoutProbing()
    {
        using ProfileFixture fixture = new();
        fixture.ModelCatalog.Runtime = new RuntimeNames("Sonnet", "Medium");

        ProfileRuntime chosen = fixture.Profiles.DescribeRuntime("codex", "sonnet", "medium");
        ProfileRuntime unchosen = fixture.Profiles.DescribeRuntime(null, null, null);

        Assert.Equal(new ProfileRuntime("Codex", "Sonnet", "Medium"), chosen);
        Assert.Equal(new ProfileRuntime("Claude Code", "Sonnet (default)", "Medium (default)"), unchosen);
        Assert.Equal(0, fixture.ModelCatalog.ProbeCount);
    }

    /// <summary>A draft stores exactly what it carries: with nothing chosen it reads "Agent default" and "Model default" although the cache names defaults, a chosen id reads its cached name, and nothing probes.</summary>
    [Fact]
    public void DescribeDraftRuntime_NothingChosen_NeverNamesTheCachedDefault()
    {
        using ProfileFixture fixture = new();
        fixture.ModelCatalog.Runtime = new RuntimeNames("Sonnet", "Medium");

        ProfileRuntime unchosen = fixture.Profiles.DescribeDraftRuntime(null, null, null);
        ProfileRuntime chosen = fixture.Profiles.DescribeDraftRuntime("codex", "sonnet", "medium");

        Assert.Equal(new ProfileRuntime("Claude Code", "Agent default", "Model default"), unchosen);
        Assert.Equal(new ProfileRuntime("Codex", "Sonnet", "Medium"), chosen);
        Assert.Equal(0, fixture.ModelCatalog.ProbeCount);
    }

    /// <summary>A chosen id the cache has no name for reads as the raw id on a draft.</summary>
    [Fact]
    public void DescribeDraftRuntime_ChosenIdsNotCached_ReadAsTheRawIds()
    {
        using ProfileFixture fixture = new();

        ProfileRuntime chosen = fixture.Profiles.DescribeDraftRuntime(null, "haiku", "low");

        Assert.Equal(new ProfileRuntime("Claude Code", "haiku", "low"), chosen);
    }

    /// <summary>The Adapter's default model is learned by probing once and then peeking what the probe reported.</summary>
    [Fact]
    public async Task DefaultModelAsync_AfterProbe_ReturnsTheReportedDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        fixture.ModelCatalog.DefaultModel = "sonnet";

        string? model = await fixture.Profiles.DefaultModelAsync("codex", ct);

        Assert.Equal("sonnet", model);
        Assert.Equal(1, fixture.ModelCatalog.ProbeCount);
    }

    /// <summary>A probe that fails yields no default instead of an exception, so adding a draft never breaks on an Adapter that cannot start.</summary>
    [Fact]
    public async Task DefaultModelAsync_ProbeFails_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        fixture.ModelCatalog.ThrowOnProbe = true;
        fixture.ModelCatalog.DefaultModel = "sonnet";

        string? model = await fixture.Profiles.DefaultModelAsync("codex", ct);

        Assert.Null(model);
    }

    /// <summary>An Agent hired on the Adapter's default model keeps its role's tier chip when the Adapter still reports that default (no tier map names a model); a different stored model shows none.</summary>
    [Theory]
    [InlineData("sonnet", ModelTier.Balanced)]
    [InlineData("opus", null)]
    public async Task BuildAsync_StoredModelIsTheAdapterDefault_KeepsTheTier(string adapterDefault, ModelTier? expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", withRole: true, model: "sonnet");
        fixture.ModelCatalog.DefaultModel = adapterDefault;

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(expected, view.Profile.Tier);
    }

    /// <summary>An Agent that chose nothing reads as the default Adapter, with the Agent card's own default wording.</summary>
    [Fact]
    public async Task BuildAsync_NoChoices_ReadsDefaults()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(new ProfileRuntime("Claude Code", "Agent default", "Model default"), view.Profile.Runtime);
    }

    /// <summary>A default the catalog has cached reads as its display name marked "(default)".</summary>
    [Fact]
    public async Task BuildAsync_NoChoicesDefaultsCached_ShowsDefaultNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.ModelCatalog.Runtime = new RuntimeNames("Opus 5.5", "High");
        fixture.AddAgent("Ada", "Lead");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(new ProfileRuntime("Claude Code", "Opus 5.5 (default)", "High (default)"), view.Profile.Runtime);
        Assert.Equal(0, fixture.ModelCatalog.ProbeCount);
    }

    /// <summary>A chosen Model and Effort read as the display names the catalog has cached, not as ids.</summary>
    [Fact]
    public async Task BuildAsync_ChosenIdsCached_ShowsDisplayNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.ModelCatalog.Runtime = new RuntimeNames("Sonnet", "Medium");
        fixture.AddAgent("Ada", "Lead", model: "sonnet", effort: "medium");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(new ProfileRuntime("Claude Code", "Sonnet", "Medium"), view.Profile.Runtime);
        Assert.Equal(("sonnet", "medium"), (fixture.ModelCatalog.PeeksAsked[0].Model, fixture.ModelCatalog.PeeksAsked[0].Effort));
    }

    /// <summary>An uncached chosen id falls back to the raw id, and an uncached default to the Agent card's wording.</summary>
    [Fact]
    public async Task BuildAsync_CacheEmpty_FallsBackToIdsAndDefaultWording()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", model: "sonnet");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(new ProfileRuntime("Claude Code", "sonnet", "Model default"), view.Profile.Runtime);
    }

    /// <summary>An empty cache starts a background warm-up against the Persona's own Adapter, without making the build wait for it.</summary>
    [Fact]
    public async Task BuildAsync_CacheEmpty_WarmsTheCatalogInTheBackground()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.ModelCatalog.NeverCompletes = true;
        fixture.AddAgent("Ada", "Lead", adapter: "codex");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);
        await fixture.ModelCatalog.FirstProbe.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.NotNull(view);
        Assert.Equal(["codex"], fixture.ModelCatalog.AdaptersProbed);
    }

    /// <summary>Names that are already cached start no warm-up.</summary>
    [Fact]
    public async Task BuildAsync_NamesCached_DoesNotWarm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.ModelCatalog.Runtime = new RuntimeNames("Opus 5.5", "High");
        fixture.AddAgent("Ada", "Lead");

        await fixture.Profiles.BuildAsync("Ada", null, ct);
        await Task.Yield();

        Assert.Empty(fixture.ModelCatalog.AdaptersProbed);
    }

    /// <summary>
    /// A cold cache hands the warm-up back on the view, a second open while it runs joins the same one
    /// rather than probing again, and once it ends the reread shows the names it filled.
    /// </summary>
    [Fact]
    public async Task BuildAsync_CacheEmpty_HandsBackTheWarmUpAndRereadShowsFilledNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ModelCatalog.ModelsGate = gate;
        fixture.AddAgent("Ada", "Lead");

        AgentProfileView? first = await fixture.Profiles.BuildAsync("Ada", null, ct);
        AgentProfileView? second = await fixture.Profiles.BuildAsync("Ada", null, ct);
        fixture.ModelCatalog.Runtime = new RuntimeNames("Opus 5.5", "High");
        gate.SetResult();
        Assert.NotNull(first?.RuntimeWarmUp);
        await first.RuntimeWarmUp.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Same(first.RuntimeWarmUp, second?.RuntimeWarmUp);
        Assert.Equal(1, fixture.ModelCatalog.ProbeCount);
        Assert.Equal(new ProfileRuntime("Claude Code", "Opus 5.5 (default)", "High (default)"), fixture.Profiles.RereadRuntime("Ada"));
    }

    /// <summary>Names that are already cached hand no warm-up back, so the popover has nothing to wait for.</summary>
    [Fact]
    public async Task BuildAsync_NamesCached_HandsBackNoWarmUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.ModelCatalog.Runtime = new RuntimeNames("Opus 5.5", "High");
        fixture.AddAgent("Ada", "Lead");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Null(view.RuntimeWarmUp);
    }

    /// <summary>
    /// A warm-up that throws is caught and logged, so it can never surface as an unobserved task
    /// exception, and it frees its Adapter's slot so the next open warms again. The fake throws
    /// synchronously, so the catch and the finally have both run by the time the build returns.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WarmFails_LogsAndRetriesOnNextOpen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.ModelCatalog.ThrowOnProbe = true;
        fixture.AddAgent("Ada", "Lead");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);
        await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal(2, fixture.Logs.AtExactly(LogLevel.Warning).Count);
        Assert.Equal(2, fixture.ModelCatalog.ProbeCount);
    }

    /// <summary>An Adapter that is no longer configured shows the default Adapter, the one the Agent will actually run on.</summary>
    [Fact]
    public async Task BuildAsync_UnconfiguredAdapter_ReadsDefaultAdapter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", adapter: "retired");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal("Claude Code", view.Profile.Runtime?.Adapter);
    }

    /// <summary>The card carries the Agent's title, skills, the role's pool description and tier.</summary>
    [Fact]
    public async Task BuildAsync_Agent_FillsCardFromPersonaAndRole()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", withRole: true);

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal("Ada", view.Profile.Name);
        Assert.Equal("Lead", view.Profile.Title);
        Assert.Equal("Teaches.", view.Profile.Description);
        Assert.Equal(ModelTier.Balanced, view.Profile.Tier);
        Assert.Equal(["lint"], view.Profile.SkillNames);
        Assert.Empty(view.Profile.SetupReasons);
    }

    /// <summary>An Agent with no role of its own has an empty description and no tier chip.</summary>
    [Fact]
    public async Task BuildAsync_NoRole_HasNoTierAndEmptyDescription()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Bo", "Helper");

        AgentProfileView? view = await fixture.Profiles.BuildAsync("Bo", null, ct);

        Assert.NotNull(view);
        Assert.Null(view.Profile.Tier);
        Assert.Equal(string.Empty, view.Profile.Description);
    }

    /// <summary>The Manager flag is true only for the manager of the Team asked about.</summary>
    [Fact]
    public async Task BuildAsync_Manager_OnlyForThatTeamsManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", "Garden");
        fixture.AddAgent("Bo", "Dev", "Garden");
        fixture.AddTeam("Garden", "ada", "Ada", "Bo");

        AgentProfileView? manager = await fixture.Profiles.BuildAsync("Ada", "Garden", ct);
        AgentProfileView? other = await fixture.Profiles.BuildAsync("Bo", "Garden", ct);
        AgentProfileView? noTeam = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.True(manager?.Manager);
        Assert.False(other?.Manager);
        Assert.False(noTeam?.Manager);
    }

    /// <summary>Membership is Member only for an Agent in the Team asked about, and None otherwise.</summary>
    [Fact]
    public async Task BuildAsync_Membership_MemberOnlyWithATeamTheyAreIn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", "Garden");
        fixture.AddAgent("Cy", "Dev");
        fixture.AddTeam("Garden", null, "Ada");

        AgentProfileView? member = await fixture.Profiles.BuildAsync("Ada", "Garden", ct);
        AgentProfileView? outsider = await fixture.Profiles.BuildAsync("Cy", "Garden", ct);
        AgentProfileView? noTeam = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.Equal(ProfileMembership.Member, member?.Membership);
        Assert.Equal(ProfileMembership.None, outsider?.Membership);
        Assert.Equal(ProfileMembership.None, noTeam?.Membership);
    }

    /// <summary>An Agent that never connected has no agent id and the Offline state; one that is online shows Online.</summary>
    [Fact]
    public async Task BuildAsync_State_ResolvedFromGateway_AndAgentIdFromDirectory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead");
        fixture.AddAgent("Bo", "Dev");
        User ada = await fixture.AddAgentAsync("Ada", ct);
        fixture.Gateway.SetOnline(ada.Id);

        AgentProfileView? online = await fixture.Profiles.BuildAsync("Ada", null, ct);
        AgentProfileView? never = await fixture.Profiles.BuildAsync("Bo", null, ct);

        Assert.Equal(PersonaState.Online, online?.Profile.State);
        Assert.Equal(ada.Id, online?.AgentId);
        Assert.Null(never?.AgentId);
        Assert.NotEqual(PersonaState.Online, never?.Profile.State);
    }
}
