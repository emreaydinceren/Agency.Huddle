using Agency.Huddle.Tests.Css;
namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins the visual audit's round-3 Tasks package (B4a-B4r, A6, A7, B1, B10 and E2, E3, E9, E10): the Board's rails and
/// swimlanes, the List's group rows and date columns, the drawer's footer, title edit and form, the page header, the
/// Fields dialog and the menus.
/// </summary>
public sealed class R3TasksTests
{
    /// <summary>A Friday: 2026-10-16.</summary>
    private static readonly DateOnly Today = new(2026, 10, 16);

    // ---- B4h: dates in a table column ----

    /// <summary>A date two to six days ahead is month and day in a table column, never a weekday; the relative words and other years are unchanged (audit B4h).</summary>
    /// <param name="offsetDays">Days from today.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Tomorrow")]
    [InlineData(2, "Oct 18")]
    [InlineData(6, "Oct 22")]
    [InlineData(7, "Oct 23")]
    [InlineData(-1, "Yesterday")]
    [InlineData(-2, "Oct 14")]
    [InlineData(80, "Jan 4, 2027")]
    public void FormatColumn_NeverShowsAWeekday(int offsetDays, string expected)
    {
        Assert.Equal(expected, HumanDate.FormatColumn(Today.AddDays(offsetDays), Today));
    }

    /// <summary>The same date outside a column keeps its weekday, so the card and the drawer are unchanged by the column rule.</summary>
    [Fact]
    public void Format_OutsideAColumn_StillShowsTheWeekday()
    {
        Assert.Equal("Sun", HumanDate.Format(Today.AddDays(2), Today));
    }

    /// <summary>The List's Due date column reads month and day for a date three days ahead, where the card would read a weekday.</summary>
    [Fact]
    public async Task ListDueDateColumn_ReadsMonthAndDay_NotAWeekday()
    {
        using TaskToolHarness harness = new();
        DateOnly due = DateOnly.FromDateTime(DateTime.Now).AddDays(3);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Alpha", "Platform", null, DueDate: due), TaskActors.Human(harness.Options.Value)));
        Assert.True(harness.Views.Save(new TaskView { Id = "dates", Name = "Dates", Kind = ViewKind.List, Fields = ["status", "due_date"] }).Saved);

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "dates");

        string text = cut.FindAll("td").Select(td => td.TextContent.Trim()).First(t => t.StartsWith(due.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        Assert.Equal(TaskDates.HumanColumn(due, TimeProvider.System), text);
        Assert.DoesNotContain(due.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
    }

    // ---- B4f, B4g: List group rows and lines ----

    /// <summary>A group row is the label at 13px/600 and a count, with an assignee group leading with an avatar; the row has no wash and the grid sets no line colour of its own (audit B4f, B4g).</summary>
    [Fact]
    public async Task ListGroupRow_HasLabelCountAndAnAssigneeAvatar_AndNoWash()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Alpha", "Platform", null, Assignee: "Nova"), TaskActors.Human(harness.Options.Value)));
        Assert.True(harness.Views.Save(new TaskView { Id = "by-person", Name = "By person", Kind = ViewKind.List, Grouping = [TaskGroupField.Assignee] }).Saved);

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "by-person");

        IElement heading = cut.Find(".task-group-heading");
        Assert.Equal("Nova", heading.QuerySelector(".task-group-label")?.TextContent.Trim());
        Assert.Equal("1", heading.QuerySelector(".task-group-count")?.TextContent.Trim());
        Assert.NotNull(heading.QuerySelector(".mud-avatar-small"));

        string css = CssText.AppCss();
        string wash = CssText.RuleBody(css, ".mud-data-grid.task-list-grid .mud-table-cell.mud-datagrid-group");
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("background-color: var(--hu-list-group-background);", wash, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-weight: 600;", CssText.RuleBody(css, ".task-group-label"), StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-size: var(--mud-typography-caption-size);", CssText.RuleBody(css, ".task-group-count"), StringComparison.Ordinal);

        foreach (string rule in RulesWithSelector(css, "task-list-grid"))
        {
            Assert.DoesNotContain("e0e0e0", rule, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("background-gray", rule, StringComparison.Ordinal);
            Assert.DoesNotContain("border-bottom", rule, StringComparison.Ordinal);
        }
    }

    /// <summary>A non-assignee group row (a Team) draws no avatar.</summary>
    [Fact]
    public async Task ListGroupRow_ForATeam_HasNoAvatar()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Alpha", "Platform", null, Assignee: "Nova"), TaskActors.Human(harness.Options.Value)));
        Assert.True(harness.Views.Save(new TaskView { Id = "by-team", Name = "By team", Kind = ViewKind.List, Grouping = [TaskGroupField.Team] }).Saved);

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "by-team");

