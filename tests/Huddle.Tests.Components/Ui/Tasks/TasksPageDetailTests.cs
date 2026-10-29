namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins how the Tasks page shows a Task's details: opening a row or card publishes that Task to the Right sidebar and opens it there (the page has no
/// drawer of its own), "+ New task" opens the edit page's new-Task Tab with the View's Team and Project and
/// selects nothing; and the page also hosts <see cref="WakeToasts"/> and <see cref="AiReactingChip"/>, the latter watching every Task in the
/// current effective View, unnarrowed by the search box.
/// </summary>
public sealed class TasksPageDetailTests
{
    /// <summary>Clicking a List row publishes that row's Task to the Right sidebar and opens it, and the page holds no read view or editor itself (read view spec D-1).</summary>
    [Fact]
    public async Task RowOpen_ClickingATask_PublishesTheTaskAndOpensTheRightSidebar()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");

        Assert.Equal(new TaskDetailSubject(task.Id), cut.FindComponent<PageSubjectSource>().Instance.Value);
        Assert.True(ctx.Services.GetRequiredService<RightSidebarService>().IsOpen);
        Assert.Empty(cut.FindComponents<TaskReadView>());
        Assert.Empty(cut.FindComponents<TaskDetail>());
    }

    /// <summary>Clicking the already-selected Task again after the Right sidebar was closed opens it again, on the same Task.</summary>
    [Fact]
    public async Task RowOpen_ClickingTheSelectedTaskWithTheSidebarClosed_OpensItAgain()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RightSidebarService sidebar = ctx.Services.GetRequiredService<RightSidebarService>();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ClickTaskRowAsync(cut, "Alpha");
        sidebar.Toggle();
        Assert.False(sidebar.IsOpen);

        await ClickTaskRowAsync(cut, "Alpha");

        Assert.True(sidebar.IsOpen);
        Assert.Equal(new TaskDetailSubject(task.Id), cut.FindComponent<PageSubjectSource>().Instance.Value);
    }

    /// <summary>The open Task's row is marked while the Right sidebar shows it, and not once the sidebar is closed.</summary>
    [Fact]
    public async Task RowOpen_TheSelectedRowIsMarkedOnlyWhileTheSidebarIsOpen()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RightSidebarService sidebar = ctx.Services.GetRequiredService<RightSidebarService>();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ClickTaskRowAsync(cut, "Alpha");
        Assert.Equal(task.Id, cut.FindComponent<TaskListView>().Instance.SelectedId);

        await cut.InvokeAsync(sidebar.Toggle);

        cut.WaitForAssertion(() => Assert.Null(cut.FindComponent<TaskListView>().Instance.SelectedId));
    }

    /// <summary>"+ New task" opens the edit page's new-Task Tab and selects nothing; with no single-Team filter there is no Team or Project to start in.</summary>
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
        Assert.Null(cut.FindComponent<PageSubjectSource>().Instance.Value);
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

    /// <summary>The Team route (<c>/teams/{Team}/tasks/{ViewId}</c>) shows only that Team's Tasks, matching its name ignoring case, says so without a Reset filters button when the Team has none, and never saves the Team to the View.</summary>
    [Fact]
    public async Task TeamRoute_ShowsOnlyThatTeamsTasks_WithoutSavingTheTeamToTheView()
    {
        using TaskToolHarness harness = new();
        CreateTask(harness, "Alpha");
        await using MudBunitContext sameCtx = UiTestHost.Create().WithHarness(harness);
        await using MudBunitContext otherCtx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> sameTeam = RenderPage(sameCtx, ViewStore.AllTasksId, team: "platform");
        Assert.NotNull(FindTaskRow(sameTeam, "Alpha"));
        Assert.Empty(sameTeam.FindAll(".tasks-empty .empty-state-title"));

        IRenderedComponent<ContainerFragment> otherTeam = RenderPage(otherCtx, ViewStore.AllTasksId, team: "Growth");
        Assert.Equal("No tasks in Growth yet", otherTeam.Find(".tasks-empty .empty-state-title").TextContent.Trim());
        Assert.Empty(otherTeam.FindAll(".tasks-empty-action"));

        TaskView saved = Assert.IsType<TaskView>(harness.Views.Get(ViewStore.AllTasksId));
        Assert.Empty(saved.Filter.Teams);
    }

    /// <summary>"+ New task" on the Team route starts the new Task in that Team, though the View's own filter names none.</summary>
    [Fact]
    public async Task NewTaskHeaderButton_TeamRoute_StartsTheNewTaskInThatTeam()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, ViewStore.AllTasksId, team: "Platform");

        await cut.InvokeAsync(() => cut.Find(".tasks-new-task").ClickAsync());

        Agency.Huddle.App.Tabs.Tab tab = Assert.Single(harness.Tabs.Tabs, static t => string.Equals(t.Key, "task-new-edit", StringComparison.Ordinal));
        Assert.Equal("/tasks/item/new/edit?team=Platform", tab.Url);
    }

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

    /// <summary>Renders <see cref="TasksPage"/>, optionally routed to <paramref name="viewId"/> and narrowed to <paramref name="team"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string? viewId = null, string? team = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            if (viewId is not null)
            {
                builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            }

            if (team is not null)
            {
                builder.AddAttribute(2, nameof(TasksPage.Team), team);
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
