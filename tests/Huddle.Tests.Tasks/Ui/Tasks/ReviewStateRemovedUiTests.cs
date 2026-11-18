namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

/// <summary>
/// Slice 1 of #669 (decisions D-11 and D-12): the Kanban board has no Review column, and the decision card shows
/// on a gated Task in Needs You only. The tests name the removed state by its wire text only, because the enum
/// member does not exist once the slice is built.
/// </summary>
public sealed class ReviewStateRemovedUiTests
{
    private const string Nova = "Nova";

    /// <summary>An <see cref="IProofSource"/> that answers every Task with a complete Proof, so the service lets a Task into Needs You.</summary>
    private sealed class CompleteProofSource : IProofSource
    {
        /// <summary>Returns a Proof of one met item.</summary>
        /// <param name="task">The Task, ignored.</param>
        /// <returns>A complete Proof.</returns>
        public Proof For(TaskItem task) => new([new ProofItem("Item", true, null, null, ProofItemSource.Process)]);
    }

    /// <summary>Renders the read view of <paramref name="id"/> beside the popover and dialog providers.</summary>
    private static IRenderedComponent<ContainerFragment> RenderReadView(MudBunitContext ctx, TaskId id) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskReadView>(0);
            builder.AddAttribute(1, nameof(TaskReadView.Id), id);
            builder.CloseComponent();
        });

    /// <summary>Creates a gated Task assigned to Nova in In Progress, as a Human would.</summary>
    private static TaskItem CreateGatedInProgress(TaskToolHarness harness)
    {
        TaskDraft draft = new("Write the reply", "Platform", null, TaskState.InProgress, Assignee: Nova) { HumanGate = true };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, TaskActors.Human(harness.Options.Value))).Task;
    }

    /// <summary>The default Board has no Review column: the headers are the five work columns and Won't do (D-11).</summary>
    [Fact]
    public async Task Board_DefaultColumns_RendersNoReviewColumn()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = new() { Id = "board-test", Name = "Board", Kind = ViewKind.Kanban, Columns = BoardLayout.DefaultColumns, Grouping = [], Fields = [] };
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        _ = ctx.Render<MudPopoverProvider>();

        IRenderedComponent<TaskBoard> cut = ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, [TestTasks.Make(id: "PLAT-0001", status: TaskState.InProgress)]);
        });

        string[] labels = [.. cut.FindAll("div.task-board-column-header .task-board-column-label").Select(e => e.TextContent.Trim())];
        Assert.Equal(["Backlog", "To Do", "In Progress", "Needs You", "Done", "Won't do"], labels);
        Assert.DoesNotContain("Nothing in review.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A gated Task that is In Progress shows no decision card in the read view: Needs You is the Human's decision (D-12).</summary>
    [Fact]
    public async Task ReadView_GatedTaskInProgress_ShowsNoDecisionCard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, proof: new CompleteProofSource());
        TaskItem task = CreateGatedInProgress(harness);
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);

        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        Assert.Equal("In Progress", harness.Store.Get(task.Id)?.Status.ToWire());
        Assert.Empty(cut.FindAll(".task-decision-card"));
    }

    /// <summary>A gated Task in Needs You shows the decision card in the read view (D-12).</summary>
    [Fact]
    public async Task ReadView_GatedTaskNeedsYou_ShowsTheDecisionCard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, proof: new CompleteProofSource());
        TaskItem created = CreateGatedInProgress(harness);
        TaskActor nova = new(TaskActorKind.Agent, Nova, "nova-id");
        TaskItem task = Assert.IsType<TaskResult.Saved>(
            harness.Service.Update(created.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, nova)).Task;
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);

        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        Assert.Equal("Needs You", harness.Store.Get(task.Id)?.Status.ToWire());
        Assert.Single(cut.FindAll(".task-decision-card"));
    }
}
