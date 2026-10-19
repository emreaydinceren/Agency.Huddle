using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.App.Teams;
using TaskDecisionDialogComponent = Agency.Huddle.App.Components.Tasks.TaskDecisionDialog;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins T11.1 (Spec §6.9; UI copy corrections rows 29-34; corrections P11): the Board's Needs You
/// column, the gate chip and lock, the drop table for a gated card dropped into Done (by a drop and by
/// the Move to menu alike), the decision dialog, and that a drop never calls <c>Update</c> with Done
/// for a gated Task.
/// </summary>
public sealed class TaskBoardHumanGateTests
{
    private const string Nova = "Nova";
    private const string SomeAction = "send the press release";

    /// <summary>The ungrouped lane's key.</summary>
    private const string NoLane = "";

    /// <summary>An <see cref="IProofSource"/> whose answer the test sets, shared by the Board and the Task service.</summary>
    private sealed class SettableProofSource : IProofSource
    {
        /// <summary>The Proof every Task gets.</summary>
        public Proof Current { get; set; } = new([new ProofItem("Item", true, null, null, ProofItemSource.Process)]);

        /// <summary>Returns <see cref="Current"/>.</summary>
        /// <param name="task">The Task, ignored.</param>
        /// <returns>The Proof.</returns>
        public Proof For(TaskItem task) => this.Current;
    }

    /// <summary>The Proof with three of four items met; the open one is the citation check owned by Ruth.</summary>
    private static Proof ThreeOfFour() => new(
    [
        new ProofItem("Draft", true, null, null, ProofItemSource.Process),
        new ProofItem("Sources", true, null, null, ProofItemSource.Process),
        new ProofItem("Tone", true, null, null, ProofItemSource.Process),
        new ProofItem("citation check", false, "Ruth", "19 of 23 in #RSH-209", ProofItemSource.Process),
    ]);

