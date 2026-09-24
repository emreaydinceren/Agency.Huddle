using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
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
/// (corrections-B3 blocking item 2 and D9 item 4).
/// </summary>
public sealed class TaskTriggerServiceTests
{
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

    /// <summary>Builds a real <see cref="TaskTriggerService"/> over a real <see cref="SqliteTeamDirectory"/> and <see cref="ChatService"/>, following the constructor snippet in the delivery facts.</summary>
    /// <param name="dir">The temporary data directory backing every store this harness builds.</param>
    /// <param name="options">The Team options the trigger service, the Task activity budget and the Chat service all share.</param>
    /// <param name="ct">Cancels the directory's async setup.</param>
    private static async Task<Harness> CreateHarnessAsync(TempDataDir dir, TeamOptions options, CancellationToken ct)
    {
        IOptions<TeamOptions> teamOptions = Options.Create(options);

        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync(options.HumanName, ct);

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

        TaskTriggerService trigger = new(
            events, store, activity, turns, chat, directory, personas, gateway, health, prompts,
            teamOptions, TimeProvider.System, NullLogger<TaskTriggerService>.Instance);

        return new Harness(trigger, activity, gateway, directory, personas, store);
    }

    /// <summary>The pieces of a composed <see cref="TaskTriggerService"/> a test needs to arrange against, and the disposable ones it owns.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TaskStore store;

        /// <summary>Creates the harness, taking ownership of <paramref name="store"/> and <paramref name="personas"/> for disposal.</summary>
        /// <param name="trigger">The service under test.</param>
        /// <param name="activity">The wake budget backing <paramref name="trigger"/>.</param>
        /// <param name="gateway">The fake Agent connection registry backing <paramref name="trigger"/>.</param>
        /// <param name="directory">The Team directory backing <paramref name="trigger"/>.</param>
        /// <param name="personas">The Persona store backing <paramref name="trigger"/>.</param>
        /// <param name="store">The Task store backing <paramref name="trigger"/>.</param>
        public Harness(TaskTriggerService trigger, TaskActivity activity, FakeAgentGateway gateway, ITeamDirectory directory, PersonaStore personas, TaskStore store)
        {
            this.Trigger = trigger;
            this.Activity = activity;
            this.Gateway = gateway;
            this.Directory = directory;
            this.Personas = personas;
            this.store = store;
        }

        /// <summary>The service under test.</summary>
        public TaskTriggerService Trigger { get; }

        /// <summary>The wake budget backing <see cref="Trigger"/>.</summary>
        public TaskActivity Activity { get; }

        /// <summary>The fake Agent connection registry backing <see cref="Trigger"/>.</summary>
        public FakeAgentGateway Gateway { get; }

        /// <summary>The Team directory backing <see cref="Trigger"/>.</summary>
        public ITeamDirectory Directory { get; }

        /// <summary>The Persona store backing <see cref="Trigger"/>.</summary>
        public PersonaStore Personas { get; }

        /// <summary>Disposes <see cref="Trigger"/>, the Task store and the Persona store.</summary>
        public void Dispose()
        {
            this.Trigger.Dispose();
            this.store.Dispose();
            this.Personas.Dispose();
        }
    }
}
