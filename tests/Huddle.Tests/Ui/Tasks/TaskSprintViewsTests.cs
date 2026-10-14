namespace Agency.Huddle.Tests.Ui.Tasks;

using System.Reflection;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins Sprints spec §12.2 to §12.4 (SP-T12): the List column and the card field, grouping and Board lanes by
/// Sprint, the View editor's and the toolbar's Sprint filter, and the Tasks page's <c>sprints.md</c> alert and
/// re-query on <see cref="ISprintCatalog.SprintsChanged"/>.
/// </summary>
public sealed class TaskSprintViewsTests
{
    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    /// <summary>The List shows a Sprint column with the Sprint's name when the View's Fields name <c>sprint</c>, and hides it otherwise (spec §12.2).</summary>
    [Fact]
    public async Task List_SprintColumn_ShowsTheNameOnlyWhenTheViewHasTheField()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem inSprint = TestTasks.Make(id: "PLAT-0001", title: "A", sprint: new SprintId(2));
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> shown = RenderList(ctx, harness, ListView(fields: ["sprint"]), [inSprint]);
        Assert.Equal(["ID", "Title", "Sprint"], shown.FindAll("span.sortable-column-header").Select(h => h.TextContent.Trim()));
        Assert.Contains("Auth hardening", shown.Markup, StringComparison.Ordinal);

