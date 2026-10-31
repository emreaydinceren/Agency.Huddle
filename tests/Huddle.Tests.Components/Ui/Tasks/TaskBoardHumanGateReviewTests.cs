using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskBoardHumanGateTests;
using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;
using ReasonDialog = Agency.Huddle.App.Components.Tasks.ReasonDialog;
using TaskDecisionDialogComponent = Agency.Huddle.App.Components.Tasks.TaskDecisionDialog;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the Opus review findings on P11 (Spec §6.9, rules H-4): the Board decides on the CURRENT Task
/// rather than the card's stale copy, a gated Task in a closed status dropped into Done is refused with
/// the H-4 text, and the decision dialog's Accept, Request changes, Decline and conflict paths.
/// </summary>
public sealed class TaskBoardHumanGateReviewTests
{
    private const string SomeAction = "send the press release";

    /// <summary>A card rendered while ungated, whose Task was gated in the store before the drop, is handled as gated: the Spec §6.9 branch message, no Update to Done.</summary>
    [Fact]
    public async Task Drop_CardRenderedUngatedThenGatedInTheStore_GetsTheGatedBranchNotAnUpdateToDone()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem stale = Ordinary(harness, TaskState.Review);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [stale]);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(stale.Id, new TaskPatch { HumanGate = Optional<bool>.Set(true) }, null, HumanOf(harness)));
        List<TaskChange> changes = Record(harness);

        await rig.StartMoveAsync(stale, TaskState.Done, viaMenu: false);

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal($"{stale.Id} goes to Needs you first (Proof 1/1).", shown.Message);
        Assert.Equal(TaskState.Review, harness.Store.Get(stale.Id)?.Status);
        Assert.DoesNotContain(changes, static c => c.After.Status == TaskState.Done);
    }

    /// <summary>A gated Task in a closed status dropped into Done shows the H-4 refusal in a Snackbar, never "goes to Needs you first", and never calls Update.</summary>
    /// <param name="closed">The closed status the gated Task is in.</param>
    [Theory]
    [InlineData(TaskState.Cancelled)]
    [InlineData(TaskState.Rejected)]
    [InlineData(TaskState.Duplicate)]
    public async Task Drop_GatedTaskInAClosedStatus_ShowsTheH4RefusalAndNeverUpdates(TaskState closed)
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem original = Ordinary(harness, TaskState.Backlog);
        TaskItem gated = Gated(harness, TaskState.Review);
        TaskPatch patch = new()
        {
            Status = closed,
            Reason = closed == TaskState.Duplicate ? null : "not needed",
            DuplicateOf = closed == TaskState.Duplicate ? Optional<TaskId?>.Set(original.Id) : default,
        };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(gated.Id, patch, null, HumanOf(harness))).Task;
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        await rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);

        string human = HumanOf(harness).Name;
        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal(
            $"{task.Id} needs {human}'s acceptance. Move it to Needs You when the Proof is complete; you'll be woken when {human} decides.",
            shown.Message);
        Assert.Equal(closed, harness.Store.Get(task.Id)?.Status);
        Assert.Empty(changes);
    }

    /// <summary>Accept in the dialog on a Task with an Action keeps the card in Needs You, the acceptance valid, and the card's chip reads Accepted once the Board refreshes.</summary>
    [Fact]
    public async Task Dialog_AcceptOnATaskWithAnAction_StaysInNeedsYouAcceptedAndTheChipReadsAccepted()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou, SomeAction);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);
        _ = rig.WaitForDecisionDialog();
        await rig.Dialogs.InvokeAsync(() => rig.DialogButton("Accept").ClickAsync(new MouseEventArgs()));
        await move;

        TaskItem after = Assert.IsType<TaskItem>(harness.Store.Get(task.Id));
        Assert.Equal(TaskState.NeedsYou, after.Status);
        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(after).State);
        rig.Board.Render(p => p.Add(b => b.Tasks, [after]));
        Assert.Equal("Accepted", rig.Card(task.Id).QuerySelector(".task-card-gate-chip")?.TextContent.Trim());
    }

    /// <summary>Request changes and Decline from inside the dialog write the decision, close the dialog and move the Task to In Progress or Rejected.</summary>
    /// <param name="decision">The decision taken in the dialog.</param>
    /// <param name="expected">The status the Task ends in.</param>
    [Theory]
    [InlineData(TaskDecision.RequestChanges, TaskState.InProgress)]
    [InlineData(TaskDecision.Decline, TaskState.Rejected)]
    public async Task Dialog_RequestChangesOrDecline_DecidesClosesTheDialogAndMovesTheTask(TaskDecision decision, TaskState expected)
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        List<TaskChange> changes = Record(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);
        string button = decision == TaskDecision.Decline ? HumanGateCopy.DeclineButton : HumanGateCopy.RequestChangesButton;

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);
        _ = rig.WaitForDecisionDialog();
        if (decision == TaskDecision.Decline)
        {
            await rig.Dialogs.InvokeAsync(() => rig.Dialogs.Find("button[aria-label='" + HumanGateCopy.MoreDecisions + "']").ClickAsync(new MouseEventArgs()));
            rig.Popovers.WaitForAssertion(() => Assert.Single(rig.Popovers.FindAll("div.mud-menu-item"), e => string.Equals(e.TextContent.Trim(), button, StringComparison.Ordinal)));
            await rig.Dialogs.InvokeAsync(() => rig.Popovers.FindAll("div.mud-menu-item").First(e => string.Equals(e.TextContent.Trim(), button, StringComparison.Ordinal)).ClickAsync(new MouseEventArgs()));
        }
        else
        {
            await rig.Dialogs.InvokeAsync(() => rig.DialogButton(button).ClickAsync(new MouseEventArgs()));
        }

        rig.Dialogs.WaitForAssertion(() => Assert.NotEmpty(rig.Dialogs.FindComponents<ReasonDialog>()));
        MudTextField<string> field = rig.Dialogs.FindComponent<ReasonDialog>().FindComponent<MudTextField<string>>().Instance;
        await rig.Dialogs.InvokeAsync(() => field.ValueChanged.InvokeAsync("Because."));
        rig.Dialogs.WaitForAssertion(() => Assert.False(LastButton(rig, button).HasAttribute("disabled")));
        await rig.Dialogs.InvokeAsync(() => LastButton(rig, button).ClickAsync(new MouseEventArgs()));
        await move;

        rig.Dialogs.WaitForAssertion(() => Assert.Empty(rig.Dialogs.FindComponents<TaskDecisionDialogComponent>()));
        Assert.Equal(expected, harness.Store.Get(task.Id)?.Status);
        TaskChange change = Assert.Single(changes);
        Assert.Equal(decision, change.Decision);
        Assert.Equal("Because.", change.Note);
    }

    /// <summary>A Decide conflict inside the dialog shows the version-conflict message and the dialog reloads the new version of the Task.</summary>
    [Fact]
    public async Task Dialog_DecideConflict_ShowsTheConflictMessageAndReloadsTheNewVersion()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);

        Task move = rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);
        IRenderedComponent<TaskDecisionDialogComponent> dialog = rig.WaitForDecisionDialog();
        TaskItem current = Assert.IsType<TaskItem>(harness.Store.Get(task.Id));
        TaskItem renamed = current with { Title = "Renamed behind the dialog" };
        _ = Assert.IsType<TaskItem>(harness.Store.Write(renamed, current.Version, TaskFileFormat.Compose(renamed)));
        await rig.Dialogs.InvokeAsync(() => rig.DialogButton("Accept").ClickAsync(new MouseEventArgs()));

        dialog.WaitForAssertion(() => Assert.Equal("Renamed behind the dialog", dialog.Find(".task-decision-dialog-title").TextContent.Trim()));
        Assert.Equal("This Task changed while you were reading it.", dialog.Find(".task-decision-message").TextContent.Trim());
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
        await rig.Dialogs.InvokeAsync(() => rig.DialogButton("Cancel").ClickAsync());
        await move;
    }

    /// <summary>Clicking Ask in the incomplete-Proof snackbar posts row 12's text from the Human to the open item's owner.</summary>
    [Fact]
    public async Task Snackbar_AskClicked_PostsTheAskMessageToTheOwner()
    {
        SettableProofSource proof = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, proof: proof);
        TaskItem task = Gated(harness, TaskState.NeedsYou);
        proof.Current = new Proof(
        [
            new ProofItem("Draft", true, null, null, ProofItemSource.Process),
            new ProofItem("citation check", false, "Kai", "19 of 23", ProofItemSource.Process),
        ]);
        List<MessagePostedEvent> posts = [];
        harness.RoomEvents.MessagePosted += posts.Add;
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, proof, [task]);
        await rig.StartMoveAsync(task, TaskState.Done, viaMenu: false);

        IElement ask = rig.Toasts.Find("button[aria-label='Ask Kai for citation check']");
        await rig.Toasts.InvokeAsync(() => ask.ClickAsync(new MouseEventArgs()));

        rig.Toasts.WaitForAssertion(() => Assert.Single(posts), TimeSpan.FromSeconds(5));
        MessagePostedEvent post = Assert.Single(posts);
        Assert.Equal($"@Kai, {task.Id} needs citation check before it can come to me.", post.Message.Text);
        Assert.Equal(HumanGateCopy.AskMessage("Kai", task.Id.ToString(), "citation check"), post.Message.Text);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
    }

    /// <summary>The last open dialog button with exactly <paramref name="text"/> (the reason dialog's confirm, not the card's button under it).</summary>
    private static IElement LastButton(Rig rig, string text) =>
        rig.Dialogs.FindAll("button").Last(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));
}
