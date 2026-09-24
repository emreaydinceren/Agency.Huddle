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
/// Pins Spec §13.3: Active/Closed is disabled on a Board; a filter, group/sort or kind change is
/// session-only and only offers <c>Save to View</c> rather than writing straight through; Search is
/// debounced and never saved; and <c>+ New task</c> defaults the Team from a single-Team filter.
/// </summary>
public sealed class TaskToolbarTests
{
    /// <summary>The Active/Closed toggle is disabled while the effective View is a Board (its scope is always Active).</summary>
    [Fact]
    public async Task ActiveClosedToggle_DisabledOnBoard_EnabledOnList()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var listView = Save(store, "v1", ViewKind.List, []);
        var boardView = Save(store, "v2", ViewKind.Board, BoardDefaultColumns);

        await using var listCtx = NewContext(store);
        var (_, listToolbar) = RenderToolbar(listCtx, listView);
        await using var boardCtx = NewContext(store);
        var (_, boardToolbar) = RenderToolbar(boardCtx, boardView);

        Assert.False(listToolbar.FindComponent<MudToggleGroup<ViewScope>>().Instance.Disabled);
        Assert.True(boardToolbar.FindComponent<MudToggleGroup<ViewScope>>().Instance.Disabled);
    }

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

        root.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), "Filter", StringComparison.Ordinal)).Click();
        var teamInput = root.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-toolbar-filter-input", StringComparison.Ordinal));
        await root.InvokeAsync(() => teamInput.Instance.ValueChanged.InvokeAsync("Atlas"));
        root.Find(".task-toolbar-filter-add").Click();

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Contains("Atlas", updated.Filter.Teams, StringComparer.Ordinal);

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.NotEmpty(root.FindAll(".task-toolbar-save"));
        TaskView? unsaved = store.Get("v1");
        Assert.NotNull(unsaved);
        Assert.Null(unsaved.Filter.Teams.SingleOrDefault());
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

    /// <summary>Switching List/Board raises the change but is session-only: <see cref="ViewStore"/> keeps the saved Kind until Save to View is clicked.</summary>
    [Fact]
    public async Task SwitchingListToBoard_IsSessionOnly_UntilSaved()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var saved = Save(store, "v1", ViewKind.List, []);

        TaskView? raised = null;
        await using var ctx = NewContext(store);
        var (root, toolbar) = RenderToolbar(ctx, saved, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(this, v => raised = v));

        var kindToggle = toolbar.FindComponent<MudToggleGroup<ViewKind>>();
        await toolbar.InvokeAsync(() => kindToggle.Instance.ValueChanged.InvokeAsync(ViewKind.Board));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(ViewKind.Board, updated.Kind);
        TaskView? stillSaved = store.Get("v1");
        Assert.NotNull(stillSaved);
        Assert.Equal(ViewKind.List, stillSaved.Kind);

        toolbar.Render(builder => builder.Add(t => t.EffectiveView, updated));

        Assert.NotEmpty(root.FindAll(".task-toolbar-save"));
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
        await oneTeamToolbar.InvokeAsync(() => oneTeamToolbar.Find(".task-toolbar-new-task").Click());
        Assert.Equal("Atlas", oneTeamDefaults?.Team);

        NewTaskDefaults? twoTeamsDefaults = null;
        await using var twoTeamsCtx = NewContext(store);
        var (_, twoTeamsToolbar) = RenderToolbar(twoTeamsCtx, twoTeams, onNewTask: EventCallback.Factory.Create<NewTaskDefaults>(this, v => twoTeamsDefaults = v));
        await twoTeamsToolbar.InvokeAsync(() => twoTeamsToolbar.Find(".task-toolbar-new-task").Click());
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
