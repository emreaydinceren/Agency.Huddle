using System.Collections.Concurrent;
using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdLedger.TryStartJudgement"/> (AgentsMdChanges Spec §8.6): the debounce, the one run per pair, the
/// spacing, the Helper and Adapters gates, the paused Helper, the two bodies that settle without a call, and the dispatcher
/// key. The Helper is a registered fake whose invoker records each call it receives. The ledger reads a
/// <see cref="ManualTimeProvider"/> that the test advances, because these rules only read the time; the dispatcher, which needs
/// its timers fired, has its own <see cref="FiringTimeProvider"/>.
/// </summary>
public sealed class AgentsMdLedgerJudgementTests : IDisposable
{
    private const string Relative = "Home/AGENTS.md";
    private const string Head = "Head rule.\n";
    private const string Edited = "Head rule.\n\nAnother rule.\n";

    private readonly TempDataDir dir = new();
    private readonly ManualTimeProvider ledgerClock = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly FiringTimeProvider dispatcherClock = new();
    private readonly IOptions<TeamOptions> options;
    private readonly HelperBudget budget;
    private readonly FakeAgentsMdHelper helper;
    private readonly RecordingInvoker invoker;
    private readonly HelperDispatcher dispatcher;
    private readonly AgentsMdLedger ledger;
    private int writes;
    private int drains;
    private Exception? dispatcherFault;

    /// <summary>Wires the ledger, a registered fake Helper and a real dispatcher over a temp data folder.</summary>
    public AgentsMdLedgerJudgementTests()
        : this(helperEnabled: true, adaptersOn: true)
    {
    }

    private AgentsMdLedgerJudgementTests(bool helperEnabled, bool adaptersOn)
    {
        this.options = this.dir.Options(team => team.Acp.Enabled = adaptersOn);
        HelperSettingsResolver settings = new(new HelperSettingsStore(this.options), this.options, NullLogger<HelperSettingsResolver>.Instance);
        this.budget = new HelperBudget(new HelperUsageStore(this.options), this.ledgerClock);
        this.helper = new FakeAgentsMdHelper(helperEnabled);
        this.invoker = new RecordingInvoker(this.helper);
        this.dispatcher = new HelperDispatcher([this.invoker], settings, new HelperStatusStore(this.options), this.options, NullLogger<HelperDispatcher>.Instance, this.dispatcherClock);
        this.ledger = new AgentsMdLedger(() => this.dispatcherFault is { } fault ? throw fault : this.dispatcher, () => this.helper, settings, this.budget, this.options, NullLogger<AgentsMdLedger>.Instance, this.ledgerClock);
    }

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private string LedgerDir => Path.Combine(this.dir.Path, "agents-md-ledger");

    /// <inheritdoc />
    public void Dispose()
    {
        this.dispatcher.Dispose();
        this.invoker.Dispose();
        this.dir.Dispose();
    }

