using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.NeedsHuman;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// The Needs Human Helper from a Teammate's save to the gate turning on: the trigger's filters, the dispatcher, the runner
/// and the Helper's apply step over a real <c>TaskService</c> (Helpers Spec §6.9 to §6.11, plan HL-12, HL-13).
/// </summary>
public sealed class NeedsHumanFlowTests : IAsyncLifetime
{
    private const string GateOn = """{"needsHuman": true, "reason": "Sends a reply to a customer"}""";
    private const string GateOff = """{"needsHuman": false, "reason": "Stays inside the team"}""";

    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Leo = new(TaskActorKind.Agent, "Leo", "leo-id");

    private readonly TaskToolHarness harness = new(clock: new ManualTimeProvider(Noon));
    private readonly FakeHelperProcessRunner process = new();
    private readonly FiringTimeProvider clock = new();
    private readonly HelperSettingsStore settingsStore;
    private readonly HelperStatusStore status;
    private readonly NeedsHumanOverrideStore overrides;
    private readonly HelperDispatcher dispatcher;
    private readonly NeedsHumanTrigger trigger;
    private readonly NeedsHumanHelper helper;

    /// <summary>Wires the real stores, runner, dispatcher, Helper and trigger over the harness's Task service.</summary>
    public NeedsHumanFlowTests()
    {
        TeamOptions team = this.harness.Options.Value;
        team.Acp.Enabled = true;
        team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["adapter.js"], UsesToolNamePrefix = false }];
        this.process.Text = GateOn;

        this.settingsStore = new HelperSettingsStore(this.harness.Options);
        HelperSettingsResolver resolver = new(this.settingsStore, this.harness.Options, NullLogger<HelperSettingsResolver>.Instance);
        this.status = new HelperStatusStore(this.harness.Options);
        this.overrides = new NeedsHumanOverrideStore(this.harness.Options, this.clock);
        this.helper = new NeedsHumanHelper(this.harness.Store, this.harness.Service, this.overrides, resolver);
        HelperRunner runner = new(
            this.process,
            new AdapterCatalog(this.harness.Options),
            new FakePromptSource(),
            new HelperBudget(new HelperUsageStore(this.harness.Options), this.clock),
            this.harness.Options,
            NullLogger<HelperRunner>.Instance,
            this.clock);
        this.dispatcher = new HelperDispatcher(
            [new HelperInvoker<NeedsHumanInput, NeedsHumanVerdict>(this.helper, runner)],
            resolver,
            this.status,
            this.harness.Options,
            NullLogger<HelperDispatcher>.Instance,
            this.clock);
        this.trigger = new NeedsHumanTrigger(this.harness.Events, this.dispatcher, this.overrides, NullLogger<NeedsHumanTrigger>.Instance);
    }

    /// <summary>Subscribes the trigger to the Task events.</summary>
    public async ValueTask InitializeAsync() => await this.trigger.StartAsync(TestContext.Current.CancellationToken);

    /// <summary>Stops the trigger and removes the harness.</summary>
    public async ValueTask DisposeAsync()
    {
        await this.trigger.StopAsync(CancellationToken.None);
        this.dispatcher.Dispose();
        this.harness.Dispose();
    }

    /// <summary>Creates a Task as <paramref name="actor"/> in Review.</summary>
    /// <param name="actor">Who creates it.</param>
    /// <param name="title">The title.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private TaskItem Create(TaskActor actor, string title = "Reply to Acme about the renewal price", string? action = null)
    {
        TaskDraft draft = new(title, Team, null, TaskState.Review) { Action = action };
        return Assert.IsType<TaskResult.Saved>(this.harness.Service.Create(draft, actor)).Task;
    }

    /// <summary>Lets the dispatcher's wait pass.</summary>
    private void PassTheWait() => this.clock.Advance(TimeSpan.FromSeconds(11));

    /// <summary>Waits until the process runner has been called <paramref name="count"/> times.</summary>
    /// <param name="count">The number of calls.</param>
    private Task CallsAsync(int count) =>
        Eventually.UntilAsync(() => this.process.Calls >= count, TestContext.Current.CancellationToken, TestBudgets.Standard);

    /// <summary>Waits until the Helper has recorded a result or an error.</summary>
    private Task RecordedAsync() =>
        Eventually.UntilAsync(
            () => this.status.Get(NeedsHumanHelper.HelperId) is { LastResult: not null } or { LastError: not null },
            TestContext.Current.CancellationToken,
            TestBudgets.Standard);

    /// <summary>Holds still long enough for a wrongly queued judgement to have run.</summary>
    private static Task SettleAsync() => Task.Delay(150, TestContext.Current.CancellationToken);

    /// <summary>A Teammate's new Task that needs the Human gets the gate, logged by the Helper with the reason (U-1).</summary>
    [Fact]
    public async Task TeammateCreate_NeedsHuman_TurnsGateOnWithReason()
    {
        TaskItem task = this.Create(Leo);

        this.PassTheWait();
        await this.RecordedAsync();

        TaskItem? after = this.harness.Store.Get(task.Id);
        Assert.NotNull(after);
        Assert.True(after.HumanGate);
        ChangeLogEntry entry = after.ChangeLog[^1];
        Assert.Equal("Needs Human (Helper)", entry.Actor);
        Assert.Equal("human gate on (reason: Sends a reply to a customer)", entry.Summary);
        Assert.Equal($"Turned the gate on for {task.Id}", this.status.Get(NeedsHumanHelper.HelperId).LastResult);
    }

    /// <summary>The model saying no writes nothing to the Task (U-2).</summary>
    [Fact]
    public async Task TeammateCreate_DoesNotNeedHuman_WritesNothing()
    {
        this.process.Text = GateOff;
        TaskItem task = this.Create(Leo, "Summarise the interview notes");

        this.PassTheWait();
        await this.RecordedAsync();

        TaskItem? after = this.harness.Store.Get(task.Id);
        Assert.NotNull(after);
        Assert.False(after.HumanGate);
        Assert.Equal(task.Version, after.Version);
    }

    /// <summary>A Task the Human creates is not judged (U-6).</summary>
    [Fact]
    public async Task HumanCreate_IsNotJudged()
    {
        _ = this.Create(TaskActors.Human(this.harness.Options.Value));

        this.PassTheWait();
        await SettleAsync();

        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>A Task that already has an Action is gated, so it is not judged (U-12).</summary>
    [Fact]
    public async Task TeammateCreate_WithAction_IsNotJudged()
    {
        _ = this.Create(Leo, action: "Send the reply");

        this.PassTheWait();
        await SettleAsync();

        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>Moving a Task to In Progress, which changes none of the text the Helper reads, makes no call (U-4).</summary>
    [Fact]
    public async Task TeammateStatusMove_IsNotJudgedAgain()
    {
        this.process.Text = GateOff;
        TaskItem task = this.Create(Leo, "Summarise the interview notes");
        this.PassTheWait();
        await this.CallsAsync(1);
        await this.RecordedAsync();

        _ = this.harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, Leo);
        this.PassTheWait();
        await SettleAsync();

        Assert.Equal(1, this.process.Calls);
    }

    /// <summary>An Evidence line added to the description is not a content change (U-4).</summary>
    [Fact]
    public async Task TeammateEvidenceLine_IsNotJudgedAgain()
    {
        this.process.Text = GateOff;
        TaskItem task = this.Create(Leo, "Summarise the interview notes");
        this.PassTheWait();
        await this.RecordedAsync();
        TaskItem? current = this.harness.Store.Get(task.Id);
        Assert.NotNull(current);
        HelperStatus before = this.status.Get(NeedsHumanHelper.HelperId);

        _ = this.harness.Service.Update(
            task.Id,
            new TaskPatch { Description = current.Description + "\n\n" + TaskFileFormat.EvidenceHeading + "\n- notes read" },
            null,
            Leo);
        this.PassTheWait();
        await SettleAsync();

        // The content cache would answer a repeat judgement without a process call, so the proof that nothing was queued is
        // that the card's last run did not move.
        Assert.Equal(1, this.process.Calls);
        Assert.Equal(before, this.status.Get(NeedsHumanHelper.HelperId));
    }

    /// <summary>A Teammate rewriting the title of an open Task is judged again.</summary>
    [Fact]
    public async Task TeammateTitleRewrite_IsJudgedAgain()
    {
        this.process.Text = GateOff;
        TaskItem task = this.Create(Leo, "Summarise the interview notes");
        this.PassTheWait();
        await this.CallsAsync(1);
        await this.RecordedAsync();

        _ = this.harness.Service.Update(task.Id, new TaskPatch { Title = "Send Acme the new contract" }, null, Leo);
        this.PassTheWait();
        await this.CallsAsync(2);

        Assert.Equal(2, this.process.Calls);
    }

    /// <summary>A rewrite of a closed Task is not judged.</summary>
    [Fact]
    public async Task TeammateRewriteOfClosedTask_IsNotJudged()
    {
        this.process.Text = GateOff;
        TaskItem task = this.Create(Leo, "Summarise the interview notes");
        this.PassTheWait();
        await this.RecordedAsync();

        _ = this.harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Cancelled, Title = "Never mind" }, null, Leo);
        this.PassTheWait();
        await SettleAsync();

        Assert.Equal(1, this.process.Calls);
    }

    /// <summary>The Helper's own gate-on write is not judged again (U-12).</summary>
    [Fact]
    public async Task HelperOwnWrite_IsNotJudged()
    {
        _ = this.Create(Leo);
        this.PassTheWait();
        await this.RecordedAsync();

        this.PassTheWait();
        await SettleAsync();

        Assert.Equal(1, this.process.Calls);
    }

    /// <summary>Once the Human turns the gate off the Helper never judges that Task again, even with the Helper switched off at the time (U-5).</summary>
    /// <param name="helperOnWhenHumanActs">Whether the Helper is on when the Human turns the gate off.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HumanTurnsGateOff_IsNeverJudgedAgain(bool helperOnWhenHumanActs)
    {
        TaskActor human = TaskActors.Human(this.harness.Options.Value);
        TaskDraft draft = new("Reply to Acme", Team, null, TaskState.Review) { HumanGate = true };
        TaskItem task = Assert.IsType<TaskResult.Saved>(this.harness.Service.Create(draft, human)).Task;
        if (!helperOnWhenHumanActs)
        {
            this.settingsStore.Save(NeedsHumanHelper.HelperId, new HelperSettingsRow(false, null, null, null, 50));
        }

        _ = this.harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(false) }, null, human);
        this.settingsStore.Delete(NeedsHumanHelper.HelperId);
        _ = this.harness.Service.Update(task.Id, new TaskPatch { Title = "Send Acme the new contract" }, null, Leo);
        this.PassTheWait();
        await SettleAsync();

        Assert.True(this.overrides.Contains(task.Id.ToString()));
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>Three quick saves are one judgement of the last text.</summary>
    [Fact]
    public async Task RepeatedSaves_AreOneCall()
    {
        TaskItem task = this.Create(Leo, "Draft one");
        _ = this.harness.Service.Update(task.Id, new TaskPatch { Title = "Draft two" }, null, Leo);
        _ = this.harness.Service.Update(task.Id, new TaskPatch { Title = "Draft three" }, null, Leo);

        this.PassTheWait();
        await this.RecordedAsync();

        Assert.Equal(1, this.process.Calls);
        Assert.Contains("Draft three", Assert.Single(this.process.Requests).Prompt, StringComparison.Ordinal); // contains-ok: the prompt carries the Task id, team and tags around the title.
    }

    /// <summary>A Verdict that arrives after the Task was rewritten is dropped, and the newer save is judged on its own (U-9).</summary>
    [Fact]
    public async Task VerdictForOldText_IsDroppedAndNewTextIsJudged()
    {
        TaskCompletionSource firstVerdict = new();
        this.process.Hold = firstVerdict.Task;
        TaskItem task = this.Create(Leo, "Draft one");
        this.PassTheWait();
        await this.CallsAsync(1);

        _ = this.harness.Service.Update(task.Id, new TaskPatch { Title = "Send Acme the new contract" }, null, Leo);
        firstVerdict.SetResult();
        await Eventually.UntilAsync(
            () => this.status.Get(NeedsHumanHelper.HelperId).LastResult?.StartsWith("Dropped a stale verdict", StringComparison.Ordinal) == true,
            TestContext.Current.CancellationToken,
            TestBudgets.Standard);

        Assert.False(this.harness.Store.Get(task.Id)?.HumanGate);
        this.PassTheWait();
        await Eventually.UntilAsync(() => this.harness.Store.Get(task.Id)?.HumanGate == true, TestContext.Current.CancellationToken, TestBudgets.Standard);
        Assert.Equal(2, this.process.Calls);
    }

    /// <summary>A failure in the dispatcher never reaches the Teammate's save.</summary>
    [Fact]
    public async Task TriggerFailure_DoesNotReachTheSaver()
    {
        HelperDispatcher noHelpers = new(
            [],
            new HelperSettingsResolver(this.settingsStore, this.harness.Options, NullLogger<HelperSettingsResolver>.Instance),
            this.status,
            this.harness.Options,
            NullLogger<HelperDispatcher>.Instance,
            this.clock);
        NeedsHumanTrigger broken = new(this.harness.Events, noHelpers, this.overrides, NullLogger<NeedsHumanTrigger>.Instance);
        await this.trigger.StopAsync(TestContext.Current.CancellationToken);
        await broken.StartAsync(TestContext.Current.CancellationToken);

        TaskItem task = this.Create(Leo);

        Assert.NotNull(this.harness.Store.Get(task.Id));
        noHelpers.Dispose();
        await broken.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A Verdict of false writes nothing.</summary>
    [Fact]
    public async Task Apply_FalseVerdict_IsNoChange()
    {
        TaskItem task = this.Create(Leo);

        HelperApplied applied = await this.helper.ApplyAsync(task.Id.ToString(), InputOf(task), new NeedsHumanVerdict(false, null), TestContext.Current.CancellationToken);

        Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.Equal(task.Version, this.harness.Store.Get(task.Id)?.Version);
    }

    /// <summary>A Verdict is dropped when the Task is gone, already gated, closed, overridden or judged for other text, or the Helper was switched off.</summary>
    [Fact]
    public async Task Apply_StaleVerdicts_AreDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        NeedsHumanVerdict yes = new(true, "outward");

        TaskItem gated = this.Create(Leo, "A", action: "Do it");
        Assert.Equal(
            "the Task is already gated",
            Assert.IsType<HelperApplied.Dropped>(await this.helper.ApplyAsync("k", InputOf(gated), yes, ct)).Why);

        TaskItem stale = this.Create(Leo, "B");
        NeedsHumanInput oldText = InputOf(stale) with { Fingerprint = "0123456789ab" };
        Assert.Equal(
            "the Task was saved again after it was judged",
            Assert.IsType<HelperApplied.Dropped>(await this.helper.ApplyAsync("k", oldText, yes, ct)).Why);

        TaskItem overridden = this.Create(Leo, "C");
        this.overrides.Add(overridden.Id.ToString());
        Assert.Equal(
            "the Human turned the gate off on this Task",
            Assert.IsType<HelperApplied.Dropped>(await this.helper.ApplyAsync("k", InputOf(overridden), yes, ct)).Why);

        TaskItem closed = this.Create(Leo, "D");
        _ = this.harness.Service.Update(closed.Id, new TaskPatch { Status = TaskState.Cancelled }, null, Leo);
        Assert.Equal(
            "the Task is closed",
            Assert.IsType<HelperApplied.Dropped>(await this.helper.ApplyAsync("k", InputOf(this.harness.Store.Get(closed.Id)!), yes, ct)).Why);

        TaskItem off = this.Create(Leo, "E");
        this.settingsStore.Save(NeedsHumanHelper.HelperId, new HelperSettingsRow(false, null, null, null, 50));
        Assert.Equal(
            "the Helper was turned off",
            Assert.IsType<HelperApplied.Dropped>(await this.helper.ApplyAsync("k", InputOf(off), yes, ct)).Why);

        NeedsHumanInput missing = InputOf(off) with { Id = new TaskId("ZZZ", 999) };
        this.settingsStore.Delete(NeedsHumanHelper.HelperId);
        Assert.Equal(
            "the Task is gone",
            Assert.IsType<HelperApplied.Dropped>(await this.helper.ApplyAsync("k", missing, yes, ct)).Why);
    }

    /// <summary>A reason longer than the Task limit is cut to 197 characters and an ellipsis rather than losing the Verdict.</summary>
    [Fact]
    public async Task Apply_LongReason_IsCutToTheTaskLimit()
    {
        TaskItem task = this.Create(Leo);
        string reason = new('x', 300);

        HelperApplied applied = await this.helper.ApplyAsync("k", InputOf(task), new NeedsHumanVerdict(true, reason), TestContext.Current.CancellationToken);

        Assert.IsType<HelperApplied.Changed>(applied);
        TaskItem? stored = this.harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal($"human gate on (reason: {new string('x', 197)}...)", stored.ChangeLog[^1].Summary);
    }

    /// <summary>A true Verdict with no reason is refused by Validate; a false one needs none.</summary>
    [Fact]
    public void Validate_TrueWithoutReason_IsRefused()
    {
        Assert.Equal("a true answer needs a reason", this.helper.Validate(new NeedsHumanVerdict(true, "  ")));
        Assert.Null(this.helper.Validate(new NeedsHumanVerdict(false, null)));
        Assert.Null(this.helper.Validate(new NeedsHumanVerdict(true, "Sends a reply")));
    }

    /// <summary>The Helper presents itself on its card as Needs Human, with the sentence the Human reads and the defaults the spec fixes.</summary>
    [Fact]
    public void Identity_IsWhatTheCardShows()
    {
        Assert.Equal("NeedsHuman", this.helper.Id.Value);
        Assert.Equal("Needs Human", this.helper.DisplayName);
        Assert.Equal("Turns on the Human gate when a Teammate's Task leaves the team or can't be undone. It never turns it off.", this.helper.Description);
        Assert.Equal(["helper.needsHuman.system", "helper.needsHuman.task"], this.helper.PromptKeys);
        Assert.Equal(new HelperDefaults(true, "claude", "haiku", "low", 50), this.helper.Defaults);
    }

    /// <summary>The prompt values carry the Task, drop the Evidence section and cut a long description.</summary>
    [Fact]
    public void PromptValues_FillTheTask_WithoutEvidence_AndCut()
    {
        NeedsHumanInput input = new(new TaskId("RSH", 208), "abc", "Reply to Acme", "Body\n\n" + TaskFileFormat.EvidenceHeading + "\n- secret line", "Research", ["a", "b"]);

        IReadOnlyDictionary<string, string> values = this.helper.PromptValues(input);

        Assert.Equal("RSH-0208", values["{{taskId}}"]);
        Assert.Equal("Research", values["{{team}}"]);
        Assert.Equal("a, b", values["{{tags}}"]);
        Assert.Equal("Body", values["{{description}}"]);
        string cut = this.helper.PromptValues(input with { Description = new string('y', 7000), Tags = [] })["{{description}}"];
        Assert.Equal(6000 + "[…cut]".Length, cut.Length);
        Assert.Equal("[…cut]", cut[^6..]);
        Assert.Equal("none", this.helper.PromptValues(input with { Tags = [] })["{{tags}}"]);
    }

    /// <summary>The input a trigger would capture from a Task.</summary>
    /// <param name="task">The Task.</param>
    private static NeedsHumanInput InputOf(TaskItem task) =>
        new(task.Id, ContentFingerprint.Of(task.Title, task.Action, task.Description), task.Title, task.Description, task.Location.Team, task.Tags);
}
