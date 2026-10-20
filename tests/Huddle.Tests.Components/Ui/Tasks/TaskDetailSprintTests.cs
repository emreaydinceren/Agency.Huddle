namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using MudBlazor;
using MudBlazor.Extensions;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins Sprints spec §12.1 for <c>TaskDetail</c> (SP-T11): the Sprint row's list, its new-name and invalid-name
/// feedback, that a typed or picked value stays pending until Save, that Save creates the Sprint and then the
/// Task, and that a failed save undoes a Sprint it created and nothing else.
/// </summary>
public sealed class TaskDetailSprintTests
{
    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    /// <summary>The Sprint field lists every Sprint name, newest first, and filters by the typed text ignoring case (spec §12.1 Search).</summary>
    [Fact]
    public async Task SprintField_Search_ListsNamesNewestFirstAndFiltersIgnoringCase()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        IRenderedComponent<MudAutocomplete<string>> field = SprintField(cut);

        IEnumerable<string> all = await field.Instance.SearchFunc!("", ct)!;
        IEnumerable<string> filtered = await field.Instance.SearchFunc!("AUTH", ct)!;

        Assert.Equal(["Auth hardening", "Sprint 1"], all);
        Assert.Equal(["Auth hardening"], filtered);
    }

    /// <summary>A typed name that is valid and new shows "New sprint: it will be created when you save", is accepted in place, and writes nothing until Save (spec §12.1, S20).</summary>
    [Fact]
    public async Task TypingNewName_ShowsNewSprintHelper_IsPending_AndWritesNothing()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        await TypeSprintAsync(cut, "Release train");

        Assert.Contains("New sprint: it will be created when you save", cut.Find(".task-detail-sprint-field .mud-input-helper-text").TextContent, StringComparison.Ordinal);
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
        Assert.Equal(TwoSprints, await File.ReadAllTextAsync(harness.SprintsFilePath, Xunit.TestContext.Current.CancellationToken));
        Assert.Null(harness.Store.Get(task.Id)?.Sprint);
    }

    /// <summary>Typing an existing name, in another case, shows no "New sprint" helper (spec §12.1 A new name).</summary>
    [Fact]
    public async Task TypingExistingName_IgnoringCase_ShowsNoNewSprintHelper()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        await TypeSprintAsync(cut, "auth HARDENING");

        Assert.DoesNotContain("New sprint", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>An invalid name shows the §6.1 problem as an error and disables Save while it stands (spec §12.1).</summary>
    [Fact]
    public async Task TypingInvalidName_ShowsErrorAndDisablesSave()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        await TypeSprintAsync(cut, "S3");

        Assert.Contains("A sprint name can't look like an id (S3).", cut.Find(".task-detail-sprint-field").TextContent, StringComparison.Ordinal);
        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));
        Assert.DoesNotContain("New sprint", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Save with a new name creates the Sprint, then saves the Task with its id (spec §12.1).</summary>
    [Fact]
    public async Task Save_NewName_CreatesTheSprintThenSavesTheTaskWithItsId()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await TypeSprintAsync(cut, "Release train");

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(new SprintId(3), harness.Store.Get(task.Id)?.Sprint);
        Assert.Equal(TwoSprints + "- S3 | Release train\n", await File.ReadAllTextAsync(harness.SprintsFilePath, Xunit.TestContext.Current.CancellationToken));
        Assert.Equal("Release train", SprintField(cut).Instance.GetState(x => x.Value));
    }

    /// <summary>Save with an existing name (picked) sets that Sprint's id and writes nothing to <c>sprints.md</c> (spec §12.1).</summary>
    [Fact]
    public async Task Save_ExistingName_SetsItsIdAndLeavesTheFileAlone()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await cut.InvokeAsync(() => SprintField(cut).Instance.ValueChanged.InvokeAsync("Sprint 1"));

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(new SprintId(1), harness.Store.Get(task.Id)?.Sprint);
        Assert.Equal(TwoSprints, await File.ReadAllTextAsync(harness.SprintsFilePath, Xunit.TestContext.Current.CancellationToken));
    }

    /// <summary>A <c>Refused</c> create (the file cannot be written) shows its problem in the Save error alert and saves nothing (spec §12.1).</summary>
    [Fact]
    public async Task Save_RefusedCreate_ShowsTheProblemAndSavesNothing()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await TypeSprintAsync(cut, "Release train");
        File.Delete(harness.SprintsFilePath);
        Directory.CreateDirectory(harness.SprintsFilePath);

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Contains("Could not write", cut.Find(".task-detail-save-error").TextContent, StringComparison.Ordinal);
        Assert.Equal("alert", cut.Find(".task-detail-save-error").GetAttribute("role"));
        Assert.Null(harness.Store.Get(task.Id)?.Sprint);
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>A save that fails (a Conflict) after the panel created the Sprint removes that Sprint again (spec §12.1, E-S25).</summary>
    [Fact]
    public async Task Save_ConflictAfterCreatedSprint_RemovesTheCreatedSprint()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await TypeSprintAsync(cut, "Release train");
        await ChangePriorityAsync(cut, TaskPriority.Urgent);
        TaskResult outside = harness.Service.Update(task.Id, new TaskPatch { Priority = TaskPriority.Low }, null, TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(outside);

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Contains("changed this task while you were editing", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(TwoSprints, await File.ReadAllTextAsync(harness.SprintsFilePath, Xunit.TestContext.Current.CancellationToken));
        Assert.Equal("Unsaved edits", TextOf(cut, ".task-detail-unsaved-text"));
    }

    /// <summary>A Sprint another tab wrote a moment before Save comes back as <c>Existing</c>, is adopted at once, the Task saves with it, and the line is left alone (spec §12.1).</summary>
    [Fact]
    public async Task Save_SprintWrittenElsewhereJustBefore_SavesWithItAndKeepsTheLine()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await TypeSprintAsync(cut, "Late sprint");
        string withLate = TwoSprints + "- S3 | Late sprint\n";
        // Written by hand a moment before Save: the store's snapshot has not seen it yet, so Create answers Existing
        // and must adopt it, or the Task call would be refused. Nothing here may undo the line.
        await File.WriteAllTextAsync(harness.SprintsFilePath, withLate, Xunit.TestContext.Current.CancellationToken);

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Equal(new SprintId(3), harness.Store.Get(task.Id)?.Sprint);
        Assert.Equal(withLate, await File.ReadAllTextAsync(harness.SprintsFilePath, Xunit.TestContext.Current.CancellationToken));
    }

    /// <summary>Revert and Cancel leave <c>sprints.md</c> as it was and drop the pending Sprint (spec §12.1, S20).</summary>
    [Fact]
    public async Task RevertAndCancel_LeaveSprintsFileUntouched()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await TypeSprintAsync(cut, "Release train");

        await cut.InvokeAsync(() => FindButton(cut, "Revert").ClickAsync());

        Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(string.Empty, SprintField(cut).Instance.GetState(x => x.Value) ?? string.Empty);
        await TypeSprintAsync(cut, "Release train");
        await cut.InvokeAsync(() => cut.Find(".task-detail-cancel").ClickAsync());
        Assert.Equal(TwoSprints, await File.ReadAllTextAsync(harness.SprintsFilePath, Xunit.TestContext.Current.CancellationToken));
        Assert.Null(harness.Store.Get(task.Id)?.Sprint);
    }

    /// <summary>After the last Task leaves a Sprint, its line is removed and the panel's list no longer offers its name (spec §12.1, E-S20).</summary>
    [Fact]
    public async Task MovingTheLastTaskOut_RemovesTheNameFromTheList()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem holder = Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft("Holder", "Platform", null, Sprint: new SprintId(2)), TaskActors.Human(harness.Options.Value))).Task;
        TaskItem viewed = CreateTask(harness, title: "Viewed");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, viewed.Id);
        Assert.Contains("Auth hardening", await SprintField(cut).Instance.SearchFunc!("", ct)!);

        TaskResult moved = harness.Service.Update(holder.Id, new TaskPatch { Sprint = Optional<SprintId?>.Set(null) }, null, TaskActors.Human(harness.Options.Value));

        Assert.IsType<TaskResult.Saved>(moved);
        Assert.Equal(["Sprint 1"], await SprintField(cut).Instance.SearchFunc!("", ct)!);
    }

    /// <summary>A Task whose Sprint is not in the file shows its id, a warning icon labelled "Not in sprints.md" and a Clear button; Clear then Save removes it (spec §12.1).</summary>
    [Fact]
    public async Task MissingSprint_ShowsIdWarningAndClear_ClearThenSaveRemovesIt()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0042", sprint: new SprintId(9)));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Equal("S9", SprintField(cut).Instance.GetState(x => x.Value));
        Assert.Equal("Not in sprints.md", cut.Find(".task-detail-sprint-missing").GetAttribute("aria-label"));
        Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => cut.Find(".task-detail-sprint-clear").ClickAsync());
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));
        Assert.Null(harness.Store.Get(task.Id)?.Sprint);
        Assert.Empty(cut.FindAll(".task-detail-sprint-missing"));
    }

    /// <summary>Editing another field of a Task with a missing Sprint saves without touching the Sprint (spec §14, E-S15).</summary>
    [Fact]
    public async Task MissingSprint_EditingAnotherField_KeepsTheMissingSprint()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0042", sprint: new SprintId(9)));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);
        await ChangePriorityAsync(cut, TaskPriority.Urgent);

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(new SprintId(9), harness.Store.Get(task.Id)?.Sprint);
        Assert.Equal(TaskPriority.Urgent, harness.Store.Get(task.Id)?.Priority);
    }

    /// <summary>The Save label still comes from <c>Preview</c>: a pending Sprint on a Task assigned to an Awake or Asleep Teammate reads "Save &amp; Notify" (spec §12.1, D-S8).</summary>
    [Fact]
    public async Task WakeLabel_FollowsPreview_WhenASprintIsPending()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken, sprintsText: TwoSprints);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        TaskItem task = CreateTask(harness, assignee: "Nova");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => SprintField(cut).Instance.ValueChanged.InvokeAsync("Sprint 1"));

        Assert.Equal("Save & notify Nova", FindButton(cut, "Save & notify Nova").TextContent.Trim());
        Assert.Equal("Nova is asleep and will be notified.", TextOf(cut, ".task-detail-wake-notice"));
    }

    /// <summary>The Sprint autocomplete: <c>CoerceValue</c> and <c>Clearable</c>, like the Project field (spec §12.1 Control).</summary>
    [Fact]
    public async Task SprintField_IsCoercingAndClearable()
    {
        using TaskToolHarness harness = new(sprintsText: TwoSprints);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        MudAutocomplete<string> field = SprintField(cut).Instance;

        Assert.True(field.CoerceValue);
        Assert.True(field.Clearable);
    }

    /// <summary>The Sprint row's autocomplete.</summary>
    private static IRenderedComponent<MudAutocomplete<string>> SprintField(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudAutocomplete<string>>().Single(m => (m.Instance.Class ?? string.Empty).Contains("task-detail-sprint-field", StringComparison.Ordinal));

    /// <summary>Types <paramref name="text"/> into the Sprint field's real input, the way a Human does.</summary>
    private static async Task TypeSprintAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        await cut.InvokeAsync(() => cut.Find(".task-detail-sprint-field input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = text }));

    /// <summary>Raises the Priority select's <c>ValueChanged</c>.</summary>
    private static async Task ChangePriorityAsync(IRenderedComponent<ContainerFragment> cut, TaskPriority priority)
    {
        IRenderedComponent<MudSelect<TaskPriority>> select = cut.FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(priority));
    }
}
