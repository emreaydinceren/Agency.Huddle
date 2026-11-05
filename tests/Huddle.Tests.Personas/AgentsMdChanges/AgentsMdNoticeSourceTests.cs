using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdNoticeSource"/> without the wait (AgentsMdChanges Spec §8.3 and §7.2): which files are
/// behind, the revisions told oldest first, what a session has heard afterwards (the last revision read, never the head), a file
/// that cannot be read, and the second Turn saying nothing after every case.
/// </summary>
public sealed class AgentsMdNoticeSourceTests : IDisposable
{
    private const string Relative = "Home/AGENTS.md";

    private readonly TempDataDir dir = new();
    private readonly List<IDisposable> owned = [];
    private int writes;

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (IDisposable item in this.owned)
        {
            item.Dispose();
        }

        this.dir.Dispose();
    }

    /// <summary>Every unchanged file after a Turn is up to date: no block, and the same map comes back.</summary>
    [Fact]
    public async Task UpToDate_NoBlock_HeardUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1) }, true, ct);

        Assert.Null(result.Block);
        AssertHeard(result, (Relative, HashOf(1)));
    }

    /// <summary>A session from before the feature counts from each file's first sight, so every edit since is told.</summary>
    [Fact]
    public async Task NullHeard_TellsEveryEditSinceFirstSight()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1, 2, 3);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], null, true, ct);

        Assert.Equal(ExpectedBlock(this.ChangedOf(TextOf(3), ["-Rule 1.", "+Rule 2."], ["-Rule 2.", "+Rule 3."])), result.Block);
        AssertHeard(result, (Relative, HashOf(3)));
    }

    /// <summary>With no heard map, a file the ledger has never seen counts as current: nothing is told and its hash is remembered.</summary>
    [Fact]
    public async Task NullHeard_NoLedgerEntry_CountsAsCurrent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile file = this.FileOf(Relative);
        this.Save(file, 1);

        AgentsMdCollected result = await NewSource(this.NewLedger()).CollectAsync([file], null, true, ct);

        Assert.Null(result.Block);
        AssertHeard(result, (Relative, HashOf(1)));
    }

    /// <summary>A file behind by two settled edits tells the older one first.</summary>
    [Fact]
    public async Task Behind_TellsRevisionsOldestFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1, 2, 3);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1) }, true, ct);

        Assert.NotNull(result.Block);
        int older = result.Block.IndexOf("+Rule 2.", StringComparison.Ordinal);
        int newer = result.Block.IndexOf("+Rule 3.", StringComparison.Ordinal);
        Assert.True(older >= 0 && newer > older, result.Block);
        AssertHeard(result, (Relative, HashOf(3)));
    }

    /// <summary>When every revision is skipped nothing is told, but the session has heard them and moves on.</summary>
    [Fact]
    public async Task SkippedOnly_NoBlock_HeardMoves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);
        this.Save(file, 2);
        _ = ledger.Observe(file);
        Assert.True(ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), TextOf(2), new AgentsMdNotice.Skipped("typo")));

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1) }, true, ct);

        Assert.Null(result.Block);
        AssertHeard(result, (Relative, HashOf(2)));
    }

    /// <summary>A revision committed while the block renders is not recorded as heard: the next Turn tells it.</summary>
    [Fact]
    public async Task Heard_IsTheLastRevisionRead_NotTheHead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1, 2);
        CommitOnRender prompts = new(() => Assert.True(ledger.CommitVerdict(Relative, HashOf(2), HashOf(3), TextOf(3), new AgentsMdNotice.Comparison(["-Rule 2.", "+Rule 3."], 0))));
        AgentsMdNoticeSource source = NewSource(ledger, prompts);

        AgentsMdCollected first = await source.CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1) }, true, ct);
        this.Save(file, 3);
        AgentsMdCollected second = await source.CollectAsync([file], first.Heard, true, ct);

        Assert.True(prompts.Committed);
        AssertHeard(first, (Relative, HashOf(2)));
        Assert.Equal(ExpectedBlock(this.ChangedOf(TextOf(3), ["-Rule 2.", "+Rule 3."])), second.Block);
        AssertHeard(second, (Relative, HashOf(3)));
    }

    /// <summary>A file absent from a non-null map is new to the session: its text is told.</summary>
    [Fact]
    public async Task NewFile_NowFollow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile file = this.FileOf(Relative);
        this.Save(file, 1);

        AgentsMdCollected result = await NewSource(this.NewLedger()).CollectAsync([file], new Dictionary<string, string>(), true, ct);

        Assert.Equal(ExpectedBlock(new AgentsMdBlockEntry.NowFollow("Teams/" + Relative, file.FullPath, "Rule 1.")), result.Block);
        AssertHeard(result, (Relative, HashOf(1)));
    }

    /// <summary>An empty heard hash means "not heard": a path that left scope with one is dropped without a word.</summary>
    [Fact]
    public async Task NoLonger_EmptyHeardHash_SaysNothing_AndDrops()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1), ["Home/Gone/AGENTS.md"] = string.Empty }, true, ct);

        Assert.Null(result.Block);
        AssertHeard(result, (Relative, HashOf(1)));
    }

    /// <summary>A new-to-session file deleted during the wait re-reads as absent: nothing is told and the session has heard an empty hash.</summary>
    [Fact]
    public async Task NewFile_DeletedDuringTheWait_TellsNothing()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], new Dictionary<string, string>(), true, stop.Token);
        Assert.False(collect.IsCompleted);
        File.Delete(file.FullPath);

        rig.Clock.Advance(TimeSpan.FromSeconds(31));
        AgentsMdCollected result = await collect;

        Assert.Null(result.Block);
        AssertHeard(result, (Relative, string.Empty));
    }

    /// <summary>A Project moved: its old path is out of scope and the new path holds the same hash, so nothing is told and the map follows.</summary>
    [Fact]
    public async Task Moved_TellsNothing_HeardMoves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile moved = this.FileOf("Home/New/AGENTS.md");
        this.Save(moved, 1);

        AgentsMdCollected result = await NewSource(this.NewLedger()).CollectAsync([moved], new Dictionary<string, string> { ["Home/Old/AGENTS.md"] = HashOf(1) }, true, ct);

        Assert.Null(result.Block);
        AssertHeard(result, ("Home/New/AGENTS.md", HashOf(1)));
    }

    /// <summary>A deleted file is an edit to empty, told as a comparison, and not an out-of-scope path.</summary>
    [Fact]
    public async Task Deleted_IsAComparison()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1, 2);
        File.Delete(file.FullPath);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(2) }, true, ct);

        Assert.Equal(ExpectedBlock(this.ChangedOf(string.Empty, ["-Rule 2."])), result.Block);
        AssertHeard(result, (Relative, string.Empty));
    }

    /// <summary>A heard path that left scope is told once as no longer applying, then dropped from the map.</summary>
    [Fact]
    public async Task OutOfScope_NoLonger_OnceThenDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);
        AgentsMdNoticeSource source = NewSource(ledger);

        AgentsMdCollected first = await source.CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1), ["Gone/AGENTS.md"] = HashOf(9) }, true, ct);
        AgentsMdCollected second = await source.CollectAsync([file], first.Heard, true, ct);

        Assert.Equal(ExpectedBlock(new AgentsMdBlockEntry.NoLonger("Teams/Gone/AGENTS.md")), first.Block);
        AssertHeard(first, (Relative, HashOf(1)));
        Assert.Null(second.Block);
    }

    /// <summary>When a Team could not be listed, a heard path missing from the scope may still apply: it is not told as gone, and heard keeps it.</summary>
    [Fact]
    public async Task OutOfScope_ScopeIncomplete_SaysNothing_AndKeepsHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1), ["Home/Proj/AGENTS.md"] = HashOf(9) }, true, scopeComplete: false, ct);

        Assert.Null(result.Block);
        AssertHeard(result, (Relative, HashOf(1)), ("Home/Proj/AGENTS.md", HashOf(9)));
    }

    /// <summary>A Team with no file has an empty current hash and an empty heard hash, so it is not behind.</summary>
    [Fact]
    public async Task TeamWithNoFile_IsNotBehind()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile file = this.FileOf(Relative);

        AgentsMdCollected result = await NewSource(this.NewLedger()).CollectAsync([file], new Dictionary<string, string>(), true, ct);

        Assert.Null(result.Block);
        Assert.Empty(result.Heard);
    }

    /// <summary>When the chain no longer reaches the heard hash, the Agent is told to read the file, and the map takes the hash of that text.</summary>
    [Fact]
    public async Task Unknown_ReadTheFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = "ABCD" }, true, ct);

        Assert.Equal(ExpectedBlock(new AgentsMdBlockEntry.Unknown("Teams/" + Relative, file.FullPath, TextOf(1))), result.Block);
        AssertHeard(result, (Relative, HashOf(1)));
    }

    /// <summary>After each case above, the next Turn with the returned map says nothing.</summary>
    /// <param name="scenario">The case to set up.</param>
    [Theory]
    [InlineData("Behind")]
    [InlineData("NullHeard")]
    [InlineData("SkippedOnly")]
    [InlineData("NewFile")]
    [InlineData("Moved")]
    [InlineData("Deleted")]
    [InlineData("NoLonger")]
    [InlineData("Unknown")]
    public async Task SecondTurn_SaysNothing_AfterEach(string scenario)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        (AgentsMdFile[] scope, Dictionary<string, string>? heard) = this.Scenario(ledger, scenario);
        AgentsMdNoticeSource source = NewSource(ledger);

        AgentsMdCollected first = await source.CollectAsync(scope, heard, true, ct);
        AgentsMdCollected second = await source.CollectAsync(scope, first.Heard, true, ct);

        if (scenario is not ("SkippedOnly" or "Moved"))
        {
            Assert.NotNull(first.Block);
        }

        Assert.Null(second.Block);
        Assert.Equal(first.Heard.OrderBy(pair => pair.Key, StringComparer.Ordinal), second.Heard.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>A file that cannot be read is left out of the block and keeps its heard hash; the others are still told.</summary>
    [Fact]
    public async Task ReadFails_LeftOutAndHeardKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile readable = this.FileOf(Relative);
        AgentsMdFile broken = this.FileOf("Other/AGENTS.md");
        this.Play(ledger, readable, 1, 2);
        Directory.CreateDirectory(broken.FullPath);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync(
            [readable, broken],
            new Dictionary<string, string> { [Relative] = HashOf(1), ["Other/AGENTS.md"] = HashOf(7) },
            true,
            ct);

        Assert.Equal(ExpectedBlock(this.ChangedOf(TextOf(2), ["-Rule 1.", "+Rule 2."])), result.Block);
        Assert.DoesNotContain("Other/AGENTS.md", result.Block, StringComparison.Ordinal);
        AssertHeard(result, (Relative, HashOf(2)), ("Other/AGENTS.md", HashOf(7)));
    }

    /// <summary>A head too large to keep has no text to compare with: the revision is an unknown change and no Helper is called.</summary>
    [Fact]
    public async Task TextlessHead_Unknown_NoCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger(maxLedgerChars: 40);
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);
        string big = new string('x', 200) + "\n";
        File.WriteAllText(file.FullPath, big);
        string bigHash = AgentsMdBody.Parse(big).Hash;

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = HashOf(1) }, true, ct);

        AgentsMdBlockEntry unknown = new AgentsMdBlockEntry.Changed("Teams/" + Relative, file.FullPath, [new AgentsMdRevision(string.Empty, string.Empty, DateTimeOffset.MinValue, new AgentsMdNotice.Unknown())], big);
        Assert.Equal(ExpectedBlock(unknown), result.Block);
        Assert.Null(ledger.JudgementStarted(Relative));
        AssertHeard(result, (Relative, bigHash));
    }

    /// <summary>A Turn that asks for a judgement waits for the summary and tells it, when the Helper answers inside the limit (Spec §8.3, O-8).</summary>
    [Fact]
    public async Task TurnAsks_WaitsForTheSummary()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        AgentsMdNotice summary = SomeSummary();

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, stop.Token);
        Assert.False(collect.IsCompleted);
        Assert.NotNull(rig.Ledger.JudgementStarted(Relative));
        Assert.True(rig.Ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), TextOf(2), summary));
        AgentsMdCollected result = await collect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), summary)), result.Block);
        AssertHeard(result, (Relative, HashOf(2)));
    }

    /// <summary>When the Helper does not answer, the wait ends 30 seconds after the judgement started and the Turn tells the comparison.</summary>
    [Fact]
    public async Task DeadlinePasses_Comparison()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, stop.Token);
        rig.Clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(collect.IsCompleted);
        rig.Clock.Advance(TimeSpan.FromSeconds(2));
        AgentsMdCollected result = await collect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 2."], 0))), result.Block);
        AssertHeard(result, (Relative, HashOf(2)));
    }

    /// <summary>A summary that lands after the fallback replaces its notice, so a Room that has not heard yet gets the summary (U-4).</summary>
    [Fact]
    public async Task ThenVerdictLands_LaterRoomGetsTheSummary()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        AgentsMdNotice summary = SomeSummary();
        Task<AgentsMdCollected> firstCollect = rig.Source.CollectAsync([file], Heard(1), true, stop.Token);
        rig.Clock.Advance(TimeSpan.FromSeconds(31));
        AgentsMdCollected first = await firstCollect;

        Assert.True(rig.Ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), TextOf(2), summary));
        AgentsMdCollected later = await rig.Source.CollectAsync([file], Heard(1), true, stop.Token);

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 2."], 0))), first.Block);
        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), summary)), later.Block);
        AssertHeard(later, (Relative, HashOf(2)));
    }

    /// <summary>Inside the spacing no judgement starts, so the Turn does not wait and tells the comparison at once.</summary>
    [Fact]
    public async Task InsideSpacing_NoWait_Comparison()
    {
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Assert.True(rig.Ledger.TryStartJudgement(file, JudgementReason.TurnAsked));
        Assert.True(rig.Ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), AgentsMdBody.Parse(TextOf(2)).Text, SomeSummary()));
        this.Save(file, 3);
        _ = rig.Ledger.Observe(file);

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(2), true, TestContext.Current.CancellationToken);
        Assert.True(collect.IsCompleted);
        AgentsMdCollected result = await collect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(3), new AgentsMdNotice.Comparison(["-Rule 2.", "+Rule 3."], 0))), result.Block);
        AssertHeard(result, (Relative, HashOf(3)));
    }

    /// <summary>With the Helper switched off no judgement starts, so the Turn does not wait.</summary>
    [Fact]
    public async Task HelperOff_NoWait()
    {
        Rig rig = this.Wire(helperEnabled: false);
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, TestContext.Current.CancellationToken);
        Assert.True(collect.IsCompleted);
        AgentsMdCollected result = await collect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 2."], 0))), result.Block);
    }

    /// <summary>A paused Helper has no judgement running, so the Turn does not wait.</summary>
    [Fact]
    public async Task Paused_NoWait()
    {
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        int cap = rig.Helper.Defaults.DailyCap;
        for (int call = 0; call < cap; call++)
        {
            Assert.True(rig.Budget.TryReserve(rig.Helper.Id, cap));
        }

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, TestContext.Current.CancellationToken);
        Assert.True(collect.IsCompleted);
        AgentsMdCollected result = await collect;

        Assert.True(rig.Budget.IsPaused(rig.Helper.Id, cap));
        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 2."], 0))), result.Block);
    }

    /// <summary>A judgement that could not be queued earlier leaves nothing running, so this Turn and the next do not wait.</summary>
    [Fact]
    public async Task JudgementFailedEarlier_NoWait_NextTurn()
    {
        Rig rig = this.Wire(dispatcherFails: true);
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Task<AgentsMdCollected> firstCollect = rig.Source.CollectAsync([file], Heard(1), true, TestContext.Current.CancellationToken);
        Assert.True(firstCollect.IsCompleted);
        AgentsMdCollected first = await firstCollect;
        this.Save(file, 3);
        _ = rig.Ledger.Observe(file);

        Task<AgentsMdCollected> nextCollect = rig.Source.CollectAsync([file], first.Heard, true, TestContext.Current.CancellationToken);
        Assert.True(nextCollect.IsCompleted);
        AgentsMdCollected next = await nextCollect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(3), new AgentsMdNotice.Comparison(["-Rule 2.", "+Rule 3."], 0))), next.Block);
    }

    /// <summary>A pair whose text was edited away and back is still marked running, but no judgement of the current text started: no wait.</summary>
    [Fact]
    public async Task StaleRunningPair_NoWait()
    {
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Assert.True(rig.Ledger.TryStartJudgement(file, JudgementReason.TurnAsked));
        this.Save(file, 3);
        _ = rig.Ledger.Observe(file);
        this.Save(file, 2);
        _ = rig.Ledger.Observe(file);

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, TestContext.Current.CancellationToken);
        Assert.True(collect.IsCompleted);
        AgentsMdCollected result = await collect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 2."], 0))), result.Block);
    }

    /// <summary>Cancelling the Turn ends the wait at once and the collection throws, well before the deadline.</summary>
    [Fact]
    public async Task Cancelled_EndsTheWait()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, stop.Token);
        Assert.False(collect.IsCompleted);

        await stop.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collect);
    }

    /// <summary>A text that changes during the wait gets no summary of the old text: the deadline passes and the Turn tells the comparison with the new text.</summary>
    [Fact]
    public async Task TextChangedDuringTheWait_NoOldSummary()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, stop.Token);
        Assert.False(collect.IsCompleted);
        this.Save(file, 3);
        _ = rig.Ledger.Observe(file);

        rig.Clock.Advance(TimeSpan.FromSeconds(31));
        AgentsMdCollected result = await collect;

        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(3), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 3."], 0))), result.Block);
        AssertHeard(result, (Relative, HashOf(3)));
        Assert.False(rig.Ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), TextOf(2), SomeSummary()));
    }

    /// <summary>A cancelled Turn hands back nothing and leaves the map it was given as it was, so the next Turn tells the edit.</summary>
    [Fact]
    public async Task Cancelled_HeardNotMoved()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Dictionary<string, string> heard = Heard(1);
        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], heard, true, stop.Token);
        Assert.False(collect.IsCompleted);
        await stop.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collect);

        rig.Clock.Advance(TimeSpan.FromSeconds(31));
        AgentsMdCollected next = await rig.Source.CollectAsync([file], heard, true, TestContext.Current.CancellationToken);

        Assert.Equal(HashOf(1), Assert.Single(heard).Value);
        Assert.Equal(ExpectedBlock(ChangedWith(file, TextOf(2), new AgentsMdNotice.Comparison(["-Rule 1.", "+Rule 2."], 0))), next.Block);
    }

    /// <summary>A cancelled Turn settles nothing: the ledger holds no revision after the first sight, where the fallback would have written one.</summary>
    [Fact]
    public async Task Cancelled_NoRevisionCommitted()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], Heard(1), true, stop.Token);
        Assert.False(collect.IsCompleted);
        await stop.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collect);

        IReadOnlyList<AgentsMdRevision>? since = rig.Ledger.Since(Relative, HashOf(1));

        Assert.NotNull(since);
        Assert.Empty(since);
    }

    /// <summary>With no heard map and a file that cannot be read, the map keeps the file's first-sight hash, so the next Turn tells the edit and not "You now also follow".</summary>
    [Fact]
    public async Task NullHeard_ReadFails_KeepsFirstSightInHeard_NextTurnTellsTheEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1, 2);
        File.Delete(file.FullPath);
        Directory.CreateDirectory(file.FullPath);
        AgentsMdNoticeSource source = NewSource(ledger);

        AgentsMdCollected first = await source.CollectAsync([file], null, true, ct);
        Directory.Delete(file.FullPath);
        this.Save(file, 2);
        AgentsMdCollected second = await source.CollectAsync([file], first.Heard, true, ct);

        Assert.Null(first.Block);
        AssertHeard(first, (Relative, HashOf(1)));
        Assert.Equal(ExpectedBlock(this.ChangedOf(TextOf(2), ["-Rule 1.", "+Rule 2."])), second.Block);
    }

    /// <summary>Without file tools the block carries the text as it is after the wait, not as it was read before it.</summary>
    [Fact]
    public async Task NoFileTools_TextChangedDuringTheWait_ToldTheNewText()
    {
        using CancellationTokenSource stop = Bounded(TestContext.Current.CancellationToken);
        Rig rig = this.Wire();
        AgentsMdFile file = this.FileOf(Relative);
        this.Pend(rig, file);
        Dictionary<string, string> heard = new() { [Relative] = "ABCD" };
        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([file], heard, false, stop.Token);
        Assert.False(collect.IsCompleted);
        this.Save(file, 3);
        _ = rig.Ledger.Observe(file);

        rig.Clock.Advance(TimeSpan.FromSeconds(31));
        AgentsMdCollected result = await collect;

        string expected = new AgentsMdNoticeRenderer(new FakePromptSource(), new AgentsMdOptions())
            .Render([new AgentsMdBlockEntry.Unknown("Teams/" + Relative, file.FullPath, AgentsMdBody.Parse(TextOf(3)).Text)], false);
        Assert.Equal(expected, result.Block);
    }

    /// <summary>Two files wait on one deadline: the latest judgement start plus the limit, so the earlier start does not end the wait.</summary>
    [Fact]
    public async Task TwoPendingFiles_DeadlineIsTheLatestStart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource stop = Bounded(ct);
        Rig rig = this.Wire();
        AgentsMdFile first = this.FileOf(Relative);
        AgentsMdFile second = this.FileOf("Other/AGENTS.md");
        this.Pend(rig, first);
        this.Pend(rig, second);
        _ = rig.Ledger.TryStartJudgement(first, JudgementReason.TurnAsked);
        Assert.NotNull(rig.Ledger.JudgementStarted(Relative));
        rig.Clock.Advance(TimeSpan.FromSeconds(20));
        Dictionary<string, string> heard = new() { [Relative] = HashOf(1), ["Other/AGENTS.md"] = HashOf(1) };

        Task<AgentsMdCollected> collect = rig.Source.CollectAsync([first, second], heard, true, stop.Token);
        rig.Clock.Advance(TimeSpan.FromSeconds(11));
        _ = await Task.WhenAny(collect, Task.Delay(TestBudgets.Short, ct));

        Assert.False(collect.IsCompleted);
        rig.Clock.Advance(TimeSpan.FromSeconds(20));
        AgentsMdCollected result = await collect;
        Assert.NotNull(result.Block);
    }

    /// <summary>The Adapter's file-tool flag reaches the renderer: with none, the block carries the file's text.</summary>
    [Fact]
    public async Task NoFileTools_ReachesTheRenderer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdFile file = this.FileOf(Relative);
        this.Play(ledger, file, 1);

        AgentsMdCollected result = await NewSource(ledger).CollectAsync([file], new Dictionary<string, string> { [Relative] = "ABCD" }, false, ct);

        string expected = new AgentsMdNoticeRenderer(new FakePromptSource(), new AgentsMdOptions())
            .Render([new AgentsMdBlockEntry.Unknown("Teams/" + Relative, file.FullPath, AgentsMdBody.Parse(TextOf(1)).Text)], false);
        Assert.Equal(expected, result.Block);
    }

    /// <summary>A moved file whose new path cannot be read yet is not told as gone: the move is told once, as nothing, when the path reads.</summary>
    [Fact]
    public async Task Moved_NewPathUnreadable_NotToldTwice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AgentsMdFile moved = this.FileOf("Home/New/AGENTS.md");
        Directory.CreateDirectory(moved.FullPath);
        AgentsMdNoticeSource source = NewSource(this.NewLedger());
        Dictionary<string, string> heard = new() { ["Home/Old/AGENTS.md"] = HashOf(1) };

        AgentsMdCollected first = await source.CollectAsync([moved], heard, true, ct);
        Directory.Delete(moved.FullPath);
        this.Save(moved, 1);
        AgentsMdCollected second = await source.CollectAsync([moved], first.Heard, true, ct);

        Assert.Null(first.Block);
        Assert.Null(second.Block);
        AssertHeard(second, ("Home/New/AGENTS.md", HashOf(1)));
    }

    /// <summary>A token that cancels itself after 30 real seconds, so no wait in a test is unbounded.</summary>
    /// <param name="cancellationToken">The test's token.</param>
    private static CancellationTokenSource Bounded(CancellationToken cancellationToken)
    {
        CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        return bound;
    }

    /// <summary>The heard map of a session that last heard version <paramref name="version"/>.</summary>
    /// <param name="version">The version number.</param>
    private static Dictionary<string, string> Heard(int version) => new() { [Relative] = HashOf(version) };

    /// <summary>A Helper's summary of the edit from version 1 to 2.</summary>
    private static AgentsMdNotice.Summary SomeSummary() => new AgentsMdNotice.Summary([new AgentsMdRuleChange("added", "A second rule was added.", "Rule 2.")]);

    /// <summary>A changed-file entry with one revision holding <paramref name="notice"/>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="currentText">The file's current text.</param>
    /// <param name="notice">The notice.</param>
    private static AgentsMdBlockEntry.Changed ChangedWith(AgentsMdFile file, string currentText, AgentsMdNotice notice) =>
        new("Teams/" + file.RelativePath, file.FullPath, [new AgentsMdRevision(string.Empty, string.Empty, DateTimeOffset.MinValue, notice)], currentText);

    /// <summary>Records version 1 as the head, then saves version 2 and observes it: an edit is pending as of now.</summary>
    /// <param name="rig">The wired ledger.</param>
    /// <param name="file">The file.</param>
    private void Pend(Rig rig, AgentsMdFile file)
    {
        this.Save(file, 1);
        _ = rig.Ledger.Observe(file);
        this.Save(file, 2);
        _ = rig.Ledger.Observe(file);
    }

    /// <summary>Wires a ledger with a real dispatcher and a registered fake Helper over one clock whose timers fire.</summary>
    /// <param name="helperEnabled">Whether the Helper is switched on.</param>
    /// <param name="dispatcherFails">Whether queueing a judgement throws.</param>
    private Rig Wire(bool helperEnabled = true, bool dispatcherFails = false)
    {
        IOptions<TeamOptions> options = this.dir.Options(team => team.Acp.Enabled = true);
        HelperSettingsResolver settings = new(new HelperSettingsStore(options), options, NullLogger<HelperSettingsResolver>.Instance);
        FiringTimeProvider clock = new();
        HelperBudget budget = new(new HelperUsageStore(options), clock);
        WaitHelper helper = new(helperEnabled);
        HelperDispatcher dispatcher = new([new NoopInvoker(helper)], settings, new HelperStatusStore(options), options, NullLogger<HelperDispatcher>.Instance, new FiringTimeProvider());
        this.owned.Add(dispatcher);
        AgentsMdLedger ledger = new(() => dispatcherFails ? throw new InvalidOperationException("The queue is down.") : dispatcher, () => helper, settings, budget, options, NullLogger<AgentsMdLedger>.Instance, clock);
        AgentsMdNoticeSource source = new(ledger, new AgentsMdNoticeRenderer(new FakePromptSource(), options.Value.AgentsMd), options.Value.AgentsMd, NullLogger<AgentsMdNoticeSource>.Instance);
        return new Rig(ledger, source, clock, budget, helper);
    }

    /// <summary>The parts of a wired ledger a test reaches.</summary>
    /// <param name="Ledger">The ledger.</param>
    /// <param name="Source">The source under test.</param>
    /// <param name="Clock">The ledger's clock; advancing it fires the wait's deadline.</param>
    /// <param name="Budget">The Helper's budget.</param>
    /// <param name="Helper">The registered Helper.</param>
    private sealed record Rig(AgentsMdLedger Ledger, AgentsMdNoticeSource Source, FiringTimeProvider Clock, HelperBudget Budget, IHelperAgent Helper);

    /// <summary>A registered Helper that only has an id, defaults and an invoker.</summary>
    /// <param name="enabled">Whether it is on.</param>
    private sealed class WaitHelper(bool enabled) : IHelperAgent
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

    /// <summary>An invoker that never runs: the dispatcher's clock is never advanced.</summary>
    /// <param name="helper">The Helper it serves.</param>
    private sealed class NoopInvoker(IHelperAgent helper) : IHelperInvoker
    {
        /// <inheritdoc />
        public IHelperAgent Helper => helper;

        /// <inheritdoc />
        public Task<HelperRunReport> ExecuteAsync(string key, object input, HelperSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new HelperRunReport(HelperRunKind.Applied));
    }

    /// <summary>The block the real renderer makes for <paramref name="entries"/> with the catalog prompts and file tools.</summary>
    /// <param name="entries">The entries, in order.</param>
    private static string ExpectedBlock(params AgentsMdBlockEntry[] entries) =>
        new AgentsMdNoticeRenderer(new FakePromptSource(), new AgentsMdOptions()).Render(entries, true);

    /// <summary>A changed-file entry for <see cref="Relative"/> with one comparison revision per line set.</summary>
    /// <param name="currentText">The file's current text.</param>
    /// <param name="revisions">The comparison lines of each revision, oldest first.</param>
    private AgentsMdBlockEntry.Changed ChangedOf(string currentText, params string[][] revisions) =>
        new AgentsMdBlockEntry.Changed(
            "Teams/" + Relative,
            this.FileOf(Relative).FullPath,
            [.. revisions.Select((lines, index) => new AgentsMdRevision(string.Empty, string.Empty, DateTimeOffset.MinValue.AddSeconds(index), new AgentsMdNotice.Comparison(lines, 0)))],
            currentText);

    /// <summary>Builds each case of the theory and returns its scope and starting map.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="scenario">The case.</param>
    private (AgentsMdFile[] Scope, Dictionary<string, string>? Heard) Scenario(AgentsMdLedger ledger, string scenario)
    {
        AgentsMdFile file = this.FileOf(Relative);
        switch (scenario)
        {
            case "Behind":
                this.Play(ledger, file, 1, 2);
                return ([file], new() { [Relative] = HashOf(1) });
            case "NullHeard":
                this.Play(ledger, file, 1, 2);
                return ([file], null);
            case "SkippedOnly":
                this.Play(ledger, file, 1);
                this.Save(file, 2);
                _ = ledger.Observe(file);
                _ = ledger.CommitVerdict(Relative, HashOf(1), HashOf(2), TextOf(2), new AgentsMdNotice.Skipped("typo"));
                return ([file], new() { [Relative] = HashOf(1) });
            case "NewFile":
                this.Save(file, 1);
                return ([file], []);
            case "Moved":
                AgentsMdFile moved = this.FileOf("Home/New/AGENTS.md");
                this.Save(moved, 1);
                return ([moved], new() { ["Home/Old/AGENTS.md"] = HashOf(1) });
            case "Deleted":
                this.Play(ledger, file, 1, 2);
                File.Delete(file.FullPath);
                return ([file], new() { [Relative] = HashOf(2) });
            case "NoLonger":
                this.Play(ledger, file, 1);
                return ([file], new() { [Relative] = HashOf(1), ["Gone/AGENTS.md"] = HashOf(9) });
            case "Unknown":
                this.Play(ledger, file, 1);
                return ([file], new() { [Relative] = "ABCD" });
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown case.");
        }
    }

    /// <summary>Builds a ledger over the temp folder; no Helper or dispatcher can be reached.</summary>
    /// <param name="maxLedgerChars">The largest text the ledger keeps.</param>
    private AgentsMdLedger NewLedger(int maxLedgerChars = 20_000)
    {
        IOptions<TeamOptions> options = this.dir.Options(team => team.AgentsMd.MaxLedgerChars = maxLedgerChars);
        HelperSettingsResolver settings = new(new HelperSettingsStore(options), options, NullLogger<HelperSettingsResolver>.Instance);
        HelperBudget budget = new(new HelperUsageStore(options));
        return new AgentsMdLedger(
            static () => throw new InvalidOperationException("The dispatcher must not be reached."),
            static () => throw new InvalidOperationException("The Helper must not be reached."),
            settings,
            budget,
            options,
            NullLogger<AgentsMdLedger>.Instance);
    }

    /// <summary>Builds the source over <paramref name="ledger"/>.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="prompts">The prompt source; the default catalog when null.</param>
    private static AgentsMdNoticeSource NewSource(AgentsMdLedger ledger, IPromptSource? prompts = null) =>
        new(ledger, new AgentsMdNoticeRenderer(prompts ?? new FakePromptSource(), new AgentsMdOptions()), new AgentsMdOptions(), NullLogger<AgentsMdNoticeSource>.Instance);

    /// <summary>A Team-level file at <paramref name="relative"/> under the Teams root.</summary>
    /// <param name="relative">The ledger's relative path.</param>
    private AgentsMdFile FileOf(string relative) =>
        new(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, relative), relative);

    /// <summary>The text for version <paramref name="version"/>; each version has its own hash.</summary>
    /// <param name="version">The version number.</param>
    private static string TextOf(int version) => $"Rule {version}.\n";

    /// <summary>The hash the ledger uses for version <paramref name="version"/>.</summary>
    /// <param name="version">The version number.</param>
    private static string HashOf(int version) => AgentsMdBody.Parse(TextOf(version)).Hash;

    /// <summary>Writes a version with a distinct modification time.</summary>
    /// <param name="file">The file.</param>
    /// <param name="version">The version number.</param>
    private void Save(AgentsMdFile file, int version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath) ?? this.TeamsRoot);
        File.WriteAllText(file.FullPath, TextOf(version));
        this.writes++;
        File.SetLastWriteTimeUtc(file.FullPath, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc).AddMinutes(this.writes));
    }

    /// <summary>Records the first version as first sight, then settles each later version as its own revision.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="file">The file.</param>
    /// <param name="versions">The versions in order; the first is the first sight.</param>
    private void Play(AgentsMdLedger ledger, AgentsMdFile file, params int[] versions)
    {
        this.Save(file, versions[0]);
        _ = ledger.Observe(file);
        foreach (int version in versions.Skip(1))
        {
            this.Save(file, version);
            _ = ledger.Observe(file);
            _ = ledger.CommitComparison(file);
        }
    }

    /// <summary>Asserts the returned map holds exactly the given pairs.</summary>
    /// <param name="result">The collected result.</param>
    /// <param name="expected">The path and hash pairs.</param>
    private static void AssertHeard(AgentsMdCollected result, params (string Path, string Hash)[] expected)
    {
        Assert.Equal(expected.Length, result.Heard.Count);
        foreach ((string path, string hash) in expected)
        {
            Assert.True(result.Heard.TryGetValue(path, out string? actual), $"'{path}' is missing from the map.");
            Assert.Equal(hash, actual);
        }
    }

    /// <summary>A prompt source that runs an action the first time the block is rendered: a commit between reading and returning.</summary>
    /// <param name="onRender">The action.</param>
    private sealed class CommitOnRender(Action onRender) : IPromptSource
    {
        private readonly FakePromptSource inner = new();

        /// <summary>Whether the action has run.</summary>
        public bool Committed { get; private set; }

        /// <inheritdoc />
        public string Render(string key, IReadOnlyDictionary<string, string> values)
        {
            if (!this.Committed)
            {
                this.Committed = true;
                onRender();
            }

            return this.inner.Render(key, values);
        }

        /// <inheritdoc />
        public string Raw(string key) => this.inner.Raw(key);

        /// <inheritdoc />
        public bool IsOverridden(string key) => this.inner.IsOverridden(key);
    }
}
