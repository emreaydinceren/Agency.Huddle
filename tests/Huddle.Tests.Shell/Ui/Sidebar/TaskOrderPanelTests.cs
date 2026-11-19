using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>
/// Pins the Task order panel (TaskOrder spec §5.2): one timeline per group, a dot coloured by state, the title then the id and
/// the reason, the open Task in bold, a collapsed Backlog, a redraw on a Task change, and nothing for a Subject that names no Team.
/// </summary>
public sealed class TaskOrderPanelTests
{
    private const string Item = ".mud-timeline-item";

    /// <summary>Creates a Task in the Platform Team through the real service.</summary>
    private static TaskItem Create(TaskToolHarness harness, string title, TaskState status = TaskState.ToDo, int? rank = null, IReadOnlyList<TaskId>? blockedBy = null) =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft(title, "Platform", null, Status: status, BlockedBy: blockedBy) { Rank = rank },
            TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>Renders the panel for <paramref name="subject"/> in the frame's context.</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, SidebarPanelContext panel, PageSubject subject) =>
        ctx.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<SidebarPanelContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<SidebarPanelContext>.Value), panel);
            builder.AddComponentParameter(2, nameof(CascadingValue<SidebarPanelContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<SidebarPanelContext>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<TaskOrderPanel>(0);
                inner.AddComponentParameter(1, "Subject", subject);
                inner.AddComponentParameter(2, "TabId", "t1");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private static List<string> Titles(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(Item + " .task-order-title").Select(static e => e.TextContent.Trim())];

    /// <summary>Started and Next each get their own timeline, with a group label above it, and the items run in TaskOrder's order.</summary>
    [Fact]
    public async Task Groups_StartedThenNext_OneTimelineEach_InOrder()
    {
        using TaskToolHarness harness = new();
        TaskItem started = Create(harness, "Started one", TaskState.InProgress);
        _ = Create(harness, "Second", rank: 2);
        _ = Create(harness, "First", rank: 1);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(started.Id));

        Assert.Equal(["Started", "Next"], cut.FindAll(".task-order-group-label").Select(static e => e.TextContent.Trim()));
        Assert.Equal(2, cut.FindAll(".mud-timeline").Count);
        Assert.Equal(["Started one", "First", "Second"], Titles(cut));
    }

    /// <summary>The Next entry's dot is Primary, a Started one Info, another startable one Default, and a blocked one Warning.</summary>
    [Fact]
    public async Task Dots_AreColouredByState()
    {
        using TaskToolHarness harness = new();
        _ = Create(harness, "Started one", TaskState.InProgress);
        _ = Create(harness, "Next one", rank: 1);
        _ = Create(harness, "Later one", rank: 2);
        TaskItem blocker = Create(harness, "Blocker", TaskState.Backlog);
        TaskItem blocked = Create(harness, "Blocked one", rank: 3, blockedBy: [blocker.Id]);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(blocked.Id));

        Dictionary<string, string> colours = cut.FindAll(Item).ToDictionary(
            static item => item.QuerySelector(".task-order-title")?.TextContent.Trim() ?? string.Empty,
            static item => string.Join(' ', item.QuerySelectorAll(".mud-timeline-item-dot-inner").SelectMany(static dot => dot.ClassList).Where(static c => c.StartsWith("mud-timeline-dot-", StringComparison.Ordinal))),
            StringComparer.Ordinal);
        Assert.Equal("mud-timeline-dot-primary", colours["Next one"]);
        Assert.Equal("mud-timeline-dot-info", colours["Started one"]);
        Assert.Equal("mud-timeline-dot-default", colours["Later one"]);
        Assert.Equal("mud-timeline-dot-warning", colours["Blocked one"]);
        Assert.Equal("mud-timeline-dot-warning", colours["Blocker"]);
    }

    /// <summary>Each item reads the title, then the id and the reason joined with a middle dot, and a plain startable entry shows only its id.</summary>
    [Fact]
    public async Task Item_TitleThenIdAndReason()
    {
        using TaskToolHarness harness = new();
        TaskItem parser = Create(harness, "Import parser", rank: 3);
        TaskItem import = Create(harness, "Import", rank: 1, blockedBy: [parser.Id]);
        TaskItem docs = Create(harness, "Docs", rank: 2);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(docs.Id));

        Assert.Equal(["Import parser", "Import", "Docs"], Titles(cut));
        Assert.Equal(
            [$"{parser.Id} · Next · blocks {import.Id}", $"{import.Id} · blocked by {parser.Id}", $"{docs.Id}"],
            cut.FindAll(Item + " .task-order-meta").Select(static e => e.TextContent.Trim()));
    }

    /// <summary>A Backlog Task listed ahead of the Task it blocks says so, and names that Task.</summary>
    [Fact]
    public async Task BacklogBlocker_SaysInBacklogAndWhatItBlocks()
    {
        using TaskToolHarness harness = new();
        TaskItem parked = Create(harness, "Parked blocker", TaskState.Backlog);
        TaskItem waiting = Create(harness, "Waiting one", rank: 1, blockedBy: [parked.Id]);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(waiting.Id));

        Assert.Equal(
            [$"{parked.Id} · in Backlog · blocks {waiting.Id}", $"{waiting.Id} · blocked by {parked.Id}"],
            cut.FindAll(Item + " .task-order-meta").Select(static e => e.TextContent.Trim()));
    }

    /// <summary>The title opens the Task through the same link the other Task rows use.</summary>
    [Fact]
    public async Task Title_LinksToTheTask()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Create(harness, "Only one", rank: 1);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(task.Id));

        Assert.Equal($"/tasks/item/{task.Id}", cut.Find(Item + " a").GetAttribute("href"));
    }

    /// <summary>Only the open Task's title is bold.</summary>
    [Fact]
    public async Task OpenTask_IsBold()
    {
        using TaskToolHarness harness = new();
        _ = Create(harness, "First", rank: 1);
        TaskItem open = Create(harness, "Open one", rank: 2);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskDetailSubject(open.Id));

        Assert.Equal(["Open one"], cut.FindAll(".task-order-current").Select(static e => e.TextContent.Trim()));
    }

    /// <summary>The Backlog group is collapsed with its count, opens on a click, and the frame counts the startable Tasks only.</summary>
    [Fact]
    public async Task Backlog_IsCollapsedWithItsCount_AndOpensOnClick()
    {
        using TaskToolHarness harness = new();
        TaskItem next = Create(harness, "Next one", rank: 1);
        _ = Create(harness, "Parked", TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, panel, new TaskSubject(next.Id));

        Assert.Equal(["Next one"], Titles(cut));
        Assert.Equal("Backlog (1)", cut.Find(".task-order-backlog-toggle").TextContent.Trim());
        Assert.Equal(1, panel.Count);

        cut.Find(".task-order-backlog-toggle").Click();

        Assert.Equal(["Next one", "Parked"], Titles(cut));
    }

    /// <summary>A new Task redraws the panel with no reload.</summary>
    [Fact]
    public async Task ANewTask_Redraws()
    {
        using TaskToolHarness harness = new();
        TaskItem first = Create(harness, "First", rank: 1);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(first.Id));
        Assert.Equal(["First"], Titles(cut));

        _ = Create(harness, "Late one", rank: 2);

        cut.WaitForAssertion(() => Assert.Equal(["First", "Late one"], Titles(cut)));
    }

    /// <summary>Raising TasksReloaded after a Task is written straight to disk redraws the panel with that Task.</summary>
    [Fact]
    public async Task TasksReloaded_Redraws()
    {
        using TaskToolHarness harness = new();
        TaskItem first = Create(harness, "First", rank: 1);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(first.Id));
        Assert.Equal(["First"], Titles(cut));

        Seed(harness, TestTasks.Make(id: "PLAT-0099", title: "Written on disk") with { Rank = 2 });
        harness.Events.RaiseTasksReloaded();

        cut.WaitForAssertion(() => Assert.Equal(["First", "Written on disk"], Titles(cut)));
    }

    /// <summary>Two Tasks that block each other each say which one they are in a cycle with, and both dots are Warning.</summary>
    [Fact]
    public async Task Cycle_BothEntriesNameTheOtherAndAreWarning()
    {
        using TaskToolHarness harness = new();
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "Cycle A", blockedBy: [new TaskId("PLAT", 2)]) with { Rank = 1 },
            TestTasks.Make(id: "PLAT-0002", title: "Cycle B", blockedBy: [new TaskId("PLAT", 1)]) with { Rank = 2 });
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(new TaskId("PLAT", 1)));

        Assert.Equal(
            ["PLAT-0002 · blocks PLAT-0001 · blocked by PLAT-0001 · cycle with PLAT-0001", "PLAT-0001 · blocked by PLAT-0002 · cycle with PLAT-0002"],
            cut.FindAll(Item + " .task-order-meta").Select(static e => e.TextContent.Trim()));
        Assert.Equal(
            ["mud-timeline-dot-warning", "mud-timeline-dot-warning"],
            cut.FindAll(Item).Select(static item => DotClass(item)));
    }

    /// <summary>A Backlog-group entry's dot is Default.</summary>
    [Fact]
    public async Task BacklogGroupEntry_DotIsDefault()
    {
        using TaskToolHarness harness = new();
        TaskItem next = Create(harness, "Next one", rank: 1);
        _ = Create(harness, "Parked", TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, new SidebarPanelContext(), new TaskSubject(next.Id));

        cut.Find(".task-order-backlog-toggle").Click();

        IElement parked = cut.FindAll(Item).Single(static item => item.QuerySelector(".task-order-title")?.TextContent.Trim() == "Parked");
        Assert.Equal("mud-timeline-dot-default", DotClass(parked));
    }

    /// <summary>The dot colour class of one timeline item.</summary>
    private static string DotClass(IElement item) =>
        string.Join(' ', item.QuerySelectorAll(".mud-timeline-item-dot-inner").SelectMany(static dot => dot.ClassList).Where(static c => c.StartsWith("mud-timeline-dot-", StringComparison.Ordinal)));

    /// <summary>Writes Tasks straight to disk and reloads the store, as a file watcher would.</summary>
    private static void Seed(TaskToolHarness harness, params TaskItem[] tasks)
    {
        foreach (TaskItem task in tasks)
        {
            string fullPath = TaskLayout.PathFor(harness.TasksDirPath, task.Location, task.Id);
            TestTaskStore.WriteTask(harness.TasksDirPath, Path.GetRelativePath(harness.TasksDirPath, fullPath), task);
        }

        harness.Store.RebuildFromWatcher();
    }

    /// <summary>A Document Subject draws nothing and reports no count.</summary>
    [Fact]
    public async Task DocumentSubject_RendersNothing()
    {
        using TaskToolHarness harness = new();
        _ = Create(harness, "First", rank: 1);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();
        LibraryRoot root = new("teams", "Teams", "/tmp/teams", LibraryRootKind.Teams);
        DocumentSubject subject = new(new LibraryPath(root, "Plan.md", "/tmp/teams/Plan.md", LibraryNodeRole.File), LibraryFileKind.Markdown, "# Hi");

        IRenderedComponent<ContainerFragment> cut = Render(ctx, panel, subject);

        Assert.Empty(cut.FindAll(".mud-timeline"));
        Assert.Empty(cut.FindAll(".task-order-group-label"));
        Assert.Null(panel.Count);
    }

    /// <summary>A Room that belongs to a Team shows that Team's order; a root Room shows nothing.</summary>
    [Fact]
    public async Task RoomSubject_ShowsItsTeamsOrder_AndARootRoomShowsNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Create(harness, "Platform work", rank: 1);
        Room teamRoom = await harness.Directory.CreateRoomAsync("Team room", [KnownIds.Human], "Platform", false, ct: ct);
        Room rootRoom = await harness.Directory.CreateRoomAsync("Root room", [KnownIds.Human], null, false, ct: ct);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> shown = Render(ctx, new SidebarPanelContext(), new RoomSubject(teamRoom.Id));
        IRenderedComponent<ContainerFragment> none = Render(ctx, new SidebarPanelContext(), new RoomSubject(rootRoom.Id));

        shown.WaitForAssertion(() => Assert.Equal(["Platform work"], Titles(shown)));
        Assert.Empty(none.FindAll(".mud-timeline"));
    }
}
