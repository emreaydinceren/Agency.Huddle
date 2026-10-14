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

        await root.InvokeAsync(() => root.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), "Filter", StringComparison.Ordinal)).ClickAsync());

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

        await OpenGroupSortAsync(root);

        Assert.Contains(TaskGroupField.State, GroupChoices(root));
        IRenderedComponent<MudSelect<TaskGroupField?>> grouping = root.FindComponents<MudSelect<TaskGroupField?>>().Single(m => string.Equals(m.Instance.Class, "task-toolbar-grouping", StringComparison.Ordinal));
        await root.InvokeAsync(() => grouping.Instance.ValueChanged.InvokeAsync(TaskGroupField.State));
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
        TaskView board = ListView() with { Kind = ViewKind.Board, Columns = [new BoardColumn("Backlog", [TaskState.Backlog])] };
        (IRenderedComponent<ContainerFragment> root, _) = RenderToolbar(ctx, board);

        await OpenGroupSortAsync(root);

        Assert.DoesNotContain(TaskGroupField.State, GroupChoices(root));
        Assert.Contains(TaskGroupField.Team, GroupChoices(root));
    }

    /// <summary>A List View with no filter, grouping or search.</summary>
    private static TaskView ListView() => new() { Id = "v1", Name = "All", Kind = ViewKind.List };

    /// <summary>Opens the toolbar's "Group, Sort" menu.</summary>
    /// <param name="root">The rendered toolbar host.</param>
    private static Task OpenGroupSortAsync(IRenderedComponent<ContainerFragment> root) =>
        root.InvokeAsync(() => root.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), "Group, Sort", StringComparison.Ordinal)).ClickAsync());

    /// <summary>The Group by choices the open menu offers.</summary>
    /// <param name="root">The rendered toolbar host.</param>
    private static IReadOnlyList<TaskGroupField?> GroupChoices(IRenderedComponent<ContainerFragment> root) =>
        [.. root.FindComponents<MudSelectItem<TaskGroupField?>>().Select(item => item.Instance.Value)];

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
