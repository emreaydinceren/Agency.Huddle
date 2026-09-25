namespace Agency.Huddle.Tests.Ui.Tasks;

using System.Text.RegularExpressions;
using AngleSharp.Dom;
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
/// Plan 12.4.t - the Board's column header menu (Spec §13.4 <i>Columns</i>, corrections-B5 D12-13) -
/// plus every corrections-B7 §12.4 item: the §13.12 empty states on the Tasks page, the default
/// columns when the toolbar switches to Board, the orphan-Team chip on Board lanes and List groups,
/// every View field on a card, the presence tooltip, the two CSS defects, <i>Edit columns…</i> opening
/// the View editor at its Columns section, and the Board rendered in the page with card-open bound to
/// the detail drawer.
/// <para>
/// Provisional (flagged): the header menu's activator <c>aria-label="{Label} column menu"</c>; the
/// inline rename field <c>.task-board-column-rename</c>; the save-failure Snackbar
/// <c>Could not save the View: {problems}</c> (space-joined); the card field spans
/// <c>.task-card-field-{key with '_' as '-'}</c> with the formats pinned in
/// <see cref="Card_ShowsEveryViewField"/>, and no span for a field with no value; the orphan chip
/// <c>.task-orphan-team-chip</c> as a sibling of the lane label; the empty state
/// <c>.tasks-empty</c> / <c>.tasks-empty-message</c> / <c>.tasks-empty-action</c>.
/// </para>
/// </summary>
public sealed partial class TaskBoardColumnMenuTests
{
    /// <summary>A saved Board View's id.</summary>
    private const string BoardId = "platform-board";

    /// <summary>The Spec §12.3 default columns, restated (plan R4).</summary>
    private static readonly IReadOnlyList<BoardColumn> DefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Review", [TaskState.Review]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>The default column labels, in order.</summary>
    private static readonly string[] DefaultLabels = ["Backlog", "To Do", "In Progress", "Review", "Done", "Won't do"];

