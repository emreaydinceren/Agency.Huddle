using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using SortDirection = Agency.Huddle.App.Tasks.Views.SortDirection;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.5 items 1-6: the View editor's Name/Description, List-or-Board and Active-or-Closed
/// toggles, the reorderable Fields/Grouping/Sort sections, and the Filters section's option lists.
/// Items 7-9 (Columns, validation, Delete, <c>MudExitPrompt</c>) are Task 13.2.t's job. Every drop
/// reorder follows corrections-B5 D13 item 2's pattern: one <see cref="MudDropContainer{T}"/> per
/// list, driven through its own <c>ItemDropped</c> with a <see cref="MudItemDropInfo{T}"/>.
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
        var options = root.FindAll(".view-editor-filter-team-option").Select(el => el.TextContent.Trim()).ToList();

        Assert.Contains("Platform", options);
        Assert.Contains("Legal", options);
        Assert.Equal(1, options.Count(o => string.Equals(o, "Platform", StringComparison.Ordinal)));
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

    /// <summary>Returns a predicate matching a rendered component whose <c>Class</c> equals <paramref name="className"/>, for picking one of several same-typed MudBlazor components apart.</summary>
    private static Func<IRenderedComponent<MudTextField<string>>, bool> HasClass(string className) =>
        component => string.Equals(component.Instance.Class, className, StringComparison.Ordinal);

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
