using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperJudge"/> runs one judgement now, for the dispatcher's queue and for an awaited caller alike: it gates on
/// enabled and Adapters-on, shares one concurrency limit, records the card and the Feed, never throws for a failed call, and says
/// whether the call ran (Memory Tidy Spec §6.7, §8.1).
/// </summary>
public sealed class HelperJudgeTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly FakeHelperProcessRunner process = new();
    private readonly ScriptedHelper helper = new();
    private readonly FakePromptSource prompts = new();
    private readonly FiringTimeProvider clock = new();
    private readonly RecordingFeedPublisher feed = new();

    /// <summary>Sets the two prompts the scripted Helper renders.</summary>
    public HelperJudgeTests()
    {
        this.prompts.SetOverride("test.system", "You judge.");
        this.prompts.SetOverride("test.task", "Judge: {{input}}");
    }

    /// <summary>Removes the temp data directory.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>Builds the dispatcher and its judge over real stores on the temp directory and the firing clock.</summary>
    /// <param name="configure">Changes the options, or <see langword="null"/>.</param>
    private Rig Build(Action<TeamOptions>? configure = null)
    {
        IOptions<TeamOptions> options = this.dir.Options(team =>
        {
            team.Acp.Enabled = true;
            team.Acp.Adapters =
            [
                new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["adapter.js"], UsesToolNamePrefix = false },
                new AdapterProfileOptions { Id = "bare", Command = "node", UsesToolNamePrefix = false },
            ];
            configure?.Invoke(team);
        });
        HelperUsageStore usage = new(options);
        HelperRunner runner = new(this.process, new AdapterCatalog(options), this.prompts, new HelperBudget(usage, this.clock), options, NullLogger<HelperRunner>.Instance, this.clock);
        HelperSettingsStore settings = new(options);
        HelperStatusStore status = new(options);
        HelperSettingsResolver resolver = new(settings, options, NullLogger<HelperSettingsResolver>.Instance);
        HelperFeed helperFeed = new(this.feed, status, new HelperBudget(usage, this.clock), new HelperRegistry([this.helper]), resolver, NullLogger<HelperFeed>.Instance);
        HelperDispatcher dispatcher = HelperDispatcherFactory.Create(
            [new HelperInvoker<string, ScriptedVerdict>(this.helper, runner, NullLogger.Instance)],
            resolver,
            status,
            options,
            this.clock,
            helperFeed,
            out HelperJudge judge);
        return new Rig(dispatcher, judge, status, settings);
    }

    /// <summary>Judges one input directly.</summary>
    /// <param name="rig">The judge's rig.</param>
    /// <param name="input">What to judge; the key is derived from it.</param>
    private Task<HelperRunReport?> JudgeAsync(Rig rig, string input) =>
        rig.Judge.JudgeAsync(this.helper.Id, "key-" + input, input, TestContext.Current.CancellationToken);

    /// <summary>The judge answers with what the run came to, and applies the Verdict.</summary>
    [Fact]
    public async Task JudgeAsync_Runs_ReturnsTheReport()
    {
        Rig rig = this.Build();
        this.helper.ApplyHandler = static (_, _, _) => new HelperApplied.Changed("Turned it on");

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Equal(new HelperRunReport(HelperRunKind.Applied, "Turned it on"), report);
        Assert.Equal(("key-text", "text"), (this.helper.Applied[0].Key, this.helper.Applied[0].Input));
    }

    /// <summary>A Helper the Human turned off is not judged, and the answer is null.</summary>
    [Fact]
    public async Task JudgeAsync_HelperDisabled_ReturnsNullAndRunsNothing()
    {
        Rig rig = this.Build();
        rig.Settings.Save(this.helper.Id, new HelperSettingsRow(false, null, null, null, 50));

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Null(report);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>With Adapters off in this installation nothing is judged, and the answer is null.</summary>
    [Fact]
    public async Task JudgeAsync_AdaptersOff_ReturnsNullAndRunsNothing()
    {
        Rig rig = this.Build(static team => team.Acp.Enabled = false);

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Null(report);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>A queued judgement and an awaited one together never pass <c>Team:HelperConcurrency</c>: the dispatcher and the caller share one judge.</summary>
    [Fact]
    public async Task JudgeAsync_WhileTheQueueRuns_SharesTheConcurrencyLimit()
    {
        TaskCompletionSource release = new();
        this.process.Hold = release.Task;
        Rig rig = this.Build(static team => team.HelperConcurrency = 1);
        rig.Dispatcher.Enqueue(this.helper.Id, "queued", "first");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => this.process.Calls == 1, TestContext.Current.CancellationToken, TestBudgets.Standard);

        Task<HelperRunReport?> awaited = this.JudgeAsync(rig, "second");
        for (int i = 0; i < 100; i++)
        {
            await Task.Yield();
        }

        // The awaited judgement is still waiting for the queued one's slot.
        Assert.Equal(1, this.process.Calls);
        release.SetResult();
        HelperRunReport? report = await awaited;
        await Eventually.UntilAsync(() => this.helper.Applied.Count == 2, TestContext.Current.CancellationToken, TestBudgets.Standard);

        Assert.Equal(HelperRunKind.Applied, report?.Kind);
        Assert.Equal(1, this.process.PeakInFlight);
    }

    /// <summary>A judgement asked for directly raises the dispatcher's <c>StatusChanged</c>, so the card repaints.</summary>
    [Fact]
    public async Task JudgeAsync_Directly_RaisesTheDispatchersStatusChanged()
    {
        Rig rig = this.Build();
        int raised = 0;
        rig.Dispatcher.StatusChanged += () => Interlocked.Increment(ref raised);

        _ = await this.JudgeAsync(rig, "text");

        Assert.Equal(1, Volatile.Read(ref raised));
    }

    /// <summary>A success is recorded on the card as the last result.</summary>
    [Fact]
    public async Task JudgeAsync_Applied_RecordsTheCardStatus()
    {
        Rig rig = this.Build();
        this.helper.ApplyHandler = static (_, _, _) => new HelperApplied.Changed("Turned it on");

        _ = await this.JudgeAsync(rig, "text");

        HelperStatus status = rig.Status.Get(this.helper.Id);
        Assert.Equal("Turned it on", status.LastResult);
        Assert.Null(status.LastError);
    }

    /// <summary>A call the cap refuses is reported as over budget and tells the Feed once that the Helper paused.</summary>
    [Fact]
    public async Task JudgeAsync_OverBudget_PostsThePauseItem()
    {
        Rig rig = this.Build();
        rig.Settings.Save(this.helper.Id, new HelperSettingsRow(true, null, null, null, 1));

        _ = await this.JudgeAsync(rig, "one");
        HelperRunReport? second = await this.JudgeAsync(rig, "two");
        _ = await this.JudgeAsync(rig, "three");

        Assert.Equal(HelperRunKind.OverBudget, second?.Kind);
        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.HelperPaused, post.SourceType);
    }

    /// <summary>Three failures in a row tell the Feed that the Helper keeps failing.</summary>
    [Fact]
    public async Task JudgeAsync_ThreeFailures_PostTheFailingItem()
    {
        Rig rig = this.Build();
        this.helper.ApplyHandler = static (_, _, _) => new HelperApplied.Refused("Only You can turn off the gate.");

        _ = await this.JudgeAsync(rig, "one");
        _ = await this.JudgeAsync(rig, "two");
        _ = await this.JudgeAsync(rig, "three");

        Assert.Equal(3, rig.Status.Get(this.helper.Id).ConsecutiveFailures);
        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.HelperFailing, post.SourceType);
    }

    /// <summary>An ApplyAsync that throws is a failed report and a failure on the card, not an exception for the caller.</summary>
    [Fact]
    public async Task JudgeAsync_ApplyThrows_IsAFailedReport()
    {
        Rig rig = this.Build();
        this.helper.ApplyHandler = static (_, _, _) => throw new InvalidOperationException("the store is locked");

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Equal(HelperRunKind.Failed, report?.Kind);
        Assert.Equal("the store is locked", report?.Error);
        Assert.Equal("the store is locked", rig.Status.Get(this.helper.Id).LastError);
    }

    /// <summary>The caller's own cancellation is rethrown, and is not recorded as a failure of the Helper.</summary>
    [Fact]
    public async Task JudgeAsync_CallerCancels_Rethrows()
    {
        TaskCompletionSource never = new();
        this.process.Hold = never.Task;
        Rig rig = this.Build();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<HelperRunReport?> judging = rig.Judge.JudgeAsync(this.helper.Id, "key", "text", cts.Token);
        await Eventually.UntilAsync(() => this.process.Calls == 1, TestContext.Current.CancellationToken, TestBudgets.Standard);

        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => judging);
        Assert.Null(rig.Status.Get(this.helper.Id).LastError);
    }

    /// <summary>A call that got an answer says it ran, whether the answer was applied or not.</summary>
    [Fact]
    public async Task JudgeAsync_AnAnswer_SaysTheCallRan()
    {
        Rig rig = this.Build();
        this.helper.ApplyHandler = static (_, input, _) => string.Equals(input, "refuse", StringComparison.Ordinal)
            ? new HelperApplied.Refused("no")
            : new HelperApplied.NoChange();

        HelperRunReport? applied = await this.JudgeAsync(rig, "fine");
        HelperRunReport? refused = await this.JudgeAsync(rig, "refuse");

        Assert.True(applied?.CallRan);
        Assert.True(refused?.CallRan);
    }

    /// <summary>A timeout or an unreadable answer is a failure of a call that ran.</summary>
    [Fact]
    public async Task JudgeAsync_UnreadableAnswer_SaysTheCallRan()
    {
        Rig rig = this.Build();
        this.process.Text = "prose, no JSON";

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Equal(HelperRunKind.Failed, report?.Kind);
        Assert.True(report?.CallRan);
    }

    /// <summary>A call the budget refused, and one answered from the cache, started no process.</summary>
    [Fact]
    public async Task JudgeAsync_OverBudgetAndCacheHit_SayTheCallDidNotRun()
    {
        Rig rig = this.Build();
        rig.Settings.Save(this.helper.Id, new HelperSettingsRow(true, null, null, null, 1));

        HelperRunReport? first = await this.JudgeAsync(rig, "same");
        HelperRunReport? cached = await rig.Judge.JudgeAsync(this.helper.Id, "other-key", "same", TestContext.Current.CancellationToken);
        HelperRunReport? refused = await this.JudgeAsync(rig, "different");

        Assert.True(first?.CallRan);
        Assert.False(cached?.CallRan);
        Assert.Equal(HelperRunKind.OverBudget, refused?.Kind);
        Assert.False(refused?.CallRan);
    }

    /// <summary>An Adapter that is not configured fails before the process starts.</summary>
    [Fact]
    public async Task JudgeAsync_AdapterNotConfigured_SaysTheCallDidNotRun()
    {
        Rig rig = this.Build();
        rig.Settings.Save(this.helper.Id, new HelperSettingsRow(true, "nope", null, null, 50));

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Equal(HelperRunKind.Failed, report?.Kind);
        Assert.False(report?.CallRan);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>An Adapter with no launch command fails as not installed, before the process starts.</summary>
    [Fact]
    public async Task JudgeAsync_AdapterNotInstalled_SaysTheCallDidNotRun()
    {
        Rig rig = this.Build();
        rig.Settings.Save(this.helper.Id, new HelperSettingsRow(true, "bare", null, null, 50));

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Equal(HelperRunKind.Failed, report?.Kind);
        Assert.False(report?.CallRan);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>A Model the session does not offer fails after the process answered, and still says the call did not run.</summary>
    [Fact]
    public async Task JudgeAsync_ModelNotOffered_SaysTheCallDidNotRun()
    {
        Rig rig = this.Build();
        rig.Settings.Save(this.helper.Id, new HelperSettingsRow(true, null, "gone-1", null, 50));
        this.process.Handler = static (_, _) => Task.FromResult(new HelperTurn(string.Empty, ModelOffered: false, 0, null));

        HelperRunReport? report = await this.JudgeAsync(rig, "text");

        Assert.Equal(HelperRunKind.Failed, report?.Kind);
        Assert.False(report?.CallRan);
    }

    /// <summary>A Helper that opts out of the Verdict cache is called again for the same fingerprint.</summary>
    [Fact]
    public async Task JudgeAsync_CachesVerdictsFalse_CallsTwiceForTheSameFingerprint()
    {
        Rig rig = this.Build();
        this.helper.CachesVerdicts = false;

        _ = await this.JudgeAsync(rig, "same");
        _ = await rig.Judge.JudgeAsync(this.helper.Id, "other-key", "same", TestContext.Current.CancellationToken);

        Assert.Equal(2, this.process.Calls);
    }

    /// <summary>A Helper that does not opt out still reuses a cached Verdict for the same fingerprint.</summary>
    [Fact]
    public async Task JudgeAsync_CachesVerdictsDefault_CallsOnceForTheSameFingerprint()
    {
        Rig rig = this.Build();

        _ = await this.JudgeAsync(rig, "same");
        _ = await rig.Judge.JudgeAsync(this.helper.Id, "other-key", "same", TestContext.Current.CancellationToken);

        Assert.Equal(1, this.process.Calls);
    }

    /// <summary>What <see cref="Build"/> made.</summary>
    /// <param name="Dispatcher">The queue.</param>
    /// <param name="Judge">The one judge the queue calls.</param>
    /// <param name="Status">The cards' stored status.</param>
    /// <param name="Settings">The Human's stored choices.</param>
    private sealed record Rig(HelperDispatcher Dispatcher, HelperJudge Judge, HelperStatusStore Status, HelperSettingsStore Settings);
}