    /// <summary>Spec §13.4 <i>Columns</i>: each column header's ⋮ menu offers exactly <i>Rename</i>, <i>Hide</i> and <i>Edit columns…</i>.</summary>
    [Fact]
    public async Task HeaderMenu_OffersRenameHideAndEditColumns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, []);
        OpenHeaderMenu(cut, "In Progress");

        string[] items = [.. cut.FindAll("div.mud-menu-item").Select(i => i.TextContent.Trim())];
        string[] expected = ["Rename", "Hide", "Edit columns…"];
        Assert.Equal(expected, items);
    }

    /// <summary>Plan 12.4.t and D12-13: <i>Rename</i> is an inline field; committing it saves the new label to the saved View through <see cref="ViewStore.Save"/> at once.</summary>
    [Fact]
    public async Task Rename_SavedBoardView_SavesTheLabelImmediately()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, []);
        await RenameAsync(cut, "In Progress", "Doing");

        string[] expected = ["Backlog", "To Do", "Doing", "Review", "Done", "Won't do"];
        Assert.Equal(expected, SavedLabels(harness));
    }

    /// <summary>Plan 12.4.t and D12-13: <i>Hide</i> saves the column as hidden to the saved View at once, touching no other column.</summary>
    [Fact]
    public async Task Hide_SavedBoardView_SavesHiddenImmediately()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, []);
        ClickHeaderItem(cut, "In Progress", "Hide");

        bool[] expected = [false, false, true, false, false, false];
        Assert.Equal(expected, SavedHidden(harness));
    }

    /// <summary>The header menu maps a visible column to its place among all columns: with Backlog already hidden, hiding Review (third visible, fourth overall) hides Review and nothing else.</summary>
    [Fact]
    public async Task Hide_WithAnotherColumnHidden_HidesTheRightColumn()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = SaveBoard(harness, [.. DefaultColumns.Select(c => c with { Hidden = string.Equals(c.Label, "Backlog", StringComparison.Ordinal) })]);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, []);
        ClickHeaderItem(cut, "Review", "Hide");

        bool[] expected = [true, false, false, true, false, false];
        Assert.Equal(expected, SavedHidden(harness));
    }

    /// <summary>corrections-B5 D12-13: the header menu saves to the <b>saved</b> View - an unsaved toolbar override on the effective View (here a grouping) is not written.</summary>
    [Fact]
    public async Task Hide_WritesTheSavedViewNotTheEffectiveOne()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = SaveBoard(harness);
        TaskView effective = saved with { Grouping = [TaskGroupField.Assignee] };

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, effective, []);
        ClickHeaderItem(cut, "Done", "Hide");

        TaskView? after = harness.Views.Get(BoardId);
        Assert.NotNull(after);
        Assert.Empty(after.Grouping);
        bool[] expected = [false, false, false, false, true, false];
        Assert.Equal(expected, SavedHidden(harness));
    }

    /// <summary>corrections-B5 D12-13: a save the View refuses (an empty label) shows a Snackbar error naming the problem, and the saved View is unchanged.</summary>
    [Fact]
    public async Task Rename_Refused_ShowsSnackbarError()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, []);
        await RenameAsync(cut, "In Progress", "");

        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal("Could not save the View: Column label '' must be 1-30 characters.", shown.Message);
        Assert.Equal(Severity.Error, shown.Severity);
        Assert.Equal(DefaultLabels, SavedLabels(harness));
    }

    /// <summary>
    /// corrections-B5 D12-13 (DM decision D): a List View shown as a Board changes only the effective
    /// View - <i>Hide</i> raises <c>ViewChanged</c> with that column hidden, and the saved List View is
    /// untouched.
    /// </summary>
    [Fact]
    public async Task ListViewShownAsBoard_Hide_ChangesTheEffectiveViewOnly()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView effective = AllTasksAsBoard(harness);
        List<TaskView> changed = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, effective, [], viewChanged: changed.Add);
        ClickHeaderItem(cut, "Done", "Hide");

        TaskView raised = Assert.Single(changed);
        bool[] expected = [false, false, false, false, true, false];
        Assert.Equal(expected, raised.Columns.Select(c => c.Hidden));
        TaskView? saved = harness.Views.Get(ViewStore.AllTasksId);
        Assert.NotNull(saved);
        Assert.Equal(ViewKind.List, saved.Kind);
        Assert.Empty(saved.Columns);
    }

    /// <summary>DM decision D, the Rename entry point: renaming on a List View shown as a Board changes only the effective View.</summary>
    [Fact]
    public async Task ListViewShownAsBoard_Rename_ChangesTheEffectiveViewOnly()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView effective = AllTasksAsBoard(harness);
        List<TaskView> changed = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, effective, [], viewChanged: changed.Add);
        await RenameAsync(cut, "In Progress", "Doing");

        TaskView raised = Assert.Single(changed);
        string[] expected = ["Backlog", "To Do", "Doing", "Review", "Done", "Won't do"];
        Assert.Equal(expected, raised.Columns.Select(c => c.Label));
        Assert.Empty(harness.Views.Get(ViewStore.AllTasksId)?.Columns ?? []);
    }

    /// <summary>Plan 12.4.t: <i>Edit columns…</i> raises <c>OnEditColumns</c> once.</summary>
    [Fact]
    public async Task EditColumns_RaisesOnEditColumns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView view = SaveBoard(harness);
        int raised = 0;

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, [], onEditColumns: () => raised++);
        ClickHeaderItem(cut, "Review", "Edit columns…");

        Assert.Equal(1, raised);
    }

    /// <summary>corrections-B7 12.4 item 3: grouped by Team, the lane of an orphan Team folder carries the chip "No teammate is in Legal" (Spec §13.12); a Team some Persona is in has none.</summary>
    [Fact]
    public async Task BoardLane_OrphanTeam_ShowsNoTeammateChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        IReadOnlyList<TaskItem> tasks = SeedLegalAndPlatform(harness);
        TaskView view = SaveBoard(harness) with { Grouping = [TaskGroupField.Team] };

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderBoard(ctx, harness, view, tasks);

        IElement legal = Lane(cut, "Legal");
        IElement platform = Lane(cut, "Platform");
        Assert.Equal("No teammate is in Legal", Assert.Single(legal.QuerySelectorAll(".task-orphan-team-chip")).TextContent.Trim());
        Assert.Empty(platform.QuerySelectorAll(".task-orphan-team-chip"));
    }

    /// <summary>corrections-B7 12.4 item 3: the List's Team group header carries the same orphan chip, once, for Legal only.</summary>
    [Fact]
    public async Task ListGroupHeader_OrphanTeam_ShowsNoTeammateChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        IReadOnlyList<TaskItem> tasks = SeedLegalAndPlatform(harness);
        TaskView view = new() { Id = "list", Name = "List", Kind = ViewKind.List, Grouping = [TaskGroupField.Team] };

        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskListView>(0);
            builder.AddAttribute(1, nameof(TaskListView.View), view);
            builder.AddAttribute(2, nameof(TaskListView.Tasks), tasks);
            builder.CloseComponent();
        });

        string[] chips = [.. cut.FindAll(".task-orphan-team-chip").Select(c => c.TextContent.Trim())];
        string[] expected = ["No teammate is in Legal"];
        Assert.Equal(expected, chips);
    }

    /// <summary>corrections-B7 12.4 item 4: a card shows every field the View names, each in its own span, with the pinned (provisional) format.</summary>
    /// <param name="key">The View field key.</param>
    /// <param name="expected">The span's exact text.</param>
    [Theory]
    [InlineData("status", "In Progress")]
    [InlineData("priority", "High")]
    [InlineData("assignee", "Nova")]
    [InlineData("creator", "You")]
    [InlineData("team", "Platform")]
    [InlineData("project", "Auth")]
    [InlineData("parent", "PLAT-0009")]
    [InlineData("blocked_by", "PLAT-0007, PLAT-0008")]
    [InlineData("tags", "auth, sso")]
    [InlineData("start_date", "2030-01-02")]
    [InlineData("due_date", "2030-01-03")]
    [InlineData("created", "2026-09-20")]
    [InlineData("updated", "2026-09-24")]
    [InlineData("closed", "2026-09-25")]
    [InlineData("origin", "room-1")]
    public async Task Card_ShowsEveryViewField(string key, string expected)
    {
        ArgumentNullException.ThrowIfNull(key);
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = FullTask();

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, harness, task, [key], presence: null);

        string cssClass = "task-card-field-" + key.Replace('_', '-');
        Assert.Equal(expected, cut.Find($".{cssClass}").TextContent.Trim());
    }

    /// <summary>corrections-B7 12.4 item 4: a field the View names but the Task has no value for (no parent, no tags) shows nothing, and fields the View doesn't name show nothing either.</summary>
    [Fact]
    public async Task Card_FieldWithNoValue_OrNotNamed_ShowsNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", creator: "You");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, harness, task, ["parent", "tags", "creator"], presence: null);

        string[] shown = [.. cut.FindAll("[class*='task-card-field-']").Select(e => e.ClassName ?? "")];
        string[] expected = ["task-card-field-creator"];
        Assert.Equal(expected, shown);
    }

    /// <summary>corrections-B7 12.4 item 5 and Spec §13.4 <i>Cards</i>: the presence badge sits inside a <c>MudTooltip</c> saying the presence in words, and keeps its <c>BadgeAriaLabel</c>.</summary>
    [Fact]
    public async Task PresenceBadge_HasTooltipInWords()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", assignee: "Nova");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, harness, task, [], presence: PresenceState.Awake);

        IRenderedComponent<MudTooltip> tooltip = Assert.Single(cut.FindComponents<MudTooltip>(), t => string.Equals(t.Instance.Text, "Nova is Awake", StringComparison.Ordinal));
        Assert.Equal("Nova is Awake", tooltip.FindComponent<MudBadge>().Instance.BadgeAriaLabel);
    }

    /// <summary>corrections-B7 12.4 item 2: switching the toolbar to Board on a View with no Columns applies the default columns.</summary>
    [Fact]
    public async Task Toolbar_SwitchToBoard_NoColumns_AppliesDefaultColumns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView list = harness.Views.Get(ViewStore.AllTasksId) ?? throw new InvalidOperationException("All Tasks is missing.");
        List<TaskView> changed = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskToolbar> toolbar = RenderToolbar(ctx, harness, list, changed.Add);
        IRenderedComponent<MudToggleGroup<ViewKind>> kind = toolbar.FindComponent<MudToggleGroup<ViewKind>>();
        await toolbar.InvokeAsync(() => kind.Instance.ValueChanged.InvokeAsync(ViewKind.Board));

        TaskView raised = Assert.Single(changed);
        Assert.Equal(ViewKind.Board, raised.Kind);
        Assert.Equal(DefaultLabels, raised.Columns.Select(c => c.Label));
        Assert.Equal(DefaultColumns.Select(c => string.Join(",", c.States)), raised.Columns.Select(c => string.Join(",", c.States)));
    }

    /// <summary>corrections-B7 12.4 item 2, the permissive side: a View that already has Columns keeps them when switched to Board.</summary>
    [Fact]
    public async Task Toolbar_SwitchToBoard_ExistingColumns_Kept()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        IReadOnlyList<BoardColumn> custom = [.. DefaultColumns.Select(c => string.Equals(c.Label, "Review", StringComparison.Ordinal) ? c with { Label = "QA" } : c)];
        TaskView list = new() { Id = "list", Name = "List", Kind = ViewKind.List, Columns = custom };
        List<TaskView> changed = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<TaskToolbar> toolbar = RenderToolbar(ctx, harness, list, changed.Add);
        IRenderedComponent<MudToggleGroup<ViewKind>> kind = toolbar.FindComponent<MudToggleGroup<ViewKind>>();
        await toolbar.InvokeAsync(() => kind.Instance.ValueChanged.InvokeAsync(ViewKind.Board));

        string[] expected = ["Backlog", "To Do", "In Progress", "QA", "Done", "Won't do"];
        Assert.Equal(expected, Assert.Single(changed).Columns.Select(c => c.Label));
    }

    /// <summary>corrections-B7 12.4 item 6: <c>app.css</c> styles the ghost bucket by its real class <c>.task-ghost-bucket</c>; no bare <c>.ghost-bucket</c> selector is left.</summary>
    [Fact]
    public void AppCss_GhostBucketSelector_MatchesTheMarkup()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.Matches(TaskGhostBucketSelector(), text);
        Assert.DoesNotMatch(BareGhostBucketSelector(), text);
    }

    /// <summary>corrections-B7 12.4 item 6: the card's focus ring is on the focusable open button (<c>.task-card-open:focus-visible</c>), not on the card's <c>div</c>.</summary>
    [Fact]
    public void AppCss_FocusRing_IsOnTheOpenButton()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.Matches(OpenButtonFocusSelector(), text);
        Assert.DoesNotMatch(CardDivFocusSelector(), text);
    }

    /// <summary>corrections-B7 12.4 item 1 and Spec §13.12: with no Tasks at all the page shows "No tasks yet." with a <b>+ New task</b> button, once.</summary>
    [Fact]
    public async Task Page_NoTasksAtAll_ShowsNoTasksYet()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: null);

        IElement empty = Assert.Single(cut.FindAll(".tasks-empty"));
        Assert.Equal("No tasks yet.", empty.QuerySelector(".tasks-empty-message")?.TextContent.Trim());
        Assert.Equal("+ New task", empty.QuerySelector(".tasks-empty-action")?.TextContent.Trim());
    }

    /// <summary>corrections-B7 12.4 item 1: Tasks exist but the effective filter matches none - "No tasks match this View." with <b>Reset filters</b>.</summary>
    [Fact]
    public async Task Page_FilterMatchesNothing_ShowsNoTasksMatchThisView()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = CreateTask(harness, "Ship it");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: null);
        await OverrideAsync(cut, view => view with { Filter = view.Filter with { States = [TaskState.Done] } });

        IElement empty = Assert.Single(cut.FindAll(".tasks-empty"));
        Assert.Equal("No tasks match this View.", empty.QuerySelector(".tasks-empty-message")?.TextContent.Trim());
        Assert.Equal("Reset filters", empty.QuerySelector(".tasks-empty-action")?.TextContent.Trim());
    }

    /// <summary>corrections-B7 12.4 item 1 (DM, J43): <b>Reset filters</b> clears the toolbar's overrides and the search, so the Task shows again and the message goes.</summary>
    [Fact]
    public async Task Page_ResetFilters_ClearsOverridesAndSearch()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = CreateTask(harness, "Ship it");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: null);
        await OverrideAsync(cut, view => view with { Filter = view.Filter with { States = [TaskState.Done] } });
        IRenderedComponent<TaskToolbar> toolbar = cut.FindComponent<TaskToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SearchChanged.InvokeAsync("Ship"));
        await cut.InvokeAsync(() => cut.Find(".tasks-empty-action").Click());

        Assert.Empty(cut.FindAll(".tasks-empty"));
        Assert.Null(cut.FindComponent<TaskToolbar>().Instance.Search);
        Assert.Empty(cut.FindComponent<TaskToolbar>().Instance.EffectiveView.Filter.States);
        var idCells = cut.FindComponent<TaskListView>().FindAll("td").Where(td => string.Equals(td.TextContent.Trim(), task.Id.ToString(), StringComparison.Ordinal));
        Assert.Single(idCells);
    }

    /// <summary>corrections-B7 12.4 item 1: a search matching nothing shows "No tasks match '{text}'." with no action button.</summary>
    [Fact]
    public async Task Page_SearchMatchesNothing_ShowsNoTasksMatchText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = CreateTask(harness, "Ship it");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: null);
        IRenderedComponent<TaskToolbar> toolbar = cut.FindComponent<TaskToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SearchChanged.InvokeAsync("zzz"));

        IElement empty = Assert.Single(cut.FindAll(".tasks-empty"));
        Assert.Equal("No tasks match 'zzz'.", empty.QuerySelector(".tasks-empty-message")?.TextContent.Trim());
        Assert.Null(empty.QuerySelector(".tasks-empty-action"));
    }

    /// <summary>corrections-B7 12.4 item 1: the empty state is one rendering above both views - on a Board View it shows once, above the Board.</summary>
    [Fact]
    public async Task Page_EmptyStateOnBoard_ShownOnceAboveTheBoard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: BoardId);

        IElement empty = Assert.Single(cut.FindAll(".tasks-empty"));
        Assert.Equal("No tasks yet.", empty.QuerySelector(".tasks-empty-message")?.TextContent.Trim());
        Assert.Single(cut.FindComponents<TaskBoard>());
    }

    /// <summary>corrections-B7 12.4 item 8 and 14.5.i item 4: a Board View renders <see cref="TaskBoard"/> in the page, and opening a card puts a real <see cref="TaskDetail"/> (Panel mode) in the detail drawer for that Task - the 14.5.i replacement for the placeholder this test used to pin.</summary>
    [Fact]
    public async Task Page_BoardView_CardOpenOpensTheDetailDrawer()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = SaveBoard(harness);
        TaskItem task = CreateTask(harness, "Ship it");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: BoardId);
        Assert.Empty(cut.FindComponents<TaskListView>());
        await cut.InvokeAsync(() => cut.Find($"button[aria-label='Open {task.Id}']").Click());

        TaskDetail detail = cut.FindComponent<TaskDetail>().Instance;
        Assert.Equal(task.Id, detail.Id);
        Assert.Equal(TaskDetailMode.Panel, detail.Mode);
    }

    /// <summary>corrections-B7 12.4 item 7: the Board's <i>Edit columns…</i> opens the View editor drawer for this View at its Columns section.</summary>
    [Fact]
    public async Task Page_EditColumns_OpensTheViewEditorAtColumns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: BoardId);
        ClickHeaderItem(cut, "Review", "Edit columns…");

        IRenderedComponent<ViewEditorDrawer> drawer = cut.FindComponent<ViewEditorDrawer>();
        Assert.True(drawer.Instance.Open);
        Assert.Equal(BoardId, drawer.Instance.Id);
        Assert.Equal(ViewEditorSection.Columns, drawer.Instance.Section);
    }

    /// <summary>Plan 12.4.t: hiding a column from the header updates the Board's hidden-count chip.</summary>
    [Fact]
    public async Task Page_Hide_UpdatesTheHiddenCountChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = SaveBoard(harness);
        _ = CreateTask(harness, "Finished", TaskState.Done);
        _ = CreateTask(harness, "Also finished", TaskState.Done);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: BoardId);
        Assert.Empty(cut.FindAll(".task-board-hidden-count"));
        ClickHeaderItem(cut, "Done", "Hide");

        cut.WaitForAssertion(() => Assert.Equal("2 tasks in hidden columns", cut.Find(".task-board-hidden-count").TextContent.Trim()));
    }

    /// <summary>DM decision D on the page: hiding a column on a List View shown as a Board changes the effective View, so the toolbar offers <b>Save to View</b>.</summary>
    [Fact]
    public async Task Page_ListShownAsBoard_Hide_OffersSaveToView()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = CreateTask(harness, "Ship it");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: null);
        IRenderedComponent<TaskToolbar> toolbar = cut.FindComponent<TaskToolbar>();
        IRenderedComponent<MudToggleGroup<ViewKind>> kind = toolbar.FindComponent<MudToggleGroup<ViewKind>>();
        await cut.InvokeAsync(() => kind.Instance.ValueChanged.InvokeAsync(ViewKind.Board));
        Assert.Equal(DefaultLabels, cut.FindAll("div.task-board-column-header .task-board-column-label").Select(e => e.TextContent.Trim()));
        ClickHeaderItem(cut, "Done", "Hide");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".task-toolbar-save")));
        Assert.Equal(ViewKind.List, harness.Views.Get(ViewStore.AllTasksId)?.Kind);
    }

    /// <summary>12.4.i: a Board grouped by a field but holding no Tasks has no lanes (there is no value to key one by) and still lays out every visible column, rather than failing.</summary>
    [Fact]
    public void BoardLayout_GroupedWithNoTasks_HasNoLanes()
    {
        TaskView view = new() { Id = BoardId, Name = "Board", Kind = ViewKind.Board, Columns = DefaultColumns, Grouping = [TaskGroupField.Assignee] };

        BoardModel model = BoardLayout.Build([], view);

        Assert.Empty(model.Lanes);
        Assert.Equal(DefaultLabels, model.VisibleColumns.Select(c => c.Label));
    }

    /// <summary>12.4.i: the page passes its search text through - the toolbar shows it and the List highlights it (a literal <c>Search="this.search"</c> would pass the words "this.search").</summary>
    [Fact]
    public async Task Page_Search_ReachesTheToolbarAndTheList()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = CreateTask(harness, "Ship it");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: null);
        IRenderedComponent<TaskToolbar> toolbar = cut.FindComponent<TaskToolbar>();
        await cut.InvokeAsync(() => toolbar.Instance.SearchChanged.InvokeAsync("Ship"));

        Assert.Equal("Ship", cut.FindComponent<TaskToolbar>().Instance.Search);
        Assert.Equal("Ship", Assert.Single(cut.FindComponent<TaskListView>().FindAll("mark")).TextContent);
    }

    /// <summary>12.4.i: after an inline rename on a saved Board View, the header shows the new label and the rename field is gone.</summary>
    [Fact]
    public async Task Page_Rename_ShowsTheNewLabel()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: BoardId);
        await RenameAsync(cut, "In Progress", "Doing");

        string[] expected = ["Backlog", "To Do", "Doing", "Review", "Done", "Won't do"];
        cut.WaitForAssertion(() => Assert.Equal(expected, cut.FindAll("div.task-board-column-header .task-board-column-label").Select(e => e.TextContent.Trim())));
        Assert.Empty(cut.FindAll(".task-board-column-rename"));
    }

    /// <summary>12.4.i: Edit View opens the editor at its General section even after <i>Edit columns…</i> had opened it at Columns.</summary>
    [Fact]
    public async Task Page_EditViewAfterEditColumns_OpensAtGeneral()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = SaveBoard(harness);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, harness, viewId: BoardId);
        ClickHeaderItem(cut, "Review", "Edit columns…");
        Assert.Equal(ViewEditorSection.Columns, cut.FindComponent<ViewEditorDrawer>().Instance.Section);
        await cut.InvokeAsync(() => cut.Find(".task-toolbar-edit-view").Click());

        Assert.Equal(ViewEditorSection.General, cut.FindComponent<ViewEditorDrawer>().Instance.Section);
    }

    /// <summary>Saves a Board View <see cref="BoardId"/> with the given columns (the defaults when null).</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="columns">The columns.</param>
    /// <returns>The View as saved.</returns>
    private static TaskView SaveBoard(TaskToolHarness harness, IReadOnlyList<BoardColumn>? columns = null)
    {
        TaskView view = new() { Id = BoardId, Name = "Platform board", Kind = ViewKind.Board, Columns = columns ?? DefaultColumns };
        ViewSaveResult result = harness.Views.Save(view);
        Assert.True(result.Saved, string.Join(" ", result.Problems));
        return harness.Views.Get(BoardId) ?? throw new InvalidOperationException("The Board View was not saved.");
    }

    /// <summary>The built-in All Tasks View shown as a Board with the default columns - an effective View only.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The effective View.</returns>
    private static TaskView AllTasksAsBoard(TaskToolHarness harness) =>
        (harness.Views.Get(ViewStore.AllTasksId) ?? throw new InvalidOperationException("All Tasks is missing."))
        with { Kind = ViewKind.Board, Columns = DefaultColumns };

    /// <summary>The saved Board View's column labels.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The labels.</returns>
    private static string[] SavedLabels(TaskToolHarness harness) =>
        [.. (harness.Views.Get(BoardId)?.Columns ?? []).Select(c => c.Label)];

    /// <summary>The saved Board View's column hidden flags.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The flags.</returns>
    private static bool[] SavedHidden(TaskToolHarness harness) =>
        [.. (harness.Views.Get(BoardId)?.Columns ?? []).Select(c => c.Hidden)];

    /// <summary>Creates a Task in Platform as the Human.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="title">The title.</param>
    /// <param name="status">The status.</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem CreateTask(TaskToolHarness harness, string title, TaskState status = TaskState.ToDo) =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft(title, "Platform", null, status, Assignee: "Nova"),
            TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>A Task in Platform, and a Task written straight into a <c>Legal</c> folder no Persona is in (an orphan Team, Spec §8.2).</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>Both Tasks, as the store holds them.</returns>
    private static IReadOnlyList<TaskItem> SeedLegalAndPlatform(TaskToolHarness harness)
    {
        TaskItem platform = CreateTask(harness, "Platform work");
        TaskLocation legalLocation = new("Legal", null, false);
        TaskItem legal = TestTasks.Make(
            id: "LEGA-0001",
            title: "Legal work",
            location: legalLocation,
            changeLog: [TestTasks.Entry("2026-09-20T10:00:00Z", "You", "created")]);
        _ = TestTaskStore.WriteTask(harness.TasksDirPath, Path.Combine("Legal", "LEGA-0001.md"), legal);
        harness.Store.RebuildFromWatcher();
        Assert.True(harness.Store.Teams.Single(t => string.Equals(t.Name, "Legal", StringComparison.Ordinal)).IsOrphan);
        return [.. harness.Store.All.Where(t => t.Id == platform.Id || string.Equals(t.Location.Team, "Legal", StringComparison.Ordinal))];
    }

    /// <summary>A Task with a value in every View field.</summary>
    /// <returns>The Task.</returns>
    private static TaskItem FullTask()
    {
        _ = TaskId.TryParse("PLAT-0009", out TaskId parent);
        _ = TaskId.TryParse("PLAT-0007", out TaskId blocker1);
        _ = TaskId.TryParse("PLAT-0008", out TaskId blocker2);
        return TestTasks.Make(
            id: "PLAT-0001",
            status: TaskState.InProgress,
            priority: TaskPriority.High,
            creator: "You",
            assignee: "Nova",
            originRoomId: "room-1",
            parent: parent,
            blockedBy: [blocker1, blocker2],
            tags: ["auth", "sso"],
            startDate: new DateOnly(2030, 1, 2),
            dueDate: new DateOnly(2030, 1, 3),
            location: new TaskLocation("Platform", "Auth", false),
            changeLog:
            [
                TestTasks.Entry("2026-09-20T10:00:00Z", "You", "created"),
                TestTasks.Entry("2026-09-24T10:00:00Z", "You", "status: To Do → In Progress"),
            ],
            closedAt: new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
    }

    /// <summary>Renders the Board, with its popovers, over <paramref name="tasks"/>.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies every service.</param>
    /// <param name="view">The effective View.</param>
    /// <param name="tasks">The Tasks.</param>
    /// <param name="onEditColumns">Receives <c>OnEditColumns</c>.</param>
    /// <param name="viewChanged">Receives <c>ViewChanged</c>.</param>
    /// <returns>The rendered root.</returns>
    private static IRenderedComponent<ContainerFragment> RenderBoard(
        MudBunitContext ctx,
        TaskToolHarness harness,
        TaskView view,
        IReadOnlyList<TaskItem> tasks,
        Action? onEditColumns = null,
        Action<TaskView>? viewChanged = null)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskBoard>(0);
            builder.AddAttribute(1, nameof(TaskBoard.View), view);
            builder.AddAttribute(2, nameof(TaskBoard.Tasks), tasks);
            if (onEditColumns is not null)
            {
                builder.AddAttribute(3, nameof(TaskBoard.OnEditColumns), EventCallback.Factory.Create(onEditColumns, onEditColumns));
            }

            if (viewChanged is not null)
            {
                builder.AddAttribute(4, nameof(TaskBoard.ViewChanged), EventCallback.Factory.Create(viewChanged, viewChanged));
            }

            builder.CloseComponent();
        });
    }

    /// <summary>Renders one card.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies the card's services.</param>
    /// <param name="task">The Task.</param>
    /// <param name="fields">The View's fields.</param>
    /// <param name="presence">The assignee's presence.</param>
    /// <returns>The rendered root.</returns>
    private static IRenderedComponent<ContainerFragment> RenderCard(MudBunitContext ctx, TaskToolHarness harness, TaskItem task, IReadOnlyList<string> fields, PresenceState? presence)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskCard>(0);
            builder.AddAttribute(1, nameof(TaskCard.Task), task);
            builder.AddAttribute(2, nameof(TaskCard.Fields), fields);
            builder.AddAttribute(3, nameof(TaskCard.Presence), presence);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders the toolbar over <paramref name="view"/>.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies the toolbar's services.</param>
    /// <param name="view">The effective View.</param>
    /// <param name="changed">Receives <c>EffectiveViewChanged</c>.</param>
    /// <returns>The toolbar.</returns>
    private static IRenderedComponent<TaskToolbar> RenderToolbar(MudBunitContext ctx, TaskToolHarness harness, TaskView view, Action<TaskView> changed)
    {
        harness.AddTo(ctx.Services);
        IRenderedComponent<ContainerFragment> root = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskToolbar>(0);
            builder.AddAttribute(1, nameof(TaskToolbar.EffectiveView), view);
            builder.AddAttribute(2, nameof(TaskToolbar.EffectiveViewChanged), EventCallback.Factory.Create(changed, changed));
            builder.CloseComponent();
        });
        return root.FindComponent<TaskToolbar>();
    }

    /// <summary>Renders the Tasks page at <paramref name="viewId"/>.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="harness">Supplies every service.</param>
    /// <param name="viewId">The route's View id, or null for <c>/tasks</c>.</param>
    /// <returns>The rendered root.</returns>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, TaskToolHarness harness, string? viewId)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            if (viewId is not null)
            {
                builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            }

            builder.CloseComponent();
        });
    }

    /// <summary>Applies a toolbar override to the page's effective View, as the toolbar would.</summary>
    /// <param name="cut">The rendered page.</param>
    /// <param name="change">The override.</param>
    /// <returns>A task that completes once applied.</returns>
    private static async Task OverrideAsync(IRenderedComponent<ContainerFragment> cut, Func<TaskView, TaskView> change)
    {
        IRenderedComponent<TaskToolbar> toolbar = cut.FindComponent<TaskToolbar>();
        TaskView updated = change(toolbar.Instance.EffectiveView);
        await cut.InvokeAsync(() => toolbar.Instance.EffectiveViewChanged.InvokeAsync(updated));
    }

    /// <summary>Opens the header menu of the column labelled <paramref name="label"/>.</summary>
    /// <param name="cut">The rendered root.</param>
    /// <param name="label">The column label.</param>
    private static void OpenHeaderMenu(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.Find($"button[aria-label='{label} column menu']").Click();

    /// <summary>Opens the header menu of <paramref name="label"/> and clicks the item <paramref name="item"/>.</summary>
    /// <param name="cut">The rendered root.</param>
    /// <param name="label">The column label.</param>
    /// <param name="item">The menu item's text.</param>
    private static void ClickHeaderItem(IRenderedComponent<ContainerFragment> cut, string label, string item)
    {
        OpenHeaderMenu(cut, label);
        cut.FindAll("div.mud-menu-item").First(e => string.Equals(e.TextContent.Trim(), item, StringComparison.Ordinal)).Click();
    }

    /// <summary>Renames the column labelled <paramref name="label"/> through its inline field.</summary>
    /// <param name="cut">The rendered root.</param>
    /// <param name="label">The column label.</param>
    /// <param name="newLabel">The new label.</param>
    /// <returns>A task that completes once committed.</returns>
    private static async Task RenameAsync(IRenderedComponent<ContainerFragment> cut, string label, string newLabel)
    {
        ClickHeaderItem(cut, label, "Rename");
        IRenderedComponent<MudTextField<string>> field = Assert.Single(
            cut.FindComponents<MudTextField<string>>(),
            f => f.Nodes.OfType<IElement>().Any(e => e.ClassList.Contains("task-board-column-rename")));
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(newLabel));
    }

    /// <summary>The lane whose label is <paramref name="label"/>.</summary>
    /// <param name="cut">The rendered root.</param>
    /// <param name="label">The lane label.</param>
    /// <returns>The lane element.</returns>
    private static IElement Lane(IRenderedComponent<ContainerFragment> cut, string label) =>
        Assert.Single(cut.FindAll("div.task-board-lane"), l => string.Equals(l.QuerySelector(".task-board-lane-label")?.TextContent.Trim(), label, StringComparison.Ordinal));

    /// <summary>A <c>.task-ghost-bucket</c> selector.</summary>
    [GeneratedRegex(@"\.task-ghost-bucket\b", RegexOptions.CultureInvariant)]
    private static partial Regex TaskGhostBucketSelector();

    /// <summary>A bare <c>.ghost-bucket</c> selector (not preceded by a name character).</summary>
    [GeneratedRegex(@"(?<![\w-])\.ghost-bucket\b", RegexOptions.CultureInvariant)]
    private static partial Regex BareGhostBucketSelector();

    /// <summary>The open button's focus-visible selector.</summary>
    [GeneratedRegex(@"\.task-card-open:focus-visible", RegexOptions.CultureInvariant)]
    private static partial Regex OpenButtonFocusSelector();

    /// <summary>The card div's focus-visible selector.</summary>
    [GeneratedRegex(@"\.task-card:focus-visible", RegexOptions.CultureInvariant)]
    private static partial Regex CardDivFocusSelector();
}
