using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the toolbar's reuse of the shared controls (U15 T15.5): the shared <see cref="SearchField"/>
/// (UX-055), the View editor's Team multi-select in place of the free-text Team field (UX-050c, OP-8), and
/// State among the Group by choices on a List (OP-8).
/// </summary>
public sealed class TaskToolbarEditorControlsTests
{
    /// <summary>The search box is the shared <see cref="SearchField"/>, named "Search tasks", and its text still reaches <c>SearchChanged</c>.</summary>
    [Fact]
    public async Task Search_IsTheSharedSearchField()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        string? searched = null;
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        (_, IRenderedComponent<TaskToolbar> toolbar) = RenderToolbar(ctx, ListView(), searchChanged: EventCallback.Factory.Create<string?>(ctx, text => searched = text));

        IRenderedComponent<SearchField> search = toolbar.FindComponent<SearchField>();
        Assert.Equal("Search tasks", search.Instance.Placeholder);
        Assert.Equal("Search tasks", search.Instance.AriaLabel);
        await toolbar.InvokeAsync(() => search.Instance.ValueChanged.InvokeAsync("urgent"));

        Assert.Equal("urgent", searched);
    }

    /// <summary>The Filter menu's Team control is a multi-select of known Teams: no free-text field or Add button, and choosing raises the filter.</summary>
    [Fact]
    public async Task FilterMenu_TeamIsAMultiSelect_NotAFreeTextField()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        _ = CreateTask(harness);
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        TaskView? raised = null;
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, ListView(), effectiveViewChanged: EventCallback.Factory.Create<TaskView>(ctx, view => raised = view));

        await root.InvokeAsync(() => root.Find(".task-toolbar-filter button").ClickAsync());

        Assert.Empty(root.FindAll(".task-toolbar-filter-input"));
        Assert.Empty(root.FindAll(".task-toolbar-filter-add"));
        IRenderedComponent<MudSelect<string>> team = root.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-toolbar-filter-team", StringComparison.Ordinal));
        Assert.True(team.Instance.MultiSelection);
        Assert.Equal("Team", team.Instance.Label);
        await root.InvokeAsync(() => team.Instance.SelectedValuesChanged.InvokeAsync(["Platform"]));

        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal("Platform", Assert.Single(updated.Filter.Teams));
    }

    /// <summary>On a List, Group by offers State, and choosing it groups the View by State.</summary>
    [Fact]
    public async Task GroupBy_OnAList_OffersState()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        TaskView? raised = null;
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, ListView(), effectiveViewChanged: EventCallback.Factory.Create<TaskView>(ctx, view => raised = view));

        await OpenGroupByAsync(root);

        Assert.Contains("Status", GroupChoices(root));
        await root.InvokeAsync(() => root.FindAll(".mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "Status", StringComparison.Ordinal)).ClickAsync());
        TaskView updated = raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.");
        Assert.Equal(TaskGroupField.State, Assert.Single(updated.Grouping));
    }

    /// <summary>On a Board State is not a Group by choice: the columns already are the states.</summary>
    [Fact]
    public async Task GroupBy_OnABoard_OffersNoState()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        TaskView board = ListView() with { Kind = ViewKind.Kanban, Columns = [new BoardColumn("Backlog", [TaskState.Backlog])] };
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, board);

        await OpenGroupByAsync(root);

        Assert.DoesNotContain("Status", GroupChoices(root));
        Assert.Contains("Team", GroupChoices(root));
    }

    /// <summary>The Filter menu's Team options are the union of the Persona and Task stores' Teams, deduplicated.</summary>
    [Fact]
    public async Task FilterMenu_TeamOffersTheUnionOfPersonaAndTaskStoreTeams()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        _ = CreateTask(harness);
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Legal"));
        harness.Store.RebuildFromWatcher();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, ListView());

        await OpenFilterAsync(root);
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter-team").MouseDownAsync());

        List<string> options = [.. root.FindAll(".task-toolbar-filter-team-option").Select(el => el.TextContent.Trim())];
        Assert.Contains("Legal", options);
        Assert.Equal(options.Count, options.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>The Project options are narrowed to the chosen Teams, and a chosen pair that no longer exists stays selected as "(missing)".</summary>
    [Fact]
    public async Task FilterMenu_ProjectNarrowsToTheChosenTeams_AndKeepsAMissingValue()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Platform", "Backend"));
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Legal", "Contracts"));
        harness.Store.RebuildFromWatcher();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        TaskView view = ListView() with { Filter = new TaskFilter { Teams = ["Platform"], Projects = [new ProjectRef("Platform", "Gone")] } };
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, view);

        await OpenFilterAsync(root);
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter-project").MouseDownAsync());

        Assert.Equal(
            ["Platform / Backend", "Platform/Gone (missing)"],
            root.FindAll(".task-toolbar-filter-project-option").Select(el => el.TextContent.Trim()));
    }

    /// <summary>The Assignee options lead with Me and Unassigned and keep a filtered-on name no Task holds.</summary>
    [Fact]
    public async Task FilterMenu_AssigneeOffersMeUnassignedThenNames()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        TaskView view = ListView() with { Filter = new TaskFilter { Assignees = ["Ghost"] } };
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, view);

        await OpenFilterAsync(root);
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter-assignee").MouseDownAsync());

        List<string> options = [.. root.FindAll(".task-toolbar-filter-assignee-option").Select(el => el.TextContent.Trim())];
        Assert.Equal(["Me", "Unassigned"], options[..2]);
        Assert.Contains("Ghost", options);
    }

    /// <summary>The Project, Assignee, Status and Priority multi-selects each raise the effective View with their choice, without saving.</summary>
    [Fact]
    public async Task FilterMenu_ProjectAssigneeStatusAndPriority_RaiseTheFilter()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        TaskView? raised = null;
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, ListView(), effectiveViewChanged: EventCallback.Factory.Create<TaskView>(ctx, view => raised = view));
        await OpenFilterAsync(root);

        await root.InvokeAsync(() => FindSelect<string>(root, "task-toolbar-filter-project").Instance.SelectedValuesChanged.InvokeAsync(["Platform/Backend", "Legal"]));
        Assert.Equal([new ProjectRef("Platform", "Backend"), new ProjectRef("Legal", null)], raised?.Filter.Projects);

        await root.InvokeAsync(() => FindSelect<string>(root, "task-toolbar-filter-assignee").Instance.SelectedValuesChanged.InvokeAsync(["@me", "Kai"]));
        Assert.Equal(["@me", "Kai"], raised?.Filter.Assignees);

        await root.InvokeAsync(() => FindSelect<string>(root, "task-toolbar-filter-state").Instance.SelectedValuesChanged.InvokeAsync(["To Do", "Needs You"]));
        Assert.Equal([TaskState.ToDo, TaskState.NeedsYou], raised?.Filter.States);

        await root.InvokeAsync(() => FindSelect<TaskPriority>(root, "task-toolbar-filter-priority").Instance.SelectedValuesChanged.InvokeAsync([TaskPriority.High]));
        Assert.Equal([TaskPriority.High], raised?.Filter.Priorities);
    }

    /// <summary>The Status select leads with selectable All, Open and Closed headers, each followed by its own statuses, and choosing a header sets the whole group (All clears the filter).</summary>
    [Fact]
    public async Task FilterMenu_StatusHeaders_AllOpenClosed_ChooseWholeGroups()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        TaskView? raised = null;
        await using MudBunitContext ctx = NewToolbarContext(harness, store);
        TaskView view = ListView() with { Filter = new TaskFilter { States = [TaskState.NeedsYou] } };
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, view, effectiveViewChanged: EventCallback.Factory.Create<TaskView>(ctx, v => raised = v));
        await OpenFilterAsync(root);
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter-state").MouseDownAsync());

        Assert.Equal(
            ["All", "Open", "Backlog", "To Do", "In Progress", "Needs You", "Closed", "Done", "Cancelled", "Duplicate", "Rejected"],
            root.FindAll(".task-toolbar-filter-state-group, .task-toolbar-filter-state-option").Select(el => el.TextContent.Trim()));

        IRenderedComponent<MudSelect<string>> select = FindSelect<string>(root, "task-toolbar-filter-state");
        await root.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync(["Needs You", "@open"]));
        Assert.Equal([TaskState.Backlog, TaskState.ToDo, TaskState.InProgress, TaskState.NeedsYou], raised?.Filter.States);

        await root.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync(["Needs You", "@closed"]));
        Assert.Equal([TaskState.Done, TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected], raised?.Filter.States);

        await root.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync(["Needs You", "@all"]));
        Assert.Empty(raised?.Filter.States ?? [TaskState.NeedsYou]);
    }
    /// <summary>Opens the toolbar's Filter menu.</summary>
    /// <param name="root">The rendered toolbar host.</param>
    private static Task OpenFilterAsync(IRenderedComponent<ContainerFragment> root) =>
        root.InvokeAsync(() => root.Find(".task-toolbar-filter button").ClickAsync());

    /// <summary>The Filter menu's select carrying <paramref name="className"/>.</summary>
    /// <typeparam name="T">The select's value type.</typeparam>
    /// <param name="root">The rendered toolbar host.</param>
    /// <param name="className">The select's own class.</param>
    private static IRenderedComponent<MudSelect<T>> FindSelect<T>(IRenderedComponent<ContainerFragment> root, string className) =>
        root.FindComponents<MudSelect<T>>().Single(m => string.Equals(m.Instance.Class, className, StringComparison.Ordinal));
    /// <summary>A List View with no filter, grouping or search.</summary>
    private static TaskView ListView() => new() { Id = "v1", Name = "All", Kind = ViewKind.List };

    /// <summary>Opens the toolbar's Group by chip menu.</summary>
    /// <param name="root">The rendered toolbar host.</param>
    private static Task OpenGroupByAsync(IRenderedComponent<ContainerFragment> root) =>
        root.InvokeAsync(() => root.Find(".task-toolbar-group .chip-trigger").ClickAsync());

    /// <summary>The Group by choices the open menu offers, after None.</summary>
    /// <param name="root">The rendered toolbar host.</param>
    private static IReadOnlyList<string> GroupChoices(IRenderedComponent<ContainerFragment> root) =>
        [.. root.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim()).Where(text => !string.Equals(text, "None", StringComparison.Ordinal))];

    /// <summary>A context holding the harness's services, the given View store and an empty sprint catalogue.</summary>
    /// <param name="harness">Supplies the Task and Persona stores.</param>
    /// <param name="store">The View store.</param>
    private static MudBunitContext NewToolbarContext(TaskToolHarness harness, ViewStore store)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton<ISprintCatalog>(new Agency.Huddle.Tests.Tasks.EmptySprintCatalog());
        return ctx;
    }

    /// <summary>Renders <see cref="TaskToolbar"/> beside the popover and dialog hosts its menus need.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="view">The effective View.</param>
    /// <param name="effectiveViewChanged">Raised when a control changes the View.</param>
    /// <param name="searchChanged">Raised with the search text.</param>
    private static (IRenderedComponent<ContainerFragment> Root, IRenderedComponent<TaskToolbar> Toolbar) RenderToolbar(
        MudBunitContext ctx,
        TaskView view,
        EventCallback<TaskView>? effectiveViewChanged = null,
        EventCallback<string?>? searchChanged = null)
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

            builder.CloseComponent();
        });

        return (root, root.FindComponent<TaskToolbar>());
    }
}
