using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Ui;
using FakeAgentGateway = Agency.Huddle.Tests.Acp.Tools.FakeAgentGateway;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskTriggerService.Preview(TaskItem?, TaskItem, TaskActor)"/> and the guards
/// it applies (Spec §10.1-§10.2, ADR-0026), plus the hosted-service registration
/// (corrections-B3 blocking item 2 and D9 item 4), and - from Task 9.4 - the coalescing and posting
/// of the wake-up Message (Spec §10.3, §10.5, D-11; corrections-B3 D9 items 7-11 and 16-18).
/// </summary>
public sealed class TaskTriggerServiceTests
{
    /// <summary>The Prompt key the wake-up Message is rendered from (Spec §10.5).</summary>
    private const string WakeMessageKey = "task.wake.message";

    /// <summary>A <c>task.wake.message</c> override naming every placeholder once, bar-separated, so one literal comparison pins what each resolved to.</summary>
    private const string ProbeTemplate = "@{{assignee}}|{{taskId}}|{{title}}|{{status}}|{{team}}|{{actor}}|{{changes}}";

    /// <summary>Rounds for <see cref="Concurrent_TwoBatchesSameTask_SerialisedByGate"/> (facts R3: ≥25 rounds of real threads).</summary>
    private const int ConcurrencyRounds = 25;

    /// <summary>The Human, acting in the interface.</summary>
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>A hand edit to the file while Huddle runs (Spec §8.4: <c>TaskActor(OutsideHuddle, humanName, KnownIds.Human)</c>).</summary>
    private static readonly TaskActor OutsideActor = new(TaskActorKind.OutsideHuddle, "You", KnownIds.Human);

    /// <summary>An <c>@</c> followed by U+2060 (word joiner), spelled as a code point so the source holds no invisible character.</summary>
    private static readonly string NeutralisedAt = "@" + (char)0x2060;

    /// <summary>The default <c>Tasks.WakeCoalesceSeconds</c> window (Spec §10.3).</summary>
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    /// <summary>How long <see cref="OverlapProbe"/> holds the first entrant open waiting for a second, concurrent one.</summary>
    private static readonly TimeSpan OverlapWindow = TimeSpan.FromMilliseconds(100);

