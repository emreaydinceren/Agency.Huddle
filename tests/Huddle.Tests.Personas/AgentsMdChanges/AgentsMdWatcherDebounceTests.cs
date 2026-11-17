using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Helpers;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins the debounce end to end through <see cref="AgentsMdWatcher"/> (AgentsMdChanges Spec §8.6, O-10): a real ledger, the real
/// Helper, a real dispatcher and a fake adapter process, all on one <see cref="FiringTimeProvider"/>. Ticks are driven by hand.
/// A negative is read from the clock's armed timers, which the dispatcher arms for every enqueue, and from the fake process.
/// </summary>
public sealed class AgentsMdWatcherDebounceTests : IDisposable
{
    private const string Home = "Home/AGENTS.md";
    private const string Wide = "AGENTS.md";

    private readonly TempDataDir dir = new();
    private readonly FiringTimeProvider clock = new();
    private readonly FakeHelperProcessRunner process = new();
    private readonly RecordingFeedPublisher feed = new();
    private readonly FakeTeamCatalog catalog = new();
    private readonly IOptions<TeamOptions> options;
    private readonly HelperBudget budget;
    private readonly HelperSettingsStore settingsStore;
    private readonly Lazy<AgentsMdChangeHelper> helper;
    private readonly Lazy<HelperDispatcher> dispatcher;
    private readonly AgentsMdLedger ledger;
    private readonly AgentsMdWatcher watcher;
    private int offers;

