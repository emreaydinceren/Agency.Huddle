using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the Tasks page saving every change to the View as it happens - a filter, the grouping, the layout, a sort,
/// a column's width - with no Save button and no editor panel.
/// </summary>
public sealed class TasksPageAutoSaveTests
{
    /// <summary>The toolbar raising a changed filter or grouping saves it straight to the View's entry in <see cref="ViewStore"/>.</summary>
    [Fact]
    public async Task ToolbarChange_IsSavedToTheView_WithoutASaveButton()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");
        IRenderedComponent<TaskToolbar> toolbar = cut.FindComponent<TaskToolbar>();
        TaskView current = toolbar.Instance.EffectiveView;

        await cut.InvokeAsync(() => toolbar.Instance.EffectiveViewChanged.InvokeAsync(current with
        {
            Filter = new TaskFilter { Teams = ["Platform"], Priorities = [TaskPriority.High] },
            Grouping = [TaskGroupField.Assignee],
        }));

        TaskView saved = Assert.IsType<TaskView>(harness.Views.Get("custom1"));
        Assert.Equal(["Platform"], saved.Filter.Teams);
        Assert.Equal([TaskPriority.High], saved.Filter.Priorities);
        Assert.Equal([TaskGroupField.Assignee], saved.Grouping);
        Assert.Empty(cut.FindAll(".task-toolbar-save, .tasks-reset"));
        Assert.Empty(cut.FindAll(".view-editor-drawer"));
    }

    /// <summary>A header click's sort and a finished column drag's widths, raised by the List, are saved to the View: the sort as its one Sort key and the widths by the visible columns' keys, ID and Title first.</summary>
    [Fact]
    public async Task ListSortAndColumnWidths_AreSavedToTheView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List, Fields = ["status", "priority"] });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");
        IRenderedComponent<TaskListView> list = cut.FindComponent<TaskListView>();

        await cut.InvokeAsync(() => list.Instance.ViewChanged.InvokeAsync(list.Instance.View with { Sort = [new SortKey("priority", Agency.Huddle.App.Tasks.Views.SortDirection.Descending)] }));
        await cut.InvokeAsync(() => list.Instance.OnColumnsResized([80.4, 300, 120, 90, 40]));

        TaskView saved = Assert.IsType<TaskView>(harness.Views.Get("custom1"));
        Assert.Equal([new SortKey("priority", Agency.Huddle.App.Tasks.Views.SortDirection.Descending)], saved.Sort);
        Assert.Equal(80, saved.ColumnWidths?["id"]);
        Assert.Equal(300, saved.ColumnWidths?["title"]);
        Assert.Equal(120, saved.ColumnWidths?["status"]);
        Assert.Equal(90, saved.ColumnWidths?["priority"]);
        Assert.Equal(4, saved.ColumnWidths?.Count);
    }

    /// <summary>A View's saved column widths come back as each header's width style, and its Sort as the column's initial direction.</summary>
    [Fact]
    public async Task SavedColumnWidths_AreAppliedToTheHeaders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView
        {
            Id = "custom1",
            Name = "Sprint Board",
            Kind = ViewKind.List,
            ColumnWidths = new Dictionary<string, int>(StringComparer.Ordinal) { ["title"] = 321 },
        });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        Assert.Contains("width: 321px", cut.Find("th.task-list-title-header").GetAttribute("style"), StringComparison.Ordinal);
    }

    /// <summary>/tasks/new's default name is "New view", then "New view 2" and so on, skipping names already taken ignoring case.</summary>
    [Fact]
    public async Task NewViewName_SkipsTakenNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Assert.Equal("New view", ViewActions.NewViewName(harness.Views));

        _ = harness.Views.Save(new TaskView { Id = "a", Name = "new VIEW", Kind = ViewKind.List });

        Assert.Equal("New view 2", ViewActions.NewViewName(harness.Views));
    }

    /// <summary>Registers the harness's services into a fresh MudBlazor-aware bUnit context.</summary>
    /// <param name="harness">The Tasks stack to wire in.</param>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Renders the Tasks page on the View <paramref name="viewId"/>, beside the popover and dialog hosts its menus need.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="viewId">The View's id.</param>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string viewId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            builder.CloseComponent();
        });
}
