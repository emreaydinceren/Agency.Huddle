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
/// Pins how an edit settles (AgentsMdChanges Spec §8.2 "CommitVerdict's stale rules" and §10 "The race with the fallback"):
/// <see cref="AgentsMdLedger.CommitVerdict"/>, the fallback <see cref="AgentsMdLedger.CommitComparison"/> and
/// <see cref="AgentsMdLedger.WaitForHeadAsync"/>. The ledger and the dispatcher read one <see cref="FiringTimeProvider"/>,
/// because the deadline of a wait is a timer that must fire. Every wait in this class goes through a token that gives up
/// after 30 seconds, so no test can hang.
/// </summary>
public sealed class AgentsMdLedgerCommitTests : IDisposable
{
    private const string Relative = "Home/AGENTS.md";
    private const string Head = "Head rule.\n";
    private const string Edited = "Head rule.\n\nAnother rule.\n";
    private const string Third = "Head rule.\n\nAnother rule.\n\nThird rule.\n";

    private readonly TempDataDir dir = new();
    private readonly FiringTimeProvider clock = new();
    private readonly FakeAgentsMdHelper helper = new();
    private readonly RecordingInvoker invoker;
    private readonly HelperDispatcher dispatcher;
    private readonly AgentsMdLedger ledger;
    private int writes;

    /// <summary>Wires the ledger over a temp data folder, with a registered fake Helper behind a real dispatcher.</summary>
    public AgentsMdLedgerCommitTests()
    {
        IOptions<TeamOptions> options = this.dir.Options(team => team.Acp.Enabled = true);
        HelperSettingsResolver settings = new(new HelperSettingsStore(options), options, NullLogger<HelperSettingsResolver>.Instance);
        HelperBudget budget = new(new HelperUsageStore(options), this.clock);
        this.invoker = new RecordingInvoker(this.helper);
        this.dispatcher = new HelperDispatcher([this.invoker], settings, new HelperStatusStore(options), options, NullLogger<HelperDispatcher>.Instance, this.clock);
        this.ledger = new AgentsMdLedger(() => this.dispatcher, () => this.helper, settings, budget, options, NullLogger<AgentsMdLedger>.Instance, this.clock);
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

    /// <summary>Records <see cref="Head"/> as the head, then saves <see cref="Edited"/> and observes it, so an edit is pending.</summary>
    private void HeadThenEdit()
    {
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());
        this.Save(Edited);
        _ = this.ledger.Observe(this.TheFile());
    }

    /// <summary>The hash the ledger uses for a text.</summary>
    /// <param name="text">The file text.</param>
    private static string HashOf(string text) => AgentsMdBody.Parse(text).Hash;

    /// <summary>A summary notice, to tell a Helper's notice from a comparison.</summary>
    private static AgentsMdNotice.Summary SomeSummary() => new([new AgentsMdRuleChange("Added", "There is another rule.", "Another rule.")]);

    /// <summary>The text of the file's one ledger file.</summary>
    private string LedgerJson() => File.ReadAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json")));

    /// <summary>One top-level string property of the ledger file, matched without regard to case.</summary>
    /// <param name="name">The property name.</param>
    private string LedgerString(string name)
    {
        using JsonDocument document = JsonDocument.Parse(this.LedgerJson());
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>A token that gives up after 30 seconds, so a wait that never ends fails the test instead of hanging it.</summary>
    /// <param name="cancellationToken">The test's own token.</param>
    private static CancellationTokenSource Bounded(CancellationToken cancellationToken)
    {
        CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        return bound;
    }

    /// <summary>Waits, within the bound, until the clock holds a timer that has not fired.</summary>
    /// <param name="cancellationToken">Gives up the wait.</param>
    private async Task WaitForTimerAsync(CancellationToken cancellationToken)
    {
        while (this.clock.ScheduledTimerCount == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }
    }

    /// <summary>
    /// Runs every queued judgement and waits until it has had the chance to reach the invoker: a sentinel on its own key is
    /// queued, the clock is moved past every wait, and the sentinel's call is awaited within the bound.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource bound = Bounded(cancellationToken);
        int armed = this.clock.ScheduledTimerCount;
        int before = this.invoker.Calls.Count;
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually.UntilAsync(() => this.invoker.Calls.Count >= before + armed, bound.Token);
        this.dispatcher.Enqueue(this.helper.Id, "sentinel", new object());
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await this.invoker.WaitForAsync("sentinel", bound.Token);
    }

