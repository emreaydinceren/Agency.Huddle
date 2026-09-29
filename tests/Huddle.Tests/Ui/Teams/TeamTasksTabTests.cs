using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.9's Tasks tab and corrections-D7b items 20-25: <c>TeamTasksTab</c> is the Board over the
/// Tasks of a Team (swimlanes by Project) or of one Project (no swimlanes), filtered through
/// <c>TaskQuery</c> by an in-memory View, requeried live on the Task events, with a toolbar whose
/// <c>New task</c> opens <c>TaskDetailDialog</c> on a draft and a card that opens the dialog on its id.
/// Runs over the real Tasks stack of <see cref="TaskToolHarness"/>: Business (Projects Marketing and
/// Taxes, and no Project) and Household (its own Marketing Project) hold Tasks, and Quiet holds none.
/// </summary>
public sealed class TeamTasksTabTests
{
    /// <summary>The toolbar's action button ("New task").</summary>
    private const string ActionButton = "button.team-tab-toolbar-action";

    /// <summary>The toolbar's search input.</summary>
    private const string SearchInput = ".team-tab-toolbar-search input";

    /// <summary>The Board's column header labels.</summary>
    private const string ColumnLabel = ".task-board-column-label";

    /// <summary>The six default Board columns, restated (Spec §12.3).</summary>
    private static readonly string[] DefaultColumnLabels = ["Backlog", "To Do", "In Progress", "Review", "Done", "Won't do"];

    /// <summary>The Team page's swimlane grouping, restated.</summary>
    private static readonly TaskGroupField[] ProjectGrouping = [TaskGroupField.Project];

    /// <summary>The ungrouped lane's key.</summary>
    private const string NoLane = "";

    /// <summary>The Team page lists exactly the Team's Tasks, in the default order, and none of Household's.</summary>
    [Fact]
    public async Task TeamPage_ShowsExactlyTheTeamsTasks()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business");

