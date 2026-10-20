namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins what the Tasks page's detail drawer does: it hosts the read-only <see cref="TaskReadView"/> for an opened row or card (and nothing editable),
/// Escape closes it, its <c>ContentKey</c> is the Task's id, "+ New task" opens the edit page's new-Task Tab with the View's Team and Project and leaves
/// the drawer closed; and the page also hosts <see cref="WakeToasts"/> and <see cref="AiReactingChip"/>, the latter watching every Task in the
/// current effective View, unnarrowed by the search box.
/// </summary>
public sealed class TasksPageDetailTests
{
    /// <summary>Clicking a List row opens the drawer's <see cref="TaskReadView"/> for that row's Task, and no editor (read view spec D-1).</summary>
    [Fact]
    public async Task RowOpen_ClickingATask_ShowsTheReadViewForThatId()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");

        Assert.Equal(task.Id, cut.FindComponent<TaskReadView>().Instance.Id);
        Assert.Empty(cut.FindComponents<TaskDetail>());
    }

    /// <summary>Clicking the already-open Task again is a no-op: no "Discard changes?" prompt, and the same <see cref="TaskReadView"/> instance stays mounted.</summary>
    [Fact]
    public async Task RowOpen_ClickingTheAlreadyOpenTask_DoesNotPromptOrRecreateTheDetail()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        TaskReadView firstInstance = cut.FindComponent<TaskReadView>().Instance;

        await ClickTaskRowAsync(cut, "Alpha");

        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
        Assert.Same(firstInstance, cut.FindComponent<TaskReadView>().Instance);
    }

    /// <summary>"+ New task" opens the edit page's new-Task Tab and leaves the drawer closed; with no single-Team filter there is no Team or Project to start in.</summary>
    [Fact]
    public async Task NewTaskHeaderButton_NoSingleTeamFilter_OpensTheNewTaskTabWithNoDefaults()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await cut.InvokeAsync(() => cut.Find(".tasks-new-task").ClickAsync());

        Agency.Huddle.App.Tabs.Tab tab = Assert.Single(harness.Tabs.Tabs, static t => string.Equals(t.Key, "task-new-edit", StringComparison.Ordinal));
        Assert.Equal("/tasks/item/new/edit", tab.Url);
        Assert.False(tab.IsPreview);
        Assert.False(cut.FindComponent<ResizableEndDrawer>().Instance.Open);
    }

    /// <summary>"+ New task" starts the new Task in the Team and Project the effective View's filter names, when it names exactly one of each (Spec §13.3).</summary>
    [Fact]
    public async Task NewTaskHeaderButton_FilterNamesOneTeamAndProject_PassesThemToTheNewTaskTab()
    {
        using TaskToolHarness harness = new();
        TaskView view = new()
        {
            Id = "platform-alpha",
            Name = "Platform Alpha",
            Kind = ViewKind.List,
            Filter = new TaskFilter { Teams = ["Platform"], Projects = [new ProjectRef("Platform", "Alpha")] },
        };
        Assert.True(harness.Views.Save(view).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "platform-alpha");

        await cut.InvokeAsync(() => cut.Find(".tasks-new-task").ClickAsync());

        Agency.Huddle.App.Tabs.Tab tab = Assert.Single(harness.Tabs.Tabs, static t => string.Equals(t.Key, "task-new-edit", StringComparison.Ordinal));
        Assert.Equal("/tasks/item/new/edit?team=Platform&project=Alpha", tab.Url);
    }

    /// <summary>Escape on the open drawer with nothing pending closes it (theming plan 7.5).</summary>
    [Fact]
    public async Task Escape_NothingPending_ClosesTheDrawer()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ClickTaskRowAsync(cut, "Alpha");
        Assert.True(cut.FindComponent<ResizableEndDrawer>().Instance.Open);

        await PressEscapeAsync(cut);

        Assert.False(cut.FindComponent<ResizableEndDrawer>().Instance.Open);
    }

    /// <summary>The drawer's <c>ContentKey</c> is the open Task's id, so each Task gets a fresh body (theming plan 7.7).</summary>
    [Fact]
    public async Task DrawerContentKey_IsTheTaskId()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ClickTaskRowAsync(cut, "Alpha");

        Assert.Equal(task.Id.ToString(), cut.FindComponent<ResizableEndDrawer>().Instance.ContentKey);
    }

    /// <summary>Presses Escape on the detail drawer's body, as the browser delivers it from a focused element inside.</summary>
    private static Task PressEscapeAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(() => cut.Find(".tasks-detail-drawer .resizable-drawer-body").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

    /// <summary>The page hosts <see cref="WakeToasts"/> (renders nothing itself) and a page-header <see cref="AiReactingChip"/>.</summary>
    [Fact]
    public async Task Page_HostsWakeToastsAndAiReactingChip()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        Assert.Single(cut.FindComponents<WakeToasts>());
        Assert.Single(cut.FindComponents<AiReactingChip>());
    }

    /// <summary>The <see cref="AiReactingChip"/> watches every Task in the current effective View - the whole set, still including a Task a search term currently excludes from the List/Board (Spec §10.8).</summary>
    [Fact]
    public async Task AiReactingChip_WatchesEveryTaskInTheEffectiveView_EvenWhenSearchExcludesOne()
    {
        using TaskToolHarness harness = new();
        TaskItem taskA = CreateTask(harness, "Alpha");
        TaskItem taskB = CreateTask(harness, "Bravo");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        IRenderedComponent<Agency.Huddle.App.Components.Shared.SearchField> searchBox = cut.FindComponent<Agency.Huddle.App.Components.Shared.SearchField>();
        await cut.InvokeAsync(() => searchBox.Instance.ValueChanged.InvokeAsync("Alpha"));

        // The search now excludes Bravo from the rendered List (Spec §13.12's own "No tasks match" path
        // never fires here because Alpha still matches), but AiReactingChip.Watched is unnarrowed.
        List<TaskId> watched = [.. cut.FindComponent<AiReactingChip>().Instance.Watched.OrderBy(id => id.ToString(), StringComparer.Ordinal)];
        List<TaskId> expected = [.. new[] { taskA.Id, taskB.Id }.OrderBy(id => id.ToString(), StringComparer.Ordinal)];
        Assert.Equal(expected, watched);
    }

    /// <summary>Renders <see cref="TasksPage"/>, optionally routed to <paramref name="viewId"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string? viewId = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            if (viewId is not null)
            {
                builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            }

            builder.CloseComponent();
        });

    /// <summary>Creates a Task through the real <see cref="TaskService"/>, exactly as a Human would.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, "Platform", null), TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Finds the List row whose Title cell contains <paramref name="title"/>.</summary>
    private static IElement FindTaskRow(IRenderedComponent<ContainerFragment> cut, string title)
    {
        IElement cell = cut.FindAll("td").First(td => td.TextContent.Contains(title, StringComparison.Ordinal));
        return cell.Closest("tr") ?? throw new InvalidOperationException("The clicked cell has no row.");
    }

    /// <summary>Clicks the List row whose Title cell contains <paramref name="title"/>.</summary>
    private static Task ClickTaskRowAsync(IRenderedComponent<ContainerFragment> cut, string title) =>
        cut.InvokeAsync(() => FindTaskRow(cut, title).ClickAsync());
}
