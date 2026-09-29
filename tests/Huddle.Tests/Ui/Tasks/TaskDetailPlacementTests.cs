namespace Agency.Huddle.Tests.Ui.Tasks;

using System.Globalization;
using Bunit;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins Spec §13.6's Team/Project, Parent, Origin and Created/Updated/Closed rows at both <c>TaskDetail</c> entry points - split out of <see cref="TaskDetailFieldsTests"/> so xUnit can run the classes in parallel.
/// </summary>
public sealed class TaskDetailPlacementTests
{
    // Team, Project (Spec §13.6: the Panel combines them; Expanded keeps them separate).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Panel combines Team and Project into one "Team / Project" control (Spec §13.6).</summary>
    [Fact]
    public async Task TeamProject_Panel_ShowsCombinedLabel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal("Team / Project", TextOf(cut, ".task-detail-team-project-label"));
    }

    /// <summary>The Expanded layout keeps Team and Project as two separately-labelled controls (Spec §13.6, the Panel-only combining implies Expanded does not).</summary>
    [Fact]
    public async Task TeamProject_Expanded_ShowsSeparateLabels()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Expanded);

        IRenderedComponent<MudSelect<string>> team = cut.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-team", StringComparison.Ordinal));
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        Assert.Equal("Team", team.Instance.Label);
        Assert.Equal("Project", project.Instance.Label);
    }

    /// <summary>
    /// Manual test TASKS-01 finding F3: in the Panel, Team and Project share only the
    /// "Team / Project" caption with no label of their own - each needs its own accessible name.
    /// Checked at both entry points (the task lists "both modes"): the Panel's Team/Project have no
    /// visible <c>Label</c>, so they need <c>aria-label</c>; the Expanded layout already shows a
    /// visible <c>Label</c>, which must keep working as the accessible name.
    /// </summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task TeamProject_HaveTheirOwnAccessibleNames(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal("Team", cut.Find(".task-detail-team input").GetAttribute("aria-label"));
        Assert.Equal("Project", cut.Find(".task-detail-project input").GetAttribute("aria-label"));
    }

    /// <summary>Typing a new Project name (<c>CoerceValue</c>) and saving creates that Project's folder (Spec §13.6).</summary>
    [Fact]
    public async Task Project_CoerceValue_NewNameCreatesFolderOnSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        Assert.True(project.Instance.CoerceValue);

        await cut.InvokeAsync(() => project.Instance.ValueChanged.InvokeAsync("NewProject"));
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.True(Directory.Exists(Path.Combine(harness.TasksDirPath, "Platform", "NewProject")));
    }

    // ---------------------------------------------------------------------------------------------
    // Parent (Spec §13.6).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Parent picker is <c>Strict</c> and its <c>ToStringFunc</c> gives "PLAT-0030 · title" (Spec §13.6's own example format).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Parent_AutocompleteStrict_ToStringFuncFormatsIdMiddleDotTitle(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        IRenderedComponent<MudAutocomplete<TaskItem>> parent = cut.FindComponents<MudAutocomplete<TaskItem>>().Single(m => string.Equals(m.Instance.Class, "task-detail-parent", StringComparison.Ordinal));
        Assert.True(parent.Instance.Strict);

        TaskItem sample = TestTasks.Make(id: "PLAT-0030", title: "Sample title");
        Func<TaskItem?, string?>? toString = parent.Instance.ToStringFunc;
        Assert.NotNull(toString);
        Assert.Equal("PLAT-0030 · Sample title", toString(sample));
    }

    // ---------------------------------------------------------------------------------------------
    // Origin (Spec §13.6).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Origin shows a link to <c>/rooms/{id}</c> when the origin Room still exists (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Origin_RoomStillExists_ShowsLinkToRoom(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        Room room = await harness.Directory.CreateRoomAsync("Origin room", [KnownIds.Human, harness.NovaId!], Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, originRoomId: room.Id);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal($"/rooms/{room.Id}", cut.Find(".task-detail-origin a").GetAttribute("href"));
    }

    /// <summary>Origin shows the exact Spec §13.6 text "(Room no longer exists)" when the origin Room is gone.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Origin_RoomNoLongerExists_ShowsExactParenthetical(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, originRoomId: "no-such-room");
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal("(Room no longer exists)", TextOf(cut, ".task-detail-origin"));
    }

    // ---------------------------------------------------------------------------------------------
    // Created, Updated, Closed (Spec §13.6: read-only, derived).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Created and Updated show <see cref="TaskItem.Created"/>/<see cref="TaskItem.Updated"/>, formatted (provisionally, the Spec gives no format) as <c>yyyy-MM-dd</c>, invariant.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task CreatedUpdated_ShowDerivedDatesInIsoFormat(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        DateTimeOffset created = task.Created ?? throw new InvalidOperationException("Task has no Created date.");
        DateTimeOffset updated = task.Updated ?? throw new InvalidOperationException("Task has no Updated date.");
        string expectedCreated = created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string expectedUpdated = updated.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(expectedCreated, TextOf(cut, ".task-detail-created"));
        Assert.Equal(expectedUpdated, TextOf(cut, ".task-detail-updated"));
    }

    /// <summary>Closed shows only when the Task has a <see cref="TaskItem.ClosedAt"/> (Spec §13.6).</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Closed_ShownOnlyWhenClosedAtIsSet(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem openTask = CreateTask(harness, title: "Still open");
        await using MudBunitContext openCtx = NewContext(harness);
        var openCut = RenderDetail(openCtx, openTask.Id, mode);
        Assert.Empty(openCut.FindAll(".task-detail-closed"));

        TaskResult closeResult = harness.Service.Close(openTask.Id, TaskActors.Human(harness.Options.Value));
        TaskItem closedTask = Assert.IsType<TaskResult.Saved>(closeResult).Task;
        await using MudBunitContext closedCtx = NewContext(harness);
        var closedCut = RenderDetail(closedCtx, closedTask.Id, mode);

        DateTimeOffset closedAt = closedTask.ClosedAt ?? throw new InvalidOperationException("Task has no ClosedAt date.");
        string expected = closedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(expected, TextOf(closedCut, ".task-detail-closed"));
    }
}