        TaskId[] expected = [tasks.Marketing.Id, tasks.Taxes.Id, tasks.NoProject.Id];
        Assert.Equal(expected, IdsOf(rig));
    }

    /// <summary>The Team page's in-memory View is a Board, keyed <c>team:Business</c>, named for the Team, filtered to the Team only, grouped by Project, on the default columns.</summary>
    [Fact]
    public async Task TeamPage_BuildsTheBoardView()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business");

        TaskView view = rig.Board.View;
        Assert.Equal(ViewKind.Board, view.Kind);
        Assert.Equal("team:Business", view.Id);
        Assert.Equal("Business", view.Name);
        Assert.Equal(ViewScope.Active, view.Scope);
        string[] teams = ["Business"];
        Assert.Equal(teams, view.Filter.Teams);
        Assert.Empty(view.Filter.Projects);
        Assert.Equal(ProjectGrouping, view.Grouping);
        Assert.Equal(DefaultColumnLabels, view.Columns.Select(c => c.Label).ToArray());
    }

    /// <summary>The Team match ignores case: a page for <c>business</c> shows the same three Tasks.</summary>
    [Fact]
    public async Task TeamPage_TeamMatchIgnoresCase()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "business");

        TaskId[] expected = [tasks.Marketing.Id, tasks.Taxes.Id, tasks.NoProject.Id];
        Assert.Equal(expected, IdsOf(rig));
    }

    /// <summary>The Project page lists only that Project's Tasks: Household's Project also named Marketing is not included.</summary>
    [Fact]
    public async Task ProjectPage_ShowsOnlyTheProjectsTasks_NotAnotherTeamsSameNamedProject()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business", "Marketing");

        TaskId[] expected = [tasks.Marketing.Id];
        Assert.Equal(expected, IdsOf(rig));
    }

    /// <summary>The Project page's View names both the Team and the Project in its filter, is keyed <c>team:Business/Marketing</c>, and has no swimlanes.</summary>
    [Fact]
    public async Task ProjectPage_BuildsTheBoardView_WithTeamAndProjectFilter()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business", "Marketing");

        TaskView view = rig.Board.View;
        Assert.Equal(ViewKind.Board, view.Kind);
        Assert.Equal("team:Business/Marketing", view.Id);
        Assert.Equal("Marketing", view.Name);
        string[] teams = ["Business"];
        Assert.Equal(teams, view.Filter.Teams);
        ProjectRef[] projects = [new ProjectRef("Business", "Marketing")];
        Assert.Equal(projects, view.Filter.Projects);
        Assert.Empty(view.Grouping);
        Assert.Equal(DefaultColumnLabels, view.Columns.Select(c => c.Label).ToArray());
    }

    /// <summary>Search narrows the Board through <c>TaskQuery</c> over the title and the id (case-insensitively), still inside the Team, and reaches the Board for highlighting.</summary>
    /// <param name="search">The search text.</param>
    [Theory]
    [InlineData("launch")]
    [InlineData("busi-0001")]
    public async Task Search_NarrowsByTitleAndId_InsideTheTeam(string search)
    {
        ArgumentNullException.ThrowIfNull(search);
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business", search: search);

        TaskId[] expected = [tasks.Marketing.Id];
        Assert.Equal(expected, IdsOf(rig));
        Assert.Equal(search, rig.Board.Search);
    }

    /// <summary>Search on the Project page narrows within the Project.</summary>
    [Fact]
    public async Task Search_OnTheProjectPage_NarrowsWithinTheProject()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business", "Taxes", "return");

        TaskId[] expected = [tasks.Taxes.Id];
        Assert.Equal(expected, IdsOf(rig));
    }

    /// <summary>The toolbar's action reads <c>New task</c> and its search field's placeholder <c>Search tasks</c>.</summary>
    [Fact]
    public async Task Toolbar_ShowsNewTaskAndSearchTasks()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Business");

        Assert.Equal("New task", rig.Tab.Find(ActionButton).TextContent.Trim());
        Assert.Equal("Search tasks", rig.Tab.Find(SearchInput).GetAttribute("placeholder"));
    }

    /// <summary>Typing in the toolbar raises <c>SearchChanged</c> with the text; the tab owns no search state of its own.</summary>
    [Fact]
    public async Task Toolbar_Typing_RaisesSearchChanged()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();
        List<string?> searched = [];
        Rig rig = Render(ctx, harness, "Business", searched: searched);

        IRenderedComponent<MudTextField<string>> field = rig.Tab.FindComponent<TeamTabToolbar>().FindComponent<MudTextField<string>>();
        await rig.Tab.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("launch"));

        Assert.Equal<string?>(["launch"], searched);
    }

    /// <summary>A Team with no Tasks still shows the six default columns, and no cards.</summary>
    [Fact]
    public async Task TeamWithNoTasks_ShowsTheSixDefaultColumns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();

        Rig rig = Render(ctx, harness, "Quiet");

        Assert.Empty(rig.Board.Tasks);
        Assert.Equal(DefaultColumnLabels, rig.Tab.FindAll(ColumnLabel).Select(e => e.TextContent.Trim()).ToArray());
    }

    /// <summary>New task on the Team page opens the create dialog on a blank draft for the Team, with no Project.</summary>
    [Fact]
    public async Task NewTask_OnTheTeamPage_OpensTheDialogWithTheTeamDraft()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, "Business");

        await rig.Tab.InvokeAsync(() => rig.Tab.Find(ActionButton).Click());

        IRenderedComponent<TaskDetailDialog> dialog = rig.WaitForDialog<TaskDetailDialog>();
        Assert.Null(dialog.Instance.Id);
        Assert.Equal(new TaskDraft(string.Empty, "Business", null), dialog.Instance.Draft);
    }

    /// <summary>New task on the Project page opens the create dialog on a draft for the Team and the Project.</summary>
    [Fact]
    public async Task NewTask_OnTheProjectPage_OpensTheDialogWithTheProjectDraft()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Seed(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, "Business", "Marketing");

        await rig.Tab.InvokeAsync(() => rig.Tab.Find(ActionButton).Click());

        IRenderedComponent<TaskDetailDialog> dialog = rig.WaitForDialog<TaskDetailDialog>();
        Assert.Null(dialog.Instance.Id);
        Assert.Equal(new TaskDraft(string.Empty, "Business", "Marketing"), dialog.Instance.Draft);
    }

    /// <summary>Opening a card opens <c>TaskDetailDialog</c> on that Task's id, with no draft.</summary>
    [Fact]
    public async Task OpeningACard_OpensTheDialogOnTheTasksId()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, "Business");

        await rig.Tab.InvokeAsync(() => rig.Tab.Find($"button.task-card-open[aria-label='Open {tasks.Taxes.Id}']").Click());

        IRenderedComponent<TaskDetailDialog> dialog = rig.WaitForDialog<TaskDetailDialog>();
        Assert.Equal(tasks.Taxes.Id, dialog.Instance.Id);
        Assert.Null(dialog.Instance.Draft);
    }

    /// <summary>A card's Copy id copies exactly the id and shows the success toast.</summary>
    [Fact]
    public async Task CopyId_CopiesTheIdAndToastsSuccess()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", tasks.Taxes.Id.ToString()).SetResult(true);
        Rig rig = Render(ctx, harness, "Business");
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await rig.ChooseCopyIdAsync(tasks.Taxes);

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal(tasks.Taxes.Id.ToString(), Assert.Single(invocation.Arguments));
        rig.Tab.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = snackbar.ShownSnackbars.Single();
        Assert.Equal($"Copied {tasks.Taxes.Id}", shown.Message);
        Assert.Equal(Severity.Success, shown.Severity);
    }

    /// <summary>A failed copy shows the Ctrl+C warning instead.</summary>
    [Fact]
    public async Task CopyId_WhenTheClipboardRefuses_ToastsTheCtrlCWarning()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", tasks.Taxes.Id.ToString()).SetResult(false);
        Rig rig = Render(ctx, harness, "Business");
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await rig.ChooseCopyIdAsync(tasks.Taxes);

        rig.Tab.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = snackbar.ShownSnackbars.Single();
        Assert.Equal("Couldn't copy. The id is selected; press Ctrl+C.", shown.Message);
        Assert.Equal(Severity.Warning, shown.Severity);
    }

    /// <summary>A Task created through the service after the tab rendered appears at once (<c>TaskChanged</c>); another Team's new Task does not.</summary>
    [Fact]
    public async Task TaskCreatedAfterRender_AppearsOnTheBoard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, "Business");

        _ = CreateTask(harness, "Mow lawn", "Household", null, TaskState.Backlog);
        TaskItem created = CreateTask(harness, "Book venue", "Business", null, TaskState.Backlog);

        TaskId[] expected = [tasks.Marketing.Id, tasks.Taxes.Id, tasks.NoProject.Id, created.Id];
        rig.Tab.WaitForAssertion(() => Assert.Equal(expected, IdsOf(rig)));
    }

    /// <summary>A rebuilt index (<c>TasksReloaded</c>, no <c>TaskChanged</c>) is requeried: a Task that appeared on disk shows up.</summary>
    [Fact]
    public async Task TasksReloaded_RequeriesTheBoard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, "Business");
        TaskItem onDisk = harness.SeedOnDisk(TestTasks.Make("BUSI-0090", "Renew domain", location: new("Business", null, false)));

        TaskId[] expected = [tasks.Marketing.Id, tasks.Taxes.Id, tasks.NoProject.Id, onDisk.Id];
        rig.Tab.WaitForAssertion(() => Assert.Equal(expected, IdsOf(rig)));
    }

    /// <summary>A drop on the Project page's Board (and the card's Move to menu) saves the new status through <c>TaskService</c> as the Human, in one change.</summary>
    /// <param name="viaMenu">Whether to use the card's Move to menu instead of a drop.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Move_OnTheBoard_SavesTheStatusInOneChange(bool viaMenu)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seeded tasks = Seed(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;
        await using MudBunitContext ctx = new();
        Rig rig = Render(ctx, harness, "Business", "Marketing");

        await rig.MoveAsync(tasks.Marketing, TaskState.InProgress, viaMenu);

        TaskItem? saved = harness.Store.Get(tasks.Marketing.Id);
        Assert.NotNull(saved);
        Assert.Equal(TaskState.InProgress, saved.Status);
        TaskChange change = Assert.Single(changes);
        Assert.Equal(TaskActorKind.Human, change.Actor.Kind);
        Assert.Equal("You", change.Actor.Name);
    }

    /// <summary>The Tasks read from the Board's own <c>Tasks</c> parameter, as ids.</summary>
    /// <param name="rig">The rendered tab.</param>
    /// <returns>The ids, in Board order.</returns>
    private static TaskId[] IdsOf(Rig rig) => rig.Board.Tasks.Select(t => t.Id).ToArray();

    /// <summary>Seeds Persona Ada (who makes Business, Household and Quiet known Teams) and the five Tasks.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The Tasks.</returns>
    private static Seeded Seed(TaskToolHarness harness)
    {
        harness.Personas.Add(new PersonaIdentity("Ada", "Ada", "Ada", ["Business", "Household", "Quiet"]), "You are Ada.");
        return new Seeded(
            CreateTask(harness, "Launch plan", "Business", "Marketing", TaskState.ToDo),
            CreateTask(harness, "File return", "Business", "Taxes", TaskState.Backlog),
            CreateTask(harness, "Renew licence", "Business", null, TaskState.Backlog),
            CreateTask(harness, "Launch party", "Household", "Marketing", TaskState.Backlog),
            CreateTask(harness, "Fix fence", "Household", null, TaskState.Backlog));
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> as the Human and returns it as saved.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="title">The title.</param>
    /// <param name="team">The Team.</param>
    /// <param name="project">The Project, or <see langword="null"/>.</param>
    /// <param name="status">The initial status (not Duplicate).</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem CreateTask(TaskToolHarness harness, string title, string team, string? project, TaskState status)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, team, project, status), Human(harness));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>The Human actor.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The actor.</returns>
    private static TaskActor Human(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Renders the providers and the tab.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies every service.</param>
    /// <param name="team">The Team.</param>
    /// <param name="project">The Project, or <see langword="null"/>.</param>
    /// <param name="search">The search text, or <see langword="null"/>.</param>
    /// <param name="searched">Collects what <c>SearchChanged</c> raises, when given.</param>
    /// <returns>The rig.</returns>
    private static Rig Render(MudBunitContext ctx, TaskToolHarness harness, string team, string? project = null, string? search = null, List<string?>? searched = null)
    {
        harness.AddTo(ctx.Services);
        IRenderedComponent<MudPopoverProvider> popovers = ctx.Render<MudPopoverProvider>();
        IRenderedComponent<MudDialogProvider> dialogs = ctx.Render<MudDialogProvider>();
        IRenderedComponent<TeamTasksTab> tab = ctx.Render<TeamTasksTab>(p =>
        {
            p.Add(t => t.Team, team);
            p.Add(t => t.Project, project);
            p.Add(t => t.Search, search);
            if (searched is not null)
            {
                p.Add(t => t.SearchChanged, EventCallback.Factory.Create<string?>(searched, searched.Add));
            }
        });
        return new Rig(tab, popovers, dialogs);
    }

    /// <summary>The five seeded Tasks: Business/Marketing, Business/Taxes, Business (no Project), Household/Marketing, Household (no Project).</summary>
    /// <param name="Marketing">Business, Project Marketing, To Do.</param>
    /// <param name="Taxes">Business, Project Taxes.</param>
    /// <param name="NoProject">Business, no Project.</param>
    /// <param name="HouseholdMarketing">Household, Project Marketing.</param>
    /// <param name="HouseholdNone">Household, no Project.</param>
    private sealed record Seeded(TaskItem Marketing, TaskItem Taxes, TaskItem NoProject, TaskItem HouseholdMarketing, TaskItem HouseholdNone);

    /// <summary>The rendered tab, its popover provider (the card menus) and its dialog provider, with the ways to drive a card.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="popovers">Where the card menus render.</param>
    /// <param name="dialogs">Where the dialogs render.</param>
    private sealed class Rig(IRenderedComponent<TeamTasksTab> tab, IRenderedComponent<MudPopoverProvider> popovers, IRenderedComponent<MudDialogProvider> dialogs)
    {
        /// <summary>The tab.</summary>
        public IRenderedComponent<TeamTasksTab> Tab { get; } = tab;

        /// <summary>The Board the tab hosts.</summary>
        public TaskBoard Board => this.Tab.FindComponent<TaskBoard>().Instance;

        /// <summary>The Board's drop container.</summary>
        private MudDropContainer<TaskItem> Container => this.Tab.FindComponent<MudDropContainer<TaskItem>>().Instance;

        /// <summary>Moves <paramref name="item"/> to <paramref name="state"/> by a drop or by the card's Move to menu.</summary>
        /// <param name="item">The card's Task.</param>
        /// <param name="state">The target state (not a Won't do state).</param>
        /// <param name="viaMenu">Whether to use the card's Move to menu.</param>
        /// <returns>A task that completes when the move is done.</returns>
        public async Task MoveAsync(TaskItem item, TaskState state, bool viaMenu)
        {
            if (viaMenu)
            {
                await this.Tab.InvokeAsync(() => this.Tab.Find($"button[aria-label='Move {item.Id}']").Click());
                await this.ClickMenuItemAsync(state.ToWire());
                return;
            }

            MudDropContainer<TaskItem> container = this.Container;
            string origin = BoardLayout.ZoneId(NoLane, item.Status);
            await this.Tab.InvokeAsync(() => container.StartTransaction(item, origin, 0, static () => Task.CompletedTask, static () => Task.CompletedTask));
            await this.Tab.InvokeAsync(() => container.CommitTransaction(BoardLayout.ZoneId(NoLane, state), false));
        }

        /// <summary>Opens the card's ⋮ menu and chooses Copy id.</summary>
        /// <param name="item">The card's Task.</param>
        /// <returns>A task that completes when the item was clicked.</returns>
        public async Task ChooseCopyIdAsync(TaskItem item)
        {
            await this.Tab.InvokeAsync(() => this.Tab.Find($"button[aria-label='Move {item.Id}']").Click());
            await this.ClickMenuItemAsync("Copy id");
        }

        /// <summary>Waits until a dialog of type <typeparamref name="TDialog"/> is open, and returns it.</summary>
        /// <typeparam name="TDialog">The dialog component.</typeparam>
        /// <returns>The open dialog.</returns>
        public IRenderedComponent<TDialog> WaitForDialog<TDialog>()
            where TDialog : IComponent
        {
            dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindComponents<TDialog>()));
            return dialogs.FindComponent<TDialog>();
        }

        private Task ClickMenuItemAsync(string text)
        {
            IElement menuItem = popovers.FindAll("div.mud-menu-item")
                .First(e => string.Equals(e.TextContent.Trim(), text, StringComparison.Ordinal));
            return menuItem.ClickAsync(new MouseEventArgs());
        }
    }
}
