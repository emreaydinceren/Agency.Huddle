namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins plan Task 15.2.t, RED-only: Spec §13.13.1 (the copy button and its <i>Copy link</i> menu item)
/// and §13.13.3 (the <c>/tasks/item/{TaskIdText}</c> route). None of this exists on either
/// <see cref="TasksPage"/> or <see cref="TaskDetail"/> yet - <see cref="TasksPage"/> carries no
/// <c>TaskIdText</c> parameter, and <see cref="TaskDetail"/>'s header has no copy button, no
/// <i>Copy link</i> menu and no Closed chip. Corrections-B6 "D15.2" and corrections-B7 "15.2.i" pin
/// the surrounding decisions this file follows: <see cref="TaskDetail.Id"/> (not a new parameter) is
/// the id TaskDetail loads; a failed copy leaves the id selected via a <c>.task-id</c> CSS class
/// (<see cref="AppCssTasksTests"/> pins that rule); and closing the panel opened by the route replaces
/// the URL rather than pushing a new history entry.
/// <para>
/// The Board card's own "Copy id" wiring (corrections-B7 "15.2.i" item 2) is pinned once here
/// (<see cref="TaskBoard_CardCopyIdMenuItem_RaisesOnCopyIdWithTheTaskId"/>) against
/// <see cref="TaskBoard"/>, which already exposes the matching <see cref="TaskCard.OnCopyId"/> callback
/// on its cards. The List row's own "Copy id" (Spec §13.13.1: "each List row also get a Copy id item")
/// is the delivery manager's settled design (J52): <see cref="TaskListView"/> gets a trailing
/// <c>TemplateColumn</c> holding a ⋮ <c>MudMenu</c> per row - activator <c>aria-label="Actions for
/// {id}"</c>, <c>Icons.Material.Filled.MoreVert</c>, mirroring <see cref="TaskCard"/>'s own menu idiom -
/// with one <c>MudMenuItem</c> "Copy id" raising a new <c>TaskListView.OnCopyId</c>
/// (<c>EventCallback&lt;TaskId&gt;</c>); the page wires both <see cref="TaskBoard.OnCopyId"/> and
/// <c>TaskListView.OnCopyId</c> to the same copy-and-toast path as the detail header's button.
/// </para>
/// </summary>
public sealed class TaskRouteAndCopyTests
{
    /// <summary>Spec §13.13.3 step 2: the item route loads the last-opened View (here, none saved, so All Tasks) and opens the named Task's panel on top of it.</summary>
    [Fact]
    public async Task TasksPage_ItemRoute_ExistingTask_OpensDetailPanelOverLastView()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Ship the thing");
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle.tasks.lastView").SetResult(null);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.TaskIdText), task.Id.ToString());
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Equal("All Tasks", cut.Find(".tasks-header h1").TextContent.Trim()));
        Assert.Equal(task.Id, cut.FindComponent<TaskDetail>().Instance.Id);
    }

    /// <summary>Spec §13.13.3 step 1: an id that parses but names no Task shows the exact wording with <c>role="status"</c>.</summary>
    [Fact]
    public async Task TasksPage_ItemRoute_UnknownButValidId_ShowsNoTaskAlertWithStatusRole()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle.tasks.lastView").SetResult(null);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.TaskIdText), "PLAT-0999");
            builder.CloseComponent();
        });

        var alert = cut.Find(".tasks-item-not-found-alert");
        Assert.Equal("No task PLAT-0999.", alert.TextContent.Trim());
        Assert.Equal("status", alert.GetAttribute("role"));
    }

    /// <summary>Spec §13.13.3 step 1's "invalid" branch: text that fails <see cref="TaskId.TryParse"/> gets the same alert, naming the raw text typed.</summary>
    [Fact]
    public async Task TasksPage_ItemRoute_MalformedIdText_ShowsNoTaskAlertWithStatusRole()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle.tasks.lastView").SetResult(null);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.TaskIdText), "not-an-id");
            builder.CloseComponent();
        });

        var alert = cut.Find(".tasks-item-not-found-alert");
        Assert.Equal("No task not-an-id.", alert.TextContent.Trim());
        Assert.Equal("status", alert.GetAttribute("role"));
    }

    /// <summary>Spec §13.13.3 step 4: closing the panel the item route opened replaces the URL with <c>/tasks/{viewId}</c>, so Back does not reopen it (corrections-B6 "D15.2" item 5).</summary>
    [Fact]
    public async Task TasksPage_ItemRouteClosingDrawer_ReplacesUrlToTheEffectiveView()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle.tasks.lastView").SetResult(null);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.TaskIdText), task.Id.ToString());
            builder.CloseComponent();
        });
        var drawer = cut.FindComponent<MudDrawer>();

        await cut.InvokeAsync(() => drawer.Instance.OpenChanged.InvokeAsync(false));

        NavigationHistory last = Assert.Single(navigation.History, h => h.Uri.EndsWith($"/tasks/{ViewStore.AllTasksId}", StringComparison.Ordinal));
        Assert.True(last.Options.ReplaceHistoryEntry);
    }

    /// <summary>Spec §13.13.3 step 3: a Closed Task's panel shows a <i>Closed</i> chip in the header.</summary>
    [Fact]
    public async Task TaskDetail_ClosedTask_ShowsClosedChipInHeader()
    {
        using TaskToolHarness harness = new();
        TaskItem created = CreateTask(harness);
        TaskItem closed = Assert.IsType<TaskResult.Saved>(harness.Service.Close(created.Id, TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = NewContext(harness);

        var cut = RenderPanel(ctx, closed.Id);

        Assert.Equal("Closed", cut.Find(".task-detail-closed-chip").TextContent.Trim());
    }

    /// <summary>Spec §13.13.1: the copy button calls <c>huddleClipboard.copy</c> with exactly the id, and Spec's own wording shows a success toast when the call returns <see langword="true"/>.</summary>
    [Fact]
    public async Task TaskDetail_CopyIdButton_CallsHuddleClipboardCopyWithExactId_AndShowsSuccessToastWhenTrue()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", task.Id.ToString()).SetResult(true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        var cut = RenderPanel(ctx, task.Id);
        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Copy task id {task.Id}\"]").ClickAsync());

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal(task.Id.ToString(), Assert.Single(invocation.Arguments));
        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = snackbar.ShownSnackbars.Single();
        Assert.Equal($"Copied {task.Id}", shown.Message);
        Assert.Equal(Severity.Success, shown.Severity);
    }

    /// <summary>Spec §13.13.1: a failed copy (the call returns <see langword="false"/>) shows the exact warning wording instead.</summary>
    [Fact]
    public async Task TaskDetail_CopyIdButton_ClipboardReturnsFalse_ShowsWarningToastWithCtrlCMessage()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", task.Id.ToString()).SetResult(false);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        var cut = RenderPanel(ctx, task.Id);
        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Copy task id {task.Id}\"]").ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = snackbar.ShownSnackbars.Single();
        Assert.Equal("Couldn't copy. The id is selected; press Ctrl+C.", shown.Message);
        Assert.Equal(Severity.Warning, shown.Severity);
    }

    /// <summary>Spec §13.13.1: <i>Copy link</i> passes <c>NavigationManager.BaseUri + "tasks/item/{id}"</c>, not the bare id.</summary>
    [Fact]
    public async Task TaskDetail_CopyLinkMenuItem_PassesBaseUriPlusRoutePath()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string expected = $"{navigation.BaseUri}tasks/item/{task.Id}";
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", expected).SetResult(true);

        var cut = RenderPanel(ctx, task.Id);
        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Copy options\"]").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".task-detail-copy-link").ClickAsync());

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    /// <summary>Corrections-B7 "15.2.i" item 2: the Board card's ⋮ menu's existing "Copy id" item (already wired to <see cref="TaskCard.OnCopyId"/>) must itself be threaded through <see cref="TaskBoard"/> to its caller.</summary>
    [Fact]
    public async Task TaskBoard_CardCopyIdMenuItem_RaisesOnCopyIdWithTheTaskId()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo);
        TaskId? copied = null;
        void OnCopyId(TaskId id) => copied = id;

        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskBoard>(0);
            builder.AddAttribute(1, nameof(TaskBoard.View), new TaskView { Id = "board-test", Name = "Board", Kind = ViewKind.Board, Columns = BoardLayout.DefaultColumns });
            builder.AddAttribute(2, nameof(TaskBoard.Tasks), (IReadOnlyList<TaskItem>)[task]);
            builder.AddAttribute(3, nameof(TaskBoard.OnCopyId), EventCallback.Factory.Create<TaskId>(this, OnCopyId));
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Move PLAT-0001\"]").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll(".mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), "Copy id", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal(task.Id, copied);
    }

    /// <summary>Settled design (J52): a List row's ⋮ menu (activator <c>aria-label="Actions for {id}"</c>, the <see cref="TaskCard"/> idiom) raises <c>TaskListView.OnCopyId</c> with the row's id when "Copy id" is chosen.</summary>
    [Fact]
    public async Task TaskListView_RowMenuCopyId_RaisesOnCopyIdWithTheTaskId()
    {
        using TempDataDir dataDir = new();
        Agency.Huddle.App.Acp.PersonaStore personas = TestTaskStore.CreatePersonaStore(dataDir);
        using TaskStore store = TestTaskStore.CreateTaskStore(dataDir, personas);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Ship the thing");
        TaskId? copied = null;
        void OnCopyId(TaskId id) => copied = id;

        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(TimeProvider.System);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(new Agency.Huddle.App.Avatars.AvatarStore(dataDir.Options(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Agency.Huddle.App.Avatars.AvatarStore>.Instance));
        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskListView>(0);
            builder.AddAttribute(1, nameof(TaskListView.View), new TaskView { Id = "v1", Name = "Test View", Kind = ViewKind.List });
            builder.AddAttribute(2, nameof(TaskListView.Tasks), (IReadOnlyList<TaskItem>)[task]);
            builder.AddAttribute(3, nameof(TaskListView.OnCopyId), EventCallback.Factory.Create<TaskId>(this, OnCopyId));
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Actions for PLAT-0001\"]").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), "Copy id", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal(task.Id, copied);
        personas.Dispose();
    }

    /// <summary>Settled design (J52): the page wires a List row's "Copy id" to the same <c>huddleClipboard.copy</c> path as the detail header's button, not just to a local event.</summary>
    [Fact]
    public async Task TasksPage_ListRowCopyId_CallsHuddleClipboardCopyWithExactId()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Ship the thing");
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle.tasks.lastView").SetResult(null);
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", task.Id.ToString()).SetResult(true);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Actions for {task.Id}\"]").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), "Copy id", StringComparison.Ordinal)).ClickAsync());

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal(task.Id.ToString(), Assert.Single(invocation.Arguments));
    }

    /// <summary>Corrections-B7 "15.2.i" item 1: the wake toast's Task link becomes a real <c>href</c>, so it is keyboard-activatable (an <c>&lt;a role="button"&gt;</c> is not).</summary>
    [Fact]
    public void WakeToasts_WokenTaskLink_HasHrefToTheItemRoute()
    {
        MudBunitContext ctx = new();
        TaskActivity activity = new(Microsoft.Extensions.Options.Options.Create(new Agency.Huddle.App.TeamOptions()));
        ctx.Services.AddSingleton(activity);
        var cut = ctx.Render(builder =>
        {
            builder.OpenComponent<MudSnackbarProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<WakeToasts>(1);
            builder.CloseComponent();
        });
        _ = TaskId.TryParse("PLAT-0042", out TaskId id);

        activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        cut.WaitForAssertion(() => Assert.Equal("/tasks/item/PLAT-0042", cut.Find(".wake-toast-task-link").GetAttribute("href")));
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> on the given harness, defaulting to no assignee (the <see cref="TaskDetailTests"/> pattern).</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title = "T") =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft(title, "Platform", null), TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/> (the <see cref="TasksPageTests"/> pattern).</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders <see cref="TaskDetail"/> in Panel mode for <paramref name="id"/> (the <see cref="TaskDetailTests"/> pattern).</summary>
    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx, TaskId id)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
            builder.CloseComponent();
        });
    }
}
