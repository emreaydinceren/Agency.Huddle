using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins T10.1 for the Task panel (Spec §6.8, corrections P10 items 5-9; UI copy corrections rows 24 and 27):
/// <c>TaskDetail</c> never offers Move anyway for a gated Task, always mounts the Human gate section,
/// mounts the Proof list when there is Proof, mounts the decision card for a gated Task in Review or
/// Needs You, blocks a decision while edits are unsaved, and drops the Human gate fields on "Take theirs".
/// </summary>
public sealed class TaskDetailHumanGateTests
{
    private const string Nova = "Nova";
    private const string UnsavedNotice = "Save your changes before deciding.";
    private const string DecisionCard = ".task-decision-card";

    /// <summary>An <see cref="IProofSource"/> that answers every Task with one fixed Proof.</summary>
    /// <param name="proof">The Proof to answer with.</param>
    private sealed class StubProofSource(Proof proof) : IProofSource
    {
        /// <summary>Returns the fixed Proof.</summary>
        /// <param name="task">The Task, ignored.</param>
        /// <returns>The fixed Proof.</returns>
        public Proof For(TaskItem task) => proof;
    }

    /// <summary>A Proof of one met item.</summary>
    private static Proof Complete() => new([new ProofItem("Draft", true, null, null, ProofItemSource.Process)]);

    /// <summary>A Proof with one met and one open item.</summary>
    private static Proof Incomplete() => new(
    [
        new ProofItem("Draft", true, null, null, ProofItemSource.Process),
        new ProofItem("citation check", false, "Ruth", null, ProofItemSource.Process),
    ]);

