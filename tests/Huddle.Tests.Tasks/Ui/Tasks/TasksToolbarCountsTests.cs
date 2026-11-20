namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins the Tasks page toolbar's "N open · N need you · N all" and the Top bar's "N Tasks need you" pill: "need you"
/// counts a gated Task still waiting in Needs You whoever holds it (once, even when it is the Human's too), "all" is
/// every Task of this route, and neither reaches into another Team or Project.
/// </summary>
public sealed class TasksToolbarCountsTests
{
    private static readonly TaskLocation AlphaProject = new("Platform", "Alpha", false);

    /// <summary>The issue's scenario: four Tasks in the Project, two gated and waiting in Needs You, more in another Project and another Team.</summary>
    [Fact]
    public async Task ProjectRoute_CountsOpenNeedYouAndAllOfThisProjectOnly()
    {
        using TaskToolHarness harness = new();
        _ = harness.SeedOnDisk(Gated("PLAT-0001", "Nova"));
        _ = harness.SeedOnDisk(Gated("PLAT-0002", "Kai"));
        _ = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0003", status: TaskState.ToDo, assignee: "Nova", location: AlphaProject));
        _ = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0004", status: TaskState.InProgress, assignee: "Kai", location: AlphaProject));
        _ = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0005", status: TaskState.NeedsYou, assignee: "Nova", location: new("Platform", "Beta", false)) with { HumanGate = true });
        _ = harness.SeedOnDisk(TestTasks.Make(id: "BUSI-0001", status: TaskState.NeedsYou, assignee: "Nova", location: new("Business", "Alpha", false)) with { HumanGate = true });
        _ = harness.SeedOnDisk(TestTasks.Make(id: "BUSI-0002", status: TaskState.ToDo, assignee: "Nova", location: new("Business", null, false)));
        _ = harness.SeedOnDisk(TestTasks.Make(id: "BUSI-0003", status: TaskState.ToDo, assignee: "Nova", location: new("Business", null, false)));

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderProjectPage(ctx);

        Assert.Equal("4", cut.Find(".tasks-count-open strong").TextContent);
        Assert.Equal("0", cut.Find(".tasks-count-need-you strong").TextContent);
        Assert.Equal("4", cut.Find(".tasks-count-all strong").TextContent);
    }

    /// <summary>A gated Task assigned to an Agent is not "assigned to you": it waits in the Needs You column, not in this count.</summary>
    [Fact]
    public async Task AssignedToYou_GatedAgentTaskWaitingInNeedsYou_DoesNotCount()
    {
        using TaskToolHarness harness = new();
        _ = harness.SeedOnDisk(Gated("PLAT-0001", "Nova"));

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderProjectPage(ctx);

        Assert.Equal("0", cut.Find(".tasks-count-need-you strong").TextContent);
    }

    /// <summary>An open Task assigned to the Human counts, and the count is labelled "assigned to you", not "need you".</summary>
    [Fact]
    public async Task AssignedToYou_OpenTaskOfTheHuman_CountsAndIsLabelled()
    {
        using TaskToolHarness harness = new();
        _ = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: harness.Options.Value.HumanName, location: AlphaProject));

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderProjectPage(ctx);

        Assert.Equal("1", cut.Find(".tasks-count-need-you strong").TextContent);
        Assert.Equal("1 assigned to you", cut.Find(".tasks-count-need-you").TextContent.Trim());
    }

    /// <summary>The pill counts only Tasks waiting on the Human's answer: an open Task merely assigned to the Human does not make it show.</summary>
    [Fact]
    public async Task Pill_OpenTaskAssignedToTheHuman_RendersNothing()
    {
        using TaskToolHarness harness = new();
        _ = harness.SeedOnDisk(TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: harness.Options.Value.HumanName, location: AlphaProject));

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<TasksNeedYouPill> cut = ctx.Render<TasksNeedYouPill>();

        Assert.Empty(cut.FindAll(".tasks-need-you-pill"));
    }

    /// <summary>The same Task once accepted at its current content no longer counts, though it is still in Needs You.</summary>
    [Fact]
    public async Task NeedYou_AcceptedTaskStillInNeedsYou_DoesNotCount()
    {
        using TaskToolHarness harness = new();
        TaskItem waiting = Gated("PLAT-0001", "Nova");
        string fingerprint = ContentFingerprint.Of(waiting.Title, waiting.Action, waiting.Description);
        _ = harness.SeedOnDisk(waiting with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", "status: Review → Needs You (Proof 4/4)"), TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "accepted " + fingerprint)] });

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderProjectPage(ctx);

        Assert.Equal("0", cut.Find(".tasks-count-need-you strong").TextContent);
        Assert.Equal("1", cut.Find(".tasks-count-open strong").TextContent);
    }

    /// <summary>A Task that is both the Human's and gated in Needs You counts once, not twice.</summary>
    [Fact]
    public async Task NeedYou_TaskAssignedToTheHumanAndGated_CountsOnce()
    {
        using TaskToolHarness harness = new();
        _ = harness.SeedOnDisk(Gated("PLAT-0001", harness.Options.Value.HumanName));

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderProjectPage(ctx);

        Assert.Equal("1", cut.Find(".tasks-count-need-you strong").TextContent);
    }

    /// <summary>The Top bar's pill counts a gated Agent-assigned Task waiting in Needs You, across the whole store.</summary>
    [Fact]
    public async Task Pill_GatedAgentTaskWaitingInNeedsYou_CountsIt()
    {
        using TaskToolHarness harness = new();
        _ = harness.SeedOnDisk(Gated("PLAT-0001", "Nova"));

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<TasksNeedYouPill> cut = ctx.Render<TasksNeedYouPill>();

        Assert.Equal("1 Task needs you", cut.Find(".tasks-need-you-pill").TextContent.Trim());
    }

    /// <summary>The pill does not count a Task that was accepted.</summary>
    [Fact]
    public async Task Pill_AcceptedTask_RendersNothing()
    {
        using TaskToolHarness harness = new();
        TaskItem waiting = Gated("PLAT-0001", "Nova");
        string fingerprint = ContentFingerprint.Of(waiting.Title, waiting.Action, waiting.Description);
        _ = harness.SeedOnDisk(waiting with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", "status: Review → Needs You (Proof 4/4)"), TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "accepted " + fingerprint)] });

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<TasksNeedYouPill> cut = ctx.Render<TasksNeedYouPill>();

        Assert.Empty(cut.FindAll(".tasks-need-you-pill"));
    }

    /// <summary>Builds a gated Task waiting in Needs You, in the Platform / Alpha Project.</summary>
    private static TaskItem Gated(string id, string assignee) =>
        TestTasks.Make(id: id, status: TaskState.NeedsYou, assignee: assignee, location: AlphaProject) with { HumanGate = true };

    /// <summary>Renders the Tasks page on the Platform / Alpha Project route, showing the All Tasks View.</summary>
    private static IRenderedComponent<ContainerFragment> RenderProjectPage(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), ViewStore.AllTasksId);
            builder.AddAttribute(2, nameof(TasksPage.Team), "Platform");
            builder.AddAttribute(3, nameof(TasksPage.Project), "Alpha");
            builder.CloseComponent();
        });
}
