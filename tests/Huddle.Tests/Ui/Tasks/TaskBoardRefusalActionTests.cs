using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins UX-V26 on the Board: a move a process gate refuses stays an error snackbar and now offers a way
/// forward, an "Open task" action that opens the Task; a Task that no longer exists offers none.
/// </summary>
public sealed class TaskBoardRefusalActionTests
{
    /// <summary>A gate refusal keeps its message and severity and adds "Open task", which raises <c>OnOpenTask</c> with the moved Task's id.</summary>
    [Fact]
    public async Task GateRefused_OffersOpenTask_WhichOpensTheTask()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: TaskServiceGateTests.BuildGates(gateDir, TaskServiceGateTests.Process(TaskServiceGateTests.QaGate)));
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);
        TaskId? opened = null;
        await using MudBunitContext ctx = NewContext(harness);
        (IRenderedComponent<TaskBoard> board, IRenderedComponent<MudPopoverProvider> popovers, IRenderedComponent<MudSnackbarProvider> toasts) = RenderBoard(ctx, moved, id => opened = id);

        await MoveViaMenuAsync(board, popovers, moved, TaskState.Done);

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal($"Could not move {moved.Id}: {TaskServiceGateTests.QaDone}", shown.Message);
        Assert.Equal(Severity.Error, shown.Severity);
        toasts.WaitForAssertion(() => Assert.Single(toasts.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Open task", StringComparison.Ordinal)));
        await toasts.InvokeAsync(() => toasts.FindAll("button").Single(button => string.Equals(button.TextContent.Trim(), "Open task", StringComparison.Ordinal)).ClickAsync());
        Assert.Equal(moved.Id, opened);
    }

    /// <summary>A Task deleted after the Board rendered it has nothing to open, so its snackbar offers no action.</summary>
    [Fact]
    public async Task NotFound_OffersNoAction()
    {
        using TaskToolHarness harness = new();
        TaskItem moved = CreateTask(harness, "Ship it", TaskState.ToDo);
        await using MudBunitContext ctx = NewContext(harness);
        (IRenderedComponent<TaskBoard> board, IRenderedComponent<MudPopoverProvider> popovers, IRenderedComponent<MudSnackbarProvider> toasts) = RenderBoard(ctx, moved, static _ => { });
        File.Delete(moved.Path);
        harness.Store.RebuildFromWatcher();

        await MoveViaMenuAsync(board, popovers, moved, TaskState.InProgress);

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal($"Could not move {moved.Id}: it no longer exists.", shown.Message);
        toasts.WaitForAssertion(() => Assert.NotEmpty(toasts.FindAll(".mud-snackbar")));
        Assert.DoesNotContain(toasts.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Open task", StringComparison.Ordinal));
    }

    /// <summary>Renders the popover and dialog hosts and a default Board over <paramref name="task"/>.</summary>
    /// <param name="ctx">The bUnit context, already holding the harness's services.</param>
    /// <param name="task">The one Task on the Board.</param>
    /// <param name="onOpenTask">Receives the id the Board asks to open.</param>
    private static (IRenderedComponent<TaskBoard> Board, IRenderedComponent<MudPopoverProvider> Popovers, IRenderedComponent<MudSnackbarProvider> Toasts) RenderBoard(MudBunitContext ctx, TaskItem task, Action<TaskId> onOpenTask)
    {
        IRenderedComponent<MudPopoverProvider> popovers = ctx.Render<MudPopoverProvider>();
        IRenderedComponent<MudSnackbarProvider> toasts = ctx.Render<MudSnackbarProvider>();
        _ = ctx.Render<MudDialogProvider>();
        TaskView view = new()
        {
            Id = "board-test",
            Name = "Board",
            Kind = ViewKind.Board,
            Columns = [new BoardColumn("To do", [TaskState.ToDo]), new BoardColumn("In progress", [TaskState.InProgress]), new BoardColumn("Done", [TaskState.Done])],
        };
        IRenderedComponent<TaskBoard> board = ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, (IReadOnlyList<TaskItem>)[task]);
            p.Add(b => b.OnOpenTask, EventCallback.Factory.Create<TaskId>(ctx, onOpenTask));
        });
        return (board, popovers, toasts);
    }

    /// <summary>Moves <paramref name="task"/> to <paramref name="target"/> through its card's Actions menu.</summary>
    /// <param name="board">The rendered Board.</param>
    /// <param name="popovers">Where the card menu renders.</param>
    /// <param name="task">The Task to move.</param>
    /// <param name="target">The state to move it to.</param>
    private static async Task MoveViaMenuAsync(IRenderedComponent<TaskBoard> board, IRenderedComponent<MudPopoverProvider> popovers, TaskItem task, TaskState target)
    {
        await board.InvokeAsync(() => board.Find($"button[aria-label='Actions for {task.Id}']").ClickAsync());
        popovers.WaitForAssertion(() => Assert.Single(popovers.FindAll("div.mud-menu-item"), e => string.Equals(e.TextContent.Trim(), target.ToWire(), StringComparison.Ordinal)));
        await board.InvokeAsync(() => popovers.FindAll("div.mud-menu-item")
            .First(e => string.Equals(e.TextContent.Trim(), target.ToWire(), StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs()));
    }
}