    /// <summary>Guard 2: no assignee at all previews as <see cref="WakeBlock.NoAssignee"/>.</summary>
    [Fact]
    public async Task Preview_NoAssignee_ReturnsNoAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem after = TestTasks.Make(assignee: null);

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.NoAssignee, preview.Block);
    }

    /// <summary>Guard 3: the assignee is the Human, compared case-insensitively with the Human's Name.</summary>
    [Fact]
    public async Task Preview_AssigneeIsHuman_ReturnsAssigneeIsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        TaskActor actor = new(TaskActorKind.Agent, "Nova", "agent-nova");
        TaskItem after = TestTasks.Make(assignee: "YOU");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.AssigneeIsHuman, preview.Block);
    }

    /// <summary>Guard 4: the self-edit guard, comparing the assignee to the actor's Name ignoring case.</summary>
    [Fact]
    public async Task Preview_AssigneeIsActor_ReturnsAssigneeIsActor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        TaskActor actor = new(TaskActorKind.Agent, "Nova", "agent-nova");
        TaskItem after = TestTasks.Make(assignee: "nova");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.AssigneeIsActor, preview.Block);
    }

    /// <summary>Guard 5: an assignee that isn't a known Persona (a removed Persona, or a typo) previews as <see cref="WakeBlock.NoAssignee"/>.</summary>
    [Fact]
    public async Task Preview_UnknownPersonaAssignee_ReturnsNoAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem after = TestTasks.Make(assignee: "Ghost");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.NoAssignee, preview.Block);
    }

    /// <summary>Guard 6: an Agent actor whose Task wake budget is already spent previews as <see cref="WakeBlock.BudgetPaused"/>.</summary>
    [Fact]
    public async Task Preview_AgentActorBudgetExhausted_ReturnsBudgetPaused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Zeta", "Zeta", "zeta", ["Platform"]), "You are Zeta.");
        TaskItem after = TestTasks.Make(id: "PLAT-0001", assignee: "Zeta");
        _ = harness.Activity.TryConsumeAgentWake(after.Id);
        TaskActor actor = new(TaskActorKind.Agent, "Nova", "agent-nova");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.BudgetPaused, preview.Block);
    }

    /// <summary>Guard 1: <c>Tasks.WakeEnabled</c> false blocks every wake with <see cref="WakeBlock.Disabled"/>.</summary>
    [Fact]
    public async Task Preview_WakeDisabled_ReturnsDisabled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeEnabled = false;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem after = TestTasks.Make(assignee: "Nova");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.Disabled, preview.Block);
    }

    /// <summary>Guard 1 (ADR-0026): Tasks turned off altogether also blocks every wake with <see cref="WakeBlock.Disabled"/>, even when <c>WakeEnabled</c> is still true.</summary>
    [Fact]
    public async Task Preview_TasksDisabled_ReturnsDisabled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.Enabled = false;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem after = TestTasks.Make(assignee: "Nova");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.Disabled, preview.Block);
    }

    /// <summary>
    /// Every guard passed: the preview is <see cref="WakeBlock.None"/> and carries the assignee's
    /// Name and its presence, resolved through <see cref="TaskPresence.For(string, ITeamDirectory, Agency.Huddle.App.Pipes.IAgentGateway, PersonaHealth, TurnActivity)"/>.
    /// </summary>
    [Fact]
    public async Task Preview_ValidChange_ReturnsNone_WithAssigneeNameAndPresence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        User? nova = await harness.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        harness.Gateway.SetOnline(nova.Id);
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem after = TestTasks.Make(assignee: "Nova");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.None, preview.Block);
        Assert.Equal("Nova", preview.AssigneeName);
        Assert.Equal(PresenceState.Asleep, preview.Presence);
    }

    /// <summary>Guard 6, not blocked: an Agent actor whose Task wake budget still has room previews as <see cref="WakeBlock.None"/>.</summary>
    [Fact]
    public async Task Preview_AgentActorBudgetNotExhausted_ReturnsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Zeta", "Zeta", "zeta", ["Platform"]), "You are Zeta.");
        TaskActor actor = new(TaskActorKind.Agent, "Nova", "agent-nova");
        TaskItem after = TestTasks.Make(assignee: "Zeta");

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.Equal(WakeBlock.None, preview.Block);
    }

    /// <summary>
    /// Corrections-B5 D14 item 4: guard 6 only ever applies to an Agent actor. A Human actor's
    /// <see cref="TaskTriggerService.Preview"/> never returns <see cref="WakeBlock.BudgetPaused"/>,
    /// even when the Task's wake budget happens to be exhausted from earlier Agent-made changes.
    /// </summary>
    [Fact]
    public async Task Preview_HumanActorBudgetExhausted_NeverReturnsBudgetPaused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Zeta", "Zeta", "zeta", ["Platform"]), "You are Zeta.");
        TaskItem after = TestTasks.Make(id: "PLAT-0001", assignee: "Zeta");
        _ = harness.Activity.TryConsumeAgentWake(after.Id);
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);

        WakePreview preview = harness.Trigger.Preview(before: null, after, actor);

        Assert.NotEqual(WakeBlock.BudgetPaused, preview.Block);
        Assert.Equal(WakeBlock.None, preview.Block);
    }

    /// <summary>Spec §10.2: reassignment is covered by the same rules, and only <c>After.Assignee</c> is considered - the previous assignee isn't told, even when it was the actor themselves.</summary>
    [Fact]
    public async Task Preview_Reassignment_PreviewsOnlyNewAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Kai", "Kai", "kai", ["Platform"]), "You are Kai.");
        TaskActor actor = new(TaskActorKind.Agent, "Nova", "agent-nova");
        TaskItem before = TestTasks.Make(assignee: "Nova");
        TaskItem after = TestTasks.Make(assignee: "Kai");

        WakePreview preview = harness.Trigger.Preview(before, after, actor);

        Assert.Equal(WakeBlock.None, preview.Block);
        Assert.Equal("Kai", preview.AssigneeName);
    }

    /// <summary>Spec §10.2: closing a Task is a change like any other, so it still previews as waking the assignee.</summary>
    [Fact]
    public async Task Preview_CloseChange_StillPreviewsWaking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem before = TestTasks.Make(status: TaskState.ToDo, assignee: "Nova");
        TaskItem after = TestTasks.Make(status: TaskState.Done, assignee: "Nova");

        WakePreview preview = harness.Trigger.Preview(before, after, actor);

        Assert.Equal(WakeBlock.None, preview.Block);
    }

    /// <summary>Spec §10.2: reopening a Task is likewise a change, and still previews as waking the assignee.</summary>
    [Fact]
    public async Task Preview_ReopenChange_StillPreviewsWaking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        TaskActor actor = new(TaskActorKind.Human, "You", KnownIds.Human);
        TaskItem before = TestTasks.Make(status: TaskState.Done, assignee: "Nova");
        TaskItem after = TestTasks.Make(status: TaskState.ToDo, assignee: "Nova");

        WakePreview preview = harness.Trigger.Preview(before, after, actor);

        Assert.Equal(WakeBlock.None, preview.Block);
    }

    /// <summary>
    /// Corrections-B3 D9 item 4: <see cref="TaskTriggerService"/> is registered as a singleton plus
    /// <c>AddHostedService(sp =&gt; sp.GetRequiredService&lt;TaskTriggerService&gt;())</c>, the same
    /// shape as <see cref="PersonaSupervisor"/>'s pair, so the composed application's
    /// <see cref="IHostedService"/> collection contains the very instance the singleton resolves to.
    /// </summary>
    [Fact]
    public async Task TaskTriggerService_RegisteredAsHostedService_SameInstanceAsSingleton()
    {
        await using TeamWebApplicationFactory factory = new();

        TaskTriggerService singleton = factory.Services.GetRequiredService<TaskTriggerService>();
        IEnumerable<IHostedService> hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.Contains(singleton, hostedServices);
    }

    /// <summary>Spec §10.3: three changes by one actor to one Task inside the window become one Message listing all three Change log summaries, in order, one <c>- </c> bullet each.</summary>
    [Fact]
    public async Task ThreeChangesWithinWindow_OneMessageListingThree()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        harness.Raise(task, HumanActor, "Priority: Medium → High");
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        harness.Raise(task, HumanActor, "Tags: added auth");
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- Status: To Do → In Progress\n- Priority: Medium → High\n- Tags: added auth", post.Message.Text);
    }

    /// <summary>Spec §10.3: the batch waits the configured <c>WakeCoalesceSeconds</c> - nothing is posted a moment before the window closes, and one Message is posted once it has.</summary>
    [Fact]
    public async Task WindowNotYetElapsed_PostsNothing_ThenPostsWhenItElapses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 3;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromMilliseconds(1));
        await harness.Trigger.WhenIdleAsync();
        Assert.Empty(harness.Posts);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await harness.Trigger.WhenIdleAsync();
        Assert.Single(harness.Posts);
    }

    /// <summary>Spec §10.3: a later change by the same actor joins the batch but does not restart the timer, which caps the delay at one window from the first change.</summary>
    [Fact]
    public async Task LaterChangeWithinWindow_DoesNotRestartTimer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "First");
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        harness.Raise(task, HumanActor, "Second");
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- First\n- Second", post.Message.Text);
    }

    /// <summary>Spec §10.3: a change by a different actor to the same Task starts its own batch, so each actor's changes arrive as a separate Message from that actor.</summary>
    [Fact]
    public async Task ChangeByAnotherActor_SeparateMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "By the Human");
        harness.Raise(task, cast.KaiActor, "By Kai");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(2, harness.Posts.Count);
        MessagePostedEvent fromHuman = Assert.Single(harness.Posts, p => p.Message.SenderId == KnownIds.Human);
        MessagePostedEvent fromKai = Assert.Single(harness.Posts, p => p.Message.SenderId == cast.Kai.Id);
        Assert.Equal("- By the Human", fromHuman.Message.Text);
        Assert.Equal("- By Kai", fromKai.Message.Text);
    }

    /// <summary>Spec §10.3: batches are keyed by Task as well as actor, so changes to two Tasks inside one window give two Messages, one per Task.</summary>
    [Fact]
    public async Task ChangesToTwoTasks_TwoMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}:{{changes}}");
        TaskItem first = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem second = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(first, HumanActor, "One");
        harness.Raise(second, HumanActor, "Two");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        string[] expected = ["PLAT-0001:- One", "PLAT-0002:- Two"];
        string[] texts = [.. harness.Posts.Select(p => p.Message.Text).Order(StringComparer.Ordinal)];
        Assert.Equal(expected, texts);
    }

    /// <summary>Spec §10.4 and D-11: a change the Human made is posted as the Human.</summary>
    [Fact]
    public async Task HumanChange_PostedAsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal(cast.Room.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4: an edit made outside Huddle (an <see cref="TaskActorKind.OutsideHuddle"/> actor) is posted as the Human too.</summary>
    [Fact]
    public async Task OutsideHuddleChange_PostedAsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, OutsideActor, "Edited outside Huddle");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
    }

    /// <summary>
    /// Spec §10.4, D-11 and corrections-B3 D9 item 17: an Agent's change is posted as that Agent -
    /// never as the Human - and is recorded through <see cref="OwnPosts"/> as the Agent's own post, so
    /// the actor's session in that Room gets its catch-up line.
    /// </summary>
    [Fact]
    public async Task AgentChange_PostedAsThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(cast.Kai.Id, post.Message.SenderId);
        Assert.StartsWith("@Nova ", post.Message.Text);
        string recorded = Assert.Single(harness.OwnPosts.Take(cast.Kai.Id, cast.Room.Id));
        Assert.Equal(post.Message.Text, recorded);
    }

    /// <summary>Spec §10.5: the default Message starts with the assignee's Mention (which <see cref="MentionParser"/> resolves), names the Task, and tells the assignee to call <c>get_task</c>.</summary>
    [Fact]
    public async Task Message_StartsWithMentionOfAssignee_AndCallsGetTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(
            title: "Fix login", status: TaskState.InProgress, assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.StartsWith("@Nova Task PLAT-0001 \"Fix login\" (In Progress, Platform) was changed by You:", post.Message.Text);
        Assert.Contains("- Status: To Do → In Progress", post.Message.Text);
        Assert.Contains("Call get_task with taskId PLAT-0001 for the full task.", post.Message.Text);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Nova.Id, mentioned.Id);
    }

    /// <summary>
    /// Spec §10.5 and corrections-B3 D9 item 10: every placeholder is rendered - <c>{{assignee}}</c>
    /// and <c>{{title}}</c> from the latest Task, <c>{{taskId}}</c>, <c>{{status}}</c> as
    /// <c>Status.ToWire()</c>, <c>{{team}}</c> as <c>Location.Team</c>, <c>{{actor}}</c> as the
    /// actor's Name, and <c>{{changes}}</c> from each batched <c>Entry.Summary</c>.
    /// </summary>
    [Fact]
    public async Task Message_RendersEveryPlaceholder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, ProbeTemplate);
        TaskItem task = harness.Seed(TestTasks.Make(
            id: "SEC-0007",
            title: "Rotate keys",
            status: TaskState.Review,
            assignee: "Nova",
            originRoomId: cast.Room.Id,
            location: new TaskLocation("Security", null, false)));

        harness.Raise(task, cast.KaiActor, "Status: In Progress → Review");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("@Nova|SEC-0007|Rotate keys|Review|Security|Kai|- Status: In Progress → Review", post.Message.Text);
    }

    /// <summary>Spec §10.3: when the assignee changes inside the window, the latest assignee gets one Message listing every change in the batch, and the previous assignee isn't Mentioned.</summary>
    [Fact]
    public async Task AssigneeChangedDuringWindow_NewAssigneeGetsAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "@{{assignee}}|{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Priority: Medium → High");
        TaskItem reassigned = harness.Replace(task with { Assignee = "Kai" });
        harness.Raise(reassigned, HumanActor, "Assignee: Nova → Kai");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("@Kai|- Priority: Medium → High\n- Assignee: Nova → Kai", post.Message.Text);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Kai.Id, mentioned.Id);
    }

    /// <summary>Corrections-B3 D9 item 7: <c>WakeCoalesceSeconds &lt;= 0</c> fires directly, with no timer and no clock advance.</summary>
    /// <param name="seconds">A non-positive coalesce window.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task WakeCoalesceSecondsZero_PostsImmediately(int seconds)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = seconds;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.StartsWith("@Nova ", post.Message.Text);
    }

    /// <summary>Corrections-B3 D9 item 16: an <c>@</c> in <c>{{title}}</c> is followed by U+2060, so Task text can't Mention a third Teammate who happens to be a Room Member.</summary>
    [Fact]
    public async Task TitleWithAtSign_Neutralised_NoThirdTeammateMentioned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(title: "Pair with @Kai", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Contains("Pair with " + NeutralisedAt + "Kai", post.Message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("@Kai", post.Message.Text, StringComparison.Ordinal);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Nova.Id, mentioned.Id);
    }

    /// <summary>Corrections-B3 D9 item 16: an <c>@</c> in a Change log summary rendered into <c>{{changes}}</c> is neutralised the same way.</summary>
    [Fact]
    public async Task ChangesWithAtSign_Neutralised_NoThirdTeammateMentioned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Description: asked @Kai to review");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Contains("- Description: asked " + NeutralisedAt + "Kai to review", post.Message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("@Kai", post.Message.Text, StringComparison.Ordinal);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(cast.Nova.Id, mentioned.Id);
    }

    /// <summary>Corrections-B3 D9 item 10: a Task deleted inside the window (its latest state is gone) is skipped and logged; another Task's batch in the same window still posts.</summary>
    [Fact]
    public async Task TaskDeletedDuringWindow_SkippedAndLogged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem deleted = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(deleted, HumanActor, "Doomed");
        harness.Raise(control, HumanActor, "Kept");
        File.Delete(deleted.Path);
        harness.Store.RebuildFromWatcher();
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Contains(harness.Logger.Entries, e => e.Message.Contains("PLAT-0001", StringComparison.Ordinal));
    }

    /// <summary>Spec §10.2 guard 4, applied when the batch fires: the assignee's own change posts nothing, while another Task's change in the same window does.</summary>
    [Fact]
    public async Task GuardBlocked_SelfEdit_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(blocked, cast.NovaActor, "Nova edited her own Task");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
    }

    /// <summary>Spec §10.2 guard 3 on the <b>latest</b> Task (Spec §10.3): reassigned to the Human inside the window, the batch posts nothing.</summary>
    [Fact]
    public async Task GuardBlocked_ReassignedToHumanDuringWindow_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(blocked, HumanActor, "Priority: Medium → High");
        TaskItem mine = harness.Replace(blocked with { Assignee = "You" });
        harness.Raise(mine, HumanActor, "Assignee: Nova → You");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
    }

    /// <summary>Spec §10.2 guard 5, applied when the batch fires: an assignee that isn't a known Persona posts nothing.</summary>
    [Fact]
    public async Task GuardBlocked_UnknownPersona_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Ghost", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(blocked, HumanActor, "Status: To Do → In Progress");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
    }

    /// <summary>Spec §10.2 guard 6 through <c>TryConsumeAgentWake</c> (corrections-B3 D9 item 9): an Agent's change to a Task whose wake budget is spent posts nothing, and consumes nothing more.</summary>
    [Fact]
    public async Task GuardBlocked_AgentWakeBudgetSpent_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Nova", originRoomId: cast.Room.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(blocked.Id));

        harness.Raise(blocked, cast.KaiActor, "Status: To Do → In Progress");
        harness.Raise(control, cast.KaiActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Equal(1, harness.Activity.Budget(blocked.Id).Used);
    }

    /// <summary>Corrections-B3 D9 item 9: an Agent-made wake whose outcome is <see cref="WakeOutcome.Offline"/> (the assignee isn't connected) is counted against the Task's wake budget.</summary>
    [Fact]
    public async Task AgentChange_AssigneeOffline_CountsOneWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Corrections-B3 D9 items 9 and 11: an Agent-made wake whose outcome is <see cref="WakeOutcome.Woken"/> (the assignee is online) is counted against the Task's wake budget.</summary>
    [Fact]
    public async Task AgentChange_AssigneeOnline_CountsOneWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>
    /// Corrections-B3 D9 item 9: a wake that doesn't end <see cref="WakeOutcome.Woken"/> or
    /// <see cref="WakeOutcome.Offline"/> is refunded. Kai's first wake fills the Room's one-message
    /// Budget; the second is refused by <see cref="ChatService"/>, so its consumed wake is given back.
    /// </summary>
    [Fact]
    public async Task AgentChange_RoomBudgetSpent_RefundsWake()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You", AgentMessageBudget = 1 };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "First");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
        harness.Raise(task, cast.KaiActor, "Second");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Spec §10.6: only Agent-made wakes count - a Human-made wake posts without touching the Task's wake budget.</summary>
    [Fact]
    public async Task HumanChange_DoesNotConsumeWakeBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Corrections-B3 D9 item 18: an <see cref="TaskActorKind.OutsideHuddle"/> change is still woken (as the Human) but does not reset the Task's wake budget the way a Human change does.</summary>
    [Fact]
    public async Task OutsideHuddleChange_DoesNotResetWakeBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Raise(task, OutsideActor, "Edited outside Huddle");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
        Assert.True(harness.Activity.Budget(task.Id).Exhausted);
    }

    /// <summary>Corrections-B3 D9 item 18: more than 10 outside-edit changes delivered within one coalesce window (a <c>git pull</c>) are logged, and nobody is woken.</summary>
    [Fact]
    public async Task OutsideEdits_MoreThanTenInOneWindow_LoggedNotWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 11);

        foreach (TaskItem task in tasks)
        {
            harness.Raise(task, OutsideActor, "Pulled from git");
        }

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        Assert.Contains(harness.Logger.Entries, e => e.Level >= LogLevel.Information);
    }

    /// <summary>Corrections-B3 D9 item 18, the boundary: exactly 10 outside-edit changes in one window are all woken.</summary>
    [Fact]
    public async Task OutsideEdits_TenInOneWindow_AllWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 10);

        foreach (TaskItem task in tasks)
        {
            harness.Raise(task, OutsideActor, "Pulled from git");
        }

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(10, harness.Posts.Count);
    }

    /// <summary>Corrections-B3 D9 item 18 applies to outside edits only: eleven Human changes to eleven Tasks in one window all wake.</summary>
    [Fact]
    public async Task HumanEdits_ElevenInOneWindow_AllWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem[] tasks = SeedNumbered(harness, cast, count: 11);

        foreach (TaskItem task in tasks)
        {
            harness.Raise(task, HumanActor, "Bulk edit");
        }

        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(11, harness.Posts.Count);
    }

    /// <summary>Corrections-B3 D9 item 7: the batch is removed when its fire <b>starts</b>, so a change arriving while that fire is still posting opens a fresh batch - it is neither folded into the in-flight Message nor lost.</summary>
    [Fact]
    public async Task ChangeDuringFire_StartsNewBatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int held = 0;
        harness.Directory.BeforeGetRoomMembers = () =>
        {
            if (Interlocked.Exchange(ref held, 1) != 0)
            {
                return Task.CompletedTask;
            }

            entered.TrySetResult();
            return release.Task;
        };

        harness.Raise(task, HumanActor, "First");
        harness.Clock.Advance(DefaultWindow);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        harness.Raise(task, HumanActor, "Second");
        release.TrySetResult();
        await harness.Trigger.WhenIdleAsync();
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        string[] expected = ["- First", "- Second"];
        Assert.Equal(expected, harness.Posts.Select(p => p.Message.Text));
    }

    /// <summary>Corrections-B3 D9 item 7: <see cref="TaskTriggerService.StopAsync"/> disposes a pending batch's timer, so a batch still inside its window posts nothing even when the window later elapses.</summary>
    [Fact]
    public async Task StopAsync_PendingBatch_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        harness.Raise(task, HumanActor, "Before stop");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        harness.Raise(task, HumanActor, "Pending at stop");
        await harness.Trigger.StopAsync(ct);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- Before stop", post.Message.Text);
    }

    /// <summary>Corrections-B3 D9 items 7-8: <see cref="TaskTriggerService.StopAsync"/> awaits a fire that is already in flight - it does not complete while the fire is held open, and does once it is released.</summary>
    [Fact]
    public async Task StopAsync_InFlightFire_AwaitsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Directory.BeforeGetRoomMembers = () =>
        {
            entered.TrySetResult();
            return release.Task;
        };

        harness.Raise(task, HumanActor, "In flight at stop");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Task stop = harness.Trigger.StopAsync(ct);
        bool completedWhileHeld = stop.IsCompleted;
        release.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.False(completedWhileHeld);
        Assert.True(stop.IsCompletedSuccessfully);
    }

    /// <summary>Corrections-B3 D9 item 7 and Spec §10.1: the service subscribes to <see cref="TaskEvents.TaskChanged"/> in <see cref="TaskTriggerService.StartAsync"/>, not in its constructor, and <see cref="TaskTriggerService.StopAsync"/> unsubscribes.</summary>
    [Fact]
    public async Task StartAsync_Subscribes_StopAsyncUnsubscribes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);

        int beforeStart = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));
        await harness.Trigger.StartAsync(ct);
        int afterStart = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));
        await harness.Trigger.StopAsync(ct);
        int afterStop = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));

        Assert.Equal(0, beforeStart);
        Assert.Equal(1, afterStart);
        Assert.Equal(0, afterStop);
    }

    /// <summary>Spec §10.1: <see cref="TaskTriggerService.Dispose"/> unsubscribes from <see cref="TaskEvents.TaskChanged"/> too.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        await harness.Trigger.StartAsync(ct);
        int afterStart = SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged));

        harness.Trigger.Dispose();

        Assert.Equal(1, afterStart);
        Assert.Equal(0, SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged)));
    }

    /// <summary>Corrections-B3 D9 item 7: a change raised before <see cref="TaskTriggerService.StartAsync"/> is not seen at all; one raised after it is.</summary>
    [Fact]
    public async Task ChangeBeforeStartAsync_Ignored_ChangeAfterIsWoken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct, start: false);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, HumanActor, "Before start");
        await harness.Trigger.WhenIdleAsync();
        await harness.Trigger.StartAsync(ct);
        harness.Raise(task, HumanActor, "After start");
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- After start", post.Message.Text);
    }

    /// <summary>Corrections-B3 D9 item 7: <c>FireAsync</c> catches and logs whatever a fire throws - <see cref="TaskTriggerService.WhenIdleAsync"/> does not rethrow it, and the next wake still posts.</summary>
    [Fact]
    public async Task FireThrows_ErrorLogged_LaterWakeStillPosts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        int thrown = 0;
        harness.Directory.BeforeGetRoomMembers = () => Interlocked.Exchange(ref thrown, 1) == 0
            ? Task.FromException(new InvalidOperationException("injected directory failure"))
            : Task.CompletedTask;

        harness.Raise(task, HumanActor, "Fails");
        await harness.Trigger.WhenIdleAsync();
        harness.Raise(task, HumanActor, "Succeeds");
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("- Succeeds", post.Message.Text);
        Assert.Contains(harness.Logger.Entries, e => e.Exception is InvalidOperationException);
    }

    /// <summary>
    /// Corrections-B3 D9 item 9 (and facts R3): fires are serialised behind one gate. Each round, two
    /// real threads released together raise one change each to the same Task as two different actors,
    /// with <c>WakeCoalesceSeconds = 0</c> so both fire at once; <see cref="OverlapProbe"/> holds the
    /// first fire open inside the directory and records whether the second ever overlaps it.
    /// </summary>
    [Fact]
    public async Task Concurrent_TwoBatchesSameTask_SerialisedByGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.WakeCoalesceSeconds = 0;
        options.Tasks.AgentWakeBudget = 0;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        OverlapProbe probe = new();
        harness.Directory.BeforeGetRoomMembers = probe.EnterAsync;

        for (int round = 0; round < ConcurrencyRounds; round++)
        {
            probe.NextRound();
            using ManualResetEventSlim go = new(initialState: false);
            Thread human = new(() =>
            {
                go.Wait(ct);
                harness.Raise(task, HumanActor, "By the Human");
            });
            Thread agent = new(() =>
            {
                go.Wait(ct);
                harness.Raise(task, cast.KaiActor, "By Kai");
            });
            human.Start();
            agent.Start();
            go.Set();
            human.Join();
            agent.Join();
            await harness.Trigger.WhenIdleAsync();
        }

        Assert.Equal(ConcurrencyRounds * 2, harness.Posts.Count);
        Assert.Equal(1, probe.MaxConcurrent);
    }

    /// <summary>
    /// Spec §10.4 step 1 and D-12: the origin Room is used when both the sender and the assignee are
    /// Members - even Archived, and even though a non-Archived Room with step 2's exact set also
    /// exists. Green on arrival (9.4 implemented step 1).
    /// </summary>
    [Fact]
    public async Task Origin_BothMembers_UsesOrigin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Nova.Id], ct);
        await harness.Directory.SetRoomArchivedAsync(origin.Id, true, ct);
        _ = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(origin.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 1 → 2: the origin Room doesn't hold the assignee, so the wake falls through to the creator Room {Human, creator, assignee}.</summary>
    [Fact]
    public async Task Origin_AssigneeNotMember_FallsThrough()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Kai.Id], ct);
        Room creatorRoom = await harness.Directory.CreateRoomAsync("Creator", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(creatorRoom.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 1 → 2: the origin Room doesn't hold the Agent sender, so the wake falls through to the creator Room, which does.</summary>
    [Fact]
    public async Task Origin_SenderNotMember_FallsThrough()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Nova.Id], ct);
        Room creatorRoom = await harness.Directory.CreateRoomAsync("Creator", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(creatorRoom.Id, post.Room.Id);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 1 → 2: an origin id that names no Room (deleted) falls through to the creator Room.</summary>
    [Fact]
    public async Task Origin_RoomGone_FallsThrough()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room creatorRoom = await harness.Directory.CreateRoomAsync("Creator", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova", originRoomId: "no-such-room"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(creatorRoom.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 2: with no origin, the Room whose Members are <b>exactly</b> {Human, creator, assignee} is used - not a larger Room that merely contains them.</summary>
    [Fact]
    public async Task CreatorRoom_ExactSetExists_UsesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        User? zeta = await harness.Directory.UpsertAgentUserAsync("Zeta", null, ct);
        Assert.NotNull(zeta);
        _ = await harness.Directory.CreateRoomAsync("Superset", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id, zeta.Id], ct);
        Room exact = await harness.Directory.CreateRoomAsync("Exact", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(exact.Id, post.Room.Id);
    }

    /// <summary>Spec §10.4 step 2 with an Agent sender: the creator is the actor, so the sender is in S and the wake is posted as that Agent in the creator Room.</summary>
    [Fact]
    public async Task CreatorRoom_AgentSenderIsCreator_PostedAsThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room exact = await harness.Directory.CreateRoomAsync("Exact", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(exact.Id, post.Room.Id);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 2: when the creator is the Human, S is {Human, assignee} - the direct Room - rather than a larger Room holding the assignee.</summary>
    [Fact]
    public async Task CreatorIsHuman_UsesDirectRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        _ = await harness.Directory.CreateRoomAsync("Trio", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(direct.Id, post.Room.Id);
    }

    /// <summary>Corrections-B3 D9 item 13: a creator with no user row (never registered, or removed) gives S = {Human, assignee}, so the direct Room is used.</summary>
    [Fact]
    public async Task CreatorUserUnknown_UsesDirectRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Ghost", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(direct.Id, post.Room.Id);
    }

    /// <summary>
    /// Spec §10.4 step 3 and D-13: a third Agent (neither creator nor assignee) isn't in step 2's set,
    /// so step 2 is skipped even though its Room exists, and {Human, actor, assignee} is used, posted
    /// as that Agent.
    /// </summary>
    [Fact]
    public async Task ThirdAgentActor_UsesActorSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        _ = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        Room actorRoom = await harness.Directory.CreateRoomAsync("Actor", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(actorRoom.Id, post.Room.Id);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 3 → 4 and corrections-B3 D9 item 13: no Room for step 3's set, so one is created with that set - the last step tried's - which contains the Agent sender.</summary>
    [Fact]
    public async Task ThirdAgentActor_NoActorSetRoom_CreatesActorSetRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(direct.Id, post.Room.Id);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>Spec §10.4 step 4: no Room fits, so exactly one Room is created for {creator, assignee}, the Human added automatically, and the wake is posted there Mentioning the assignee.</summary>
    [Fact]
    public async Task NoRoom_CreatesOne_HumanAutoAdded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));
        int roomsBefore = (await harness.Directory.GetRoomsAsync(ct)).Count;

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
        Assert.Equal(roomsBefore + 1, (await harness.Directory.GetRoomsAsync(ct)).Count);
        User mentioned = Assert.Single(post.Mentions);
        Assert.Equal(agents.Nova.Id, mentioned.Id);
    }

    /// <summary>Spec §10.4 step 4 when the creator is the Human: S is {Human, assignee}, so the direct Room is created.</summary>
    [Fact]
    public async Task NoRoom_CreatorHuman_CreatesDirectRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>Spec E-12 and D-12: an Archived Room with step 2's exact set is skipped, and a new Room with that set is created.</summary>
    [Fact]
    public async Task ArchivedExactSet_Skipped_CreatesNew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room archived = await harness.Directory.CreateRoomAsync("Old", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        await harness.Directory.SetRoomArchivedAsync(archived.Id, true, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(archived.Id, post.Room.Id);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Kai.Id, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>Spec E-12 and D-12 for the two-Member set: an Archived direct Room is skipped too, and a new direct Room is created.</summary>
    [Fact]
    public async Task ArchivedDirectRoom_Skipped_CreatesNew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room archived = await harness.Directory.CreateRoomAsync("Old", [KnownIds.Human, agents.Nova.Id], ct);
        await harness.Directory.SetRoomArchivedAsync(archived.Id, true, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova"));

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.NotEqual(archived.Id, post.Room.Id);
        Assert.Equal(SortedIds(KnownIds.Human, agents.Nova.Id), SortedIds(post.Members));
    }

    /// <summary>
    /// Corrections-B3 D9 item 9 (<c>CreateRoomForAsync</c> isn't idempotent): two batches for the same
    /// Task fire together and both need a new Room for the same set; the gate serialises them, so
    /// exactly one Room is created and both wakes land in it.
    /// </summary>
    [Fact]
    public async Task TwoFiresNeedingSameNewRoom_CreateExactlyOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));
        int roomsBefore = (await harness.Directory.GetRoomsAsync(ct)).Count;

        harness.Raise(task, HumanActor, "By the Human");
        harness.Raise(task, agents.KaiActor, "By Kai");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(2, harness.Posts.Count);
        Assert.Single(harness.Posts.Select(p => p.Room.Id).Distinct(StringComparer.Ordinal));
        Assert.Equal(roomsBefore + 1, (await harness.Directory.GetRoomsAsync(ct)).Count);
    }

    /// <summary>
    /// Spec §10.4: an assignee who is a known Persona but has never registered (no user row) stops the
    /// wake - nothing is posted and no Room is created - while another Task's wake still posts. Green
    /// on arrival (9.4).
    /// </summary>
    [Fact]
    public async Task AssigneeNeverRegistered_NothingPosted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Zeta", "Zeta", "zeta", ["Platform"]), "You are Zeta.");
        Room direct = await harness.Directory.CreateRoomAsync("Direct", [KnownIds.Human, agents.Nova.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", creator: "You", assignee: "Zeta"));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", creator: "You", assignee: "Nova", originRoomId: direct.Id));
        int roomsBefore = (await harness.Directory.GetRoomsAsync(ct)).Count;

        harness.Raise(blocked, HumanActor, "Status: To Do → In Progress");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Equal(roomsBefore, (await harness.Directory.GetRoomsAsync(ct)).Count);
    }

    /// <summary>Corrections-B3 D9 item 12: an Agent actor whose <see cref="TaskActor.UserId"/> is null is resolved by Name, and the wake is posted as that Agent. Green on arrival (9.4).</summary>
    [Fact]
    public async Task AgentActorWithoutUserId_ResolvedByName_PostedAsThatAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "You", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(task, new TaskActor(TaskActorKind.Agent, "Kai", null), "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal(agents.Kai.Id, post.Message.SenderId);
    }

    /// <summary>
    /// Corrections-B3 D9 item 12: an Agent actor with no user id and no user by that Name posts
    /// nothing - never falling back to the Human - while another Task's Human change still posts.
    /// Green on arrival (9.4).
    /// </summary>
    [Fact]
    public async Task AgentActorUnknown_NothingPosted_NeverAsHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        Room origin = await harness.Directory.CreateRoomAsync("Origin", [KnownIds.Human, agents.Kai.Id, agents.Nova.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{taskId}}");
        TaskItem blocked = harness.Seed(TestTasks.Make(id: "PLAT-0001", creator: "You", assignee: "Nova", originRoomId: origin.Id));
        TaskItem control = harness.Seed(TestTasks.Make(id: "PLAT-0002", creator: "You", assignee: "Nova", originRoomId: origin.Id));

        harness.Raise(blocked, new TaskActor(TaskActorKind.Agent, "Phantom", null), "Status: To Do → In Progress");
        harness.Raise(control, HumanActor, "Kept");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("PLAT-0002", post.Message.Text);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
    }

    /// <summary>
    /// Spec §10.5 outcomes and §10.7: a wake posted while the assignee is online is recorded as
    /// <see cref="WakeOutcome.Woken"/> with the Task, the assignee, the Room's id and name and the
    /// fire time, and <see cref="TaskActivity.Woken"/> is raised once with that record.
    /// </summary>
    [Fact]
    public async Task Woken_RecordedInTaskActivity_WithRoomName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Gateway.SetOnline(cast.Nova.Id);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        List<WakeRecord> raised = [];
        harness.Activity.Woken += raised.Add;

        harness.Raise(task, HumanActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Woken, record.Outcome);
        Assert.Equal("Nova", record.Assignee);
        Assert.Equal(cast.Room.Id, record.RoomId);
        Assert.Equal("Platform", record.RoomName);
        Assert.Equal(harness.Clock.GetUtcNow(), record.At);
        Assert.Equal(record, Assert.Single(raised));
    }

    /// <summary>Spec §10.5: an offline assignee is still posted to, and the outcome is <see cref="WakeOutcome.Offline"/>, with the Room recorded.</summary>
    [Fact]
    public async Task AssigneeOffline_PostedAndOutcomeOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Single(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Offline, record.Outcome);
        Assert.Equal(cast.Room.Id, record.RoomId);
    }

    /// <summary>
    /// Spec §10.5 and corrections-B3 D9 item 14: the Room's Budget is spent by an Agent-authored
    /// Message after the last Human one, so Kai's wake is refused with <c>BudgetExhausted</c>. The
    /// outcome is <see cref="WakeOutcome.BudgetSpent"/> with the Room recorded, nothing escapes as an
    /// error, and the Task wake is refunded.
    /// </summary>
    [Fact]
    public async Task RoomBudgetSpent_OutcomeBudgetSpent_NoThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You", AgentMessageBudget = 1 };
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        _ = await harness.Chat.PostAsync(cast.Room.Id, cast.Kai.Id, "Working on it.", ct: ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        MessagePostedEvent only = Assert.Single(harness.Posts);
        Assert.Equal("Working on it.", only.Message.Text);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.BudgetSpent, record.Outcome);
        Assert.Equal(cast.Room.Id, record.RoomId);
        Assert.Equal("Platform", record.RoomName);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
        Assert.DoesNotContain(harness.Logger.Entries, e => e.Level >= LogLevel.Error);
    }

    /// <summary>Spec §10.6: an Agent's change to a Task whose wake budget is spent is <see cref="WakeOutcome.WakePaused"/> - recorded with no Room, and nothing posted.</summary>
    [Fact]
    public async Task WakeBudgetSpent_OutcomeWakePaused_NoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Raise(task, cast.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.WakePaused, record.Outcome);
        Assert.Equal("Nova", record.Assignee);
        Assert.Null(record.RoomId);
        Assert.Null(record.RoomName);
    }

    /// <summary>Spec §10.6: ten Agent-made wakes are posted; the eleventh change is still saved, but is <see cref="WakeOutcome.WakePaused"/> and posts nothing.</summary>
    [Fact]
    public async Task TenAgentWakes_EleventhPaused_ChangeStillSaved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        for (int i = 1; i <= 11; i++)
        {
            TaskItem saved = harness.Replace(task with { Title = string.Create(CultureInfo.InvariantCulture, $"Step {i}") });
            harness.Raise(saved, cast.KaiActor, "Title changed");
            harness.Clock.Advance(DefaultWindow);
            await harness.Trigger.WhenIdleAsync();
        }

        Assert.Equal(10, harness.Posts.Count);
        Assert.Equal("Step 11", harness.Store.Get(task.Id)?.Title);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.WakePaused, record.Outcome);
    }

    /// <summary>Spec §10.6: <see cref="TaskActivity.Grant"/> on a paused Task lets the next Agent-made change wake the assignee again.</summary>
    [Fact]
    public async Task Grant_ResumesWaking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        await RaiseAndFireAsync(harness, task, cast.KaiActor, "First");
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Paused");
        WakeRecord? paused = harness.Activity.LastWake(task.Id);
        harness.Activity.Grant(task.Id);
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "Resumed");

        Assert.Equal(2, harness.Posts.Count);
        Assert.Equal(WakeOutcome.WakePaused, paused?.Outcome);
        Assert.Equal(WakeOutcome.Offline, harness.Activity.LastWake(task.Id)?.Outcome);
    }

    /// <summary>Spec §10.6: a Human change resets the Task's wake budget, so a paused Task's next Agent-made change wakes the assignee again.</summary>
    [Fact]
    public async Task HumanChange_ResetsBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        harness.Prompts.SetOverride(WakeMessageKey, "{{changes}}");
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        await RaiseAndFireAsync(harness, task, HumanActor, "By the Human");
        await RaiseAndFireAsync(harness, task, cast.KaiActor, "By Kai");

        string[] expected = ["- By the Human", "- By Kai"];
        Assert.Equal(expected, harness.Posts.Select(p => p.Message.Text));
        Assert.Equal(1, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Corrections-B3 D9 item 15: the budget is reset when a Human change is <b>received</b> - before any window elapses, and even when a guard (here, the Task is assigned to the Human) blocks the wake itself.</summary>
    [Fact]
    public async Task HumanChange_GuardBlocked_StillResetsBudgetOnReceipt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        using Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "You", originRoomId: cast.Room.Id));
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        harness.Raise(task, HumanActor, "Priority: Medium → High");
        WakeBudget onReceipt = harness.Activity.Budget(task.Id);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(0, onReceipt.Used);
        Assert.False(onReceipt.Exhausted);
        Assert.Empty(harness.Posts);
    }

    /// <summary>Corrections-B3 D9 item 12 and Spec §10.5: an Agent actor with no user to post as is <see cref="WakeOutcome.Failed"/>, recorded with no Room - never posted as the Human.</summary>
    [Fact]
    public async Task UnknownAgentSender_OutcomeFailed_NoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: cast.Room.Id));

        harness.Raise(task, new TaskActor(TaskActorKind.Agent, "Phantom", null), "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Failed, record.Outcome);
        Assert.Equal("Nova", record.Assignee);
        Assert.Null(record.RoomId);
        Assert.Null(record.RoomName);
    }

    /// <summary>
    /// Spec §10.5: a <see cref="ChatException"/> other than <c>BudgetExhausted</c> - here from
    /// <see cref="ChatService.CreateRoomForAsync"/> meeting a stale Agent while creating step 4's Room -
    /// is <see cref="WakeOutcome.Failed"/>, recorded with no Room, and Kai's Task wake is not left counted.
    /// </summary>
    [Fact]
    public async Task StaleAgentWhileCreatingRoom_OutcomeFailed_NoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        Agents agents = await RegisterAgentsAsync(harness, ct);
        TaskItem task = harness.Seed(TestTasks.Make(creator: "Kai", assignee: "Nova"));
        harness.Directory.HideUserId = agents.Kai.Id;

        harness.Raise(task, agents.KaiActor, "Status: To Do → In Progress");
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Empty(harness.Posts);
        WakeRecord? record = harness.Activity.LastWake(task.Id);
        Assert.NotNull(record);
        Assert.Equal(WakeOutcome.Failed, record.Outcome);
        Assert.Null(record.RoomId);
        Assert.Null(record.RoomName);
        Assert.Equal(0, harness.Activity.Budget(task.Id).Used);
    }

    /// <summary>Raises one change and lets its window elapse, awaiting the fire.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="task">The Task changed.</param>
    /// <param name="actor">Who changed it.</param>
    /// <param name="summary">The change's summary.</param>
    /// <returns>A task that completes once the fire is done.</returns>
    private static async Task RaiseAndFireAsync(Harness harness, TaskItem task, TaskActor actor, string summary)
    {
        harness.Raise(task, actor, summary);
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>Registers Nova and Kai as Personas and Agent users, creates no Room, and starts the service - the arrangement the Room-choice tests (Spec §10.4) build their own Rooms on.</summary>
    /// <param name="harness">The harness to arrange.</param>
    /// <param name="ct">Cancels the directory writes and <see cref="TaskTriggerService.StartAsync"/>.</param>
    /// <returns>The two Agents.</returns>
    private static async Task<Agents> RegisterAgentsAsync(Harness harness, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Kai", "Kai", "kai", ["Platform"]), "You are Kai.");
        User? nova = await harness.Directory.UpsertAgentUserAsync("Nova", null, ct);
        User? kai = await harness.Directory.UpsertAgentUserAsync("Kai", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(kai);
        await harness.Trigger.StartAsync(ct);
        return new Agents(nova, kai);
    }

    /// <summary>Sorts user ids ordinally, so two Member sets compare as sets.</summary>
    /// <param name="ids">The ids.</param>
    /// <returns>The ids, sorted.</returns>
    private static string[] SortedIds(params string[] ids) => [.. ids.Order(StringComparer.Ordinal)];

    /// <summary>Sorts users' ids ordinally, so two Member sets compare as sets.</summary>
    /// <param name="users">The users.</param>
    /// <returns>Their ids, sorted.</returns>
    private static string[] SortedIds(IEnumerable<User> users) => [.. users.Select(u => u.Id).Order(StringComparer.Ordinal)];

    /// <summary>Seeds <paramref name="count"/> Tasks <c>PLAT-0001</c> onwards, each assigned to Nova with <paramref name="cast"/>'s Room as origin.</summary>
    /// <param name="harness">The harness whose store receives the Tasks.</param>
    /// <param name="cast">Supplies the origin Room.</param>
    /// <param name="count">How many Tasks to seed.</param>
    /// <returns>The seeded Tasks, as written.</returns>
    private static TaskItem[] SeedNumbered(Harness harness, Cast cast, int count)
    {
        TaskItem[] tasks = new TaskItem[count];
        for (int i = 0; i < count; i++)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"PLAT-{i + 1:D4}");
            tasks[i] = harness.Seed(TestTasks.Make(id: id, assignee: "Nova", originRoomId: cast.Room.Id));
        }

        return tasks;
    }

    /// <summary>
    /// Registers Nova (the usual assignee) and Kai (a third Teammate) as Personas and Agent users,
    /// creates the origin Room {Human, Nova, Kai} every waking test uses (Spec §10.4 step 1 - both the
    /// sender and the assignee are Members; choosing among Rooms is Task 9.5's), and starts the service.
    /// </summary>
    /// <param name="harness">The harness to arrange.</param>
    /// <param name="ct">Cancels the directory writes and <see cref="TaskTriggerService.StartAsync"/>.</param>
    /// <param name="start">Whether to call <see cref="TaskTriggerService.StartAsync"/>.</param>
    /// <returns>The two Agents and the Room.</returns>
    private static async Task<Cast> StartWakingAsync(Harness harness, CancellationToken ct, bool start = true)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        _ = harness.Personas.Add(new PersonaIdentity("Kai", "Kai", "kai", ["Platform"]), "You are Kai.");
        User? nova = await harness.Directory.UpsertAgentUserAsync("Nova", null, ct);
        User? kai = await harness.Directory.UpsertAgentUserAsync("Kai", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(kai);
        Room room = await harness.Directory.CreateRoomAsync("Platform", [KnownIds.Human, nova.Id, kai.Id], ct);

        if (start)
        {
            await harness.Trigger.StartAsync(ct);
        }

        return new Cast(nova, kai, room);
    }

    /// <summary>Counts <paramref name="source"/>'s current subscribers to the event named <paramref name="eventName"/>, via the compiler-generated backing field (facts R4).</summary>
    /// <param name="source">The hub instance to inspect.</param>
    /// <param name="eventName">The event's own name.</param>
    /// <returns>The number of delegates in the event's invocation list.</returns>
    private static int SubscriberCount(object source, string eventName)
    {
        FieldInfo field = source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{source.GetType().Name}' has no backing field for '{eventName}'.");

        return (field.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>Builds a real <see cref="TaskTriggerService"/> over a real <see cref="SqliteTeamDirectory"/> and <see cref="ChatService"/>, following the constructor snippet in the delivery facts.</summary>
    /// <param name="dir">The temporary data directory backing every store this harness builds.</param>
    /// <param name="options">The Team options the trigger service, the Task activity budget and the Chat service all share.</param>
    /// <param name="ct">Cancels the directory's async setup.</param>
    private static async Task<Harness> CreateHarnessAsync(TempDataDir dir, TeamOptions options, CancellationToken ct)
    {
        IOptions<TeamOptions> teamOptions = Options.Create(options);

        SqliteTeamDirectory sqliteDirectory = new(dir.Options());
        await sqliteDirectory.InitializeAsync(options.HumanName, ct);
        HoldingTeamDirectory directory = new(sqliteDirectory);

        FileChatStore chatStore = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        RoomEvents roomEvents = new(NullLogger<RoomEvents>.Instance);
        ProposalStore proposals = new(roomEvents);
        ChatService chat = new(
            directory, chatStore, roomEvents, new FakeMentionAliasSource(), teamOptions, proposals, NullLogger<ChatService>.Instance);

        PersonaStore personas = new(
            dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        TaskStore store = new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
        TaskEvents events = new();
        TaskActivity activity = new(teamOptions);
        TurnActivity turns = new();
        FakeAgentGateway gateway = new();
        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        FakePromptSource prompts = new();
        OwnPosts ownPosts = new(teamOptions);
        FiringTimeProvider clock = new();
        RecordingLogger<TaskTriggerService> logger = new();

        TaskTriggerService trigger = new(
            events, store, activity, turns, chat, directory, personas, gateway, health, prompts,
            teamOptions, clock, logger, ownPosts: ownPosts);

        Harness harness = new()
        {
            Trigger = trigger,
            Activity = activity,
            Gateway = gateway,
            Directory = directory,
            Personas = personas,
            Store = store,
            Events = events,
            Prompts = prompts,
            OwnPosts = ownPosts,
            Clock = clock,
            Logger = logger,
            Chat = chat,
        };
        roomEvents.MessagePosted += harness.OnMessagePosted;
        return harness;
    }

    /// <summary>The two registered Agents a Room-choice test arranges (<see cref="RegisterAgentsAsync"/>), with no Room.</summary>
    /// <param name="Nova">The usual assignee.</param>
    /// <param name="Kai">A second Teammate: creator, third-Agent actor, or neither.</param>
    private sealed record Agents(User Nova, User Kai)
    {
        /// <summary>Kai, acting through an App Tool.</summary>
        public TaskActor KaiActor => new(TaskActorKind.Agent, this.Kai.Name, this.Kai.Id);
    }

    /// <summary>The two Agents and the origin Room a waking test arranges (<see cref="StartWakingAsync"/>).</summary>
    /// <param name="Nova">The usual assignee.</param>
    /// <param name="Kai">A third Teammate, the usual Agent actor.</param>
    /// <param name="Room">The origin Room {Human, Nova, Kai}.</param>
    private sealed record Cast(User Nova, User Kai, Room Room)
    {
        /// <summary>Kai, acting through an App Tool.</summary>
        public TaskActor KaiActor => new(TaskActorKind.Agent, this.Kai.Name, this.Kai.Id);

        /// <summary>Nova, acting through an App Tool.</summary>
        public TaskActor NovaActor => new(TaskActorKind.Agent, this.Nova.Name, this.Nova.Id);
    }

    /// <summary>The pieces of a composed <see cref="TaskTriggerService"/> a test needs to arrange against, and the disposable ones it owns.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly ConcurrentQueue<MessagePostedEvent> posts = new();

        /// <summary>The service under test.</summary>
        public required TaskTriggerService Trigger { get; init; }

        /// <summary>The wake budget backing <see cref="Trigger"/>.</summary>
        public required TaskActivity Activity { get; init; }

        /// <summary>The fake Agent connection registry backing <see cref="Trigger"/>.</summary>
        public required FakeAgentGateway Gateway { get; init; }

        /// <summary>The Team directory backing <see cref="Trigger"/> and its <see cref="ChatService"/>, with a hook to hold or fail a fire.</summary>
        public required HoldingTeamDirectory Directory { get; init; }

        /// <summary>The Persona store backing <see cref="Trigger"/>.</summary>
        public required PersonaStore Personas { get; init; }

        /// <summary>The Task store <see cref="Trigger"/> reads the latest Task from, on <see cref="TimeProvider.System"/> (corrections-B3 D9 item 8).</summary>
        public required TaskStore Store { get; init; }

        /// <summary>The hub <see cref="Trigger"/> subscribes to.</summary>
        public required TaskEvents Events { get; init; }

        /// <summary>The Chat service <see cref="Trigger"/> posts through, for a test to post its own Messages.</summary>
        public required ChatService Chat { get; init; }

        /// <summary>The Prompt source <see cref="Trigger"/> renders <c>task.wake.message</c> through.</summary>
        public required FakePromptSource Prompts { get; init; }

        /// <summary>Where an Agent-posted wake is recorded as that Agent's own post.</summary>
        public required OwnPosts OwnPosts { get; init; }

        /// <summary>The clock whose timers drive coalescing.</summary>
        public required FiringTimeProvider Clock { get; init; }

        /// <summary>The logger handed to <see cref="Trigger"/>.</summary>
        public required RecordingLogger<TaskTriggerService> Logger { get; init; }

        /// <summary>Every Message posted in any Room since the harness was built, in publish order.</summary>
        public IReadOnlyList<MessagePostedEvent> Posts => [.. this.posts];

        /// <summary>Records one posted Message; subscribed to <see cref="RoomEvents.MessagePosted"/>.</summary>
        /// <param name="posted">The published Message.</param>
        public void OnMessagePosted(MessagePostedEvent posted) => this.posts.Enqueue(posted);

        /// <summary>Writes <paramref name="task"/> as a new file at its layout path, so <see cref="TaskStore.Get"/> returns it.</summary>
        /// <param name="task">The Task to create.</param>
        /// <returns>The Task as written.</returns>
        public TaskItem Seed(TaskItem task)
        {
            TaskItem placed = task with { Path = TaskLayout.PathFor(this.Store.RootDirectory, task.Location, task.Id) };
            TaskItem? written = this.Store.Create(placed, TaskFileFormat.Compose(placed));
            Assert.NotNull(written);
            return written;
        }

        /// <summary>Overwrites the stored Task with <paramref name="updated"/>, so the latest state <see cref="TaskStore.Get"/> returns changes.</summary>
        /// <param name="updated">The Task's new state; its path and location are taken from the stored Task.</param>
        /// <returns>The Task as written.</returns>
        public TaskItem Replace(TaskItem updated)
        {
            TaskItem? current = this.Store.Get(updated.Id);
            Assert.NotNull(current);
            TaskItem placed = updated with { Path = current.Path, Location = current.Location };
            TaskItem? written = this.Store.Write(placed, current.Version, TaskFileFormat.Compose(placed));
            Assert.NotNull(written);
            return written;
        }

        /// <summary>Raises <see cref="TaskEvents.TaskChanged"/> for <paramref name="after"/>, with one Change log entry carrying <paramref name="summary"/>.</summary>
        /// <param name="after">The Task after the change.</param>
        /// <param name="actor">Who made the change.</param>
        /// <param name="summary">The entry's summary, which the Message's <c>{{changes}}</c> lists.</param>
        public void Raise(TaskItem after, TaskActor actor, string summary) =>
            this.Events.RaiseTaskChanged(new TaskChange(
                null, after, [], actor, new ChangeLogEntry(this.Clock.GetUtcNow(), actor.Name, summary)));

        /// <summary>Disposes <see cref="Trigger"/>, the Task store and the Persona store.</summary>
        public void Dispose()
        {
            this.Trigger.Dispose();
            this.Store.Dispose();
            this.Personas.Dispose();
        }
    }

    /// <summary>
    /// Records whether two fires ever overlap: the first entrant of a round waits up to
    /// <see cref="OverlapWindow"/> for a second, concurrent entrant, and <see cref="MaxConcurrent"/>
    /// keeps the most entrants ever seen at once.
    /// </summary>
    private sealed class OverlapProbe
    {
        private readonly Lock gate = new();
        private int inFlight;
        private bool heldThisRound;
        private TaskCompletionSource secondEntrant = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The most entrants ever inside <see cref="EnterAsync"/> at once.</summary>
        public int MaxConcurrent { get; private set; }

        /// <summary>Starts a round: its first entrant will be held open again.</summary>
        public void NextRound()
        {
            lock (this.gate)
            {
                this.heldThisRound = false;
                this.secondEntrant = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>Counts one entrant; holds the round's first one open briefly so a concurrent second can be seen.</summary>
        /// <returns>A task that completes when this entrant leaves.</returns>
        public async Task EnterAsync()
        {
            Task? hold = null;
            lock (this.gate)
            {
                this.inFlight++;
                this.MaxConcurrent = Math.Max(this.MaxConcurrent, this.inFlight);
                if (this.inFlight == 1 && !this.heldThisRound)
                {
                    this.heldThisRound = true;
                    hold = this.secondEntrant.Task;
                }
                else
                {
                    this.secondEntrant.TrySetResult();
                }
            }

            try
            {
                if (hold is not null)
                {
                    await WaitBrieflyAsync(hold);
                }
            }
            finally
            {
                lock (this.gate)
                {
                    this.inFlight--;
                }
            }
        }

        /// <summary>Waits for <paramref name="hold"/> up to <see cref="OverlapWindow"/>.</summary>
        /// <param name="hold">Completes when a second entrant arrives.</param>
        /// <returns>A task that completes when the second entrant arrived or the window passed.</returns>
        private static async Task WaitBrieflyAsync(Task hold)
        {
            try
            {
                await hold.WaitAsync(OverlapWindow);
            }
            catch (TimeoutException)
            {
                // No second entrant arrived inside the window: the fires were serialised, which is
                // the passing case - nothing to record.
            }
        }
    }

    /// <summary>A thread-safe fake <see cref="ILogger{T}"/> that records every call (this repo has no mocking framework).</summary>
    /// <typeparam name="T">The category type the logger stands in for.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> entries = new();

        /// <summary>Every call made so far, in call order.</summary>
        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => [.. this.entries];

        /// <summary>Scoping is irrelevant to these tests, so this returns no scope.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns><see langword="null"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call is recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one call's level, formatted message and exception.</summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused.</param>
        /// <param name="state">The call's structured state.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/>.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            this.entries.Enqueue((logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>
    /// Forwards every <see cref="ITeamDirectory"/> member to a real directory, except that
    /// <see cref="GetRoomMembersAsync"/> first awaits <see cref="BeforeGetRoomMembers"/> when set -
    /// the seam a test uses to hold a fire open (it runs inside every post, <c>ChatService.PostAsync</c>)
    /// or make it throw. The hook deliberately ignores the call's token, so cancelling the service's
    /// lifetime does not release a hold the test owns.
    /// </summary>
    /// <param name="inner">The real directory.</param>
    private sealed class HoldingTeamDirectory(ITeamDirectory inner) : ITeamDirectory
    {
        /// <summary>Awaited before every <see cref="GetRoomMembersAsync"/>; <see langword="null"/> passes straight through.</summary>
        public Func<Task>? BeforeGetRoomMembers { get; set; }

        /// <inheritdoc />
        public Task InitializeAsync(string humanName, CancellationToken ct = default) => inner.InitializeAsync(humanName, ct);

        /// <inheritdoc />
        public Task<User> GetHumanAsync(CancellationToken ct = default) => inner.GetHumanAsync(ct);

        /// <inheritdoc />
        public Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default) =>
            inner.UpsertAgentUserAsync(name, description, ct);

        /// <inheritdoc />
        public bool RenameUser(string userId, string newName) => inner.RenameUser(userId, newName);

        /// <inheritdoc />
        public Task<User?> GetUserAsync(string id, CancellationToken ct = default) =>
            this.HideUserId is { } hidden && string.Equals(hidden, id, StringComparison.Ordinal)
                ? Task.FromResult<User?>(null)
                : inner.GetUserAsync(id, ct);

        /// <summary>When set, <see cref="GetUserAsync"/> reports this user id as unknown - a stale Agent, as <c>ChatService.CreateRoomForAsync</c> sees one.</summary>
        public string? HideUserId { get; set; }

        /// <inheritdoc />
        public Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default) => inner.FindUserByNameAsync(name, ct);

        /// <inheritdoc />
        public User? FindUserByName(string name) => inner.FindUserByName(name);

        /// <inheritdoc />
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default) => inner.GetUsersAsync(ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken ct = default) => inner.GetRoomsAsync(ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<Room>> GetRoomsForUserAsync(string userId, CancellationToken ct = default) =>
            inner.GetRoomsForUserAsync(userId, ct);

        /// <inheritdoc />
        public Task<Room?> GetRoomAsync(string roomId, CancellationToken ct = default) => inner.GetRoomAsync(roomId, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<User>> GetRoomMembersAsync(string roomId, CancellationToken ct = default)
        {
            if (this.BeforeGetRoomMembers is { } hook)
            {
                await hook();
            }

            return await inner.GetRoomMembersAsync(roomId, ct);
        }

        /// <inheritdoc />
        public Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, CancellationToken ct = default) =>
            inner.CreateRoomAsync(name, memberIds, ct);

        /// <inheritdoc />
        public Task<bool> AddMemberAsync(string roomId, string userId, CancellationToken ct = default) =>
            inner.AddMemberAsync(roomId, userId, ct);

        /// <inheritdoc />
        public Task RenameRoomAsync(string roomId, string name, CancellationToken ct = default) => inner.RenameRoomAsync(roomId, name, ct);

        /// <inheritdoc />
        public Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, CancellationToken ct = default) =>
            inner.FindRoomWithExactMembersAsync(humanId, agentId, ct);

        /// <inheritdoc />
        public Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct = default) =>
            inner.FindRoomWithExactMemberSetAsync(memberIds, ct);

        /// <inheritdoc />
        public Task SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default) =>
            inner.SetRoomArchivedAsync(roomId, archived, ct);

        /// <inheritdoc />
        public Task DeleteRoomAsync(string roomId, CancellationToken ct = default) => inner.DeleteRoomAsync(roomId, ct);
    }
}