    /// <summary>Wires the install; the Helper and the dispatcher are built on first use, because each needs the ledger.</summary>
    public AgentsMdWatcherDebounceTests()
    {
        this.options = this.dir.Options(team =>
        {
            team.Acp.Enabled = true;
            team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["adapter.js"], UsesToolNamePrefix = false }];
        });
        FakePromptSource prompts = new();
        prompts.SetOverride("helper.agentsMdChange.system", "You judge.");
        prompts.SetOverride("helper.agentsMdChange.edit", "{{newText}}");
        this.settingsStore = new HelperSettingsStore(this.options);
        HelperSettingsResolver settings = new(this.settingsStore, this.options, NullLogger<HelperSettingsResolver>.Instance);
        HelperUsageStore usage = new(this.options);
        this.budget = new HelperBudget(usage, this.clock);
        HelperStatusStore status = new(this.options);
        this.helper = new Lazy<AgentsMdChangeHelper>(this.NewHelper);
        this.dispatcher = new Lazy<HelperDispatcher>(() =>
        {
            HelperRunner runner = new(this.process, new AdapterCatalog(this.options), prompts, this.budget, this.options, NullLogger<HelperRunner>.Instance, this.clock);
            HelperFeed helperFeed = new(this.feed, status, this.budget, new HelperRegistry([this.helper.Value]), settings, NullLogger<HelperFeed>.Instance);
            return HelperDispatcherFactory.Create(
                [new HelperInvoker<AgentsMdChangeInput, AgentsMdChangeVerdict>(this.helper.Value, runner, NullLogger.Instance)],
                settings,
                status,
                this.options,
                this.clock,
                helperFeed);
        });
        this.ledger = new AgentsMdLedger(() => this.dispatcher.Value, this.OfferedHelper, settings, this.budget, this.options, NullLogger<AgentsMdLedger>.Instance, this.clock);
        this.watcher = new AgentsMdWatcher(this.ledger, this.catalog, this.options, NullLogger<AgentsMdWatcher>.Instance, this.clock);
        this.catalog.Teams = [new TeamSummary("Home", [], [], true)];
    }

    /// <summary>Whether the first request for the Helper throws, as a failing registry would.</summary>
    private bool ThrowOnFirstOffer { get; set; }

    private string TeamsRoot => Path.Combine(this.options.Value.DataDir, this.options.Value.Teams.Dir);

    /// <inheritdoc />
    public void Dispose()
    {
        this.watcher.Dispose();
        if (this.dispatcher.IsValueCreated)
        {
            this.dispatcher.Value.Dispose();
        }

        this.dir.Dispose();
    }

    /// <summary>Builds the Helper over the ledger; called only after the constructor, when the ledger exists.</summary>
    private AgentsMdChangeHelper NewHelper() =>
        new(this.ledger, this.feed, this.options, NullLogger<AgentsMdChangeHelper>.Instance);

    /// <summary>The ledger's Helper factory; it throws once when the test asks.</summary>
    private AgentsMdChangeHelper OfferedHelper() =>
        this.ThrowOnFirstOffer && Interlocked.Increment(ref this.offers) == 1
            ? throw new InvalidOperationException("The Helper registry is unavailable.")
            : this.helper.Value;

    /// <summary>Writes a level file stamped with the clock's time, then moves the clock on one second so the next save differs.</summary>
    /// <param name="relative">The path below the Teams root.</param>
    /// <param name="text">The text.</param>
    private void Save(string relative, string text)
    {
        string path = Path.Combine(this.TeamsRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.TeamsRoot);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, this.clock.GetUtcNow().UtcDateTime);
        this.clock.Advance(TimeSpan.FromSeconds(1));
    }

    /// <summary>The hash the ledger uses for a text.</summary>
    /// <param name="text">The text.</param>
    private static string HashOf(string text) => AgentsMdBody.Parse(text).Hash;

    /// <summary>Records the first text of each file as its head and edits it, so the edit is pending as of now.</summary>
    /// <param name="ct">Cancels the ticks.</param>
    /// <param name="files">The files.</param>
    private async Task HeadThenEditAsync(CancellationToken ct, params string[] files)
    {
        foreach (string file in files)
        {
            this.Save(file, "Head rule.\n");
        }

        await this.watcher.TickAsync(ct);
        foreach (string file in files)
        {
            this.Save(file, "Head rule.\n\nAnother rule.\n");
        }

        await this.watcher.TickAsync(ct);
    }

    /// <summary>Puts the Helper at its cap of one call, which is spent, so it reads as paused.</summary>
    private void PauseTheHelper()
    {
        this.settingsStore.Save(AgentsMdChangeHelper.HelperId, new HelperSettingsRow(true, null, null, null, 1));
        Assert.True(this.budget.TryReserve(AgentsMdChangeHelper.HelperId, 1));
    }

    /// <summary>While the file keeps changing nothing is judged; once it is quiet one judgement covers the net change.</summary>
    [Fact]
    public async Task SaveLoop_NoCallWhileSaving_OneCallAfterQuiet_OnTheNetChange()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Save(Home, "Head rule.\n");
        await this.watcher.TickAsync(ct);
        string last = string.Empty;
        for (int save = 0; save < 5; save++)
        {
            last = $"Head rule.\n\nEdit {save}.\n";
            this.Save(Home, last);
            this.clock.Advance(TimeSpan.FromSeconds(20));
            await this.watcher.TickAsync(ct);
        }

        int armedWhileSaving = this.clock.ScheduledTimerCount;
        this.clock.Advance(TimeSpan.FromSeconds(61));
        await this.watcher.TickAsync(ct);
        int armedAfterQuiet = this.clock.ScheduledTimerCount;
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.process.Calls == 1, ct, TestBudgets.Slow);
        await this.WaitForTheRunToEndAsync(ct);
        await this.watcher.TickAsync(ct);

        Assert.Equal(0, armedWhileSaving);
        Assert.Equal(1, armedAfterQuiet);
        Assert.Equal(0, this.clock.ScheduledTimerCount);
        Assert.Equal(1, this.process.Calls);
        Assert.Equal(HashOf(last), Assert.Single(this.ledger.PendingEdits()).Hash);
        Assert.Equal(last.TrimEnd(), this.process.Requests[0].Prompt);
    }

    /// <summary>
    /// Waits until the Helper run has ended. The runner arms a timeout on the clock for as long as a call is out, so a call
    /// seen by the fake process is not yet an idle clock: the timer is gone only when the run is over.
    /// </summary>
    /// <param name="ct">Cancels the wait.</param>
    private async Task WaitForTheRunToEndAsync(CancellationToken ct) =>
        await Eventually.UntilAsync(() => this.clock.ScheduledTimerCount == 0, ct, TestBudgets.Slow);

    /// <summary>A save 50 seconds into the wait starts it again: the file is not judged 61 seconds after the first save.</summary>
    [Fact]
    public async Task EachSaveRestartsTheDebounce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.HeadThenEditAsync(ct, Home);
        this.clock.Advance(TimeSpan.FromSeconds(50));
        await this.watcher.TickAsync(ct);
        this.Save(Home, "Head rule.\n\nAnother rule, reworded.\n");
        await this.watcher.TickAsync(ct);
        this.clock.Advance(TimeSpan.FromSeconds(50));
        await this.watcher.TickAsync(ct);
        int armedInsideTheNewWindow = this.clock.ScheduledTimerCount;

        this.clock.Advance(TimeSpan.FromSeconds(11));
        await this.watcher.TickAsync(ct);

        Assert.Equal(0, armedInsideTheNewWindow);
        Assert.Equal(1, this.clock.ScheduledTimerCount);
    }

    /// <summary>With the Helper at its cap, a quiet edit still reaches the dispatcher, which tells the Feed once and calls nothing.</summary>
    [Fact]
    public async Task PausedHelper_PostsItsFeedItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.PauseTheHelper();
        await this.HeadThenEditAsync(ct, Home);
        this.clock.Advance(TimeSpan.FromSeconds(61));

        await this.watcher.TickAsync(ct);
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.feed.Posts.Count == 1, ct, TestBudgets.Slow);

        Assert.Equal(FeedSourceType.HelperPaused, Assert.Single(this.feed.Posts).SourceType);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>A paused Helper's edit is queued once; the next tick inside the spacing queues nothing more.</summary>
    [Fact]
    public async Task PausedHelper_SecondTickInsideSpacing_NoSecondEnqueue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.PauseTheHelper();
        await this.HeadThenEditAsync(ct, Home);
        this.clock.Advance(TimeSpan.FromSeconds(61));
        await this.watcher.TickAsync(ct);
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.feed.Posts.Count == 1, ct, TestBudgets.Slow);

        this.clock.Advance(TimeSpan.FromSeconds(10));
        await this.watcher.TickAsync(ct);

        Assert.Equal(0, this.clock.ScheduledTimerCount);
        Assert.Single(this.feed.Posts);
    }

    /// <summary>A Helper that cannot be reached for one file does not stop the next file from being offered, and the first is offered again.</summary>
    [Fact]
    public async Task OfferThrows_OtherFilesStillOffered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.HeadThenEditAsync(ct, Home, Wide);
        this.clock.Advance(TimeSpan.FromSeconds(61));
        this.ThrowOnFirstOffer = true;

        await this.watcher.TickAsync(ct);
        int armedAfterTheFailure = this.clock.ScheduledTimerCount;
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.process.Calls == 1, ct, TestBudgets.Slow);
        await this.watcher.TickAsync(ct);
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.process.Calls == 2, ct, TestBudgets.Slow);

        Assert.Equal(1, armedAfterTheFailure);
        Assert.Equal(2, this.process.Calls);
    }
}