        Assert.Empty(cut.FindAll(".task-group-heading .mud-avatar"));
    }

    // ---- B4a, B4b, B4c: the Board's rails, swimlanes and empty cells ----

    /// <summary>With one Task in To Do, the empty Done and Won't do columns are 40px rails with a name and a count, and the open columns are 260-300px (audit B4a).</summary>
    [Fact]
    public async Task Board_EmptyTerminalColumns_AreRails_AndOthersAre260To300()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await using MudBunitContext ctx = new();

        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo)]);

        Assert.Equal(["Done", "Won't do"], cut.FindAll(".task-board-column-rail .task-board-rail-label").Select(label => label.TextContent.Trim()).Distinct(StringComparer.Ordinal));
        Assert.Equal(2, cut.FindAll("div.task-board-column-header-rail").Count);
        string style = cut.Find(".task-board-columns").GetAttribute("style") ?? string.Empty;
        Assert.Equal("grid-template-columns: minmax(260px, 300px) minmax(260px, 300px) minmax(260px, 300px) minmax(260px, 300px) minmax(260px, 300px) 40px 40px;", style);
        Assert.Equal(style, cut.Find(".task-board-lane-columns").GetAttribute("style"));
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("grid-auto-columns: minmax(260px, 300px)", CssText.RuleBody(CssText.AppCss(), ".task-board-lane-columns"), StringComparison.Ordinal);
        Assert.Equal(["0", "0"], cut.FindAll("div.task-board-column-header-rail .task-board-column-count").Select(count => count.TextContent.Trim()));
    }

    /// <summary>A terminal column holding a Task is an ordinary column, and a column that is not terminal never collapses, however empty.</summary>
    [Fact]
    public async Task Board_TerminalColumnWithATask_IsNotARail_AndOpenEmptyColumnsAreNot()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await using MudBunitContext ctx = new();

        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", status: TaskState.Done), TestTasks.Make(id: "PLAT-0002", status: TaskState.Cancelled)]);

        Assert.Empty(cut.FindAll("div.task-board-column-header-rail"));
        Assert.Equal(["Backlog is empty.", "Nothing to do yet.", "Nothing in progress.", "Nothing in review.", "Nothing needs you."], cut.FindAll(".task-board-column-empty").Select(e => e.TextContent.Trim()));
    }

    /// <summary>A rail is a column that opens while its name is being renamed, so the field fits.</summary>
    [Fact]
    public async Task Board_RenamingARail_OpensIt()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Assert.True(harness.Views.Save(BoardView() with { Id = "rail-rename" }).Saved);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoardWithPopovers(ctx, harness, BoardView() with { Id = "rail-rename" }, []);
        Assert.Equal(2, cut.FindAll("div.task-board-column-header-rail").Count);

        await cut.InvokeAsync(() => cut.FindAll("button[aria-label='Done column menu']").Single().ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll(".mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "Rename", StringComparison.Ordinal)).ClickAsync());

        Assert.Single(cut.FindAll("div.task-board-column-header-rail"));
        Assert.NotEmpty(cut.FindAll(".task-board-column-rename"));
    }

    /// <summary>In swimlanes, an empty cell is blank: a column empty in every lane says so once, and a column with a Task in any lane says nothing in the lanes without one (audit B4c).</summary>
    [Fact]
    public async Task Board_Swimlanes_EmptyCellsAreBlank_TheLineShowsOnlyForAWhollyEmptyColumn()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks =
        [
            TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova"),
            TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, assignee: "Kai"),
        ];
        await using MudBunitContext ctx = new();

        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), tasks);

        string[] lines = [.. cut.FindAll(".task-board-column-empty").Select(e => e.TextContent.Trim())];
        Assert.Single(lines, "Backlog is empty.");
        Assert.DoesNotContain("Nothing to do yet.", lines);
        Assert.Single(lines, "Nothing in progress.");
        Assert.Single(lines, "Nothing in review.");
    }

    /// <summary>A swimlane label is the section heading (an h2 in the group-heading style) with its count; a Chrome line sits under it (audit B4b).</summary>
    [Fact]
    public async Task Board_SwimlaneLabel_IsASectionHeadingWithItsCount_UnderAChromeLine()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks =
        [
            TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova"),
            TestTasks.Make(id: "PLAT-0002", status: TaskState.InProgress, assignee: "Nova"),
        ];
        await using MudBunitContext ctx = new();

        IRenderedComponent<TaskBoard> cut = RenderBoard(ctx, harness, BoardView(grouping: [TaskGroupField.Assignee]), tasks);

        IElement heading = cut.Find("h2.group-heading.task-board-lane-heading");
        Assert.Equal("Nova", heading.QuerySelector(".task-board-lane-label")?.TextContent.Trim());
        Assert.Equal("2", heading.QuerySelector(".task-board-lane-count")?.TextContent.Trim());
        string rule = CssText.RuleBody(CssText.AppCss(), ".task-board-lane-heading.group-heading");
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("border-bottom: 1px solid var(--hu-line-region);", rule, StringComparison.Ordinal);
    }

    // ---- B4d, B10f, B10i: the row and card menus ----

    /// <summary>A card's menu activator names its Task, has no tooltip, and is revealed on hover or focus; every item has an icon (audit B4d, B10f, B10i).</summary>
    [Fact]
    public async Task CardMenu_NamesItsTask_HasNoTooltip_IsRevealed_AndEveryItemHasAnIcon()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoardWithPopovers(ctx, harness, BoardView(), [TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo)]);

        Assert.NotNull(cut.Find(".task-card .task-actions-reveal"));
        Assert.Empty(cut.FindAll(".task-card .mud-tooltip-root"));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());

        IReadOnlyList<IElement> items = cut.FindAll(".mud-menu-item");
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.NotNull(item.QuerySelector(".mud-icon-root")));
        Assert.NotNull(cut.Find(".task-card .task-actions-menu.is-open")); // the activator stays visible while its menu is open

        string css = CssText.AppCss();
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("opacity: 0;", CssText.RuleBody(css, ".task-actions-reveal .task-actions-button"), StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains(".task-card:hover .task-actions-reveal .task-actions-button", css, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains(".mud-table-row:focus-within .task-actions-reveal .task-actions-button", css, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("@media (hover: none)", css, StringComparison.Ordinal);
    }

    /// <summary>A List row's menu is the same revealed menu, named for its Task.</summary>
    [Fact]
    public async Task ListRowMenu_IsRevealed_AndNamedForItsTask()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        Assert.NotNull(cut.Find($"tr .task-actions-reveal button[aria-label='Actions for {task.Id}']"));
        Assert.Empty(cut.FindAll("tr .mud-tooltip-root"));
    }

    // ---- B4e: the need-you count ----

    /// <summary>"need you" wears its signal only above 0 (audit B4e).</summary>
    [Fact]
    public async Task NeedYouCount_WearsItsSignalOnlyAboveZero()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> empty = RenderPage(ctx);
        Assert.Contains("is-zero", empty.Find(".tasks-count-need-you").ClassList);

        using TaskToolHarness other = new();
        _ = Assert.IsType<TaskResult.Saved>(other.Service.Create(new TaskDraft("Mine", "Platform", null, Assignee: other.Options.Value.HumanName), TaskActors.Human(other.Options.Value)));
        await using MudBunitContext ctx2 = UiTestHost.Create().WithHarness(other);
        IRenderedComponent<ContainerFragment> some = RenderPage(ctx2);

        Assert.DoesNotContain("is-zero", some.Find(".tasks-count-need-you").ClassList);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains(".tasks-count-need-you:not(.is-zero)", CssText.AppCss(), StringComparison.Ordinal);
    }

    // ---- B1, E2: the page header and document title ----

    /// <summary>The page has no visible PageHeader (T2): the View's display name is a visually hidden h1, and New task is the header row's filled button, the page's one primary action (audit B1).</summary>
    [Fact]
    public async Task Page_HasNoVisibleHeader_HiddenViewNameH1_AndAFilledNewTaskInTheHeaderRow()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "platform-board", Name = "Platform board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "platform-board");

        Assert.Empty(cut.FindAll(".page-header"));
        IElement heading = cut.Find("h1.tasks-view-heading");
        Assert.Equal("Platform board", heading.TextContent.Trim());
        Assert.Contains("visually-hidden", heading.ClassList);
        IElement create = cut.Find(".tasks-header .task-toolbar-header .tasks-new-task");
        Assert.Equal("New task", create.TextContent.Trim());
        Assert.Contains("mud-button-filled", create.ClassList);
        Assert.Contains("btn-action-tight", create.ClassList);
    }

    /// <summary>
    /// With no visible h1, the page names its View in the shell breadcrumb (S1a): the Tab's trail is "Tasks › {View name}", the
    /// display name rather than the slug, with only "Tasks" a link. The URL alone would give just "Tasks".
    /// </summary>
    [Fact]
    public async Task Breadcrumb_IsTasksThenTheViewsDisplayName()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "platform-board", Name = "Platform board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        Tab tab = harness.Tabs.Open("/tasks/platform-board");
        Assert.Equal([new TrailCrumb("Tasks", "/tasks", "tasks")], harness.Tabs.Trail(tab));

        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<CascadingValue<TabPageContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<TabPageContext>.Value), new TabPageContext(tab.Url, tab.Id));
            builder.AddAttribute(2, nameof(CascadingValue<TabPageContext>.ChildContent), (RenderFragment)(static page =>
            {
                page.OpenComponent<TasksPage>(0);
                page.AddAttribute(1, nameof(TasksPage.ViewId), "platform-board");
                page.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal([new TrailCrumb("Tasks", "/tasks", "tasks"), new TrailCrumb("Platform board", null, string.Empty)], harness.Tabs.Trail(tab));
    }

    /// <summary>The document title follows the Tab's title: a Task's item route is "Task {id}" and a View's route is the View's name, never the stale "All Tasks" (audit E2).</summary>
    [Fact]
    public async Task DocumentTitle_FollowsTheTabsTitle()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        Assert.True(harness.Views.Save(new TaskView { Id = "platform-board", Name = "Platform board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> view = RenderPageWithTitle(ctx, viewId: "platform-board", taskIdText: null);
        Assert.Equal("Platform board", DocumentTitleOf(view));
        await using MudBunitContext ctx2 = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> item = RenderPageWithTitle(ctx2, viewId: null, taskIdText: task.Id.ToString());

        Assert.Equal($"Task {task.Id}", DocumentTitleOf(item));
    }

    /// <summary>Switching Views in place (the sidebar's View link re-parameterises the mounted page) or by remounting under a new key updates the document title with the h1 (audit E2, round 3).</summary>
    /// <param name="remount">True to mount the next View under a fresh key, as a new Tab does; false to re-parameterise the same instance.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentTitle_FollowsAnInAppViewSwitch(bool remount)
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "platform-board", Name = "Platform board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ViewSwitchHost? host = null;
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ViewSwitchHost>(0);
            builder.AddAttribute(1, nameof(ViewSwitchHost.Remount), remount);
            builder.AddComponentReferenceCapture(2, instance => host = (ViewSwitchHost)instance);
            builder.CloseComponent();
        });
        Assert.Equal("All Tasks", DocumentTitleOf(cut));

        Assert.NotNull(host);
        await cut.InvokeAsync(() => host.Show("platform-board"));

        cut.WaitForAssertion(() => Assert.Equal("Platform board", cut.Find("title").TextContent.Trim()));
        Assert.Equal("Platform board", cut.Find(".tasks-view-heading").TextContent.Trim());
    }

    /// <summary>Following a View link from the Tasks Tab re-points the preview Tab, and the document title follows (audit E2, round 3).</summary>
    [Fact]
    public async Task DocumentTitle_FollowsAViewLinkThroughTheTabHost()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "platform-board", Name = "Platform board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        ctx.Services.AddSingleton(new Agency.Huddle.App.Tabs.TabPageRoutes(typeof(TasksPage).Assembly));
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/tasks");
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Microsoft.AspNetCore.Components.Web.HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<Agency.Huddle.App.Components.Layout.TabHost>(1);
            builder.CloseComponent();
        });
        Assert.Equal("All Tasks", DocumentTitleOf(cut));

        await cut.InvokeAsync(() => nav.NavigateTo("/tasks/platform-board"));

        cut.WaitForAssertion(() => Assert.Equal("Platform board", cut.Find(".tasks-view-heading").TextContent.Trim()));
        Assert.Equal("Platform board", cut.Find("title").TextContent.Trim());
    }

    /// <summary>With the Tasks page open in two panes, the document title is the focused pane's page whichever pane mounted last, and follows the focus (audit E2, round 3: the pane mounted last owned the title).</summary>
    [Fact]
    public async Task DocumentTitle_IsTheFocusedPanesPage_NotTheLastMountedPanes()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "platform-board", Name = "Platform board", Kind = ViewKind.List }).Saved);
        Assert.True(harness.Views.Save(new TaskView { Id = "other", Name = "Other", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        Agency.Huddle.App.Tabs.TabService tabs = new();
        ctx.Services.AddSingleton(tabs);
        ctx.Services.AddSingleton(new Agency.Huddle.App.Tabs.TabPageRoutes(typeof(TasksPage).Assembly));
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/tasks/platform-board");
        Agency.Huddle.App.Tabs.Tab board = tabs.Open("/tasks/platform-board");
        tabs.Keep(board.Id);
        Agency.Huddle.App.Tabs.Tab other = tabs.Open("/tasks/other");
        tabs.Keep(other.Id);
        Assert.True(tabs.MoveRight(other.Id));
        tabs.FocusGroup(tabs.Groups[0].Id);

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Microsoft.AspNetCore.Components.Web.HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<Agency.Huddle.App.Components.Layout.TabHost>(1);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Equal(["Platform board", "Other"], cut.FindAll(".tasks-view-heading").Select(h => h.TextContent.Trim()).ToArray()));
        Assert.Equal("Platform board", cut.Find("title").TextContent.Trim());

        await cut.InvokeAsync(() => tabs.FocusGroup(tabs.Groups[1].Id));

        cut.WaitForAssertion(() => Assert.Equal("Other", cut.Find("title").TextContent.Trim()));
    }

    /// <summary>Mounts the Tasks page beside a <c>HeadOutlet</c> and lets a test change its View the way a sidebar link does.</summary>
    private sealed class ViewSwitchHost : ComponentBase
    {
        private string? viewId;

        /// <summary>Whether the next View mounts under a fresh key, as a new Tab does.</summary>
        [Parameter]
        public bool Remount { get; set; }

        /// <summary>Shows the View with this id.</summary>
        /// <param name="id">The View id.</param>
        public void Show(string id)
        {
            this.viewId = id;
            this.StateHasChanged();
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Microsoft.AspNetCore.Components.Web.HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<TasksPage>(1);
            if (this.Remount)
            {
                builder.SetKey(this.viewId ?? "all");
            }

            if (this.viewId is not null)
            {
                builder.AddAttribute(2, nameof(TasksPage.ViewId), this.viewId);
            }

            builder.CloseComponent();
        }
    }

    // ---- B4k, B4j, B4p: the drawer's footer, title edit and draft ----

    /// <summary>Opening an existing Task shows it in the Right sidebar, so the page itself holds no footer, no Cancel or Save and no form control (read view spec D-1).</summary>
    [Fact]
    public async Task Page_ExistingTask_HasNoFooterAndNoEditor()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ClickTaskRowAsync(cut, "Alpha");

        Assert.IsType<TaskDetailSubject>(cut.FindComponent<PageSubjectSource>().Instance.Value);
        Assert.Empty(cut.FindAll(".task-detail-save"));
        Assert.Empty(cut.FindAll(".task-read-view"));
    }

    /// <summary>The read view has no title edit (the title is edited on the edit page), and the inline title field keeps the title's size (audit B4j).</summary>
    [Fact]
    public async Task ReadView_HasNoTitleEdit_AndTheTitleFieldKeepsTheTitleSize()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = ctx.Render(builder =>
        {
            builder.OpenComponent<TaskDetailPanel>(0);
            builder.AddComponentParameter(1, "Subject", new TaskDetailSubject(task.Id));
            builder.AddComponentParameter(2, "TabId", "t1");
            builder.CloseComponent();
        });

        Assert.NotEmpty(cut.FindAll(".task-read-view"));
        Assert.Empty(cut.FindAll("button[aria-label='Edit title']"));
        string css = CssText.AppCss();
        string field = CssText.RuleBody(css, ".task-detail-title .mud-input-slot,\n.task-detail-title .mud-input > input.mud-input-root.mud-input-root-outlined");
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-size: var(--mud-typography-h5-size);", field, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("font-weight: var(--mud-typography-h5-weight);", field, StringComparison.Ordinal);
    }

    // ---- B4i, B4l, B4m, B4n, B4o, B4q, B4r: the drawer's form ----

    /// <summary>The drawer's groups are Group labels, including Description, and the title is the only heading in the Task's body (audit B4i, B4n).</summary>
    [Fact]
    public async Task Drawer_GroupsUseGroupLabels_IncludingDescription()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        Assert.Equal(["Planning", "Dates", "Description", "History (1)"], cut.FindAll(".task-detail h3.group-label").Select(h => h.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".task-detail .group-heading"));
        Assert.NotNull(cut.Find(".task-detail-description-box .task-detail-description-bar .task-detail-description-toggle"));
    }

    /// <summary>The Description field is a long-text field (line height 1.55, audit B4m) with its floating label kept in the DOM and visually hidden.</summary>
    [Fact]
    public async Task DescriptionField_IsALongTextField_WithItsLabelHidden()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        IElement field = cut.Find(".task-detail-description-field");
        Assert.Contains("long-text-field", field.ClassList);
        Assert.Equal("Description", field.QuerySelector("label")?.TextContent.Trim());
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("clip-path: inset(50%);", CssText.RuleBody(CssText.AppCss(), ".task-detail-description-field .mud-input-label"), StringComparison.Ordinal);
    }

    /// <summary>The History is always open: a heading with the entry count over the list, with no expansion panel to toggle.</summary>
    [Fact]
    public async Task History_IsAlwaysOpen_WithoutAnExpansionPanel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        Assert.Empty(cut.FindComponents<MudExpansionPanels>());
        Assert.StartsWith("History (", cut.Find(".task-detail-history-heading").TextContent.Trim(), StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".task-detail-history-row"));
    }

    /// <summary>An empty optional field shows a Quiet-ink hint instead of a blank box (audit B4o).</summary>
    [Fact]
    public async Task EmptyOptionalFields_ShowAPlaceholder()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        Assert.Equal("None", cut.FindComponents<MudAutocomplete<string>>().Single(m => HasClass(m.Instance.Class, "task-detail-project")).Instance.Placeholder);
        Assert.Equal("None", cut.FindComponents<MudAutocomplete<string>>().Single(m => HasClass(m.Instance.Class, "task-detail-sprint-field")).Instance.Placeholder);
        Assert.Equal("Add tag…", cut.FindComponents<MudTextField<string>>().Single(m => HasClass(m.Instance.Class, "task-detail-tags-add")).Instance.Placeholder);
        Assert.All(cut.FindComponents<MudDatePicker>(), picker => Assert.Equal("None", picker.Instance.Placeholder));
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("color: var(--mud-palette-text-secondary);", CssText.RuleBody(CssText.AppCss(), ".task-detail-prop-value input::placeholder,\n.task-detail-prop-value textarea::placeholder,\n.task-detail-description-field textarea::placeholder"), StringComparison.Ordinal);
    }

    /// <summary>A date picker's input reads "MMM d, yyyy" and never an ISO date (audit B4q).</summary>
    [Fact]
    public void TaskDates_PickerFormat_IsReadable()
    {
        Assert.Equal("MMM d, yyyy", TaskDates.PickerFormat);
        Assert.Equal("yyyy-MM-dd", TaskDates.Format);
    }

    /// <summary>
    /// One mark per row (#471): the Status row has no state dot before its select, every row has a text label and the Assignee row's is the word, not a person icon,
    /// and the Priority select keeps its caret (its icon is drawn inside the field, not as the select's own adornment) beside the priority's icon (#470).
    /// </summary>
    [Fact]
    public async Task StatusRow_HasNoStateDot_EveryRowHasATextLabel_AndPriorityKeepsItsCaretAndIcon()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha", status: TaskState.InProgress, priority: TaskPriority.High);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        Assert.Empty(cut.FindAll(".task-detail-strip .task-state-dot"));
        Assert.Equal(
            ["Assignee", "Status", "Priority", "Team", "Project", "Sprint"],
            cut.FindAll(".task-detail-strip-item > .task-detail-strip-label").Select(label => label.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".task-detail-strip-label .mud-icon-root"));
        IRenderedComponent<MudSelect<TaskPriority>> priority = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        Assert.Null(priority.Instance.AdornmentIcon);
        Assert.Equal(Adornment.End, priority.Instance.Adornment);
        MudIcon icon = cut.FindComponents<MudIcon>().Single(i => string.Equals(i.Instance.Class, "task-detail-priority-icon", StringComparison.Ordinal)).Instance;
        Assert.Equal(TaskColors.Icon(TaskPriority.High), icon.Icon);
    }

    /// <summary>The state dot is the Board's: Backlog is dashed and hollow, and the Board header draws it through the same component.</summary>
    [Fact]
    public void TaskStateDot_Backlog_IsHollow()
    {
        // contains-ok: a class list is space-separated; one token is pinned.
        Assert.Contains("task-state-dot-backlog", TaskStateDot.ClassFor(TaskState.Backlog), StringComparison.Ordinal);
        Assert.DoesNotContain("task-state-dot-backlog", TaskStateDot.ClassFor(TaskState.ToDo), StringComparison.Ordinal);
        // contains-ok: a class list is space-separated; one token is pinned.
        Assert.Contains("task-state-dot-tertiary", TaskStateDot.ClassFor(TaskState.Review), StringComparison.Ordinal);
    }

    // ---- E10, B10i: the drawer's menu and Close task… ----

    /// <summary>The drawer's menu has an icon on every item, Close task… last after a divider in Error ink; the activator names the Task and has no tooltip (audit B10f, B10i, E10).</summary>
    [Fact]
    public async Task DrawerMenu_EveryItemHasAnIcon_AndCloseTaskIsLastAfterADivider()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find($"button[aria-label='Actions for {task.Id}']").ClickAsync());

        IReadOnlyList<IElement> items = cut.FindAll(".mud-menu-item");
        Assert.Equal(["Copy id", "Copy link", "Make a copy…", "Close task…"], items.Select(i => i.TextContent.Trim()));
        Assert.All(items, item => Assert.NotNull(item.QuerySelector(".mud-icon-root")));
        Assert.Contains("menu-item-destructive", items[^1].ClassList);
        Assert.NotNull(items[^1].PreviousElementSibling?.ClassList.Contains("mud-divider") is true ? items[^1].PreviousElementSibling : null);
        Assert.Empty(cut.FindAll(".task-detail-title-row .mud-tooltip-root + button[aria-label^='Actions for']"));
    }

    /// <summary>Close task… asks first, naming the Task and its assignee; Cancel closes nothing, wakes nobody and shows no undo snackbar (audit E10).</summary>
    [Fact]
    public async Task CloseTask_AsksFirst_AndCancelLeavesTheTaskOpen()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, "Alpha", assignee: "Nova");
        int log = task.ChangeLog.Count;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find($"button[aria-label='Actions for {task.Id}']").ClickAsync());
        Task closing = cut.InvokeAsync(() => cut.FindAll(".mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), "Close task…", StringComparison.Ordinal)).ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        Assert.Equal($"Close {task.Id}?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("This ends the task. Nova is notified. You can reopen it from this menu.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(["Cancel", "Close task"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".mud-menu-item"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());
        await closing;

        TaskItem after = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.False(after.Location.Closed);
        Assert.Equal(log, after.ChangeLog.Count);
    }

    /// <summary>Reopen undoes a close, so it asks nothing and is an ordinary item.</summary>
    [Fact]
    public async Task Reopen_AsksNothing()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Alpha");
        _ = harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => cut.Find($"button[aria-label='Actions for {task.Id}']").ClickAsync());
        IReadOnlyList<IElement> items = cut.FindAll(".mud-menu-item");
        Assert.Equal(["Copy id", "Copy link", "Make a copy…", "Reopen"], items.Select(i => i.TextContent.Trim()));
        Assert.DoesNotContain("menu-item-destructive", items[^1].ClassList);
        await cut.InvokeAsync(() => items[^1].ClickAsync());

        Assert.False(harness.Store.Get(task.Id)?.Location.Closed);
        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
    }

    // ---- B10e, B10f, E9: the View menu and Fields ----

    /// <summary>The View menu's activator names the View with no tooltip, its items end in "…", Delete view… is last after a divider, and choosing Rename… closes the menu before the dialog opens (audit B10e, B10f).</summary>
    [Fact]
    public async Task ViewMenu_NamesItsView_AndClosesBeforeTheDialogOpens()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "custom", Name = "Sprint board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom");

        Assert.Empty(cut.FindAll(".tasks-header .mud-tooltip-root"));
        await cut.InvokeAsync(() => cut.Find(".tasks-header button[aria-label='Actions for Sprint board']").ClickAsync());
        IReadOnlyList<IElement> items = cut.FindAll(".mud-menu-item");
        Assert.Equal(["Rename…", "Fields…", "Delete view…"], items.Select(i => i.TextContent.Trim()));
        Assert.Contains("menu-item-destructive", items[^1].ClassList);

        Task renaming = cut.InvokeAsync(() => items[0].ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        Assert.Equal("Rename view", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Empty(cut.FindAll(".mud-menu-item"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].ClickAsync());
        await renaming;
    }

    /// <summary>Fields… opens "Fields in {View}", with display names, Accent switches that have accessible names, and the first row's up and last row's down disabled (audit E9, B10j).</summary>
    [Fact]
    public async Task FieldsDialog_HasDisplayNames_AccentSwitches_AndDisabledEnds()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "custom", Name = "Sprint board", Kind = ViewKind.List, Fields = ["status", "due_date", "blocked_by"] }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom");
        await cut.InvokeAsync(() => cut.Find(".tasks-header button[aria-label='Actions for Sprint board']").ClickAsync());

        Task opening = cut.InvokeAsync(() => cut.FindAll(".mud-menu-item").Single(i => string.Equals(i.TextContent.Trim(), "Fields…", StringComparison.Ordinal)).ClickAsync());
        await cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".view-editor-field-row")));
        await opening;

        Assert.Equal("Fields in Sprint board", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(["Status", "Due date", "Blocked by"], cut.FindAll(".view-editor-field-row .mud-switch").Select(s => s.TextContent.Trim()));
        Assert.DoesNotContain("due_date", cut.Find(".mud-dialog-content").TextContent, StringComparison.Ordinal);
        Assert.All(cut.FindComponents<MudSwitch<bool>>(), s => Assert.Equal(Color.Primary, s.Instance.Color));
        Assert.True(cut.Find("button[aria-label='Move Status up']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Move Status down']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[aria-label='Move Blocked by down']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Move Blocked by up']").HasAttribute("disabled"));
    }

    /// <summary>A field key reads as its display name: <c>due_date</c> is "Due date".</summary>
    /// <param name="key">A field key.</param>
    /// <param name="label">Its display name.</param>
    [Theory]
    [InlineData("due_date", "Due date")]
    [InlineData("blocked_by", "Blocked by")]
    [InlineData("status", "Status")]
    public void FieldLabel_IsADisplayName(string key, string label)
    {
        Assert.Equal(label, TaskFieldView.Label(key));
    }

    /// <summary>The sidebar's View rows name their View with no tooltip and ask through the same idiom.</summary>
    [Fact]
    public async Task SidebarViewRow_NamesItsView_WithNoTooltip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Assert.True(harness.Views.Save(new TaskView { Id = "custom", Name = "Sprint board", Kind = ViewKind.List }).Saved);
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskViewNav>(0);
            builder.CloseComponent();
        });
        await cut.InvokeAsync(() => cut.Find("button.mud-nav-group-header, .mud-nav-group button").ClickAsync());

        Assert.NotNull(cut.Find("button[aria-label='Actions for Sprint board']"));
        Assert.Empty(cut.FindAll(".hover-reveal-row .mud-tooltip-root"));
    }

    // ---- A6, A7: the toolbar row ----

    /// <summary>The toolbar's filter chips wear the Filter chip's classes, the search is the compact 28px field, and the row's controls are 28px (audit A6, A7).</summary>
    [Fact]
    public async Task Toolbar_UsesTheFilterChipAndTheCompactSearch()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "chips", Name = "Chips", Kind = ViewKind.List, Filter = new TaskFilter { Teams = ["Platform"] } }).Saved);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "chips");

        IElement set = cut.Find(".task-toolbar-team");
        Assert.Contains("filter-chip", set.ClassList);
        Assert.Contains("is-set", set.ClassList);
        Assert.Contains("filter-chip", cut.Find(".task-toolbar-group").ClassList);
        Assert.DoesNotContain("is-set", cut.Find(".task-toolbar-group").ClassList);
        Assert.Contains("toolbar-compact", cut.Find(".task-toolbar-search").ClassList);
        Assert.Contains("filter-chip", cut.Find(".task-toolbar-requests").ClassList);

        string css = CssText.AppCss();
        string chip = CssText.RuleBody(css, ".task-filter-chip");
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("height: 28px;", chip, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("background-color: var(--hu-selected-background);", CssText.RuleBody(css, ".task-filter-chip-set"), StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("height: 28px;", CssText.RuleBody(css, ".task-toolbar .mud-icon-button:not(.mud-switch-base),\n.task-toolbar-header .mud-icon-button:not(.mud-switch-base)"), StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("color: var(--hu-accent-ink);", CssText.RuleBody(css, ".task-filter-chip-set .task-filter-chip-button"), StringComparison.Ordinal);
    }

    /// <summary>The toolbar has no Dense switch: the List has one row height, so there is no mode to toggle.</summary>
    [Fact]
    public async Task Toolbar_HasNoDenseSwitch()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        Assert.DoesNotContain(cut.FindComponents<MudSwitch<bool>>(), s => HasClass(s.Instance.Class, "task-toolbar-dense"));
        Assert.Empty(cut.FindAll(".task-toolbar-dense"));
    }

    // ---- helpers ----

    /// <summary>Whether a component's <c>Class</c> contains the token <paramref name="name"/>.</summary>
    /// <param name="classes">The component's Class parameter.</param>
    /// <param name="name">The class token.</param>
    private static bool HasClass(string? classes, string name) =>
        classes is not null && classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal);

    /// <summary>The declarations of every rule whose selector mentions <paramref name="needle"/>.</summary>
    /// <param name="css">The stylesheet, with LF endings.</param>
    /// <param name="needle">A class name.</param>
    private static IEnumerable<string> RulesWithSelector(string css, string needle)
    {
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(css, @"(?:^|\n)([^{}\n/@][^{}]*)\{([^}]*)\}", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)))
        {
            if (match.Groups[1].Value.Contains(needle, StringComparison.Ordinal))
            {
                yield return match.Groups[2].Value;
            }
        }
    }

    /// <summary>A Board View with the default columns, optionally grouped.</summary>
    /// <param name="grouping">The lane grouping.</param>
    private static TaskView BoardView(IReadOnlyList<TaskGroupField>? grouping = null) => new()
    {
        Id = "board-r3",
        Name = "Board",
        Kind = ViewKind.Kanban,
        Columns = BoardLayout.DefaultColumns,
        Grouping = grouping ?? [],
        Fields = [],
    };

    /// <summary>Renders the Board with the harness's services and a popover provider.</summary>
    private static IRenderedComponent<TaskBoard> RenderBoard(MudBunitContext ctx, TaskToolHarness harness, TaskView view, IReadOnlyList<TaskItem> tasks)
    {
        harness.AddTo(ctx.Services);
        _ = ctx.Render<MudPopoverProvider>();
        return ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, view);
            p.Add(b => b.Tasks, tasks);
        });
    }

    /// <summary>Renders the Board inside the popover provider, so a menu opened on it can be found.</summary>
    private static IRenderedComponent<ContainerFragment> RenderBoardWithPopovers(MudBunitContext ctx, TaskToolHarness harness, TaskView view, IReadOnlyList<TaskItem> tasks)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskBoard>(0);
            builder.AddAttribute(1, nameof(TaskBoard.View), view);
            builder.AddAttribute(2, nameof(TaskBoard.Tasks), tasks);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders the Tasks page, optionally routed to <paramref name="viewId"/>.</summary>
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

    /// <summary>Renders the page with a <c>HeadOutlet</c> beside it, so the page's <c>PageTitle</c> can be read.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPageWithTitle(MudBunitContext ctx, string? viewId, string? taskIdText)
    {
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Microsoft.AspNetCore.Components.Web.HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<TasksPage>(1);
            if (viewId is not null)
            {
                builder.AddAttribute(2, nameof(TasksPage.ViewId), viewId);
            }

            if (taskIdText is not null)
            {
                builder.AddAttribute(3, nameof(TasksPage.TaskIdText), taskIdText);
            }

            builder.CloseComponent();
        });
    }

    /// <summary>The text of the <c>title</c> element the page's <c>PageTitle</c> rendered into the head outlet.</summary>
    private static string DocumentTitleOf(IRenderedComponent<ContainerFragment> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("title")));
        return cut.Find("title").TextContent.Trim();
    }

    /// <summary>Creates a Task through the real service.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title, TaskState status = TaskState.ToDo, TaskPriority priority = TaskPriority.Medium, string? assignee = null)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, "Platform", null, Status: status, Priority: priority, Assignee: assignee), TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Clicks the List row whose Title cell contains <paramref name="title"/>.</summary>
    private static Task ClickTaskRowAsync(IRenderedComponent<ContainerFragment> cut, string title) =>
        cut.InvokeAsync(() => cut.FindAll("td").First(td => td.TextContent.Contains(title, StringComparison.Ordinal)).Closest("tr")?.ClickAsync() ?? throw new InvalidOperationException("The cell has no row."));

}