    /// <summary>Creates a gated Task assigned to Nova in <paramref name="status"/>; a Needs You Task is created in Review and then moved by Nova.</summary>
    private static TaskItem CreateGated(TaskToolHarness harness, TaskState status, string? action = null)
    {
        TaskState first = status == TaskState.NeedsYou ? TaskState.Review : status;
        TaskDraft draft = new("Write the reply", "Platform", null, first, Assignee: Nova) { HumanGate = true, Action = action };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
        if (status != TaskState.NeedsYou)
        {
            return task;
        }

        TaskActor nova = new(TaskActorKind.Agent, Nova, "nova-id");
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, nova)).Task;
    }

    /// <summary>The Human's actor.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Renders the Task panel with <paramref name="proof"/> as the Proof source the panel resolves.</summary>
    private static IRenderedComponent<ContainerFragment> RenderOver(MudBunitContext ctx, Proof proof, TaskId id)
    {
        ctx.Services.AddSingleton<IProofSource>(new StubProofSource(proof));
        return RenderDetail(ctx, id, TaskDetailMode.Panel);
    }

    /// <summary>The "Requires your acceptance" switch of the Human gate section.</summary>
    private static IRenderedComponent<MudSwitch<bool>> GateSwitch(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudSwitch<bool>>().Single(s => string.Equals(s.Instance.Label, HumanGateCopy.SwitchLabel, StringComparison.Ordinal));

    /// <summary>The Accept button of the decision card.</summary>
    private static AngleSharp.Dom.IElement AcceptButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(DecisionCard).QuerySelectorAll("button").First(b => string.Equals(b.TextContent.Trim(), "Accept", StringComparison.Ordinal));

    /// <summary>A gate-refused move to Done on an ordinary Task still offers Move anyway.</summary>
    [Fact]
    public async Task MoveAnyway_OrdinaryTask_StillOffered()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        TaskItem task = CreateTask(harness, status: TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Proof.Empty, task.Id);

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Equal(TaskGateText.QaDone, TextOf(cut, ".task-detail-gate-refusal .task-detail-gate-refusal-text"));
        Assert.Single(cut.FindAll(".task-detail-gate-override"));
    }

    /// <summary>A refusal that cannot be overridden (the Human may not mark a gated Task Done) shows its message and no Move anyway.</summary>
    [Fact]
    public async Task MoveAnyway_GatedTask_RefusalNotOverridable_IsNotOffered()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)), proof: new StubProofSource(Complete()));
        TaskItem task = CreateGated(harness, TaskState.Review);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Complete(), task.Id);

        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Single(cut.FindAll(".task-detail-gate-refusal"));
        Assert.Empty(cut.FindAll(".task-detail-gate-override"));
        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A refusal that could be overridden stops offering Move anyway the moment the Task becomes gated underneath it.</summary>
    [Fact]
    public async Task MoveAnyway_RefusalOverridableButTaskBecomesGated_IsNoLongerOffered()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskGateText.BuildGates(gateDir, TaskGateText.Process(TaskGateText.QaGate)));
        TaskItem task = CreateTask(harness, status: TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Proof.Empty, task.Id);
        await SetStatusAsync(cut, TaskState.Done);
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());
        Assert.Single(cut.FindAll(".task-detail-gate-override"));

        TaskResult outside = harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(true) }, null, HumanOf(harness));
        Assert.IsType<TaskResult.Saved>(outside);

        cut.WaitForAssertion(() => Assert.True(GateSwitch(cut).Instance.GetState(x => x.Value)));
        Assert.Single(cut.FindAll(".task-detail-gate-refusal"));
        Assert.Empty(cut.FindAll(".task-detail-gate-override"));
    }

    /// <summary>The Human gate section is mounted for an ordinary Task, with the switch off - it is how the gate is turned on.</summary>
    [Fact]
    public async Task HumanGateSection_OrdinaryTask_IsMountedWithTheSwitchOff()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Proof.Empty, task.Id);

        Assert.Single(cut.FindAll(".task-human-gate"));
        Assert.False(GateSwitch(cut).Instance.GetState(x => x.Value));
    }

    /// <summary>Turning the switch on marks the panel dirty and saves nothing.</summary>
    [Fact]
    public async Task HumanGateSection_SwitchToggled_MarksPendingWithoutSaving()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Proof.Empty, task.Id);
        Assert.False(cut.FindComponent<TaskDetail>().Instance.HasPending);

        IRenderedComponent<MudSwitch<bool>> toggle = GateSwitch(cut);
        await cut.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(true));

        Assert.True(cut.FindComponent<TaskDetail>().Instance.HasPending);
        Assert.True(GateSwitch(cut).Instance.GetState(x => x.Value));
        TaskItem stored = Assert.IsType<TaskItem>(harness.Store.Get(task.Id));
        Assert.False(stored.HumanGate);
        Assert.Equal(task.Version, stored.Version);
    }

    /// <summary>The Proof list is mounted when the Proof has items and left out when it has none.</summary>
    /// <param name="withItems">Whether the Proof source answers with items.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProofList_IsMountedOnlyWhenTheProofHasItems(bool withItems)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,withItems ? Incomplete() : Proof.Empty, task.Id);

        Assert.Equal(withItems ? 1 : 0, cut.FindAll(".task-proof-list").Count);
    }

    /// <summary>The decision card is mounted for a gated Task in Review or Needs You and for no other Task.</summary>
    /// <param name="gated">Whether the Task is gated.</param>
    /// <param name="status">The Task's status.</param>
    /// <param name="mounted">Whether the card is expected.</param>
    [Theory]
    [InlineData(true, TaskState.Review, true)]
    [InlineData(true, TaskState.NeedsYou, true)]
    [InlineData(true, TaskState.InProgress, false)]
    [InlineData(false, TaskState.Review, false)]
    public async Task DecisionCard_IsMountedOnlyForAGatedTaskInReviewOrNeedsYou(bool gated, TaskState status, bool mounted)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: new StubProofSource(Complete()));
        TaskItem task = gated ? CreateGated(harness, status) : CreateTask(harness, status: status, assignee: Nova);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Complete(), task.Id);

        Assert.Equal(mounted ? 1 : 0, cut.FindAll(DecisionCard).Count);
    }

    /// <summary>With unsaved edits the card says to save first and Accept is disabled; without them the notice is absent and Accept is enabled.</summary>
    [Fact]
    public async Task DecisionCard_UnsavedEdits_ShowTheSaveFirstNoticeAndDisableAccept()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: new StubProofSource(Complete()));
        TaskItem task = CreateGated(harness, TaskState.NeedsYou);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Complete(), task.Id);
        Assert.Empty(cut.FindAll(DecisionCard + " .task-decision-unsaved"));
        Assert.False(AcceptButton(cut).HasAttribute("disabled"));

        IRenderedComponent<MudSelect<TaskPriority>> priority = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(TaskPriority.Urgent));

        Assert.Equal("Save your changes before deciding.", UnsavedNotice);
        Assert.Equal(UnsavedNotice, cut.Find(DecisionCard + " .task-decision-unsaved").TextContent.Trim());
        Assert.True(AcceptButton(cut).HasAttribute("disabled"));
    }

    /// <summary>Choosing Theirs on a conflicting Action drops the Human's pending Action, and the section shows their Action.</summary>
    [Fact]
    public async Task Conflict_ActionTakeTheirs_ShowsTheirAction()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Write the reply", "Platform", null, TaskState.Backlog, Assignee: Nova) { HumanGate = true }, HumanOf(harness))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Proof.Empty, task.Id);
        IRenderedComponent<MudTextField<string?>> action = cut.FindComponents<MudTextField<string?>>().Single(f => (f.Instance.Class ?? string.Empty).Contains("task-human-gate-action", StringComparison.Ordinal));
        await cut.InvokeAsync(() => action.Instance.ValueChanged.InvokeAsync("send mine"));
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set("send theirs") }, null, HumanOf(harness)));
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-row")));

        await ChooseTheirsAsync(cut, "action");

        Assert.Equal("send theirs", cut.FindComponent<TaskHumanGate>().Instance.Action);
    }

    /// <summary>Choosing Theirs on a conflicting Human gate switch drops the Human's pending switch, and the section shows their value.</summary>
    [Fact]
    public async Task Conflict_HumanGateTakeTheirs_ShowsTheirSwitch()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: Nova);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderOver(ctx,Proof.Empty, task.Id);
        IRenderedComponent<MudSwitch<bool>> toggle = GateSwitch(cut);
        await cut.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(true));
        await cut.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(false));
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(true) }, null, HumanOf(harness)));
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-row")));

        await ChooseTheirsAsync(cut, "human_gate");

        Assert.True(GateSwitch(cut).Instance.GetState(x => x.Value));
    }

    /// <summary>Raises the Theirs choice of the conflict row whose text contains <paramref name="fieldLabel"/>.</summary>
    private static async Task ChooseTheirsAsync(IRenderedComponent<ContainerFragment> cut, string fieldLabel)
    {
        List<AngleSharp.Dom.IElement> rows = [.. cut.FindAll(".task-detail-conflict-row")];
        int index = rows.FindIndex(r => r.TextContent.Contains(fieldLabel, StringComparison.Ordinal));
        Assert.True(index >= 0, "No conflict row for " + fieldLabel);
        IRenderedComponent<MudRadioGroup<ConflictChoice?>> group = cut.FindComponents<MudRadioGroup<ConflictChoice?>>()[index];
        await cut.InvokeAsync(() => group.Instance.ValueChanged.InvokeAsync(ConflictChoice.Theirs));
    }
}
