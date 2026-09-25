using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using SortDirection = Agency.Huddle.App.Tasks.Views.SortDirection;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.5 items 1-9: the View editor's Name/Description, List-or-Board and Active-or-Closed
/// toggles, the reorderable Fields/Grouping/Sort sections, the Filters section's option lists (items
/// 1-6, Task 13.1.t), and the Columns section, validation, Delete and <c>MudExitPrompt</c> (items
/// 7-9, Task 13.2.t, corrections-B5 D13 items 1 and 3-7). Every drop reorder follows corrections-B5
/// D13 item 2's pattern: one <see cref="MudDropContainer{T}"/> per list, driven through its own
/// <c>ItemDropped</c> with a <see cref="MudItemDropInfo{T}"/>.
/// </summary>
public sealed class ViewEditorDrawerTests
{
    /// <summary>Editing Name and Description and clicking Save writes both through to <see cref="ViewStore"/>.</summary>
    [Fact]
    public async Task NameAndDescription_Bind_AndPersistOnSave()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Original", description: "Old");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var name = root.FindComponents<MudTextField<string>>().Single(HasClass("view-editor-name"));
        var description = root.FindComponents<MudTextField<string>>().Single(HasClass("view-editor-description"));
        await root.InvokeAsync(() => name.Instance.ValueChanged.InvokeAsync("Renamed"));
        await root.InvokeAsync(() => description.Instance.ValueChanged.InvokeAsync("New description"));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Renamed", persisted.Name);
        Assert.Equal("New description", persisted.Description);
    }

    /// <summary>Switching Kind to Board disables the Active/Closed toggle (a Board is always Active), mirroring <c>TaskToolbar</c>'s own rule.</summary>
    [Fact]
    public async Task ClosedToggle_DisabledOnceKindSwitchesToBoard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView listView = Save(harness, "v1", ViewKind.List);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, listView.Id);

        var scopeToggle = root.FindComponent<MudToggleGroup<ViewScope>>();
        Assert.False(scopeToggle.Instance.Disabled);

        var kindToggle = root.FindComponent<MudToggleGroup<ViewKind>>();
        await root.InvokeAsync(() => kindToggle.Instance.ValueChanged.InvokeAsync(ViewKind.Board));

        scopeToggle = root.FindComponent<MudToggleGroup<ViewScope>>();
        Assert.True(scopeToggle.Instance.Disabled);
    }

    /// <summary>Dragging a Fields row updates the draft's order (read from <c>ItemDropped</c>'s <c>IndexInZone</c>), persisted on Save.</summary>
    [Fact]
    public async Task Fields_ReorderByDrop_UpdatesOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, fields: ["status", "priority", "assignee"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var container = root.FindComponent<MudDropContainer<string>>();
        await root.InvokeAsync(() => container.Instance.ItemDropped.InvokeAsync(new MudItemDropInfo<string>("assignee", "fields", 0)));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["assignee", "status", "priority"], persisted.Fields);
    }

    /// <summary>The up and down icon buttons on a Fields row are a keyboard alternative to dragging.</summary>
    [Fact]
    public async Task Fields_ReorderByUpDownButtons_UpdatesOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, fields: ["status", "priority"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await ClickAsync(root, drawer, ".view-editor-field-down-status");
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["priority", "status"], persisted.Fields);
    }

    /// <summary>A Fields row's remove button drops that field from the shown list.</summary>
    [Fact]
    public async Task Fields_RemoveButton_RemovesTheField()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, fields: ["status", "priority"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await ClickAsync(root, drawer, ".view-editor-field-remove-priority");
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["status"], persisted.Fields);
    }

    /// <summary>The + Add field menu offers a field that isn't already shown, and adds it to the end of the list.</summary>
    [Fact]
    public async Task AddFieldMenu_AddsAnUnusedFieldToTheEnd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, fields: ["status"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await OpenMenuAsync(root, drawer, ".view-editor-fields-add");
        var priorityOption = root.FindAll(".view-editor-fields-add-option").Single(el => string.Equals(el.TextContent.Trim(), "priority", StringComparison.Ordinal));
        await root.InvokeAsync(() => priorityOption.Click());
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["status", "priority"], persisted.Fields);
    }

    /// <summary>Switching a Field's row off and saving excludes it from the saved <see cref="TaskView.Fields"/>, without disturbing the order of the fields still on.</summary>
    [Fact]
    public async Task Fields_SwitchOff_ExcludesTheFieldOnSave_KeepsOtherOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, fields: ["status", "priority", "assignee"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var priority = root.FindComponents<MudSwitch<bool>>().Single(HasSwitchClass("priority"));
        await root.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(false));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["status", "assignee"], persisted.Fields);
    }

    /// <summary>Switching a Field's row off, then back on, and saving restores it at its original position rather than at the end.</summary>
    [Fact]
    public async Task Fields_SwitchOffThenOn_RestoresItsOriginalPosition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, fields: ["status", "priority", "assignee"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var priority = root.FindComponents<MudSwitch<bool>>().Single(HasSwitchClass("priority"));
        await root.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(false));
        priority = root.FindComponents<MudSwitch<bool>>().Single(HasSwitchClass("priority"));
        await root.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(true));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["status", "priority", "assignee"], persisted.Fields);
    }

    /// <summary>The Team filter offers the union of <c>PersonaStore.Teams</c> and <c>TaskStore.Teams</c>, deduplicated.</summary>
    [Fact]
    public async Task TeamFilter_OffersUnionOfPersonaAndTaskStoreTeams()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        // Nova's frontmatter names "Platform" (PersonaStore.Teams); "Legal" exists only as an
        // empty Task folder on disk (TaskStore.Teams), so the union must carry both without duplicating Platform.
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Legal"));
        harness.Store.RebuildFromWatcher();
        TaskView saved = Save(harness, "v1", ViewKind.List);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await OpenSelectAsync(root, drawer, ".view-editor-filter-team");
        var options = root.FindAll(".view-editor-filter-team-option-label").Select(el => el.TextContent.Trim()).ToList();

        Assert.Contains("Platform", options);
        Assert.Contains("Legal", options);
        Assert.Equal(1, options.Count(o => string.Equals(o, "Platform", StringComparison.Ordinal)));
    }

    /// <summary>An orphan Team (no Teammate lists it) gets the Spec §13.12 warning note next to its filter option, exact wording.</summary>
    [Fact]
    public async Task TeamFilterOption_ForAnOrphanTeam_ShowsTheOrphanNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        // "Legal" is a Task folder with no Persona naming it, so TaskStore marks it IsOrphan.
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Legal"));
        harness.Store.RebuildFromWatcher();
        TaskView saved = Save(harness, "v1", ViewKind.List);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await OpenSelectAsync(root, drawer, ".view-editor-filter-team");

        var notes = root.FindAll(".view-editor-filter-team-orphan");
        Assert.Equal(["No teammate is in Legal"], notes.Select(el => el.TextContent.Trim()));
    }

    /// <summary>The Project filter's options are narrowed to only the chosen Teams once one or more Teams are selected.</summary>
    [Fact]
    public async Task ProjectFilter_NarrowsToTheChosenTeams()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Platform", "Backend"));
        Directory.CreateDirectory(Path.Combine(harness.TasksDirPath, "Legal", "Contracts"));
        harness.Store.RebuildFromWatcher();
        TaskView saved = Save(harness, "v1", ViewKind.List, teams: ["Platform"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await OpenSelectAsync(root, drawer, ".view-editor-filter-project");
        var options = root.FindAll(".view-editor-filter-project-option").Select(el => el.TextContent.Trim()).ToList();

        Assert.Equal(["Platform / Backend"], options);
    }

    /// <summary>The Assignee filter offers Me and Unassigned first, then every Persona (Spec §13.5 item 4).</summary>
    [Fact]
    public async Task AssigneeFilter_OffersMeUnassignedThenEveryPersona()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await OpenSelectAsync(root, drawer, ".view-editor-filter-assignee");
        var options = root.FindAll(".view-editor-filter-assignee-option").Select(el => el.TextContent.Trim()).ToList();

        Assert.Equal(["Me", "Unassigned", "Kai", "Nova"], options);
    }

    /// <summary>A filter value that names no current Team/Assignee/etc. is listed as "{value} (missing)" and stays selected.</summary>
    [Fact]
    public async Task MissingFilterValue_StaysSelected_AndIsLabelledMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, assignees: ["Ghost"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        Assert.Contains(root.FindAll(".view-editor-filter-assignee-chip"), el => string.Equals(el.TextContent.Trim(), "Ghost", StringComparison.Ordinal));

        await OpenSelectAsync(root, drawer, ".view-editor-filter-assignee");
        Assert.Contains(root.FindAll(".view-editor-filter-assignee-option"), el => string.Equals(el.TextContent.Trim(), "Ghost (missing)", StringComparison.Ordinal));
    }

    /// <summary>Two selected values in a filter row are joined by an "OR" label between their chips.</summary>
    [Fact]
    public async Task FilterRow_TwoSelectedValues_AreJoinedByAnOrLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, teams: ["Atlas", "Zephyr"]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);

        var orLabels = root.FindAll(".view-editor-filter-team-or");

        Assert.Single(orLabels);
        Assert.Equal("OR", orLabels[0].TextContent.Trim());
    }

    /// <summary>Dragging a Grouping chip reorders <see cref="TaskView.Grouping"/>.</summary>
    [Fact]
    public async Task Grouping_ReorderByDrop_UpdatesOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, grouping: [TaskGroupField.State, TaskGroupField.Assignee]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var container = root.FindComponent<MudDropContainer<TaskGroupField>>();
        await root.InvokeAsync(() => container.Instance.ItemDropped.InvokeAsync(new MudItemDropInfo<TaskGroupField>(TaskGroupField.Assignee, "grouping", 0)));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal([TaskGroupField.Assignee, TaskGroupField.State], persisted.Grouping);
    }

    /// <summary>State grouping is disabled for a Board (a Board already groups its columns by state).</summary>
    [Fact]
    public async Task Grouping_StateOption_DisabledForBoard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.Board, columns: BoardDefaultColumns);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        await OpenMenuAsync(root, drawer, ".view-editor-grouping-add");
        var stateOption = root.FindComponents<MudMenuItem>().Single(m => string.Equals(m.Instance.Class, "view-editor-grouping-add-state", StringComparison.Ordinal));

        Assert.True(stateOption.Instance.Disabled);
    }

    /// <summary>Dragging a Sort row reorders <see cref="TaskView.Sort"/>.</summary>
    [Fact]
    public async Task Sort_ReorderByDrop_UpdatesOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, sort: [new SortKey("priority", SortDirection.Descending), new SortKey("due_date", SortDirection.Ascending)]);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var container = root.FindComponent<MudDropContainer<SortKey>>();
        SortKey dueDate = saved.Sort[1];
        await root.InvokeAsync(() => container.Instance.ItemDropped.InvokeAsync(new MudItemDropInfo<SortKey>(dueDate, "sort", 0)));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["due_date", "priority"], persisted.Sort.Select(k => k.Field));
    }

    /// <summary>Corrections-B7 §13.2 item 1: the public open-at-section parameter marks its section active - the Columns section, at least.</summary>
    [Fact]
    public async Task OpenAtSection_Columns_MarksTheColumnsSectionActive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.Board, columns: BoardDefaultColumns);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> generalRoot = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ViewEditorDrawer>(0);
            builder.AddAttribute(1, nameof(ViewEditorDrawer.Open), true);
            builder.AddAttribute(2, nameof(ViewEditorDrawer.Id), saved.Id);
            builder.CloseComponent();
        });
        Assert.Empty(generalRoot.FindAll(".view-editor-section-columns.view-editor-section-active"));

        await using MudBunitContext columnsCtx = NewContext(harness);
        IRenderedComponent<ContainerFragment> columnsRoot = columnsCtx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ViewEditorDrawer>(0);
            builder.AddAttribute(1, nameof(ViewEditorDrawer.Open), true);
            builder.AddAttribute(2, nameof(ViewEditorDrawer.Id), saved.Id);
            builder.AddAttribute(3, nameof(ViewEditorDrawer.Section), ViewEditorSection.Columns);
            builder.CloseComponent();
        });
        Assert.Single(columnsRoot.FindAll(".view-editor-section-columns.view-editor-section-active"));
    }

    /// <summary>Corrections-B7 §13.2 item 2: switching Kind to Board seeds <see cref="BoardLayout.DefaultColumns"/> when the draft has none.</summary>
    [Fact]
    public async Task SwitchingToBoard_WithNoColumns_SeedsTheDefaultColumns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        var kindToggle = root.FindComponent<MudToggleGroup<ViewKind>>();
        await root.InvokeAsync(() => kindToggle.Instance.ValueChanged.InvokeAsync(ViewKind.Board));
        await ClickAsync(root, drawer, ".view-editor-save");

        TaskView? persisted = harness.Views.Get(saved.Id);
        Assert.NotNull(persisted);
        // BoardColumn's record equality compares States by reference (never == per corrections-B5
        // D13 item 1), and JSON round-tripping through ViewStore always yields fresh list instances -
        // so columns are compared by their own values, not record equality.
        Assert.Equal(BoardLayout.DefaultColumns.Select(Flatten), persisted.Columns.Select(Flatten));
    }

    /// <summary>Flattens a <see cref="BoardColumn"/> into a value xunit's collection comparer can compare structurally, sidestepping <see cref="BoardColumn"/>'s own States-by-reference equality.</summary>
    private static (string Label, string States, bool Hidden) Flatten(BoardColumn column) =>
        (column.Label, string.Join(",", column.States), column.Hidden);

    /// <summary>A state already placed in another column is shown disabled, labelled "{State} (in {Column label})" - the delivery-manager's settled text (J39).</summary>
    [Fact]
    public async Task Columns_StateUsedInAnotherColumn_ShownDisabled_NamedWithTheColumn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        // Every state must be placed exactly once for ViewStore.Save to accept the fixture; Review
        // lives inside "Doing" (alongside InProgress) so it can be named as the state's own column.
        IReadOnlyList<BoardColumn> columns =
        [
            new("Backlog", [TaskState.Backlog]),
            new("To Do", [TaskState.ToDo]),
            new("Doing", [TaskState.InProgress, TaskState.Review]),
            new("Done", [TaskState.Done]),
            new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
        ];
        TaskView saved = Save(harness, "v1", ViewKind.Board, columns: columns);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);

        await OpenSelectAsync(root, root.FindComponent<ViewEditorDrawer>(), ".view-editor-column-states-0");
        var reviewOption = root.FindAll(".view-editor-column-state-option").Single(el => el.TextContent.Trim().StartsWith("Review", StringComparison.Ordinal));
        Assert.Equal("Review (in Doing)", reviewOption.TextContent.Trim());

        // Every column's MudSelect instantiates its MudSelectItem children regardless of popover
        // visibility, so the Review item must be found scoped to Backlog's own select (not by class
        // and value alone, which would also match Doing's, To Do's, Done's and Won't do's copies).
        var backlogSelect = root.FindComponents<MudSelect<TaskState>>()
            .Single(c => string.Equals(c.Instance.Class, "view-editor-column-states-0", StringComparison.Ordinal));
        MudSelectItem<TaskState> reviewItem = backlogSelect.FindComponents<MudSelectItem<TaskState>>()
            .Single(c => c.Instance.Value == TaskState.Review)
            .Instance;
        Assert.True(reviewItem.Disabled);
    }

    /// <summary>An unplaced state disables Save and the exact placement text (<c>ViewValidator.cs:107</c>) appears in the validation alert.</summary>
    [Fact]
    public async Task Columns_UnplacedState_DisablesSave_AndShowsThePlacementMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        // Saved valid (every state placed once), then Review is cleared from its own column through
        // the rendered editor - ViewStore.Save would refuse an already-invalid fixture outright.
        TaskView saved = Save(harness, "v1", ViewKind.Board, columns: BoardDefaultColumns);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        await ClearColumnStatesAsync(root, reviewColumnIndex: 3);

        var save = root.FindComponents<MudButton>().Single(HasButtonClass("view-editor-save"));
        Assert.True(save.Instance.Disabled);

        var problem = root.FindAll(".view-editor-validation-problem")
            .Single(el => string.Equals(el.TextContent.Trim(), "Place Review in a column.", StringComparison.Ordinal));
        Assert.Equal("Place Review in a column.", problem.TextContent.Trim());
    }

    /// <summary>Every <see cref="ViewValidator"/> problem is listed, inside an alert carrying <c>role="alert"</c>.</summary>
    [Fact]
    public async Task ValidationAlert_ListsEveryProblem_WithAlertRole()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView existing = Save(harness, "v1", ViewKind.List, name: "Taken");
        // Saved valid under a placeholder name, then edited in the drawer to collide with "v1"'s
        // name and to lose Review's placement, so both problems appear from one draft at once.
        TaskView saved = Save(harness, "v2", ViewKind.Board, name: "Not Taken Yet", columns: BoardDefaultColumns);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        var name = root.FindComponents<MudTextField<string>>().Single(HasClass("view-editor-name"));
        await root.InvokeAsync(() => name.Instance.ValueChanged.InvokeAsync("Taken"));
        await ClearColumnStatesAsync(root, reviewColumnIndex: 3);

        var alert = root.Find(".view-editor-validation-alert");
        Assert.Equal("alert", alert.GetAttribute("role"));
        var problems = root.FindAll(".view-editor-validation-problem").Select(el => el.TextContent.Trim()).ToList();
        Assert.Equal(
            ["Another View already has this name.", "Column 'Review' must have at least one state.", "Place Review in a column."],
            problems);
    }

    /// <summary>Neither built-in View offers a Delete control.</summary>
    [Fact]
    public async Task Delete_NotOfferedForABuiltInView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, ViewStore.AllTasksId);

        Assert.Empty(root.FindAll(".view-editor-delete"));
    }

    /// <summary>Delete opens <c>ShowMessageBoxAsync</c> with the exact title, text and button labels Spec §13.5 item 8 gives.</summary>
    [Fact]
    public async Task Delete_MessageBox_HasTheExactTitleTextAndButtons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Platform board");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        _ = drawer.InvokeAsync(() => root.Find(".view-editor-delete").Click());
        root.WaitForAssertion(() => Assert.NotEmpty(root.FindAll(".mud-dialog-actions button")));

        Assert.Equal("Delete View", root.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Delete 'Platform board'? Tasks are not affected.", root.Find(".mud-dialog-content").TextContent.Trim());
        var buttons = root.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Cancel", "Delete"], buttons);

        await drawer.InvokeAsync(() => root.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());
    }

    /// <summary>Confirming Delete removes the View from <see cref="ViewStore"/> and navigates to <c>/tasks</c>.</summary>
    [Fact]
    public async Task Delete_Confirmed_RemovesTheViewAndNavigatesToTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Platform board");

        await using MudBunitContext ctx = NewContext(harness);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        _ = drawer.InvokeAsync(() => root.Find(".view-editor-delete").Click());
        root.WaitForAssertion(() => Assert.NotEmpty(root.FindAll(".mud-dialog-actions button")));
        await drawer.InvokeAsync(() => root.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).Click());

        Assert.Null(harness.Views.Get(saved.Id));
        Assert.EndsWith("/tasks", navigation.Uri, StringComparison.Ordinal); // contains-ok: a full URI, whose scheme/host prefix bUnit's FakeNavigationManager owns; only the path is this test's concern.
    }

    /// <summary>Cancelling Delete keeps the View.</summary>
    [Fact]
    public async Task Delete_Cancelled_KeepsTheView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Platform board");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        IRenderedComponent<ViewEditorDrawer> drawer = root.FindComponent<ViewEditorDrawer>();

        _ = drawer.InvokeAsync(() => root.Find(".view-editor-delete").Click());
        root.WaitForAssertion(() => Assert.NotEmpty(root.FindAll(".mud-dialog-actions button")));
        await drawer.InvokeAsync(() => root.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());

        Assert.NotNull(harness.Views.Get(saved.Id));
    }

    /// <summary><c>MudExitPrompt</c> is disabled until the draft differs from the saved View, then enabled.</summary>
    [Fact]
    public async Task ExitPrompt_DisabledUntilDirty_ThenEnabled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Original");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);

        var exitPrompt = root.FindComponent<MudExitPrompt>();
        Assert.True(exitPrompt.Instance.Disabled);

        var name = root.FindComponents<MudTextField<string>>().Single(HasClass("view-editor-name"));
        await root.InvokeAsync(() => name.Instance.ValueChanged.InvokeAsync("Edited"));

        exitPrompt = root.FindComponent<MudExitPrompt>();
        Assert.False(exitPrompt.Instance.Disabled);
    }

    /// <summary>While <see cref="ViewStore.LoadError"/> is set, every control - Name, Save and Delete alike - is disabled.</summary>
    [Fact]
    public async Task LoadError_DisablesEveryControl()
    {
        using TempDataDir dir = new();
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "views.json"), "{ not json", TestContext.Current.CancellationToken);
        PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        using ViewStore views = new(dir.Options(), NullLogger<ViewStore>.Instance);
        Assert.NotNull(views.LoadError);

        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(personas);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(views);
        ctx.Services.AddSingleton(Options.Create(new TeamOptions()));
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, "any-id");

        var name = root.FindComponents<MudTextField<string>>().Single(HasClass("view-editor-name"));
        Assert.True(name.Instance.Disabled);
        var save = root.FindComponents<MudButton>().Single(HasButtonClass("view-editor-save"));
        Assert.True(save.Instance.Disabled);
        var delete = root.FindComponents<MudButton>().Single(HasButtonClass("view-editor-delete"));
        Assert.True(delete.Instance.Disabled);

        await ctx.DisposeAsync();
    }

    /// <summary>An external <see cref="ViewStore.ViewsChanged"/> re-seeds the draft from the store when it is not dirty.</summary>
    [Fact]
    public async Task ViewsChanged_NotDirty_ReseedsFromTheStore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Original");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);

        Assert.True(harness.Views.Save(saved with { Name = "Renamed Elsewhere" }).Saved);

        root.WaitForAssertion(() => Assert.Equal("Renamed Elsewhere", NameInputValue(root)));
    }

    /// <summary>An external <see cref="ViewStore.ViewsChanged"/> does not overwrite an unsaved (dirty) edit.</summary>
    [Fact]
    public async Task ViewsChanged_Dirty_DoesNotOverwriteThePendingEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskView saved = Save(harness, "v1", ViewKind.List, name: "Original");

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = RenderDrawer(ctx, saved.Id);
        var name = root.FindComponents<MudTextField<string>>().Single(HasClass("view-editor-name"));
        await root.InvokeAsync(() => name.Instance.ValueChanged.InvokeAsync("My unsaved edit"));

        Assert.True(harness.Views.Save(saved with { Name = "Renamed Elsewhere" }).Saved);
        await Task.Delay(50, ct);

        Assert.Equal("My unsaved edit", NameInputValue(root));
    }

    /// <summary>The columns a Board View needs to pass <c>ViewValidator</c>: every <see cref="TaskState"/> covered exactly once, restated because <c>BoardLayout.DefaultColumns</c> is internal to production code (same restatement as <c>TaskToolbarTests</c>).</summary>
    private static readonly IReadOnlyList<BoardColumn> BoardDefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Review", [TaskState.Review]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>Saves a fresh View with the given shape and returns the copy <see cref="ViewStore"/> actually resolved.</summary>
    private static TaskView Save(
        TaskToolHarness harness,
        string id,
        ViewKind kind,
        string? name = null,
        string? description = null,
        IReadOnlyList<string>? fields = null,
        IReadOnlyList<string>? teams = null,
        IReadOnlyList<string>? assignees = null,
        IReadOnlyList<TaskGroupField>? grouping = null,
        IReadOnlyList<SortKey>? sort = null,
        IReadOnlyList<BoardColumn>? columns = null)
    {
        TaskView view = new()
        {
            Id = id,
            Name = name ?? id,
            Description = description,
            Kind = kind,
            Fields = fields ?? [],
            Filter = new TaskFilter { Teams = teams ?? [], Assignees = assignees ?? [] },
            Grouping = grouping ?? [],
            Sort = sort ?? [],
            Columns = columns ?? [],
        };
        ViewSaveResult result = harness.Views.Save(view);
        Assert.True(result.Saved);
        return harness.Views.Get(id) ?? throw new InvalidOperationException($"'{id}' was just saved but is missing.");
    }

    /// <summary>Renders <see cref="ViewEditorDrawer"/>, open, editing the View named <paramref name="id"/>, alongside the popover/dialog hosts its <c>MudSelect</c>/<c>MudMenu</c> controls need.</summary>
    private static IRenderedComponent<ContainerFragment> RenderDrawer(MudBunitContext ctx, string id) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ViewEditorDrawer>(0);
            builder.AddAttribute(1, nameof(ViewEditorDrawer.Open), true);
            builder.AddAttribute(2, nameof(ViewEditorDrawer.Id), id);
            builder.CloseComponent();
        });

    /// <summary>Clicks the element matching <paramref name="cssSelector"/> inside <paramref name="root"/>, dispatched through <paramref name="drawer"/>'s render scheduler.</summary>
    private static Task ClickAsync(IRenderedComponent<ContainerFragment> root, IRenderedComponent<ViewEditorDrawer> drawer, string cssSelector) =>
        drawer.InvokeAsync(() => root.Find(cssSelector).Click());

    /// <summary>Opens a <c>MudMenu</c> whose root carries <paramref name="cssSelector"/>, by clicking its inner activator button.</summary>
    private static Task OpenMenuAsync(IRenderedComponent<ContainerFragment> root, IRenderedComponent<ViewEditorDrawer> drawer, string cssSelector) =>
        drawer.InvokeAsync(() => root.Find($"{cssSelector} button").Click());

    /// <summary>Opens a <c>MudSelect</c> whose root carries <paramref name="cssSelector"/>: it toggles on <c>mousedown</c>, not <c>click</c>.</summary>
    private static Task OpenSelectAsync(IRenderedComponent<ContainerFragment> root, IRenderedComponent<ViewEditorDrawer> drawer, string cssSelector) =>
        drawer.InvokeAsync(() => root.Find(cssSelector).MouseDown());

    /// <summary>Clears the states multiselect of the column at <paramref name="reviewColumnIndex"/> (its own <see cref="MudSelect{T}.SelectedValuesChanged"/>, driven directly - no popover interaction needed), leaving whichever states it held unplaced.</summary>
    private static Task ClearColumnStatesAsync(IRenderedComponent<ContainerFragment> root, int reviewColumnIndex)
    {
        var select = root.FindComponents<MudSelect<TaskState>>()
            .Single(c => string.Equals(c.Instance.Class, $"view-editor-column-states-{reviewColumnIndex}", StringComparison.Ordinal));
        return root.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync([]));
    }

    /// <summary>Returns a predicate matching a rendered component whose <c>Class</c> equals <paramref name="className"/>, for picking one of several same-typed MudBlazor components apart.</summary>
    private static Func<IRenderedComponent<MudTextField<string>>, bool> HasClass(string className) =>
        component => string.Equals(component.Instance.Class, className, StringComparison.Ordinal);

    /// <summary>Returns a predicate matching a rendered <see cref="MudButton"/> whose <c>Class</c> equals <paramref name="className"/>.</summary>
    private static Func<IRenderedComponent<MudButton>, bool> HasButtonClass(string className) =>
        component => string.Equals(component.Instance.Class, className, StringComparison.Ordinal);

    /// <summary>Reads the Name field's live value straight from its rendered <c>&lt;input&gt;</c>, since <c>MudTextField.Value</c> is analyzer-blocked (<c>MUD0012</c>) for external access.</summary>
    private static string NameInputValue(IRenderedComponent<ContainerFragment> root) =>
        root.Find(".view-editor-name input").GetAttribute("value") ?? string.Empty;

    /// <summary>Returns a predicate matching the Fields row switch for <paramref name="key"/> (<c>.view-editor-field-switch-{key}</c>).</summary>
    private static Func<IRenderedComponent<MudSwitch<bool>>, bool> HasSwitchClass(string key) =>
        component => string.Equals(component.Instance.Class, $"view-editor-field-switch-{key}", StringComparison.Ordinal);

    /// <summary>Registers the harness's services into a fresh MudBlazor-aware bUnit context.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }
}
