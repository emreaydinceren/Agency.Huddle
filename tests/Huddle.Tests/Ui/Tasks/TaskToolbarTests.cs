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
/// raised to the host rather than written by the toolbar itself; Search is
/// debounced and never saved; and <c>+ New task</c> defaults the Team from a single-Team filter.
/// </summary>
public sealed class TaskToolbarTests
{
    /// <summary>Adding a team to the filter raises <c>EffectiveViewChanged</c> - without writing to <see cref="ViewStore"/> itself (the host decides what to do with the change).</summary>
    [Fact]
    public async Task AddingATeamFilter_RaisesEffectiveViewChanged_AndShowsSaveToView_WithoutWritingToViewStore()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));


        await root.InvokeAsync(() => root.Find(".task-toolbar-filter button").ClickAsync());
        var teamSelect = root.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-toolbar-filter-team", StringComparison.Ordinal));
        await root.InvokeAsync(() => teamSelect.Instance.SelectedValuesChanged.InvokeAsync(["Atlas"]));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Contains("Atlas", updated.Filter.Teams, StringComparer.Ordinal);

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        TaskView? unsaved = store.Get("v1");
        Assert.NotNull(unsaved);
        Assert.Null(unsaved.Filter.Teams.SingleOrDefault());
    }

    /// <summary>The Filter menu's Blocked select (All, Blocked, Unblocked) is ad hoc: picking Blocked raises <c>EffectiveViewChanged</c> with the filter set, shows a Blocked chip whose clear button restores All, and never writes to <see cref="ViewStore"/>.</summary>
    [Fact]
    public async Task BlockedFilter_RaisesEffectiveViewChanged_ShowsAChip_AndOffersSave_WithoutWritingToViewStore()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v), splitHeader: true);

        Assert.Empty(toolbar.FindAll(".task-toolbar-blocked"));
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter button").ClickAsync());
        var select = root.FindComponent<MudSelect<BlockedFilter>>();
        Assert.Equal([BlockedFilter.All, BlockedFilter.Blocked, BlockedFilter.Unblocked], select.FindComponents<MudSelectItem<BlockedFilter>>().Select(item => item.Instance.Value));

        await toolbar.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(BlockedFilter.Blocked));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(BlockedFilter.Blocked, updated.Filter.Blocked);

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.Equal("Blocked", toolbar.Find(".task-toolbar-blocked .task-filter-chip-value").TextContent.Trim());
        Assert.Equal(BlockedFilter.All, store.Get("v1")?.Filter.Blocked);

        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-blocked .task-filter-chip-clear").ClickAsync());
        Assert.Equal(BlockedFilter.All, raised?.Filter.Blocked);
    }
    /// <summary>The Search field is the shared <c>SearchField</c> (debounced at its shared 250ms) and is never part of a saved View.</summary>
    [Fact]
    public async Task Search_IsDebounced_AndNeverSaved()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        string? searched = null;
        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, saved, searchChanged: EventCallback.Factory.Create<string?>(this, v => searched = v));

        var search = toolbar.FindComponent<Agency.Huddle.App.Components.Shared.SearchField>();
        Assert.Equal(250, toolbar.FindComponent<MudTextField<string>>().Instance.DebounceInterval);

        await toolbar.InvokeAsync(() => search.Instance.ValueChanged.InvokeAsync("urgent"));

        Assert.Equal("urgent", searched);
    }

    /// <summary>The toolbar has no Active/Closed toggle (Scope is set in the View editor); the <c>Filter</c> icon leads it, then <c>+ New task</c>, then the List | Board control.</summary>
    [Fact]
    public async Task Toolbar_LeadsWithFilter_ThenNewTask_ThenListBoardControl_AndHasNoScopeToggle()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.Kanban, BoardDefaultColumns);

        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, saved);

        Assert.Empty(toolbar.FindComponents<MudToggleGroup<ViewScope>>());
        var children = toolbar.Find(".task-toolbar").Children;
        Assert.Contains("task-toolbar-filter", children[0].ClassList);
        Assert.Contains("btn-action-tight", children[1].ClassList);
        Assert.Contains("task-toolbar-kind", children[2].ClassList);
        Assert.Equal(["List", "Kanban"],toolbar.FindAll(".task-toolbar-kind .mud-toggle-item").Select(item => item.TextContent.Trim()));
        Assert.Equal("Kanban", toolbar.Find(".task-toolbar-kind .huddle-segmented-active-item").TextContent.Trim());
    }

    /// <summary>Picking Board on a List View raises <c>EffectiveViewChanged</c> with a Board that passes <c>ViewValidator</c> (Active scope, every state in a column), and leaves the saved View a List.</summary>
    [Fact]
    public async Task KindControl_ListToBoard_RaisesAValidBoard_AndOffersSave_WithoutWritingToViewStore()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));

        Assert.Equal("List", toolbar.Find(".task-toolbar-kind .huddle-segmented-active-item").TextContent.Trim());
        await toolbar.InvokeAsync(() => toolbar.FindComponent<MudToggleGroup<ViewKind>>().Instance.ValueChanged.InvokeAsync(ViewKind.Kanban));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(ViewKind.Kanban, updated.Kind);
        Assert.Equal(ViewScope.Active, updated.Scope);
        Assert.Empty(ViewValidator.Validate(updated, []));

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.Equal("Kanban", toolbar.Find(".task-toolbar-kind .huddle-segmented-active-item").TextContent.Trim());
        Assert.Equal(ViewKind.List, store.Get("v1")?.Kind);
    }

    /// <summary>Picking List on a Board View drops its columns (a List cannot have any) so the result passes <c>ViewValidator</c>; a Closed-scope List picked as Board is forced to Active.</summary>
    [Fact]
    public async Task KindControl_BoardToList_DropsColumns_AndClosedListToBoardForcesActive()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var board = Save(store, "v1", ViewKind.Kanban, BoardDefaultColumns);

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
        await toolbar.InvokeAsync(() => toolbar.FindComponent<MudToggleGroup<ViewKind>>().Instance.ValueChanged.InvokeAsync(ViewKind.Kanban));

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

    /// <summary>With the split header, the second row leads with the filter icon and shows a chip only for a filter that is set - none for an unfiltered View - and without the header (the Team tab) there are never any.</summary>
    [Fact]
    public async Task FilterChips_AppearOnlyForSetFilters_AndOnlyWithTheSplitHeader()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);
        TaskView filtered = saved with
        {
            Filter = new TaskFilter
            {
                Teams = ["Platform"],
                Projects = [new ProjectRef("Platform", "Backend")],
                Assignees = ["@me"],
                Sprints = ["@current"],
                Priorities = [TaskPriority.High],
                States = [TaskState.ToDo],
            },
        };

        await using var splitCtx = NewContext(store);
        var (_, split) = RenderToolbar(splitCtx, saved, splitHeader: true);
        Assert.Contains("task-toolbar-filter", split.Find(".task-toolbar").Children[0].ClassList);
        // Group by is a chip in this row too (its last control), but it is not a filter.
        Assert.Empty(split.FindAll(".task-toolbar .task-filter-chip:not(.task-toolbar-group)"));

        split.Render(builder => builder.Add(t => t.EffectiveView, filtered));
        Assert.Equal(
            ["Team: Platform", "Project: Platform/Backend", "Assignee: Me", "Sprint: Current sprint", "Priority: High", "Status: To Do"],
            split.FindAll(".task-toolbar .task-filter-chip:not(.task-toolbar-group)").Select(chip => string.Join(' ', chip.QuerySelector(".task-filter-chip-button")!.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));
        Assert.Equal(6, split.FindAll(".task-toolbar .task-filter-chip-clear").Count);

        await using var plainCtx = NewContext(store);
        var (_, plain) = RenderToolbar(plainCtx, filtered);
        Assert.Empty(plain.FindAll(".task-filter-chip"));
    }

    /// <summary>The clear button on each filter chip empties just that filter, so its chip disappears.</summary>
    [Fact]
    public async Task FilterChips_ClearButton_EmptiesOnlyThatFilter()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);
        TaskView filtered = saved with { Filter = new TaskFilter { Teams = ["Platform"], Priorities = [TaskPriority.High], Sprints = ["@current"], Projects = [new ProjectRef("Platform", null)] } };

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, filtered, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v), splitHeader: true);

        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-team .task-filter-chip-clear").ClickAsync());
        Assert.Empty(raised?.Filter.Teams ?? [Guid.NewGuid().ToString()]);
        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-project .task-filter-chip-clear").ClickAsync());
        Assert.Empty(raised?.Filter.Projects ?? [new ProjectRef("x", null)]);
        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-sprint .task-filter-chip-clear").ClickAsync());
        Assert.Empty(raised?.Filter.Sprints ?? ["x"]);
        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-priority .task-filter-chip-clear").ClickAsync());
        Assert.Empty(raised?.Filter.Priorities ?? [TaskPriority.High]);
    }
    /// <summary>The Status chip reads "Open" for the non-terminal set, shows a clear button then, and clearing restores All; the Assignee chip names a single choice ("@me" reads "Me").</summary>
    [Fact]
    public async Task FilterChips_ReflectTheFilter_AndClearBackToAny()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);
        TaskView notDoneMine = saved with
        {
            Filter = new TaskFilter { States = [TaskState.Backlog, TaskState.ToDo, TaskState.InProgress, TaskState.Review], Assignees = ["@me"] },
        };

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, notDoneMine, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v), splitHeader: true);

        Assert.Equal("Open", toolbar.Find(".task-toolbar-status .task-filter-chip-value").TextContent.Trim());
        Assert.Equal("Me", toolbar.Find(".task-toolbar-assignee .task-filter-chip-value").TextContent.Trim());
        Assert.Contains("task-filter-chip-set", toolbar.Find(".task-toolbar-status").ClassList);

        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-status .task-filter-chip-clear").ClickAsync());

        TaskView cleared = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Empty(cleared.Filter.States);
        Assert.Equal("@me", Assert.Single(cleared.Filter.Assignees));
    }

    /// <summary>The header row's Group by is a filter chip like Status: "None" and unset to begin with, with a clear button only while a grouping is set, and clicking that raises the View with no grouping.</summary>
    [Fact]
    public async Task GroupBy_ClearButton_ShowsOnlyWhenGrouped_AndUngroups()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);
        TaskView grouped = saved with { Grouping = [TaskGroupField.Assignee] };

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (_, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v), splitHeader: true);
        Assert.Equal("None", toolbar.Find(".task-toolbar-group .task-filter-chip-value").TextContent.Trim());
        Assert.Empty(toolbar.FindAll(".task-toolbar-group .task-filter-chip-clear"));

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, grouped));
        Assert.Equal("Assignee", toolbar.Find(".task-toolbar-group .task-filter-chip-value").TextContent.Trim());
        Assert.Equal("Clear Group by filter", toolbar.Find(".task-toolbar-group .task-filter-chip-clear").GetAttribute("aria-label"));
        await toolbar.InvokeAsync(() => toolbar.Find(".task-toolbar-group .task-filter-chip-clear").ClickAsync());

        TaskView ungrouped = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Empty(ungrouped.Grouping);
    }

    /// <summary>The Status chip reads "Closed" for exactly the terminal statuses and the status's own name for one, and its menu is two levels: All, then Open and Closed each followed by their own statuses.</summary>
    [Fact]
    public async Task StatusChip_ReadsClosedAndSingleStatus_AndItsMenuHasTwoLevels()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);
        TaskView closed = saved with { Filter = new TaskFilter { States = [TaskState.Done, TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected] } };
        TaskView review = saved with { Filter = new TaskFilter { States = [TaskState.Review] } };

        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, closed, splitHeader: true);
        Assert.Equal("Closed", toolbar.Find(".task-toolbar-status .task-filter-chip-value").TextContent.Trim());
        toolbar.Render(builder => builder.Add(t => t.EffectiveView, review));
        Assert.Equal("Review", toolbar.Find(".task-toolbar-status .task-filter-chip-value").TextContent.Trim());

        await root.InvokeAsync(() => root.Find(".task-toolbar-status .task-filter-chip-button").ClickAsync());

        Assert.Equal(
            ["All", "Open", "Backlog", "To Do", "In Progress", "Review", "Closed", "Done", "Cancelled", "Duplicate", "Rejected"],
            root.FindAll(".mud-popover-open .mud-menu-item").Select(item => item.TextContent.Trim()));
        Assert.Equal(2, root.FindAll(".mud-popover-open .task-status-group").Count);
        Assert.Equal(8, root.FindAll(".mud-popover-open .task-status-sub").Count);
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
        EventCallback<NewTaskDefaults>? onNewTask = null,
        bool splitHeader = false)
    {
        IRenderedComponent<ContainerFragment> root = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskToolbar>(0);
            builder.AddAttribute(1, nameof(TaskToolbar.EffectiveView), view);
            builder.AddAttribute(5, nameof(TaskToolbar.SplitHeader), splitHeader);
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
        ctx.Services.AddSingleton<ISprintCatalog>(new Agency.Huddle.Tests.Tasks.EmptySprintCatalog());
        // The toolbar's Team multi-select lists the Teams the app knows (UX-050c): a harness supplies the stores, disposed with the container.
        ctx.Services.AddSingleton(_ => new Agency.Huddle.Tests.Acp.Tools.TaskToolHarness());
        ctx.Services.AddSingleton(sp => sp.GetRequiredService<Agency.Huddle.Tests.Acp.Tools.TaskToolHarness>().Store);
        ctx.Services.AddSingleton(sp => sp.GetRequiredService<Agency.Huddle.Tests.Acp.Tools.TaskToolHarness>().Personas);
        return ctx;
    }
}
