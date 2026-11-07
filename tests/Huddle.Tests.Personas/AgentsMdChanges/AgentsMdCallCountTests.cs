using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Conformance;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Helpers;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins the owner's requirement O-10 as call counts (AgentsMdChanges Spec §8.6): saving AGENTS.md in a loop, with or without Turns, costs
/// a bounded number of Helper calls. A real ledger, <see cref="HelperDispatcher"/>, <see cref="AgentsMdChangeHelper"/>,
/// <see cref="AgentsMdWatcher"/> and (for the Turn case) <see cref="AgentsMdTurnHook"/> run on one <see cref="FiringTimeProvider"/>
/// against a fake adapter process that counts the calls. Ticks are driven by hand.
/// </summary>
public sealed class AgentsMdCallCountTests : IDisposable
{
    private const string Home = "Home/AGENTS.md";
    private const string RoomId = "room-1";

    private static readonly TurnHookScope Scope = new("Vera", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    private readonly TempDataDir dir = new();
    private readonly FiringTimeProvider clock = new();
    private readonly FakeHelperProcessRunner process = new();
    private readonly RecordingFeedPublisher feed = new();
    private readonly FakeTeamCatalog catalog = new();
    private readonly IOptions<TeamOptions> options;
    private readonly PersonaStore personas;
    private readonly Lazy<AgentsMdChangeHelper> helper;
    private readonly Lazy<HelperDispatcher> dispatcher;
    private readonly AgentsMdLedger ledger;
    private readonly AgentsMdWatcher watcher;

    /// <summary>Wires the install: the Helper and the dispatcher are built on first use, because each needs the ledger.</summary>
    public AgentsMdCallCountTests()
    {
        this.options = this.dir.Options(team =>
        {
            team.Acp.Enabled = true;
            team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["adapter.js"], UsesToolNamePrefix = false }];
            team.AgentsMd.NoticeWaitSeconds = 0;
        });
        FakePromptSource prompts = new();
        prompts.SetOverride("helper.agentsMdChange.system", "You judge.");
        prompts.SetOverride("helper.agentsMdChange.edit", "{{newText}}");
        this.process.Text = MockAdapterFixture.VerdictJson("Head rule.");
        HelperSettingsStore settingsStore = new(this.options);
        HelperSettingsResolver settings = new(settingsStore, this.options, NullLogger<HelperSettingsResolver>.Instance);
        HelperBudget budget = new(new HelperUsageStore(this.options), this.clock);
        HelperStatusStore status = new(this.options);
        this.helper = new Lazy<AgentsMdChangeHelper>(this.NewHelper);
        this.dispatcher = new Lazy<HelperDispatcher>(() =>
        {
            HelperRunner runner = new(this.process, new AdapterCatalog(this.options), prompts, budget, this.options, NullLogger<HelperRunner>.Instance, this.clock);
            HelperFeed helperFeed = new(this.feed, status, budget, new HelperRegistry([this.helper.Value]), settings, NullLogger<HelperFeed>.Instance);
            return new HelperDispatcher(
                [new HelperInvoker<AgentsMdChangeInput, AgentsMdChangeVerdict>(this.helper.Value, runner, NullLogger.Instance)],
                settings,
                status,
                this.options,
                NullLogger<HelperDispatcher>.Instance,
                this.clock,
                helperFeed);
        });
        this.ledger = new AgentsMdLedger(() => this.dispatcher.Value, () => this.helper.Value, settings, budget, this.options, NullLogger<AgentsMdLedger>.Instance, this.clock);
        this.watcher = new AgentsMdWatcher(this.ledger, this.catalog, this.options, NullLogger<AgentsMdWatcher>.Instance, this.clock);
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];

        AgentPaths paths = new(this.options);
        Directory.CreateDirectory(Path.Combine(paths.DefinitionsRoot, "Vera"));
        File.WriteAllText(
            Path.Combine(paths.DefinitionsRoot, "Vera", "Vera.md"),
            "---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nteams: ['Home']\n---\nYou help.");
        this.personas = new PersonaStore(
            paths,
            new PersonaModelStore(this.options),
            new PersonaEffortStore(this.options),
            new PersonaWorkModeStore(this.options),
            NullLogger<PersonaStore>.Instance);
    }

    private string TeamsRoot => Path.Combine(this.options.Value.DataDir, this.options.Value.Teams.Dir);

    /// <inheritdoc />
    public void Dispose()
    {
        this.watcher.Dispose();
        if (this.dispatcher.IsValueCreated)
        {
            this.dispatcher.Value.Dispose();
        }

        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>
    /// Saves every 3 seconds for 10 minutes with no Turn: the 60-second debounce restarts at every save, so nothing is judged while the
    /// loop runs and one call covers the net change once it has been quiet. Observed 1 call after about 11.5 minutes of fake time;
    /// with <c>DebounceSeconds</c> 0 it is 3.
    /// </summary>
    [Fact]
    public async Task SavesEvery3Seconds_For10Minutes_ExactlyOneCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.RecordHeadAsync(ct);

        for (int save = 0; save < 200; save++)
        {
            this.Save($"Head rule.\n\nEdit {save}.\n");
            await this.StepAsync(TimeSpan.FromSeconds(2), ct);
        }

        await this.QuietAsync(TimeSpan.FromSeconds(90), ct);

        Assert.Equal(1, this.process.Calls);
    }

    /// <summary>
    /// Saves every 90 seconds for 30 minutes with no Turn: each save outlasts the debounce, so the 300-second spacing is what limits the
    /// calls to one per five minutes. Observed 6 calls, under the cap of 7; with <c>MinJudgeSeconds</c> 0 it is 20.
    /// </summary>
    [Fact]
    public async Task SavesEvery90Seconds_For30Minutes_AtMostSevenCalls()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.RecordHeadAsync(ct);

        for (int save = 0; save < 20; save++)
        {
            this.Save($"Head rule.\n\nEdit {save}.\n");
            await this.StepAsync(TimeSpan.FromSeconds(2), ct);
            await this.QuietAsync(TimeSpan.FromSeconds(87), ct);
        }

        await this.QuietAsync(TimeSpan.FromSeconds(90), ct);

        Assert.InRange(this.process.Calls, 1, 7);
    }

    /// <summary>
    /// Saves and a Turn every 30 seconds for 10 minutes: the spacing keeps the calls to one per five minutes, and every Turn still hears
    /// the change at once, with the comparison when no summary is ready. Observed 2 calls and 20 Turns, each told; with
    /// <c>MinJudgeSeconds</c> 0 the calls are 20.
    /// </summary>
    [Fact]
    public async Task SavesAndMessagesEvery30Seconds_For10Minutes_AtMostThreeCalls()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.RecordHeadAsync(ct);
        AgentsMdTurnHook hook = this.NewHook();
        TurnHookPipeline pipeline = TurnHookPipeline.ForPersona(Scope, [hook], NullLogger.Instance);
        Dictionary<string, string?> state = new(StringComparer.Ordinal)
        {
            [hook.Name] = await hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct),
        };

        int messages = 0;
        int turns = 0;
        int told = 0;
        for (int save = 0; save < 20; save++)
        {
            this.Save($"Head rule.\n\nEdit {save}.\n");
            await this.watcher.TickAsync(ct);
            messages++;
            TurnHookBeforeResult before = await pipeline.BeforeTurnAsync(RoomId, state, ct, CancellationToken.None);
            await pipeline.AfterTurnAsync(RoomId, state, before, [], ct);
            turns++;
            told += before.Blocks.Count > 0 ? 1 : 0;
            await this.StepAsync(TimeSpan.FromSeconds(2), ct);
            await this.QuietAsync(TimeSpan.FromSeconds(27), ct);
        }

        await this.QuietAsync(TimeSpan.FromSeconds(90), ct);

        Assert.Equal(messages, turns);
        Assert.Equal(messages, told);
        Assert.InRange(this.process.Calls, 1, 3);
    }

    /// <summary>Builds the Helper over the ledger; called only after the constructor, when the ledger exists.</summary>
    private AgentsMdChangeHelper NewHelper() =>
        new(this.ledger, this.feed, this.options, NullLogger<AgentsMdChangeHelper>.Instance);

    /// <summary>The hook over the real Scopes, ledger and source.</summary>
    private AgentsMdTurnHook NewHook()
    {
        AgentsMdScopes scopes = new(this.personas, this.catalog, this.options);
        AgentsMdNoticeSource source = new(
            this.ledger,
            new AgentsMdNoticeRenderer(new FakePromptSource(), this.options.Value.AgentsMd),
            this.options.Value.AgentsMd,
            NullLogger<AgentsMdNoticeSource>.Instance);
        return new AgentsMdTurnHook(scopes, source, NullLogger<AgentsMdTurnHook>.Instance);
    }

    /// <summary>Writes the first text of the file and lets the ledger record it as the head, so the next save is a pending edit.</summary>
    /// <param name="ct">Cancels the tick.</param>
    private async Task RecordHeadAsync(CancellationToken ct)
    {
        this.Save("Head rule.\n");
        await this.watcher.TickAsync(ct);
        this.clock.Advance(TimeSpan.FromSeconds(10));
    }

    /// <summary>Writes the file stamped with the clock's time, then moves the clock on one second so the next save differs.</summary>
    /// <param name="text">The text.</param>
    private void Save(string text)
    {
        string path = Path.Combine(this.TeamsRoot, Home);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, this.clock.GetUtcNow().UtcDateTime);
        this.clock.Advance(TimeSpan.FromSeconds(1));
    }

    /// <summary>Runs one watcher tick, then moves the clock on by <paramref name="span"/>; a call the tick queued is awaited to its end.</summary>
    /// <param name="span">At least the Helper's wait (2 seconds), so a queued judgement fires inside it.</param>
    /// <param name="ct">Cancels the tick and the wait.</param>
    private async Task StepAsync(TimeSpan span, CancellationToken ct)
    {
        await this.watcher.TickAsync(ct);
        bool queued = this.clock.ScheduledTimerCount > 0;
        int before = this.process.Calls;
        this.clock.Advance(span);
        if (queued)
        {
            await this.SettleAsync(before, ct);
        }
    }

    /// <summary>Lets the time pass in steps of ten seconds, each with a tick, as the watcher's poll would.</summary>
    /// <param name="span">How long.</param>
    /// <param name="ct">Cancels the ticks.</param>
    private async Task QuietAsync(TimeSpan span, CancellationToken ct)
    {
        TimeSpan left = span;
        TimeSpan chunk = TimeSpan.FromSeconds(10);
        while (left > TimeSpan.Zero)
        {
            TimeSpan step = left < chunk ? left : chunk;
            await this.StepAsync(step < TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : step, ct);
            left -= step;
        }
    }

    /// <summary>Polls until the call the tick queued has run and its verdict is committed, for at most <see cref="TestBudgets.Short"/>.</summary>
    /// <param name="before">The call count before the clock moved.</param>
    /// <param name="ct">Cancels the poll.</param>
    private async Task SettleAsync(int before, CancellationToken ct)
    {
        using CancellationTokenSource window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        window.CancelAfter(TestBudgets.Short);
        try
        {
            while (this.process.Calls <= before || this.ledger.PendingEdits().Count > 0)
            {
                await Task.Delay(5, window.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // No call came: the Helper is paused or the offer was spaced out. The caller reads the count, which is what the test pins.
        }
    }
}
