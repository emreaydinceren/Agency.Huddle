namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins the Task edit page (<see cref="TaskEditPage"/>, K of <c>docs/Huddle.TaskReadView-Specifications.md</c>): its header holds
/// Cancel and Save (D-5), Cancel closes the page's Tab (B6), the Tab is marked while edits are pending, the Right sidebar's Subject
/// names the saved Task and is withheld for a missing one (D-8), and a missing Task or the Tasks feature being off says so (K5, K6).
/// </summary>
public sealed class TaskEditPageTests
{
    /// <summary>Opens the edit Tab for <paramref name="idText"/> and renders the page inside it, as a Tab's page host does.</summary>
    private static IRenderedComponent<ContainerFragment> RenderInTab(MudBunitContext ctx, TaskToolHarness harness, string idText, out Tab tab, string suffix = "edit", string? team = null)
    {
        Tab opened = harness.Tabs.Open($"/tasks/item/{idText}/{suffix}");
        harness.Tabs.Keep(opened.Id);
        tab = opened;
        TabPageContext context = new(opened.Url, opened.Id);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<CascadingValue<TabPageContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<TabPageContext>.Value), context);
            builder.AddAttribute(2, nameof(CascadingValue<TabPageContext>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<TaskEditPage>(0);
                inner.AddAttribute(1, nameof(TaskEditPage.TaskIdText), idText);
                inner.AddAttribute(2, nameof(TaskEditPage.Team), team);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    /// <summary>Creates a Task through the real service, as a Human would.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title = "Edit me") =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft(title, "Platform", null), TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>Sets the editor's Priority through its select, the way the Tasks page tests add a pending edit.</summary>
    private static async Task EditPriorityAsync(IRenderedComponent<ContainerFragment> cut, TaskPriority value)
    {
        IRenderedComponent<MudSelect<TaskPriority>> select = cut.FindComponent<TaskDetail>().FindComponents<MudSelect<TaskPriority>>().Single();
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value));
    }

    /// <summary>A new Task opens blank in the same editor: "New task" header, no id line, no View task or Copy id, and Save is on at once because it is a change from the start.</summary>
    [Fact]
    public async Task New_OpensABlankEditorInTheTeam_WithSaveOn()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, "new", out _, team: "Platform");

        Assert.Equal("New task", cut.Find(".task-edit-page-header h1").TextContent.Trim());
        Assert.Empty(cut.FindAll(".task-edit-page-eyebrow"));
        Assert.Empty(cut.FindAll(".task-edit-page-view, .task-edit-page-copy-id"));
        Assert.False(cut.Find(".task-edit-page-header .task-detail-save").HasAttribute("disabled"));
        TaskDraft draft = cut.FindComponent<TaskDetail>().Instance.Draft ?? throw new InvalidOperationException("Draft missing.");
        Assert.Equal("Platform", draft.Team);
    }

    /// <summary>Saving a new Task creates it, and the same Tab becomes that Task's edit page.</summary>
    [Fact]
    public async Task New_Save_CreatesTheTask_AndTheTabBecomesItsEditPage()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, "new", out Tab tab, team: "Platform");
        IRenderedComponent<MudTextField<string>> title = cut.FindComponent<TaskDetail>().FindComponents<MudTextField<string>>()
            .Single(static field => string.Equals(field.Instance.Class, "task-detail-title", StringComparison.Ordinal));
        await cut.InvokeAsync(() => title.Instance.ValueChanged.InvokeAsync("Brand new task"));

        await cut.InvokeAsync(() => cut.Find(".task-edit-page-header .task-detail-save").ClickAsync());

        TaskItem created = Assert.Single(harness.Store.All);
        Assert.Equal("Brand new task", created.Title);
        Tab moved = Assert.Single(harness.Tabs.Tabs, t => string.Equals(t.Id, tab.Id, StringComparison.Ordinal));
        Assert.Equal($"task-edit:{created.Id}", moved.Key);
        Assert.Equal($"/tasks/item/{created.Id}/edit", moved.Url);
    }

    /// <summary>Cancel on a new Task creates nothing and closes its Tab.</summary>
    [Fact]
    public async Task New_Cancel_CreatesNothing_AndClosesTheTab()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, "new", out Tab tab, team: "Platform");

        await cut.InvokeAsync(() => cut.Find(".task-edit-page-header .task-detail-cancel").ClickAsync());

        Assert.Empty(harness.Store.All);
        Assert.DoesNotContain(harness.Tabs.Tabs, t => string.Equals(t.Id, tab.Id, StringComparison.Ordinal));
    }

    /// <summary>A copy starts as a new Task with "Copy of" the title and Backlog status, and creates nothing until it is saved.</summary>
    [Fact]
    public async Task Copy_StartsAsADraftOfTheTask_AndCreatesNothingYet()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Ship the thing");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out _, suffix: "copy");

        TaskDraft draft = cut.FindComponent<TaskDetail>().Instance.Draft ?? throw new InvalidOperationException("Draft missing.");
        Assert.Equal("Copy of Ship the thing", draft.Title);
        Assert.Equal(TaskState.Backlog, draft.Status);
        Assert.Equal(task.Location.Team, draft.Team);
        Assert.Single(harness.Store.All);
        Assert.Equal("New task", cut.Find(".task-edit-page-header h1").TextContent.Trim());
    }

    /// <summary>A copy of a Task that does not exist is the missing notice.</summary>
    [Fact]
    public async Task Copy_OfAMissingTask_ShowsTheMissingNotice()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, "PLAT-0999", out _, suffix: "copy");

        Assert.Equal("There is no Task PLAT-0999", cut.Find(".empty-state-title").TextContent.Trim());
    }

    /// <summary>The page header holds the Task's saved title as its h1 and the eyebrow with the id and Team above it (K2).</summary>
    [Fact]
    public async Task Header_NamesTheSavedTask()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Rewrite the parser");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out _);

        Assert.Equal("Rewrite the parser", cut.Find(".task-edit-page-header h1").TextContent.Trim());
        Assert.Equal($"{task.Id} · Platform", cut.Find(".task-edit-page-eyebrow").TextContent.Trim());
    }

    /// <summary>Cancel and Save sit in the header's last item, Cancel first and Save last, and Save is the one filled button (D-5, K2a).</summary>
    [Fact]
    public async Task Header_HoldsCancelThenSave_SaveFilled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out _);

        Assert.Equal(["Cancel", "Save"], cut.FindAll(".task-edit-page-header .task-edit-page-actions button").Select(static b => b.TextContent.Trim()));
        Assert.Equal(Variant.Filled, cut.FindComponents<MudButton>().Single(static b => string.Equals(b.Instance.Class, "task-detail-save", StringComparison.Ordinal)).Instance.Variant);
        Assert.Equal(Variant.Text, cut.FindComponents<MudButton>().Single(static b => string.Equals(b.Instance.Class, "task-detail-cancel", StringComparison.Ordinal)).Instance.Variant);
        Assert.Empty(cut.FindAll(".task-edit-page-editor .task-detail-actions"));
    }

    /// <summary>Save is off while nothing has changed and on once an edit is pending; saving writes the Task and turns it off again (B5).</summary>
    [Fact]
    public async Task Save_FollowsPendingEdits_AndWritesTheTask()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out Tab tab);
        Assert.True(cut.Find(".task-edit-page-header .task-detail-save").HasAttribute("disabled"));

        await EditPriorityAsync(cut, TaskPriority.Urgent);

        cut.WaitForAssertion(() => Assert.False(cut.Find(".task-edit-page-header .task-detail-save").HasAttribute("disabled")));
        Assert.Equal($"Edit {task.Id} ●", harness.Tabs.Tabs.Single(t => string.Equals(t.Id, tab.Id, StringComparison.Ordinal)).Title);
        Assert.True(harness.Tabs.IsDirty(tab.Key));

        await cut.InvokeAsync(() => cut.Find(".task-edit-page-header .task-detail-save").ClickAsync());

        Assert.Equal(TaskPriority.Urgent, harness.Store.Get(task.Id)?.Priority);
        cut.WaitForAssertion(() => Assert.True(cut.Find(".task-edit-page-header .task-detail-save").HasAttribute("disabled")));
        Assert.Equal($"Edit {task.Id}", harness.Tabs.Tabs.Single(t => string.Equals(t.Id, tab.Id, StringComparison.Ordinal)).Title);
    }

    /// <summary>Cancel discards the edits and closes the page's own Tab (B6).</summary>
    [Fact]
    public async Task Cancel_DiscardsEditsAndClosesTheTab()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out Tab tab);
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        await cut.InvokeAsync(() => cut.Find(".task-edit-page-header .task-detail-cancel").ClickAsync());

        Assert.Equal(TaskPriority.Medium, harness.Store.Get(task.Id)?.Priority);
        Assert.DoesNotContain(harness.Tabs.Tabs, t => string.Equals(t.Id, tab.Id, StringComparison.Ordinal));
    }

    /// <summary>View task opens the item route in its own Tab and leaves the edit Tab open (B13).</summary>
    [Fact]
    public async Task ViewTask_OpensTheItemRoute_AndKeepsTheEditTab()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out Tab tab);

        await cut.InvokeAsync(() => cut.Find(".task-edit-page-view").ClickAsync());

        Tab viewTab = Assert.Single(harness.Tabs.Tabs, t => string.Equals(t.Key, $"task:{task.Id}", StringComparison.Ordinal));
        Assert.Equal($"/tasks/item/{task.Id}", viewTab.Url);
        Assert.Equal(tab.Url, Assert.Single(harness.Tabs.Tabs, t => string.Equals(t.Id, tab.Id, StringComparison.Ordinal)).Url);
    }

    /// <summary>The page publishes a Subject naming the saved Task, with the flags the blocker panels read (D-8).</summary>
    [Fact]
    public async Task Subject_NamesTheSavedTask_WithItsBlockerFlags()
    {
        using TaskToolHarness harness = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem blocker = CreateTask(harness, "Blocker");
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Blocked", "Platform", null, BlockedBy: [blocker.Id]), human)).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        RenderInTab(ctx, harness, blocker.Id.ToString(), out Tab tab);

        TaskSubject subject = Assert.IsType<TaskSubject>(ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor(tab.Id));
        Assert.Equal(blocker.Id, subject.Id);
        Assert.False(subject.HasBlockers);
        Assert.True(subject.IsBlocking);
        Assert.Equal(task.Id, Assert.Single(harness.Store.BlockedOn(blocker.Id)).Id);
    }

    /// <summary>A Task that does not exist shows the missing notice with an Open Tasks action, and publishes no Subject (K5, D-8).</summary>
    [Fact]
    public async Task MissingTask_ShowsTheNoticeAndPublishesNoSubject()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, "PLAT-0999", out Tab tab);

        Assert.Equal("There is no Task PLAT-0999", cut.Find(".empty-state-title").TextContent.Trim());
        Assert.Equal("It may have been deleted or moved.", cut.Find(".empty-state-text").TextContent.Trim());
        Assert.Equal("Open Tasks", cut.Find(".task-edit-page-open-tasks").TextContent.Trim());
        Assert.Null(ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor(tab.Id));
        Assert.Empty(cut.FindAll(".task-edit-page-header"));
    }

    /// <summary>An id that does not parse is the same notice (K5).</summary>
    [Fact]
    public async Task UnparsableId_ShowsTheMissingNotice()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, "not-an-id", out _);

        Assert.Equal("There is no Task not-an-id", cut.Find(".empty-state-title").TextContent.Trim());
    }

    /// <summary>With Tasks turned off the page shows the Tasks page's notice, in the same words, and no editor (K6).</summary>
    [Fact]
    public async Task TasksOff_ShowsTheSameNoticeAsTheTasksPage()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        harness.Options.Value.Tasks.Enabled = false;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out _);

        Assert.Equal("Tasks are turned off.", cut.Find(".tasks-disabled-alert").TextContent.Trim());
        Assert.Equal("status", cut.Find(".tasks-disabled-alert").GetAttribute("role"));
        Assert.Empty(cut.FindAll(".task-edit-page-editor"));
    }

    /// <summary>The editor is the Expanded <see cref="TaskDetail"/> with its footer drawn by the page (D-3).</summary>
    [Fact]
    public async Task Editor_IsTheExpandedTaskDetailWithAnExternalFooter()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderInTab(ctx, harness, task.Id.ToString(), out _);

        TaskDetail detail = cut.Find(".task-edit-page-editor").ParentElement is not null ? cut.FindComponent<TaskDetail>().Instance : throw new InvalidOperationException("No editor.");
        Assert.Equal(TaskDetailMode.Expanded, detail.Mode);
        Assert.True(detail.ExternalFooter);
        Assert.Equal(task.Id, detail.Id);
    }
}
