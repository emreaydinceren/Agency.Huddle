namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;

/// <summary>
/// Renders <see cref="TaskListView"/>, the <c>MudDataGrid&lt;TaskItem&gt;</c> that backs the List
/// (Spec §13.3a): column visibility follows <see cref="TaskView.Fields"/>, grouping follows
/// <see cref="TaskView.Grouping"/> and uses <see cref="TaskQuery.GroupLabel"/> for its labels, a
/// header click reorders the grid without touching the <see cref="TaskView"/>, an overdue
/// non-terminal row is flagged, a row click raises <see cref="TaskListView.OnOpenTask"/>, and a
/// search term is highlighted with <c>MudHighlighter</c>.
/// </summary>
public sealed class TaskListViewTests : IDisposable
{
    private readonly TempDataDir dataDir = new();
    private readonly Agency.Huddle.App.Acp.PersonaStore personas;
    private readonly TaskStore store;

    /// <summary>Builds an empty <see cref="TaskStore"/>: <see cref="TaskListView"/> reads its Team folders for the orphan-Team chip (corrections-B7 12.4 item 3).</summary>
    public TaskListViewTests()
    {
        this.personas = TestTaskStore.CreatePersonaStore(this.dataDir);
        this.store = TestTaskStore.CreateTaskStore(this.dataDir, this.personas);
    }

    /// <summary>Releases the stores and the temporary data directory.</summary>
    public void Dispose()
    {
        this.store.Dispose();
        this.personas.Dispose();
        this.dataDir.Dispose();
    }

    /// <summary>ID and Title are always shown; every other field column is hidden unless named in <see cref="TaskView.Fields"/>.</summary>
    [Fact]
    public async Task Columns_HiddenWhenNotInFields_ExceptIdAndTitle()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Only status shown");
        TaskView view = MakeView(fields: ["status"]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [task]);

