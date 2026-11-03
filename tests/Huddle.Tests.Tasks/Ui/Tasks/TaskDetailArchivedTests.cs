using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>A Task of an archived Team opens read-only in the detail view (Spec 8.5, corrections-12 item 10).</summary>
public sealed class TaskDetailArchivedTests
{
    /// <summary>An id found only in the Archive shows the Archived chip and its title, and offers no field, menu, upload or footer control.</summary>
    [Fact]
    public async Task Render_ArchivedTask_IsReadOnlyWithArchivedChip()
    {
        using TaskToolHarness harness = new();
        TaskItem archived = TestTasks.Make(id: "HOME-0001", title: "Old plan", location: new TaskLocation("Home", null, false));
        _ = TestTaskStore.WriteTask(Path.Combine(harness.Options.Value.DataDir, "Archive"), TestTaskStore.RelativePath("Home", null, false, "HOME-0001.md"), archived);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)archived.Id);
            builder.CloseComponent();
        });

        Assert.Contains("Archived", cut.Find(".task-detail-archived-chip").TextContent, StringComparison.Ordinal);
        Assert.Contains("Old plan", cut.Find(".task-detail-archived-name").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("input"));
        Assert.Empty(cut.FindAll("textarea"));
        Assert.Empty(cut.FindAll("button"));
        Assert.Empty(cut.FindAll(".task-detail-actions"));
    }
}