        await using MudBunitContext other = new();
        IRenderedComponent<ContainerFragment> hidden = RenderList(other, harness, ListView(fields: ["status"]), [inSprint]);
        Assert.DoesNotContain("Sprint", hidden.FindAll("span.sortable-column-header").Select(h => h.TextContent.Trim()));
    }

    /// <summary>A Task whose Sprint is not in the file shows <c>S9 (missing)</c> and a warning icon in the List (spec §12.2).</summary>
    [Fact]
    public async Task List_SprintColumn_MissingSprint_ShowsIdAndWarningIcon()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem orphan = TestTasks.Make(id: "PLAT-0001", title: "A", sprint: new SprintId(9));
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderList(ctx, harness, ListView(fields: ["sprint"]), [orphan]);

        Assert.Equal("S9 (missing)", cut.Find(".task-list-sprint").TextContent.Trim());
        Assert.Equal("Not in sprints.md", cut.Find(".task-list-sprint .task-sprint-missing").GetAttribute("aria-label"));
    }

    /// <summary>Grouping by Sprint orders the groups newest first with <c>No sprint</c> last, labelled by name (spec §11.4, D-S13).</summary>
    [Fact]
    public async Task List_GroupBySprint_OrdersNewestFirstWithNoSprintLast()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem none = TestTasks.Make(id: "PLAT-0001", title: "A");
        TaskItem old = TestTasks.Make(id: "PLAT-0002", title: "B", sprint: new SprintId(1));
        TaskItem current = TestTasks.Make(id: "PLAT-0003", title: "C", sprint: new SprintId(2));
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderList(ctx, harness, ListView(grouping: [TaskGroupField.Sprint]), [old, current, none]);

        string markup = cut.Markup;
        int newest = markup.IndexOf("Auth hardening (1)", StringComparison.Ordinal);
        int oldest = markup.IndexOf("Sprint 1 (1)", StringComparison.Ordinal);
        int noSprint = markup.IndexOf("No sprint (1)", StringComparison.Ordinal);
        Assert.True(newest >= 0 && oldest > newest && noSprint > oldest, $"group order was {newest}, {oldest}, {noSprint}");
    }

    /// <summary>A group by a Sprint that is not in the file reads <c>S9 (missing)</c> (spec §11.4).</summary>
    [Fact]
    public async Task List_GroupBySprint_MissingSprint_IsLabelledMissing()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem orphan = TestTasks.Make(id: "PLAT-0001", title: "A", sprint: new SprintId(9));
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderList(ctx, harness, ListView(grouping: [TaskGroupField.Sprint]), [orphan]);

        Assert.Contains("S9 (missing) (1)", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A card shows the Sprint's name as a field when the View names <c>sprint</c>, and <c>S9 (missing)</c> with a warning icon for an unresolved one (spec §12.2).</summary>
    [Fact]
    public async Task Card_SprintField_ShowsTheNameOrMissing()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> named = RenderCard(ctx, harness, TestTasks.Make(sprint: new SprintId(1)));
        Assert.Equal("Sprint 1", named.Find(".task-card-field-sprint").TextContent.Trim());
        Assert.Empty(named.FindAll(".task-sprint-missing"));

        await using MudBunitContext other = new();
        IRenderedComponent<ContainerFragment> missing = RenderCard(other, harness, TestTasks.Make(sprint: new SprintId(9)));
        Assert.Equal("S9 (missing)", missing.Find(".task-card-field-sprint").TextContent.Trim());
        Assert.Equal("Not in sprints.md", missing.Find(".task-card-field-sprint .task-sprint-missing").GetAttribute("aria-label"));

        await using MudBunitContext none = new();
        Assert.Empty(RenderCard(none, harness, TestTasks.Make()).FindAll(".task-card-field-sprint"));
    }

    /// <summary>A Board grouped by Sprint has one lane per Sprint, newest first, labelled by name, with <c>No sprint</c> last (spec §11.4).</summary>
    [Fact]
    public async Task Board_GroupBySprint_LanesAreNewestFirstByName()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem none = TestTasks.Make(id: "PLAT-0001", title: "A");
        TaskItem old = TestTasks.Make(id: "PLAT-0002", title: "B", sprint: new SprintId(1));
        TaskItem current = TestTasks.Make(id: "PLAT-0003", title: "C", sprint: new SprintId(2));
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        _ = ctx.Render<MudPopoverProvider>();

        IRenderedComponent<TaskBoard> cut = ctx.Render<TaskBoard>(p =>
        {
            p.Add(b => b.View, new TaskView { Id = "b", Name = "B", Kind = ViewKind.Board, Columns = BoardLayout.DefaultColumns, Grouping = [TaskGroupField.Sprint] });
            p.Add(b => b.Tasks, new List<TaskItem> { none, old, current });
        });

        Assert.Equal(["Auth hardening", "Sprint 1", "No sprint"], cut.FindAll(".task-board-lane-label").Select(l => l.TextContent.Trim()));
    }

    /// <summary>The toolbar's Filter menu has a multi-select Sprint row that raises <c>EffectiveViewChanged</c> without saving (spec §12.3).</summary>
    [Fact]
    public async Task Toolbar_SprintFilter_RaisesEffectiveViewChanged()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskView saved = await SaveViewAsync(harness, new TaskFilter());
        TaskView? raised = null;
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        IRenderedComponent<ContainerFragment> root = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskToolbar>(0);
            builder.AddAttribute(1, nameof(TaskToolbar.EffectiveView), saved);
            builder.AddAttribute(2, nameof(TaskToolbar.EffectiveViewChanged), EventCallback.Factory.Create<TaskView>(this, v => raised = v));
            builder.CloseComponent();
        });
        await root.InvokeAsync(() => root.Find(".task-toolbar-filter button").ClickAsync());
        IRenderedComponent<MudSelect<string>> select = root.FindComponents<MudSelect<string>>().Single(c => string.Equals(c.Instance.Class, "task-toolbar-filter-sprint", StringComparison.Ordinal));

        await root.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync(["@none"]));

        Assert.Equal(["@none"], (raised ?? throw new InvalidOperationException("EffectiveViewChanged was not raised.")).Filter.Sprints);
        Assert.Empty(Assert.IsType<TaskView>(harness.Views.Get("v1")).Filter.Sprints);
    }

    /// <summary>A hand-edited <c>sprints.md</c> with a bad line shows the page's alert with a <c>role</c> and the problem text (spec §12.4).</summary>
    [Fact]
    public async Task Page_SprintsProblems_ShowAStatusAlert()
    {
        using TaskToolHarness harness = new(sprintsText: "# Sprints\n- S1 | Sprint 1\n- S1 | Again\n");
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, null);

        Assert.Equal("status", cut.Find(".tasks-sprints-alert").GetAttribute("role"));
        Assert.Contains("sprints.md has lines that were ignored", cut.Find(".tasks-sprints-alert").TextContent, StringComparison.Ordinal);
        Assert.Contains("duplicate id S1", cut.Find(".tasks-sprints-alert").TextContent, StringComparison.Ordinal);
    }

    /// <summary>With no problems there is no <c>sprints.md</c> alert.</summary>
    [Fact]
    public async Task Page_NoSprintsProblems_ShowsNoAlert()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, null);

        Assert.Empty(cut.FindAll(".tasks-sprints-alert"));
    }

    /// <summary>A View filtered on <c>@current</c> gets the catalogue's current Sprint, and re-queries when <c>sprints.md</c> changes (spec §11.2, E-S17).</summary>
    [Fact]
    public async Task Page_CurrentSprintFilter_FollowsTheFileOnSprintsChanged()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        await SaveViewAsync(harness, new TaskFilter { Sprints = ["@current"] });
        TaskActor actor = TaskActors.Human(harness.Options.Value);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Old work", "Platform", null, Sprint: new SprintId(1)), actor));
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("New work", "Platform", null, Sprint: new SprintId(2)), actor));
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "v1");
        Assert.Equal(["New work"], cut.FindAll(".task-list-title").Select(t => t.TextContent.Trim()));

        SprintStore store = Assert.IsType<SprintStore>(harness.Sprints);
        _ = Assert.IsType<SprintCreateResult.Created>(store.Create("Sprint 3"));

        cut.WaitForAssertion(() => Assert.Contains("No tasks match this View.", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll(".task-list-title"));
    }

    /// <summary>Disposing the page, its List and its cards leaves no handler on <see cref="ISprintCatalog.SprintsChanged"/> (spec §12.4, traps.md).</summary>
    [Fact]
    public async Task Page_Dispose_UnsubscribesFromSprintsChanged()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Work", "Platform", null, Sprint: new SprintId(2)), TaskActors.Human(harness.Options.Value)));
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        _ = RenderPage(ctx, null);
        Assert.True(HandlerCount(harness.Sprints) > 0);

        await ctx.DisposeAsync();

        Assert.Equal(0, HandlerCount(harness.Sprints));
    }

    /// <summary>Saves a List View <c>v1</c> with <paramref name="filter"/> into the harness's store and waits for the store's watcher echo (see <see cref="ViewStoreEcho"/>).</summary>
    private static async Task<TaskView> SaveViewAsync(TaskToolHarness harness, TaskFilter filter)
    {
        TaskView view = new() { Id = "v1", Name = "Sprint view", Kind = ViewKind.List, Fields = ["status"], Filter = filter };
        Assert.True((await ViewStoreEcho.SaveAndSettleAsync(harness.Views, view, Xunit.TestContext.Current.CancellationToken)).Saved);
        return harness.Views.Get("v1") ?? throw new InvalidOperationException("The View was just saved.");
    }

    /// <summary>A List View with the given fields and grouping.</summary>
    private static TaskView ListView(IReadOnlyList<string>? fields = null, IReadOnlyList<TaskGroupField>? grouping = null) => new()
    {
        Id = "list",
        Name = "List",
        Kind = ViewKind.List,
        Fields = fields ?? [],
        Grouping = grouping ?? [],
    };

    /// <summary>Renders <see cref="TaskListView"/> over the harness's services.</summary>
    private static IRenderedComponent<ContainerFragment> RenderList(MudBunitContext ctx, TaskToolHarness harness, TaskView view, IReadOnlyList<TaskItem> tasks)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskListView>(0);
            builder.AddAttribute(1, nameof(TaskListView.View), view);
            builder.AddAttribute(2, nameof(TaskListView.Tasks), tasks);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders <see cref="TaskCard"/> with the Sprint field over the harness's services.</summary>
    private static IRenderedComponent<ContainerFragment> RenderCard(MudBunitContext ctx, TaskToolHarness harness, TaskItem task)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskCard>(0);
            builder.AddAttribute(1, nameof(TaskCard.Task), task);
            builder.AddAttribute(2, nameof(TaskCard.Fields), (IReadOnlyList<string>)["sprint"]);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders the Tasks page on View <paramref name="viewId"/> (All Tasks when <see langword="null"/>).</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string? viewId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            if (viewId is not null)
            {
                builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            }

            builder.CloseComponent();
        });

    /// <summary>Counts <paramref name="catalog"/>'s subscribers to <see cref="ISprintCatalog.SprintsChanged"/> through the event's backing field.</summary>
    private static int HandlerCount(ISprintCatalog catalog)
    {
        FieldInfo field = catalog.GetType().GetField(nameof(ISprintCatalog.SprintsChanged), BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("No backing field for SprintsChanged.");
        return (field.GetValue(catalog) as Delegate)?.GetInvocationList().Length ?? 0;
    }
}
