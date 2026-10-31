namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using MudBlazor;
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

    /// <summary>The Panel shows Team and Project as two rows, each with its own visible label (the old combined "Team / Project" caption left both controls unlabelled).</summary>
    [Fact]
    public async Task TeamProject_Panel_ShowsSeparateLabels()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        IRenderedComponent<MudSelect<string>> team = cut.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-team", StringComparison.Ordinal));
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        Assert.Equal("Team", team.Instance.Label);
        Assert.Equal("Project", project.Instance.Label);
        Assert.Empty(cut.FindAll(".task-detail-team-project-label"));
    }

    /// <summary>The Expanded layout keeps Team and Project as two separately-labelled controls (Spec §13.6, the Panel-only combining implies Expanded does not).</summary>
    [Fact]
    public async Task TeamProject_Expanded_ShowsSeparateLabels()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        IRenderedComponent<MudSelect<string>> team = cut.FindComponents<MudSelect<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-team", StringComparison.Ordinal));
        IRenderedComponent<MudAutocomplete<string>> project = cut.FindComponents<MudAutocomplete<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-project", StringComparison.Ordinal));
        Assert.Equal("Team", team.Instance.Label);
        Assert.Equal("Project", project.Instance.Label);
    }

    /// <summary>
    /// Manual test TASKS-01 finding F3: in the Panel, Team and Project share only the
    /// "Team / Project" caption with no label of their own - each needs its own accessible name.
    /// Checked at both entry points (the task lists "both modes"): both
    /// layouts now show a visible <c>Label</c> too, and the <c>aria-label</c> must keep working as the accessible name.
    /// </summary>
    [Fact]
    public async Task TeamProject_HaveTheirOwnAccessibleNames()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Equal("Team", cut.Find(".task-detail-team input").GetAttribute("aria-label"));
        Assert.Equal("Project", cut.Find(".task-detail-project input").GetAttribute("aria-label"));
    }

    /// <summary>Typing a new Project name (<c>CoerceValue</c>) and saving creates that Project's folder (Spec §13.6).</summary>
    [Fact]
    public async Task Project_CoerceValue_NewNameCreatesFolderOnSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
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
    [Fact]
    public async Task Parent_AutocompleteStrict_ToStringFuncFormatsIdMiddleDotTitle()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
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
    [Fact]
    public async Task Origin_RoomStillExists_ShowsLinkToRoom()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        Room room = await harness.Directory.CreateRoomAsync("Origin room", [KnownIds.Human, harness.NovaId!], Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, originRoomId: room.Id);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Equal($"/rooms/{room.Id}", cut.Find(".task-detail-origin a").GetAttribute("href"));
    }

    /// <summary>Origin shows the exact Spec §13.6 text "(Room no longer exists)" when the origin Room is gone.</summary>
    [Fact]
    public async Task Origin_RoomNoLongerExists_ShowsExactParenthetical()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, originRoomId: "no-such-room");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Equal("(Room no longer exists)", TextOf(cut, ".task-detail-origin"));
    }

    // ---------------------------------------------------------------------------------------------
    // Created, Updated, Closed (Spec §13.6: read-only, derived).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Created and Updated show <see cref="TaskItem.Created"/>/<see cref="TaskItem.Updated"/> as a human date ("Today"), with the full date in the tooltip.</summary>
    [Fact]
    public async Task CreatedUpdated_ShowDerivedDatesAsHumanDates()
    {
        Agency.Huddle.Tests.Acp.Fakes.ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        using TaskToolHarness harness = new(clock);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Equal("Created Today", TextOf(cut, ".task-detail-created"));
        Assert.Equal("Updated Today", TextOf(cut, ".task-detail-updated"));
        Assert.Equal("Fri, Oct 16, 2026", cut.Find(".task-detail-created").GetAttribute("title"));
    }

    /// <summary>Closed shows only when the Task has a <see cref="TaskItem.ClosedAt"/> (Spec §13.6).</summary>
    [Fact]
    public async Task Closed_ShownOnlyWhenClosedAtIsSet()
    {
        Agency.Huddle.Tests.Acp.Fakes.ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        using TaskToolHarness harness = new(clock);
        TaskItem openTask = CreateTask(harness, title: "Still open");
        await using MudBunitContext openCtx = UiTestHost.Create().WithHarness(harness);
        var openCut = RenderDetail(openCtx, openTask.Id);
        Assert.Empty(openCut.FindAll(".task-detail-closed"));

        TaskResult closeResult = harness.Service.Close(openTask.Id, TaskActors.Human(harness.Options.Value));
        TaskItem closedTask = Assert.IsType<TaskResult.Saved>(closeResult).Task;
        await using MudBunitContext closedCtx = UiTestHost.Create().WithHarness(harness);
        var closedCut = RenderDetail(closedCtx, closedTask.Id);

        Assert.Equal("Closed Today", TextOf(closedCut, ".task-detail-closed"));
    }
}