    /// <summary>The file under test.</summary>
    private AgentsMdFile TheFile() => new(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, Relative), Relative);

    /// <summary>Writes the file with a distinct modification time.</summary>
    /// <param name="text">The new text.</param>
    private void Save(string text)
    {
        AgentsMdFile file = this.TheFile();
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath) ?? this.TeamsRoot);
        File.WriteAllText(file.FullPath, text);
        this.writes++;
        File.SetLastWriteTimeUtc(file.FullPath, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc).AddMinutes(this.writes));
    }

    /// <summary>Records <see cref="Head"/> as the head, then saves <paramref name="edit"/> and observes it, so an edit is pending as of now.</summary>
    /// <param name="edit">The edited text.</param>
    private void HeadThenEdit(string edit = Edited)
    {
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());
        this.Save(edit);
        _ = this.ledger.Observe(this.TheFile());
    }

    /// <summary>The hash the ledger uses for a text.</summary>
    /// <param name="text">The file text.</param>
    private static string HashOf(string text) => AgentsMdBody.Parse(text).Hash;

    /// <summary>The ledger's key for the file, read from the name of its one ledger file.</summary>
    private string LedgerKey() => Path.GetFileNameWithoutExtension(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json")));

    /// <summary>
    /// Runs every queued judgement and waits until they have had the chance to reach the invoker: a sentinel on its own key
    /// is queued after them, the dispatcher's clock is moved past every wait, and the sentinel's call is awaited.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        this.drains++;
        string sentinel = "sentinel-" + this.drains;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        int armed = this.dispatcherClock.ScheduledTimerCount;
        int before = this.JudgementCalls().Count;
        this.dispatcherClock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.JudgementCalls().Count >= before + armed, bound.Token);
        this.dispatcher.Enqueue(this.helper.Id, sentinel, new object());
        this.dispatcherClock.Advance(TimeSpan.FromMinutes(1));
        await this.invoker.WaitForAsync(sentinel, bound.Token);
    }

    /// <summary>
    /// Moves the dispatcher's clock past its waits, so every armed timer fires, and says whether
    /// <paramref name="count"/> judgements have reached the invoker. A poll condition: each round fires the timers the last run armed.
    /// </summary>
    /// <param name="count">How many judgement calls are expected.</param>
    private bool JudgementsReachedTheInvoker(int count)
    {
        this.dispatcherClock.Advance(TimeSpan.FromMinutes(1));
        return this.JudgementCalls().Count == count;
    }

    /// <summary>The calls that reached the invoker, without the sentinels.</summary>
    private List<RecordedCall> JudgementCalls() => [.. this.invoker.Calls.Where(static call => !call.Key.StartsWith("sentinel-", StringComparison.Ordinal))];

    /// <summary>An edit left alone for less than the debounce does not start a judgement when no Turn asked.</summary>
    [Fact]
    public async Task Debounced_InsideTheWindow_False()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        this.ledgerClock.Advance(TimeSpan.FromSeconds(59));

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
    }

    /// <summary>An edit left alone for the whole debounce is queued once, with the head and the current text as the pair.</summary>
    [Fact]
    public async Task Debounced_AfterTheWindow_Enqueues_True()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        this.ledgerClock.Advance(TimeSpan.FromSeconds(61));

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        int armedTimers = this.dispatcherClock.ScheduledTimerCount;
        await this.DrainAsync(ct);

        Assert.True(started);
        Assert.Equal(1, armedTimers);
        RecordedCall call = Assert.Single(this.JudgementCalls());
        AgentsMdChangeInput input = Assert.IsType<AgentsMdChangeInput>(call.Input);
        Assert.Equal(HashOf(Head), input.FromHash);
        Assert.Equal(HashOf(Edited), input.ToHash);
        Assert.Equal(Relative, input.RelativePath);
        Assert.Equal(this.ledgerClock.GetUtcNow(), this.ledger.JudgementStarted(Relative));
    }

    /// <summary>A Turn asking starts the judgement at once, with no wait for the debounce.</summary>
    [Fact]
    public async Task TurnAsked_SkipsTheDebounce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.True(started);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>Asking again for a pair that is already running answers true and queues nothing more.</summary>
    [Fact]
    public async Task SamePair_Running_TrueWithoutASecondEnqueue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        _ = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        bool again = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.True(again);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>
    /// A pair that already settled answers false and queues nothing. The pair settles here the one way task 4.2 can reach:
    /// the new body is over <c>MaxLedgerChars</c>, so the ledger settles it with no call.
    /// </summary>
    [Fact]
    public async Task SamePair_Done_False()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.AgentsMd.MaxLedgerChars = Head.Length;
        this.HeadThenEdit();
        Assert.False(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        int revisionsAfterSettling = (this.ledger.Since(Relative, HashOf(Head)) ?? []).Count;

        bool again = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(again);
        Assert.Equal(1, revisionsAfterSettling);
        Assert.Empty(this.JudgementCalls());
    }

    /// <summary>A second judgement of the file inside the spacing is refused even though its debounce has passed.</summary>
    [Fact]
    public async Task InsideSpacing_False()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        this.Save(Edited + "Third rule.\n");
        _ = this.ledger.Observe(this.TheFile());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(120));

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>Once the spacing has passed, the next pair of the file is queued.</summary>
    [Fact]
    public async Task AfterSpacing_Enqueues()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await Eventually.UntilAsync(() => this.JudgementsReachedTheInvoker(1), bound.Token);
        this.Save(Edited + "Third rule.\n");
        _ = this.ledger.Observe(this.TheFile());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(301));

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        await Eventually.UntilAsync(() => this.JudgementsReachedTheInvoker(2), bound.Token);

        Assert.True(started);
        Assert.Equal(2, this.JudgementCalls().Count);
    }

    /// <summary>With the Helper switched off nothing is queued and the answer is false.</summary>
    [Fact]
    public void HelperOff_False_NoEnqueue()
    {
        using AgentsMdLedgerJudgementTests off = new(helperEnabled: false, adaptersOn: true);
        off.HeadThenEdit();

        bool started = off.ledger.TryStartJudgement(off.TheFile(), JudgementReason.TurnAsked);

        Assert.False(started);
        Assert.Equal(0, off.dispatcherClock.ScheduledTimerCount);
        off.dispatcherClock.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(off.JudgementCalls());
    }

    /// <summary>With Adapters switched off nothing is queued and the answer is false.</summary>
    [Fact]
    public void AdaptersOff_False_NoEnqueue()
    {
        using AgentsMdLedgerJudgementTests off = new(helperEnabled: true, adaptersOn: false);
        off.HeadThenEdit();

        bool started = off.ledger.TryStartJudgement(off.TheFile(), JudgementReason.TurnAsked);

        Assert.False(started);
        Assert.Equal(0, off.dispatcherClock.ScheduledTimerCount);
        off.dispatcherClock.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(off.JudgementCalls());
    }

    /// <summary>A paused Helper still gets the input queued, so its run reaches the over-budget path and the Feed hears once, but the answer is false.</summary>
    [Fact]
    public async Task Paused_Enqueues_ButFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int cap = this.helper.Defaults.DailyCap;
        for (int call = 0; call < cap; call++)
        {
            Assert.True(this.budget.TryReserve(this.helper.Id, cap));
        }

        Assert.True(this.budget.IsPaused(this.helper.Id, cap));
        this.HeadThenEdit();

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>An edit that deleted the file settles at once as a comparison: no Helper call, and the answer is false.</summary>
    [Fact]
    public async Task AbsentBody_SettlesAsAComparison_NoCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());
        File.Delete(this.TheFile().FullPath);
        _ = this.ledger.Observe(this.TheFile());

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        Assert.IsType<AgentsMdNotice.Comparison>(revision.Notice);
    }

    /// <summary>A body over <c>MaxLedgerChars</c> settles at once as Unknown: no Helper call, and the answer is false.</summary>
    [Fact]
    public async Task OverMaxLedgerChars_SettlesAsUnknown_NoCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.AgentsMd.MaxLedgerChars = Head.Length;
        this.HeadThenEdit();

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        Assert.IsType<AgentsMdNotice.Unknown>(revision.Notice);
    }

    /// <summary>The dispatcher key is the ledger's hash key (the name of its file), not the path.</summary>
    [Fact]
    public async Task EnqueueKey_IsTheLedgerKey_NotThePath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();

        _ = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        RecordedCall call = Assert.Single(this.JudgementCalls());
        Assert.Equal(this.LedgerKey(), call.Key);
        Assert.NotEqual(Relative, call.Key);
        Assert.Equal(32, call.Key.Length);
    }

    /// <summary>Fills the Helper's daily cap, so it is paused.</summary>
    private void PauseTheHelper()
    {
        int cap = this.helper.Defaults.DailyCap;
        for (int call = 0; call < cap; call++)
        {
            Assert.True(this.budget.TryReserve(this.helper.Id, cap));
        }

        Assert.True(this.budget.IsPaused(this.helper.Id, cap));
    }

    /// <summary>The number of revisions in the ledger file on disk.</summary>
    private int RevisionCount()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json"))));
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "revisions", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetArrayLength();
            }
        }

        return -1;
    }

    /// <summary>Settles <paramref name="text"/> after <see cref="Head"/> and checks it cost no call and became a Comparison.</summary>
    /// <param name="text">The emptied or blank file text.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private async Task AssertSettlesFreeAsync(string text, CancellationToken cancellationToken)
    {
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());
        this.Save(text);
        _ = this.ledger.Observe(this.TheFile());

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(cancellationToken);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        Assert.IsType<AgentsMdNotice.Comparison>(revision.Notice);
    }

    /// <summary>A head whose text was not kept (it went over <c>MaxLedgerChars</c>) cannot be compared, so the edit settles as Unknown with no call (Spec §6.2).</summary>
    [Fact]
    public async Task TextlessHead_SettlesAsUnknown_NoCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.AgentsMd.MaxLedgerChars = 20;
        string longHead = new string('x', 40) + "\n";
        this.Save(longHead);
        _ = this.ledger.Observe(this.TheFile());
        this.Save("Short rule.\n");
        _ = this.ledger.Observe(this.TheFile());

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(longHead)) ?? []);
        Assert.IsType<AgentsMdNotice.Unknown>(revision.Notice);
    }

    /// <summary>A file emptied to nothing settles as a free comparison, with no Helper call (Spec §8.1, §8.6).</summary>
    [Fact]
    public async Task EmptiedFile_SettlesAsAComparison_NoCall()
    {
        await this.AssertSettlesFreeAsync(string.Empty, TestContext.Current.CancellationToken);
    }

    /// <summary>A file holding only whitespace is absent too: a free comparison, no Helper call.</summary>
    [Fact]
    public async Task WhitespaceOnlyFile_SettlesAsAComparison_NoCall()
    {
        await this.AssertSettlesFreeAsync("  \n\n \t\n", TestContext.Current.CancellationToken);
    }

    /// <summary>A file first seen absent has an absent head, so its first text settles as a free comparison and costs no call (Spec §6.2).</summary>
    [Fact]
    public async Task FirstSightAbsent_ThenAFirstText_NoCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = this.ledger.Observe(this.TheFile());
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, string.Empty) ?? []);
        Assert.IsType<AgentsMdNotice.Comparison>(revision.Notice);
    }

    /// <summary>The start time answers for the file's current text: once a newer text is seen, it is null (Spec §6.2).</summary>
    [Fact]
    public void JudgementStarted_NewTextSinceTheJudgement_Null()
    {
        this.HeadThenEdit();
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        Assert.NotNull(this.ledger.JudgementStarted(Relative));
        this.Save(Edited + "Third rule.\n");
        _ = this.ledger.Observe(this.TheFile());

        DateTimeOffset? started = this.ledger.JudgementStarted(Relative);

        Assert.Null(started);
    }

    /// <summary>Once the judged pair is committed there is no judgement of the current text running, so the start time is null.</summary>
    [Fact]
    public void JudgementStarted_AfterACommit_Null()
    {
        this.HeadThenEdit();
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));

        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, new AgentsMdNotice.Unknown()));

        Assert.Null(this.ledger.JudgementStarted(Relative));
    }

    /// <summary>A pair that was judged, reverted and saved again is done: false, and no second call. Reachable only after a revert, because settling clears the pending edit first.</summary>
    [Fact]
    public async Task SamePair_Done_AfterARevert_False()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, new AgentsMdNotice.Unknown()));
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());
        _ = this.ledger.CommitComparison(this.TheFile());
        this.Save(Edited);
        _ = this.ledger.Observe(this.TheFile());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(400));

        bool again = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(again);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>Each save restarts the debounce: a second save 59 seconds before the first one's window ends moves the window.</summary>
    [Fact]
    public async Task LaterSave_RestartsTheDebounce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        this.ledgerClock.Advance(TimeSpan.FromSeconds(59));
        this.Save(Edited + "Third rule.\n");
        _ = this.ledger.Observe(this.TheFile());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(59));

        bool inside = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        await this.DrainAsync(ct);
        Assert.False(inside);
        Assert.Empty(this.JudgementCalls());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(2));

        bool after = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        await this.DrainAsync(ct);

        Assert.True(after);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>The spacing binds a Turn too: a second ask inside it is refused and queues nothing.</summary>
    [Fact]
    public async Task TurnAsked_InsideSpacing_False()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        this.Save(Edited + "Third rule.\n");
        _ = this.ledger.Observe(this.TheFile());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(10));

        bool again = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(again);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>Settling a judgement does not reset the spacing: the next edit's ask, inside it, is still refused.</summary>
    [Fact]
    public async Task AppendDoesNotClearSpacing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, new AgentsMdNotice.Unknown()));
        this.Save(Edited + "Third rule.\n");
        _ = this.ledger.Observe(this.TheFile());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(10));

        bool again = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(again);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>A save the watcher has not yet observed counts as a new save: the debounce restarts and nothing is queued.</summary>
    [Fact]
    public async Task Debounced_DiskMovedSinceLastObserve_RestartsTheDebounce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        this.ledgerClock.Advance(TimeSpan.FromSeconds(61));
        this.Save(Edited + "Unobserved rule.\n");

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.Debounced);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.Empty(this.JudgementCalls());
        AgentsMdPendingEdit pending = Assert.Single(this.ledger.PendingEdits());
        Assert.Equal(HashOf(Edited + "Unobserved rule.\n"), pending.Hash);
        Assert.Equal(this.ledgerClock.GetUtcNow(), pending.ChangedAt);
    }

    /// <summary>A Turn may skip the debounce, so on a stale Observe it judges the text that is on disk.</summary>
    [Fact]
    public async Task TurnAsked_DiskMovedSinceLastObserve_JudgesTheDiskText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        this.Save(Edited + "Unobserved rule.\n");

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.True(started);
        AgentsMdChangeInput input = Assert.IsType<AgentsMdChangeInput>(Assert.Single(this.JudgementCalls()).Input);
        Assert.Equal(HashOf(Edited + "Unobserved rule.\n"), input.ToHash);
    }

    /// <summary>A paused Helper's enqueue records the spacing, so a second ask inside it queues nothing more.</summary>
    [Fact]
    public async Task Paused_SecondAskInsideSpacing_NoSecondEnqueue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.PauseTheHelper();
        this.HeadThenEdit();
        Assert.False(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        this.ledgerClock.Advance(TimeSpan.FromSeconds(10));

        bool again = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(again);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>After Continue lifts the pause, the spacing from the paused enqueue still holds: one call in the window, then the next after it.</summary>
    [Fact]
    public async Task Paused_AfterContinue_OneCallInTheWindow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.PauseTheHelper();
        this.HeadThenEdit();
        Assert.False(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        this.budget.Grant(this.helper.Id, 5);
        Assert.False(this.budget.IsPaused(this.helper.Id, this.helper.Defaults.DailyCap));
        this.ledgerClock.Advance(TimeSpan.FromSeconds(10));

        bool inside = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);
        Assert.False(inside);
        Assert.Single(this.JudgementCalls());
        this.ledgerClock.Advance(TimeSpan.FromSeconds(300));

        bool after = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.True(after);
        Assert.Equal(2, this.JudgementCalls().Count);
    }

    /// <summary>A failing enqueue never throws into the caller's Turn: it gives the reservation back and answers false, so the next ask can try again.</summary>
    [Fact]
    public async Task Enqueue_Throws_RollsBack_AndReturnsFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        this.dispatcherFault = new IOException("The store is locked.");

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        this.dispatcherFault = null;

        Assert.False(started);
        Assert.Null(this.ledger.JudgementStarted(Relative));
        Assert.True(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await this.DrainAsync(ct);
        Assert.Single(this.JudgementCalls());
    }

    /// <summary>A loop of oversize saves settled with no call stays within <c>MaxRevisionsKept</c>, like every other settling.</summary>
    [Fact]
    public void Settle_OversizeLoop_StaysWithinMaxRevisions()
    {
        this.options.Value.AgentsMd.MaxLedgerChars = Head.Length;
        this.options.Value.AgentsMd.MaxRevisionsKept = 3;
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());

        for (int round = 0; round < 6; round++)
        {
            this.Save(Head + "A rule that is far over the limit, number " + round + ".\n");
            _ = this.ledger.Observe(this.TheFile());
            Assert.False(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        }

        Assert.Equal(3, this.RevisionCount());
    }

    /// <summary>Settling with no call completes a waiter for the new head, like any other settling.</summary>
    [Fact]
    public async Task Settle_CompletesAWaiter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        this.options.Value.AgentsMd.MaxLedgerChars = Head.Length;
        this.HeadThenEdit();
        Task wait = this.ledger.WaitForHeadAsync(Relative, HashOf(Edited), this.ledgerClock.GetUtcNow().AddHours(1), bound.Token);
        Assert.False(wait.IsCompleted);

        Assert.False(this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked));
        await wait.WaitAsync(bound.Token);

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>One call that reached the invoker.</summary>
    /// <param name="Key">The dispatcher key.</param>
    /// <param name="Input">The input.</param>
    private sealed record RecordedCall(string Key, object Input);

    /// <summary>A registered Helper whose only job is to have an id, defaults and an invoker.</summary>
    private sealed class FakeAgentsMdHelper(bool enabled) : IHelperAgent
    {
        /// <inheritdoc />
        public HelperId Id { get; } = new("AgentsMdChange");

        /// <inheritdoc />
        public string DisplayName => "AGENTS.md changes";

        /// <inheritdoc />
        public string Description => "A fake.";

        /// <inheritdoc />
        public IReadOnlyList<string> PromptKeys { get; } = [];

        /// <inheritdoc />
        public HelperDefaults Defaults { get; } = new(enabled, null, null, null, DailyCap: 2, WaitSeconds: 1);
    }

    /// <summary>Records each call and lets a test wait for the call with a given key.</summary>
    private sealed class RecordingInvoker(IHelperAgent helper) : IHelperInvoker, IDisposable
    {
        private readonly SemaphoreSlim arrived = new(0);

        /// <summary>The calls so far, oldest first.</summary>
        public ConcurrentQueue<RecordedCall> Calls { get; } = new();

        /// <inheritdoc />
        public IHelperAgent Helper => helper;

        /// <inheritdoc />
        public Task<HelperRunReport> ExecuteAsync(string key, object input, HelperSettings settings, CancellationToken cancellationToken)
        {
            this.Calls.Enqueue(new RecordedCall(key, input));
            _ = this.arrived.Release();
            return Task.FromResult(new HelperRunReport(HelperRunKind.Applied));
        }

        /// <inheritdoc />
        public void Dispose() => this.arrived.Dispose();

        /// <summary>Completes once a call with <paramref name="key"/> has arrived.</summary>
        /// <param name="key">The dispatcher key to wait for.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        public async Task WaitForAsync(string key, CancellationToken cancellationToken)
        {
            while (!this.Calls.Any(call => string.Equals(call.Key, key, StringComparison.Ordinal)))
            {
                await this.arrived.WaitAsync(cancellationToken);
            }
        }
    }
}