        List<string> headers = [.. cut.FindAll("span.sortable-column-header").Select(header => header.TextContent.Trim())];
        Assert.Equal(["ID", "Title", "Status"], headers);
    }

    /// <summary>Two-level grouping (Team, then Assignee) nests in that order, labels come from <see cref="TaskQuery.GroupLabel"/>, and the null Assignee group ("Unassigned") sorts last within its Team.</summary>
    [Fact]
    public async Task Grouping_TwoLevels_FollowsViewGroupingWithGroupLabelText()
    {
        TaskItem alphaAmy = TestTasks.Make(id: "PLAT-0001", title: "A", assignee: "Amy", location: new("Alpha", null, false));
        TaskItem alphaUnassigned = TestTasks.Make(id: "PLAT-0002", title: "B", assignee: null, location: new("Alpha", null, false));
        TaskItem bravoZoe = TestTasks.Make(id: "PLAT-0003", title: "C", assignee: "Zoe", location: new("Bravo", null, false));
        TaskView view = MakeView(grouping: [TaskGroupField.Team, TaskGroupField.Assignee]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [alphaAmy, alphaUnassigned, bravoZoe]);

        int alphaIndex = cut.Markup.IndexOf("Alpha", StringComparison.Ordinal);
        int bravoIndex = cut.Markup.IndexOf("Bravo", StringComparison.Ordinal);
        int amyIndex = cut.Markup.IndexOf("Amy", StringComparison.Ordinal);
        int unassignedIndex = cut.Markup.IndexOf("Unassigned", StringComparison.Ordinal);

        Assert.True(alphaIndex >= 0 && bravoIndex >= 0 && amyIndex >= 0 && unassignedIndex >= 0);
        Assert.True(alphaIndex < bravoIndex, "Team group 'Alpha' should render before 'Bravo'.");
        Assert.True(amyIndex < unassignedIndex, "A real Assignee name should render before the null-Assignee group 'Unassigned'.");
    }

    /// <summary>Clicking a column header re-sorts the grid's own rows without writing anything back to the <see cref="TaskView"/> passed in.</summary>
    [Fact]
    public async Task HeaderClick_ReordersRows_WithoutChangingTheView()
    {
        TaskItem taskZ = TestTasks.Make(id: "PLAT-0001", title: "Zulu");
        TaskItem taskA = TestTasks.Make(id: "PLAT-0002", title: "Alfa");
        TaskView view = MakeView();
        IReadOnlyList<SortKey> originalSort = view.Sort;

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [taskZ, taskA]);

        int zuluBefore = cut.Markup.IndexOf("Zulu", StringComparison.Ordinal);
        int alfaBefore = cut.Markup.IndexOf("Alfa", StringComparison.Ordinal);
        Assert.True(zuluBefore < alfaBefore, "Rows should render in the order given before any header click.");

        IElement titleHeader = cut.FindAll("span.sortable-column-header")
            .First(header => header.TextContent.Contains("Title", StringComparison.Ordinal));
        await cut.InvokeAsync(() => titleHeader.ClickAsync());

        int zuluAfter = cut.Markup.IndexOf("Zulu", StringComparison.Ordinal);
        int alfaAfter = cut.Markup.IndexOf("Alfa", StringComparison.Ordinal);
        Assert.True(alfaAfter < zuluAfter, "A click on the Title header should sort ascending by title.");
        Assert.Same(originalSort, view.Sort);
    }

    /// <summary>An overdue, non-terminal row is flagged with the <c>task-overdue</c> class and a warning icon; a terminal row with the same overdue date is not.</summary>
    [Fact]
    public async Task OverdueNonTerminalRow_HasOverdueClassAndWarningIcon()
    {
        DateOnly longPastDue = new(2000, 1, 1);
        TaskItem overdue = TestTasks.Make(id: "PLAT-0001", title: "Still open", status: TaskState.ToDo, dueDate: longPastDue);
        TaskItem doneOnTime = TestTasks.Make(id: "PLAT-0002", title: "Finished", status: TaskState.Done, dueDate: longPastDue);
        TaskView view = MakeView(fields: ["due_date"]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [overdue, doneOnTime]);

        IElement overdueRow = cut.FindAll("tr").First(row => row.TextContent.Contains("Still open", StringComparison.Ordinal));
        Assert.Contains("task-overdue", (overdueRow.ClassName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        Assert.NotNull(overdueRow.QuerySelector("[aria-label='Overdue']"));

        IElement doneRow = cut.FindAll("tr").First(row => row.TextContent.Contains("Finished", StringComparison.Ordinal));
        Assert.DoesNotContain("task-overdue", doneRow.ClassName, StringComparison.Ordinal);
    }

    /// <summary>Clicking a row raises <see cref="TaskListView.OnOpenTask"/> with that row's id.</summary>
    [Fact]
    public async Task RowClick_RaisesOnOpenTask()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0007", title: "Click me");
        TaskView view = MakeView();
        List<TaskId> opened = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [task], onOpenTask: id => opened.Add(id));

        IElement cell = cut.FindAll("td").First(td => td.TextContent.Contains("Click me", StringComparison.Ordinal));
        IElement row = cell.Closest("tr") ?? throw new InvalidOperationException("The clicked cell has no row.");
        await cut.InvokeAsync(() => row.ClickAsync());

        Assert.Single(opened);
        Assert.Equal(task.Id, opened[0]);
    }

    /// <summary>A search term is wrapped in <c>&lt;mark&gt;</c> by <c>MudHighlighter</c> in the Title cell.</summary>
    [Fact]
    public async Task SearchTerm_IsWrappedByMudHighlighterInTheTitleCell()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Widget Alpha Release");
        TaskView view = MakeView();

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [task], search: "Alpha");

        Assert.Equal("Alpha", cut.Find(".task-list-title mark").TextContent.Trim());
    }

    /// <summary>Column order follows <see cref="TaskView.Fields"/>, with ID and Title always first regardless of Fields.</summary>
    [Fact]
    public async Task Columns_FollowViewFieldsOrder()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T");
        TaskView view = MakeView(fields: ["due_date", "status", "assignee"]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [task]);

        List<string> headers = cut.FindAll("span.sortable-column-header")
            .Select(header => header.TextContent.Trim())
            .ToList();

        Assert.Equal(["ID", "Title", "Due date", "Status", "Assignee"], headers);
    }

    /// <summary>A field the View groups on still groups the grid even when it isn't in <see cref="TaskView.Fields"/> - its column exists, just hidden.</summary>
    [Fact]
    public async Task Grouping_OnAFieldNotInFields_StillGroups()
    {
        TaskItem alpha = TestTasks.Make(id: "PLAT-0001", title: "A", location: new("Alpha", null, false));
        TaskItem bravo = TestTasks.Make(id: "PLAT-0002", title: "B", location: new("Bravo", null, false));
        TaskView view = MakeView(grouping: [TaskGroupField.Team]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, view, [alpha, bravo]);

        List<string> headers = cut.FindAll("span.sortable-column-header")
            .Select(header => header.TextContent.Trim())
            .ToList();

        List<string> groupLabels = [.. cut.FindAll("tr")
            .Select(row => row.TextContent.Trim())
            .Where(text => text is "Alpha (1)" or "Bravo (1)")];
        Assert.Equal(["Alpha (1)", "Bravo (1)"], groupLabels);
        Assert.DoesNotContain("Team", headers);
    }

    /// <summary>Builds a minimal List <see cref="TaskView"/> for these tests, with a fixed id and name.</summary>
    private static TaskView MakeView(
        IReadOnlyList<string>? fields = null,
        IReadOnlyList<TaskGroupField>? grouping = null) => new()
    {
        Id = "v1",
        Name = "Test View",
        Kind = ViewKind.List,
        Fields = fields ?? [],
        Grouping = grouping ?? [],
    };

    /// <summary>Renders <see cref="TaskListView"/> with the popover provider present, registering a fixed <see cref="TimeProvider"/> so overdue checks are deterministic.</summary>
    private IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx,
        TaskView view,
        IReadOnlyList<TaskItem> tasks,
        string? search = null,
        Action<TaskId>? onOpenTask = null)
    {
        ManualTimeProvider clock = new() { UtcNow = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero) };
        ctx.Services.AddSingleton<TimeProvider>(clock);
        ctx.Services.AddSingleton(this.store);

        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskListView>(0);
            builder.AddAttribute(1, nameof(TaskListView.View), view);
            builder.AddAttribute(2, nameof(TaskListView.Tasks), tasks);
            builder.AddAttribute(3, nameof(TaskListView.Search), search);
            if (onOpenTask is not null)
            {
                builder.AddAttribute(4, nameof(TaskListView.OnOpenTask), EventCallback.Factory.Create<TaskId>(onOpenTask, onOpenTask));
            }

            builder.CloseComponent();
        });
    }
}
