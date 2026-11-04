using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>
/// Pins the four Right sidebar panels a Task open in its edit page gets (read view spec D-8): Activity, Blocked by, Blocks and
/// Linked documents. Each reads the Task as stored, so each redraws on a <see cref="TaskEvents"/> change and none of them can see
/// an unsaved edit in the form.
/// </summary>
public sealed class TaskPanelsTests
{
    /// <summary>Creates a Task through the real service.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title, IReadOnlyList<TaskId>? blockedBy = null, string description = "") =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(
            new TaskDraft(title, "Platform", null, BlockedBy: blockedBy, Description: description),
            TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>Renders <typeparamref name="TPanel"/> for the Task in the frame's context, which receives the panel's count.</summary>
    private static IRenderedComponent<ContainerFragment> Render<TPanel>(MudBunitContext ctx, SidebarPanelContext panel, TaskId id)
        where TPanel : IComponent =>
        ctx.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<SidebarPanelContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<SidebarPanelContext>.Value), panel);
            builder.AddComponentParameter(2, nameof(CascadingValue<SidebarPanelContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<SidebarPanelContext>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent(0, typeof(TPanel));
                inner.AddComponentParameter(1, "Subject", new TaskSubject(id));
                inner.AddComponentParameter(2, "TabId", "t1");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

    /// <summary>The Task panel draws the Task's read view with its id line and title, since the Right sidebar has no header of its own for it, and no form control.</summary>
    [Fact]
    public async Task Detail_DrawsTheReadViewWithItsIdLineAndTitle()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Legal review of price claims");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = ctx.Render(builder =>
        {
            builder.OpenComponent<TaskDetailPanel>(0);
            builder.AddComponentParameter(1, "Subject", new TaskDetailSubject(task.Id));
            builder.AddComponentParameter(2, "TabId", "t1");
            builder.CloseComponent();
        });

        Assert.Equal($"{task.Id} · Platform", cut.Find(".task-read-view-eyebrow").TextContent.Trim());
        Assert.Equal("Legal review of price claims", cut.Find(".task-read-view-title").TextContent.Trim());
        Assert.Empty(cut.FindAll(".task-read-view input, .task-read-view textarea, .task-read-view select"));
    }

    /// <summary>The Task panel renders nothing for a Subject that is not a selected Task.</summary>
    [Fact]
    public async Task Detail_AnotherSubject_RendersNothing()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Anything");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = ctx.Render(builder =>
        {
            builder.OpenComponent<TaskDetailPanel>(0);
            builder.AddComponentParameter(1, "Subject", new TaskSubject(task.Id));
            builder.AddComponentParameter(2, "TabId", "t1");
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll(".task-read-view"));
    }

    /// <summary>Blocked by lists each blocker with its id, title and state, and counts the ones still open.</summary>
    [Fact]
    public async Task Blockers_ListsEachBlocker_AndCountsTheOpenOnes()
    {
        using TaskToolHarness harness = new();
        TaskItem open = CreateTask(harness, "Open blocker");
        TaskItem done = CreateTask(harness, "Done blocker");
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(done.Id, new TaskPatch { Status = TaskState.Done }, done.Version, TaskActors.Human(harness.Options.Value)));
        TaskItem task = CreateTask(harness, "Blocked", [open.Id, done.Id]);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();

        IRenderedComponent<ContainerFragment> cut = Render<TaskBlockersPanel>(ctx, panel, task.Id);

        Assert.Equal(["Open blocker", "Done blocker"], cut.FindAll(".room-tasks-row a").Select(static a => a.TextContent.Trim()));
        Assert.Equal($"/tasks/item/{open.Id}", cut.FindAll(".room-tasks-row a")[0].GetAttribute("href"));
        Assert.Equal(1, panel.Count);
    }

    /// <summary>The count follows the blockers: finishing the open one drops it to zero.</summary>
    [Fact]
    public async Task Blockers_ABlockerFinishes_TheCountRedraws()
    {
        using TaskToolHarness harness = new();
        TaskItem blocker = CreateTask(harness, "Open blocker");
        TaskItem task = CreateTask(harness, "Blocked", [blocker.Id]);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();
        IRenderedComponent<ContainerFragment> cut = Render<TaskBlockersPanel>(ctx, panel, task.Id);
        Assert.Equal(1, panel.Count);

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(blocker.Id, new TaskPatch { Status = TaskState.Done }, blocker.Version, TaskActors.Human(harness.Options.Value)));

        cut.WaitForAssertion(() => Assert.Equal(0, panel.Count));
    }

    /// <summary>Blocks lists the open Tasks whose Blocked by names this one, and leaves out a finished one.</summary>
    [Fact]
    public async Task Blocks_ListsTheOpenTasksWaitingOnThisOne()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Foundation");
        _ = CreateTask(harness, "Waiting one", [task.Id]);
        TaskItem finished = CreateTask(harness, "Waiting but done", [task.Id]);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(finished.Id, new TaskPatch { Status = TaskState.Done }, finished.Version, TaskActors.Human(harness.Options.Value)));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();

        IRenderedComponent<ContainerFragment> cut = Render<TaskBlocksPanel>(ctx, panel, task.Id);

        Assert.Equal(["Waiting one"], cut.FindAll(".room-tasks-row a").Select(static a => a.TextContent.Trim()));
        Assert.Equal(1, panel.Count);
    }

    /// <summary>A new Task that names this one redraws the panel (a <see cref="TaskEvents"/> change), with no reload.</summary>
    [Fact]
    public async Task Blocks_ANewDependantAppears_ThePanelRedraws()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Foundation");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();
        IRenderedComponent<ContainerFragment> cut = Render<TaskBlocksPanel>(ctx, panel, task.Id);
        Assert.Empty(cut.FindAll(".room-tasks-row"));

        _ = CreateTask(harness, "Late dependant", [task.Id]);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".room-tasks-row")));
    }

    /// <summary>With no wake and no budget spent, Activity says so.</summary>
    [Fact]
    public async Task Activity_NeverWoken_SaysSo()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Quiet");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render<TaskActivityPanel>(ctx, new SidebarPanelContext(), task.Id);

        Assert.Equal("Not woken yet.", cut.Find(".task-activity-empty").TextContent.Trim());
    }

    /// <summary>A recorded wake shows the Room as a link with its time, and the budget row; the panel redraws when the wake is recorded.</summary>
    [Fact]
    public async Task Activity_AWakeIsRecorded_ShowsTheRoomAndTheBudget()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, "Woken");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render<TaskActivityPanel>(ctx, new SidebarPanelContext(), task.Id);

        harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", "room-1", "General", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        cut.WaitForAssertion(() => Assert.Equal("Woken in Room General", cut.Find(".task-activity-woken a").TextContent.Trim()));
        Assert.Equal("/rooms/room-1", cut.Find(".task-activity-woken a").GetAttribute("href"));
        Assert.Equal($"Wakes: 0 of {harness.TaskActivity.Budget(task.Id).Granted}", cut.Find(".task-activity-budget").TextContent.Trim());
        Assert.Empty(cut.FindAll(".task-activity-budget-exhausted"));
    }

    /// <summary>A hand-off a spent budget refused says who is waiting and why, and never reads as "Woken in Room".</summary>
    [Fact]
    public async Task Activity_AWakeWasRefused_SaysItIsWaitingAndNotWoken()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, "Refused");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render<TaskActivityPanel>(ctx, new SidebarPanelContext(), task.Id);

        harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", "room-1", "General", WakeOutcome.BudgetSpent, DateTimeOffset.UtcNow));

        cut.WaitForAssertion(() => Assert.Equal("Waiting to wake Nova: Room General is paused", cut.Find(".task-activity-held a").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".task-activity-woken"));

        harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", null, null, WakeOutcome.WakePaused, DateTimeOffset.UtcNow));

        cut.WaitForAssertion(() => Assert.Equal("Waiting to wake Nova: Allow more on this Task", cut.Find(".task-activity-held .sidebar-row-text").TextContent.Trim()));
    }

    /// <summary>A spent budget is marked, so the row can wear Error ink.</summary>
    [Fact]
    public async Task Activity_BudgetSpent_IsMarkedExhausted()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Looping");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        for (int count = 0; count < harness.TaskActivity.Budget(task.Id).Granted; count++)
        {
            _ = harness.TaskActivity.TryConsumeAgentWake(task.Id);
        }

        IRenderedComponent<ContainerFragment> cut = Render<TaskActivityPanel>(ctx, new SidebarPanelContext(), task.Id);

        Assert.Single(cut.FindAll(".task-activity-budget-exhausted"));
    }

    /// <summary>
    /// Linked documents lists the Library files the saved Description names, once each, with the file name as the link and the folder as the
    /// tooltip; a file that is gone is a Quiet row marked "Not found".
    /// </summary>
    [Fact]
    public async Task Documents_ListsTheLibraryFilesTheDescriptionLinks()
    {
        using TaskToolHarness harness = new();
        const string present = "file:///notes/plan/Design.md";
        const string gone = "file:///notes/plan/Old.md";
        TaskItem task = CreateTask(harness, "Linked", description: $"See {present} and {gone} and {present} again.");
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver(
            resolvePath: path => path.EndsWith("Design.md", StringComparison.Ordinal)
                ? new LibraryReference("notes", "plan/Design.md", Exists: true)
                : new LibraryReference("notes", "plan/Old.md", Exists: false)))).WithHarness(harness);
        SidebarPanelContext panel = new();

        IRenderedComponent<ContainerFragment> cut = Render<TaskDocumentsPanel>(ctx, panel, task.Id);

        Assert.Equal(2, cut.FindAll(".task-documents-row").Count);
        Assert.Equal("Design.md", cut.Find(".task-documents-row a").TextContent.Trim());
        Assert.Equal("plan", cut.Find(".task-documents-row").GetAttribute("title"));
        Assert.Equal("Not found", cut.FindAll(".task-documents-row")[1].QuerySelector(".sidebar-row-meta")?.TextContent.Trim());
        Assert.Equal(2, panel.Count);
    }

    /// <summary>A rooted POSIX path written as a whole code span is a link in the Description preview, so it is a row here too.</summary>
    [Fact]
    public async Task Documents_PosixPathInACodeSpan_IsListed()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Spanned", description: "See `/notes/plan/Design.md` for the plan.");
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver(
            resolvePath: _ => new LibraryReference("notes", "plan/Design.md", Exists: true)))).WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render<TaskDocumentsPanel>(ctx, new SidebarPanelContext(), task.Id);

        Assert.Equal("Design.md", cut.Find(".task-documents-row a").TextContent.Trim());
    }

    /// <summary>A Description with no Library path says so.</summary>
    [Fact]
    public async Task Documents_NoLinks_SaysSo()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Plain", description: "Nothing linked here.");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = Render<TaskDocumentsPanel>(ctx, new SidebarPanelContext(), task.Id);

        Assert.Equal("The description links to no files.", cut.Find("[role='status']").TextContent.Trim());
    }

    /// <summary>The panels show the Task as stored: a change saved elsewhere moves them, and they have no way to see a pending edit in a form.</summary>
    [Fact]
    public async Task Documents_ADescriptionIsSaved_ThePanelFollowsTheStoredTask()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, "Plain", description: "Nothing yet.");
        const string path = "file:///notes/Added.md";
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver(
            resolvePath: _ => new LibraryReference("notes", "Added.md", Exists: true)))).WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = Render<TaskDocumentsPanel>(ctx, new SidebarPanelContext(), task.Id);
        Assert.Empty(cut.FindAll(".task-documents-row"));

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Description = $"Now see {path}." }, task.Version, TaskActors.Human(harness.Options.Value)));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".task-documents-row")));
    }
}