    /// <summary>When a pair was settled by the fallback more than once (a revert), a Verdict replaces the latest one, and never the first sight.</summary>
    [Fact]
    public void CommitVerdict_RevertedPair_ReplacesTheLastFallback_NotFirstSight()
    {
        this.HeadThenEdit();
        _ = this.ledger.CommitComparison(this.TheFile());
        this.Save(Head);
        _ = this.ledger.Observe(this.TheFile());
        _ = this.ledger.CommitComparison(this.TheFile());
        this.Save(Edited);
        _ = this.ledger.Observe(this.TheFile());
        _ = this.ledger.CommitComparison(this.TheFile());

        bool committed = this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary());
        bool firstSight = this.ledger.CommitVerdict(Relative, string.Empty, HashOf(Head), Head, SomeSummary());

        Assert.True(committed);
        Assert.False(firstSight);
        IReadOnlyList<AgentsMdRevision> chain = this.ledger.Since(Relative, string.Empty) ?? [];
        Assert.Equal(4, chain.Count);
        Assert.IsType<AgentsMdNotice.Comparison>(chain[0].Notice);
        Assert.IsType<AgentsMdNotice.Comparison>(chain[1].Notice);
        Assert.IsType<AgentsMdNotice.Comparison>(chain[2].Notice);
        Assert.IsType<AgentsMdNotice.Summary>(chain[3].Notice);
    }

    /// <summary>A Verdict never replaces a settled Summary: a second one for the same pair is stale.</summary>
    [Fact]
    public void CommitVerdict_DoesNotReplaceASummary()
    {
        this.HeadThenEdit();
        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary()));
        AgentsMdNotice other = new AgentsMdNotice.Summary([new AgentsMdRuleChange("Removed", "A different rule.", "Another rule.")]);

        bool committed = this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, other);

        Assert.False(committed);
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        AgentsMdNotice.Summary kept = Assert.IsType<AgentsMdNotice.Summary>(revision.Notice);
        Assert.Equal("There is another rule.", Assert.Single(kept.Changes).Summary);
    }

    /// <summary>A Verdict for the head's own pair appends the revision, and the head moves with its text and its hash together.</summary>
    [Fact]
    public void CommitVerdict_HeadMatches_AppendsAndMovesTheHead_TextAndHashMatch()
    {
        this.HeadThenEdit();

        bool committed = this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary());

        Assert.True(committed);
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        Assert.Equal(HashOf(Edited), revision.ToHash);
        Assert.IsType<AgentsMdNotice.Summary>(revision.Notice);
        Assert.Equal(HashOf(Edited), this.LedgerString("HeadHash"));
        Assert.Equal(Edited, this.LedgerString("HeadText"));
        Assert.Empty(this.ledger.PendingEdits());
    }

    /// <summary>When the fallback settled the pair first, a Verdict for the same two hashes replaces its notice (Spec §10).</summary>
    [Fact]
    public void CommitVerdict_FallbackAlreadySettledThePair_ReplacesItsNotice()
    {
        this.HeadThenEdit();
        AgentsMdRevision fallback = this.ledger.CommitComparison(this.TheFile());
        Assert.IsType<AgentsMdNotice.Comparison>(fallback.Notice);

        bool committed = this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary());

        Assert.True(committed);
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        Assert.IsType<AgentsMdNotice.Summary>(revision.Notice);
        Assert.Equal(HashOf(Edited), this.LedgerString("HeadHash"));
    }

    /// <summary>A Verdict for a pair that is neither the head's nor a settled one answers false and changes nothing.</summary>
    [Fact]
    public void CommitVerdict_Stale_False_ChangesNothing()
    {
        this.HeadThenEdit();
        string before = this.LedgerJson();

        bool committed = this.ledger.CommitVerdict(Relative, HashOf(Third), HashOf(Edited), Edited, SomeSummary());

        Assert.False(committed);
        Assert.Equal(before, this.LedgerJson());
        Assert.Empty(this.ledger.Since(Relative, HashOf(Head)) ?? [new AgentsMdRevision(string.Empty, string.Empty, default, new AgentsMdNotice.Unknown())]);
        Assert.Single(this.ledger.PendingEdits());
    }

    /// <summary>The fallback reads the disk once: the text it stores and the hash it moves to come from the same read, even when the file changed after it was observed.</summary>
    [Fact]
    public void CommitComparison_ReadsOnce_TextAndHashAreOneUnit()
    {
        this.HeadThenEdit();
        this.Save(Third);

        AgentsMdRevision revision = this.ledger.CommitComparison(this.TheFile());

        Assert.Equal(HashOf(Head), revision.FromHash);
        Assert.Equal(HashOf(Third), revision.ToHash);
        Assert.IsType<AgentsMdNotice.Comparison>(revision.Notice);
        Assert.Equal(HashOf(Third), this.LedgerString("HeadHash"));
        Assert.Equal(AgentsMdBody.Parse(Third).Text, this.LedgerString("HeadText"));
    }

    /// <summary>When a Verdict already moved the head, the fallback finds it current and changes nothing (Spec §10).</summary>
    [Fact]
    public void CommitComparison_HeadAlreadyCurrent_DoesNothing()
    {
        this.HeadThenEdit();
        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary()));
        string before = this.LedgerJson();

        _ = this.ledger.CommitComparison(this.TheFile());

        Assert.Equal(before, this.LedgerJson());
        AgentsMdRevision revision = Assert.Single(this.ledger.Since(Relative, HashOf(Head)) ?? []);
        Assert.IsType<AgentsMdNotice.Summary>(revision.Notice);
    }

    /// <summary>A wait for a hash completes, without a fault, once a Verdict moves the head to it.</summary>
    [Fact]
    public async Task WaitForHead_CompletesOnCommit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = Bounded(ct);
        this.HeadThenEdit();
        Task wait = this.ledger.WaitForHeadAsync(Relative, HashOf(Edited), this.clock.GetUtcNow().AddHours(1), bound.Token);
        Assert.False(wait.IsCompleted);

        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary()));
        await wait.WaitAsync(bound.Token);

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>A wait whose hash never arrives completes, without a fault, when the deadline passes.</summary>
    [Fact]
    public async Task WaitForHead_CompletesOnTheDeadline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = Bounded(ct);
        this.HeadThenEdit();
        Task wait = this.ledger.WaitForHeadAsync(Relative, HashOf(Edited), this.clock.GetUtcNow().AddMinutes(5), bound.Token);
        await this.WaitForTimerAsync(bound.Token);
        Assert.False(wait.IsCompleted);

        this.clock.Advance(TimeSpan.FromMinutes(6));
        await wait.WaitAsync(bound.Token);

        Assert.True(wait.IsCompletedSuccessfully);
        Assert.Equal(HashOf(Head), this.LedgerString("HeadHash"));
    }

    /// <summary>A wait completes, without a fault, when its token is cancelled; the caller then falls back.</summary>
    [Fact]
    public async Task WaitForHead_CompletesOnCancellation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = Bounded(ct);
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(bound.Token);
        this.HeadThenEdit();
        Task wait = this.ledger.WaitForHeadAsync(Relative, HashOf(Edited), this.clock.GetUtcNow().AddHours(1), stop.Token);
        Assert.False(wait.IsCompleted);

        await stop.CancelAsync();
        await wait.WaitAsync(bound.Token);

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>A pair a Verdict has settled is done: asking again, even for a Turn, answers false and queues nothing.</summary>
    [Fact]
    public async Task TryStartJudgement_SamePair_AfterCommitVerdict_False()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.HeadThenEdit();
        Assert.True(this.ledger.CommitVerdict(Relative, HashOf(Head), HashOf(Edited), Edited, SomeSummary()));

        bool started = this.ledger.TryStartJudgement(this.TheFile(), JudgementReason.TurnAsked);
        await this.DrainAsync(ct);

        Assert.False(started);
        Assert.DoesNotContain(this.invoker.Calls, static call => !string.Equals(call, "sentinel", StringComparison.Ordinal));
    }

    /// <summary>A registered Helper whose only job is to have an id, defaults and an invoker.</summary>
    private sealed class FakeAgentsMdHelper : IHelperAgent
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
        public HelperDefaults Defaults { get; } = new(true, null, null, null, DailyCap: 2, WaitSeconds: 1);
    }

    /// <summary>Records the key of each call and lets a test wait for the call with a given key.</summary>
    private sealed class RecordingInvoker(IHelperAgent helper) : IHelperInvoker, IDisposable
    {
        private readonly SemaphoreSlim arrived = new(0);

        /// <summary>The keys of the calls so far, oldest first.</summary>
        public ConcurrentQueue<string> Calls { get; } = new();

        /// <inheritdoc />
        public IHelperAgent Helper => helper;

        /// <inheritdoc />
        public Task<HelperRunReport> ExecuteAsync(string key, object input, HelperSettings settings, CancellationToken cancellationToken)
        {
            this.Calls.Enqueue(key);
            _ = this.arrived.Release();
            return Task.FromResult(new HelperRunReport(HelperRunKind.Applied));
        }

        /// <inheritdoc />
        public void Dispose() => this.arrived.Dispose();

        /// <summary>Completes once a call with <paramref name="key"/> has arrived, or throws when the token is cancelled.</summary>
        /// <param name="key">The dispatcher key to wait for.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        public async Task WaitForAsync(string key, CancellationToken cancellationToken)
        {
            while (!this.Calls.Any(call => string.Equals(call, key, StringComparison.Ordinal)))
            {
                await this.arrived.WaitAsync(cancellationToken);
            }
        }
    }
}
