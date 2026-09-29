namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

/// <summary>
/// Pins Spec §13.3: a filter, group/sort or kind change is
/// session-only and only offers <c>Save to View</c> rather than writing straight through; Search is
/// debounced and never saved; and <c>+ New task</c> defaults the Team from a single-Team filter.
/// </summary>
public sealed class TaskToolbarTests
{
    /// <summary>Adding a team to the filter raises <c>EffectiveViewChanged</c> and, once the parent reflects it back, shows <c>Save to View</c> - without writing to <see cref="ViewStore"/>.</summary>
    [Fact]
    public async Task AddingATeamFilter_RaisesEffectiveViewChanged_AndShowsSaveToView_WithoutWritingToViewStore()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));

        Assert.Empty(root.FindAll(".task-toolbar-save"));

        await root.InvokeAsync(() => root.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), "Filter", StringComparison.Ordinal)).ClickAsync());
        var teamInput = root.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-toolbar-filter-input", StringComparison.Ordinal));
        await root.InvokeAsync(() => teamInput.Instance.ValueChanged.InvokeAsync("Atlas"));
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter-add").ClickAsync());

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Contains("Atlas", updated.Filter.Teams, StringComparer.Ordinal);

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.NotEmpty(root.FindAll(".task-toolbar-save"));
        TaskView? unsaved = store.Get("v1");
        Assert.NotNull(unsaved);
        Assert.Null(unsaved.Filter.Teams.SingleOrDefault());
    }

    /// <summary>The toolbar's All | Blocked | Unblocked control is ad hoc: picking Blocked raises <c>EffectiveViewChanged</c> with the filter set, offers <c>Save to View</c> once the parent reflects it, and never writes to <see cref="ViewStore"/>.</summary>
    [Fact]
    public async Task BlockedControl_RaisesEffectiveViewChanged_AndOffersSave_WithoutWritingToViewStore()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));

        var control = toolbar.FindComponent<MudToggleGroup<BlockedFilter>>();
        Assert.Equal(["All", "Blocked", "Unblocked"], toolbar.FindAll(".task-toolbar-blocked .mud-toggle-item").Select(item => item.TextContent.Trim()));
        Assert.Equal("All", toolbar.Find(".task-toolbar-blocked .huddle-segmented-active-item").TextContent.Trim());

        await toolbar.InvokeAsync(() => control.Instance.ValueChanged.InvokeAsync(BlockedFilter.Blocked));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(BlockedFilter.Blocked, updated.Filter.Blocked);

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.Equal("Blocked", toolbar.Find(".task-toolbar-blocked .huddle-segmented-active-item").TextContent.Trim());
        Assert.NotEmpty(root.FindAll(".task-toolbar-save"));
        Assert.Equal(BlockedFilter.All, store.Get("v1")?.Filter.Blocked);
    }

    /// <summary>The Search field debounces at 200ms and is never part of a saved View.</summary>
    [Fact]
    public async Task Search_IsDebounced_AndNeverSaved()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        string? searched = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, searchChanged: EventCallback.Factory.Create<string?>(this, v => searched = v));

        var search = toolbar.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-toolbar-search", StringComparison.Ordinal));
        Assert.Equal(200, search.Instance.DebounceInterval);

        await toolbar.InvokeAsync(() => search.Instance.ValueChanged.InvokeAsync("urgent"));

        Assert.Equal("urgent", searched);
        Assert.Empty(root.FindAll(".task-toolbar-save"));
    }

    /// <summary>The toolbar has no Active/Closed toggle (Scope is set in the View editor); <c>+ New task</c> leads it and the List | Board control sits directly after it, before <c>Filter</c>.</summary>
    [Fact]
    public async Task Toolbar_LeadsWithNewTask_ThenListBoardControl_ThenFilter_AndHasNoScopeToggle()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.Board, BoardDefaultColumns);

        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, saved);

        Assert.Empty(toolbar.FindComponents<MudToggleGroup<ViewScope>>());
        var children = toolbar.Find(".task-toolbar").Children;
        Assert.Contains("btn-action-tight", children[0].ClassList);
        Assert.Contains("task-toolbar-kind", children[1].ClassList);
        Assert.Contains("task-toolbar-filter", children[2].ClassList);
        Assert.Equal(["List", "Board"], toolbar.FindAll(".task-toolbar-kind .mud-toggle-item").Select(item => item.TextContent.Trim()));
        Assert.Equal("Board", toolbar.Find(".task-toolbar-kind .huddle-segmented-active-item").TextContent.Trim());
    }

    /// <summary>Picking Board on a List View raises <c>EffectiveViewChanged</c> with a Board that passes <c>ViewValidator</c> (Active scope, every state in a column), offers <c>Save to View</c> once reflected, and leaves the saved View a List.</summary>
    [Fact]
    public async Task KindControl_ListToBoard_RaisesAValidBoard_AndOffersSave_WithoutWritingToViewStore()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));

        Assert.Equal("List", toolbar.Find(".task-toolbar-kind .huddle-segmented-active-item").TextContent.Trim());
        await toolbar.InvokeAsync(() => toolbar.FindComponent<MudToggleGroup<ViewKind>>().Instance.ValueChanged.InvokeAsync(ViewKind.Board));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(ViewKind.Board, updated.Kind);
        Assert.Equal(ViewScope.Active, updated.Scope);
        Assert.Empty(ViewValidator.Validate(updated, []));

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.Equal("Board", toolbar.Find(".task-toolbar-kind .huddle-segmented-active-item").TextContent.Trim());
        Assert.NotEmpty(root.FindAll(".task-toolbar-save"));
        Assert.Equal(ViewKind.List, store.Get("v1")?.Kind);
    }

    /// <summary>Picking List on a Board View drops its columns (a List cannot have any) so the result passes <c>ViewValidator</c>; a Closed-scope List picked as Board is forced to Active.</summary>
    [Fact]
    public async Task KindControl_BoardToList_DropsColumns_AndClosedListToBoardForcesActive()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var board = Save(store, "v1", ViewKind.Board, BoardDefaultColumns);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, board, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));

        await toolbar.InvokeAsync(() => toolbar.FindComponent<MudToggleGroup<ViewKind>>().Instance.ValueChanged.InvokeAsync(ViewKind.List));

        TaskView list = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(ViewKind.List, list.Kind);
        Assert.Empty(list.Columns);
        Assert.Empty(ViewValidator.Validate(list, []));

        raised = null;
        TaskView closedList = list with { Scope = ViewScope.Closed };
        toolbar.Render(builder => builder.Add(t => t.EffectiveView, closedList));
        await toolbar.InvokeAsync(() => toolbar.FindComponent<MudToggleGroup<ViewKind>>().Instance.ValueChanged.InvokeAsync(ViewKind.Board));

        TaskView backToBoard = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(ViewScope.Active, backToBoard.Scope);
        Assert.Equal(board.Columns.Count, backToBoard.Columns.Count);
        Assert.Empty(ViewValidator.Validate(backToBoard, []));
    }

    /// <summary><c>+ New task</c> defaults the Team from the filter when it names exactly one, and leaves it unset otherwise.</summary>
    [Fact]
    public async Task NewTask_DefaultsTeam_WhenTheFilterNamesExactlyOne()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var oneTeam = new TaskView { Id = "v1", Name = "One Team", Kind = ViewKind.List, Filter = new TaskFilter { Teams = ["Atlas"] } };
        var twoTeams = new TaskView { Id = "v2", Name = "Two Teams", Kind = ViewKind.List, Filter = new TaskFilter { Teams = ["Atlas", "Zephyr"] } };
        _ = store.Save(oneTeam);
        _ = store.Save(twoTeams);

        NewTaskDefaults? oneTeamDefaults = null;
        await using var oneTeamCtx = NewContext(store);
        var (_, oneTeamToolbar) = RenderToolbar(oneTeamCtx, oneTeam, onNewTask: EventCallback.Factory.Create<NewTaskDefaults>(this, v => oneTeamDefaults = v));
        await oneTeamToolbar.InvokeAsync(() => oneTeamToolbar.Find(".btn-action-tight").ClickAsync());
        Assert.Equal("Atlas", oneTeamDefaults?.Team);

        NewTaskDefaults? twoTeamsDefaults = null;
        await using var twoTeamsCtx = NewContext(store);
        var (_, twoTeamsToolbar) = RenderToolbar(twoTeamsCtx, twoTeams, onNewTask: EventCallback.Factory.Create<NewTaskDefaults>(this, v => twoTeamsDefaults = v));
        await twoTeamsToolbar.InvokeAsync(() => twoTeamsToolbar.Find(".btn-action-tight").ClickAsync());
        Assert.Null(twoTeamsDefaults?.Team);
    }

    /// <summary>The columns a Board View needs to pass <c>ViewValidator</c>: every <see cref="TaskState"/> covered exactly once, mirroring <c>BoardLayout.DefaultColumns</c> (internal to the production assembly, so restated here rather than referenced).</summary>
    private static readonly IReadOnlyList<BoardColumn> BoardDefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Review", [TaskState.Review]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>Saves a fresh, minimal View with <paramref name="id"/>, <paramref name="kind"/> and <paramref name="columns"/>, and returns the copy <paramref name="store"/> actually resolved, so a test's "saved" baseline matches what <see cref="TaskToolbar"/> itself will read back through <c>ViewStore.Get</c>.</summary>
    private static TaskView Save(ViewStore store, string id, ViewKind kind, IReadOnlyList<BoardColumn> columns)
    {
        TaskView view = new() { Id = id, Name = id, Kind = kind, Columns = columns };
        ViewSaveResult result = store.Save(view);
        Assert.True(result.Saved);
        return store.Get(id) ?? throw new InvalidOperationException($"'{id}' was just saved but is missing.");
    }

    /// <summary>
    /// Renders <see cref="TaskToolbar"/> alongside the popover/dialog hosts its <c>MudMenu</c>s need,
    /// wired to <paramref name="ctx"/>'s registered <c>ViewStore</c> and the given optional callbacks.
    /// Returns both the outer fragment - the one to query for anything a <c>MudMenu</c> portals into
    /// its sibling popover provider - and the <see cref="TaskToolbar"/> component itself, which is
    /// what a later parameter update (<c>Render</c>) must target.
    /// </summary>
    private static (IRenderedComponent<ContainerFragment> Root, IRenderedComponent<TaskToolbar> Toolbar) RenderToolbar(
        MudBunitContext ctx,
        TaskView view,
        EventCallback<TaskView>? effectiveViewChanged = null,
        EventCallback<string?>? searchChanged = null,
        EventCallback<NewTaskDefaults>? onNewTask = null)
    {
        IRenderedComponent<ContainerFragment> root = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskToolbar>(0);
            builder.AddAttribute(1, nameof(TaskToolbar.EffectiveView), view);
            if (effectiveViewChanged is { } evc)
            {
                builder.AddAttribute(2, nameof(TaskToolbar.EffectiveViewChanged), evc);
            }

            if (searchChanged is { } sc)
            {
                builder.AddAttribute(3, nameof(TaskToolbar.SearchChanged), sc);
            }

            if (onNewTask is { } ont)
            {
                builder.AddAttribute(4, nameof(TaskToolbar.OnNewTask), ont);
            }

            builder.CloseComponent();
        });

        return (root, root.FindComponent<TaskToolbar>());
    }

    private static MudBunitContext NewContext(ViewStore store)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(store);
        return ctx;
    }
}