    /// <summary>The Human's actor.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a gated Task assigned to Nova in <paramref name="status"/>; a Needs You Task is moved there by Nova.</summary>
    private static TaskItem Gated(TaskToolHarness harness, TaskState status, string? action = null)
    {
        TaskState start = status == TaskState.NeedsYou ? TaskState.Review : status;
        TaskDraft draft = new("Write the reply", "Platform", null, start, Assignee: Nova) { HumanGate = true, Action = action };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
        if (status != TaskState.NeedsYou)
        {
            return task;
        }

        TaskActor nova = new(TaskActorKind.Agent, Nova, "nova-id");
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, nova)).Task;
    }

    /// <summary>Creates an ordinary Task in <paramref name="status"/>.</summary>
    private static TaskItem Ordinary(TaskToolHarness harness, TaskState status)
    {
        TaskDraft draft = new("Plain job", "Platform", null, status);
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
    }

    /// <summary>Renders the providers and the Board over <paramref name="tasks"/> with the default Board View.</summary>
    private static Rig Render(MudBunitContext ctx, TaskToolHarness harness, SettableProofSource proof, IReadOnlyList<TaskItem> tasks)
    {
        ctx.Services.AddSingleton<IProofSource>(proof);
        harness.AddTo(ctx.Services);
        IRenderedComponent<MudPopoverProvider> popovers = ctx.Render<MudPopoverProvider>();
        IRenderedComponent<MudSnackbarProvider> toasts = ctx.Render<MudSnackbarProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = ctx.Render<MudDialogProvider>();
        TaskView view = new() { Id = "board-test", Name = "Board", Kind = ViewKind.Kanban, Columns = BoardLayout.DefaultColumns };
        IRenderedComponent<TaskBoard> board = ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, tasks);
        });
        return new Rig(board, popovers, toasts, dialogs);
    }

    /// <summary>Records every Task change the service raises.</summary>
    private static List<TaskChange> Record(TaskToolHarness harness)
    {
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;
        return changes;
    }

    /// <summary>The Board and its provider hosts, with the two ways to move a card.</summary>
    private sealed class Rig(
        IRenderedComponent<TaskBoard> board,
        IRenderedComponent<MudPopoverProvider> popovers,
        IRenderedComponent<MudSnackbarProvider> toasts,
        IRenderedComponent<MudDialogProvider> dialogs)
    {
        /// <summary>The Board.</summary>
        public IRenderedComponent<TaskBoard> Board { get; } = board;

        /// <summary>Where the card menus render.</summary>
        public IRenderedComponent<MudPopoverProvider> Popovers { get; } = popovers;

        /// <summary>Where the snackbars render.</summary>
        public IRenderedComponent<MudSnackbarProvider> Toasts { get; } = toasts;

        /// <summary>Where the dialogs render.</summary>
        public IRenderedComponent<MudDialogProvider> Dialogs { get; } = dialogs;

        /// <summary>
        /// Moves <paramref name="item"/> to <paramref name="state"/> by a drop or by the card's Move to menu.
        /// A test that expects a dialog holds the returned task without awaiting it, answers the dialog, then awaits.
        /// </summary>
        public async Task StartMoveAsync(TaskItem item, TaskState state, bool viaMenu)
        {
            if (viaMenu)
            {
                await this.Board.InvokeAsync(() => this.Board.Find($"button[aria-label='Actions for {item.Id}']").ClickAsync());
                this.Popovers.WaitForAssertion(() => Assert.Single(this.Popovers.FindAll("div.mud-menu-item"), e => string.Equals(e.TextContent.Trim(), state.ToWire(), StringComparison.Ordinal)));
                await this.Board.InvokeAsync(() => this.Popovers.FindAll("div.mud-menu-item")
                    .First(e => string.Equals(e.TextContent.Trim(), state.ToWire(), StringComparison.Ordinal))
                    .ClickAsync(new MouseEventArgs()));
                return;
            }

            MudDropContainer<TaskItem> container = this.Board.FindComponent<MudDropContainer<TaskItem>>().Instance;
            string origin = BoardLayout.ZoneId(NoLane, item.Status);
            await this.Board.InvokeAsync(() => container.StartTransaction(item, origin, 0, static () => Task.CompletedTask, static () => Task.CompletedTask));
            await this.Board.InvokeAsync(() => container.CommitTransaction(BoardLayout.ZoneId(NoLane, state), false));
        }

        /// <summary>Waits until the decision dialog is open, and returns it.</summary>
        public IRenderedComponent<TaskDecisionDialogComponent> WaitForDecisionDialog()
        {
            this.Dialogs.WaitForAssertion(() => Assert.NotEmpty(this.Dialogs.FindComponents<TaskDecisionDialogComponent>()));
            return this.Dialogs.FindComponent<TaskDecisionDialogComponent>();
        }

        /// <summary>The open dialog's button with exactly <paramref name="text"/>.</summary>
        public IElement DialogButton(string text) =>
            this.Dialogs.FindAll("button").First(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

        /// <summary>The card of <paramref name="id"/>.</summary>
        public IElement Card(TaskId id) =>
            this.Board.FindAll("div.task-card").First(c => c.TextContent.Contains(id.ToString(), StringComparison.Ordinal));
    }

    /// <summary>The Needs You column sits between Review and Done, in the View's order and on screen.</summary>
    [Fact]
    public async Task Columns_NeedsYouSitsBetweenReviewAndDone()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, new SettableProofSource(), []);

        string[] expected = ["Backlog", "To Do", "In Progress", "Review", "Needs You", "Done", "Won't do"];
        string[] configured = [.. BoardLayout.DefaultColumns.Select(c => c.Label)];
        string[] shown = [.. rig.Board.FindAll("div.task-board-column-header .task-board-column-label").Select(e => e.TextContent.Trim())];
        Assert.Equal(expected, configured);
        Assert.Equal(expected, shown);
    }

    /// <summary>A gated card in Needs You with no acceptance reads "Needs you" (row 29).</summary>
    [Fact]
    public async Task Chip_GatedInNeedsYou_ReadsNeedsYou()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, new SettableProofSource(), [task]);

        Assert.Equal("Needs you", HumanGateCopy.BoardChipNeedsYou);
        Assert.Equal("Needs you", rig.Card(task.Id).QuerySelector(".task-card-gate-chip")?.TextContent.Trim());
        Assert.DoesNotContain("Accepted", rig.Card(task.Id).TextContent, StringComparison.Ordinal);
    }

    /// <summary>A gated card in Needs You whose acceptance is valid reads "Accepted" and no longer "Needs you" (row 29).</summary>
    [Fact]
    public async Task Chip_GatedAcceptedWithAnAction_ReadsAccepted()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: new SettableProofSource());
        TaskItem needsYou = Gated(harness, TaskState.NeedsYou, SomeAction);
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(needsYou.Id, TaskDecision.Accept, null, needsYou.Version, HumanOf(harness))).Task;
        Assert.Equal(TaskState.NeedsYou, accepted.Status);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, new SettableProofSource(), [accepted]);

        Assert.Equal("Accepted", HumanGateCopy.BoardChipAccepted);
        Assert.Equal("Accepted", rig.Card(accepted.Id).QuerySelector(".task-card-gate-chip")?.TextContent.Trim());
        Assert.DoesNotContain("Needs you", rig.Card(accepted.Id).TextContent, StringComparison.Ordinal);
    }

    /// <summary>A gated card in another status shows the lock icon labelled "Human gate"; an ordinary card shows none (row 29).</summary>
    [Fact]
    public async Task Lock_GatedInReview_ShowsTheLabelledIcon_OrdinaryCardDoesNot()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem gated = Gated(harness, TaskState.Review);
        TaskItem plain = Ordinary(harness, TaskState.Review);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, new SettableProofSource(), [gated, plain]);

        Assert.Equal("Human gate", HumanGateCopy.LockAriaLabel);
        Assert.NotNull(rig.Card(gated.Id).QuerySelector("[aria-label='Human gate']"));
        Assert.Null(rig.Card(plain.Id).QuerySelector("[aria-label='Human gate']"));
        Assert.DoesNotContain("Needs you", rig.Card(gated.Id).TextContent, StringComparison.Ordinal);
    }

    /// <summary>A gated card in Review or earlier dropped into Done is refused with row 30, stays put, and saves nothing (row 1 of the drop table).</summary>
    /// <param name="start">The card's status.</param>
    /// <param name="viaMenu">Whether the card's Move to menu is used instead of a drop.</param>
    [Theory]
    [InlineData(TaskState.Review, false)]
    [InlineData(TaskState.Review, true)]
    [InlineData(TaskState.ToDo, false)]
    [InlineData(TaskState.ToDo, true)]
    public async Task Drop_GatedBeforeNeedsYou_IsRefusedWithGoesToNeedsYouFirst(TaskState start, bool viaMenu)
    {
        SettableProofSource proof = new() { Current = ThreeOfFour() };
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, start);
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        await rig.StartMoveAsync(task, TaskState.Done, viaMenu);

        string expected = $"{task.Id} goes to Needs you first (Proof 3/4).";
        Assert.Equal(expected, HumanGateCopy.GoesToNeedsYouFirst(task.Id.ToString(), 3, 4));
        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal(expected, shown.Message);
        Assert.Equal(start, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(changes);
    }

    /// <summary>A gated card in Needs You with incomplete Proof is refused with the locked notice and an Ask button, and stays put (row 31).</summary>
    /// <param name="viaMenu">Whether the card's Move to menu is used instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Drop_NeedsYouWithIncompleteProof_ShowsTheLockedNoticeAndAsk(bool viaMenu)
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        proof.Current = ThreeOfFour();
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        await rig.StartMoveAsync(task, TaskState.Done, viaMenu);

        rig.Toasts.WaitForAssertion(() => Assert.Contains(
            "Proof 3/4 — citation check still open (Ruth, 19 of 23 in #RSH-209). Accept unlocks when it's in.",
            rig.Toasts.Markup,
            StringComparison.Ordinal));
        Assert.Single(rig.Toasts.FindAll("button"), b => string.Equals(b.TextContent.Trim(), "Ask Ruth", StringComparison.Ordinal));
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(changes);
        Assert.Empty(rig.Dialogs.FindComponents<TaskDecisionDialogComponent>());
    }

    /// <summary>A gated card in Needs You whose acceptance is already valid is refused with row 34 and stays put.</summary>
    /// <param name="viaMenu">Whether the card's Move to menu is used instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Drop_AlreadyAccepted_IsRefusedWithRow34(bool viaMenu)
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem needsYou = Gated(harness, TaskState.NeedsYou, SomeAction);
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(needsYou.Id, TaskDecision.Accept, null, needsYou.Version, HumanOf(harness))).Task;
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [accepted]);

        await rig.StartMoveAsync(accepted, TaskState.Done, viaMenu);

        string expected = $"{accepted.Id} is accepted: {Nova} moves it to Done after the Action.";
        Assert.Equal(expected, HumanGateCopy.AlreadyAccepted(accepted.Id.ToString(), Nova));
        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal(expected, shown.Message);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(accepted.Id)?.Status);
        Assert.Empty(changes);
        Assert.Empty(rig.Dialogs.FindComponents<TaskDecisionDialogComponent>());
    }

    /// <summary>A gated card in Needs You with complete Proof opens the decision dialog titled "Your decision on id" (row 32); nothing is saved while it is open.</summary>
    /// <param name="viaMenu">Whether the card's Move to menu is used instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Drop_NeedsYouWithCompleteProof_OpensTheDecisionDialog(bool viaMenu)
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu);

        IRenderedComponent<TaskDecisionDialogComponent> dialog = rig.WaitForDecisionDialog();
        Assert.Equal($"Your decision on {task.Id}", HumanGateCopy.DecisionDialogTitle(task.Id.ToString()));
        rig.Dialogs.WaitForAssertion(() => Assert.Equal($"Your decision on {task.Id}", rig.Dialogs.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.NotEmpty(dialog.FindComponents<TaskDecisionCard>());
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(changes);
        await rig.Dialogs.InvokeAsync(() => rig.DialogButton("Cancel").ClickAsync());
        await move;
    }

    /// <summary>Cancel in the decision dialog closes it and leaves the card in Needs You (row 33).</summary>
    [Fact]
    public async Task Dialog_Cancel_LeavesTheCardWhereItWas()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);
        _ = rig.WaitForDecisionDialog();
        Assert.Equal("Cancel", HumanGateCopy.CancelButton);
        await rig.Dialogs.InvokeAsync(() => rig.DialogButton("Cancel").ClickAsync());
        await move;

        rig.Dialogs.WaitForAssertion(() => Assert.Empty(rig.Dialogs.FindComponents<TaskDecisionDialogComponent>()));
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(changes);
    }

    /// <summary>The board opens the decision dialog with Escape enabled (MudBlazor handles Escape through a JS key interceptor bUnit cannot drive), and the Canceled result that Escape produces leaves the card in Needs You.</summary>
    [Fact]
    public async Task Dialog_EscapeIsEnabledAndCanceledLeavesTheCardWhereItWas()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);
        _ = rig.WaitForDecisionDialog();
        IMudDialogInstance instance = rig.Dialogs.FindComponent<MudDialogContainer>().Instance;
        Assert.True(instance.Options.CloseOnEscapeKey);
        await rig.Dialogs.InvokeAsync(() => instance.Cancel());
        await move;

        rig.Dialogs.WaitForAssertion(() => Assert.Empty(rig.Dialogs.FindComponents<TaskDecisionDialogComponent>()));
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(changes);
    }

    /// <summary>Accept in the dialog calls Decide, so the card lands in Done through a decision; the Board never saves Done through an Update.</summary>
    [Fact]
    public async Task Dialog_Accept_CallsDecide_NeverUpdateWithDone()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);
        _ = rig.WaitForDecisionDialog();
        await rig.Dialogs.InvokeAsync(() => rig.DialogButton("Accept").ClickAsync(new MouseEventArgs()));
        await move;

        Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status);
        TaskChange change = Assert.Single(changes);
        Assert.Equal(TaskDecision.Accept, change.Decision);
        Assert.DoesNotContain(changes, c => c.Decision is null && c.After.Status == TaskState.Done);
    }

    /// <summary>An ordinary card dropped into Done moves as before: no dialog, no refusal, saved as Done.</summary>
    /// <param name="viaMenu">Whether the card's Move to menu is used instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Drop_OrdinaryCard_IsUnchanged(bool viaMenu)
    {
        SettableProofSource proof = new() { Current = ThreeOfFour() };
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Ordinary(harness, TaskState.Review);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        await rig.StartMoveAsync(task, TaskState.Done, viaMenu);

        Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Empty(rig.Dialogs.FindComponents<TaskDecisionDialogComponent>());
    }
}
