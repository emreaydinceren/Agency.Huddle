namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Smart wakes spec 5.4 (SW-T6 first clause and correction 6) in <c>TaskDetail</c>: the Save label and the wake notice follow
/// <see cref="TaskTriggerService.Preview(TaskItem?, TaskItem, TaskActor)"/>, so a pending edit of only silent fields (rank) reads plain "Save"
/// with no notice, while an edit that wakes the assignee still reads "Save &amp; notify Nova". Create mode passes no before, so a new Task
/// assigned to an Agent still notifies.
/// </summary>
public sealed class TaskDetailWakePreviewTests
{
    /// <summary>The Rank number field of the rendered Task detail.</summary>
    /// <param name="cut">The rendered Task detail.</param>
    private static IRenderedComponent<MudNumericField<int?>> RankField(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudNumericField<int?>>().Single(field => string.Equals(field.Instance.Class, "task-detail-rank", StringComparison.Ordinal));

    /// <summary>Builds a harness whose assignee Nova is registered and online (asleep), and a Task assigned to her.</summary>
    private static async Task<(TaskToolHarness Harness, TaskItem Task)> ArrangeAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        return (harness, CreateTask(harness, assignee: "Nova"));
    }

    /// <summary>
    /// A pending rank edit on a Task assigned to an asleep Agent is <see cref="WakeBlock.NothingWakes"/>: the Save button reads plain "Save" and
    /// there is no wake notice. Adding a Title edit to it brings back "Save &amp; notify Nova" and the notice, since one waking field is enough.
    /// </summary>
    [Fact]
    public async Task PendingRankEdit_ReadsPlainSaveWithNoNotice_UntilAWakingEditJoinsIt()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        (TaskToolHarness harness, TaskItem task) = await ArrangeAsync(ct);
        using TaskToolHarness owned = harness;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => RankField(cut).Instance.ValueChanged.InvokeAsync(7));

        Assert.Equal("Save", FindButton(cut, "Save").TextContent.Trim());
        Assert.Empty(cut.FindAll(".task-detail-wake-notice"));

        await cut.InvokeAsync(() => cut.Find("button[aria-label=\"Edit title\"]").ClickAsync());
        IRenderedComponent<MudTextField<string>> titleField = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, "task-detail-title", StringComparison.Ordinal));
        await cut.InvokeAsync(() => titleField.Instance.ValueChanged.InvokeAsync("Retitled by the Human"));

        Assert.Equal("Save & notify Nova", FindButton(cut, "Save & notify Nova").TextContent.Trim());
        Assert.Equal("Nova is asleep and will be notified.", TextOf(cut, ".task-detail-wake-notice"));
    }

    /// <summary>The footer's Save label (the one a dialog host draws) follows the same rule: a pending rank edit is plain "Save", not "Save &amp; notify Nova".</summary>
    [Fact]
    public async Task FooterSaveLabel_PendingRankEdit_IsPlainSave()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        (TaskToolHarness harness, TaskItem task) = await ArrangeAsync(ct);
        using TaskToolHarness owned = harness;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => RankField(cut).Instance.ValueChanged.InvokeAsync(7));

        Assert.Equal("Save", cut.FindComponent<TaskDetail>().Instance.FooterSaveLabel);
    }

    /// <summary>
    /// Create mode passes no before, so a new Task assigned to an asleep Agent reads "Save &amp; notify Nova" with the notice, though the draft
    /// has no pending edit (the draft is the Task before and after). A create always wakes (spec 3.2).
    /// </summary>
    [Fact]
    public async Task CreateMode_DraftAssignedToAnAgent_ReadsSaveAndNotify()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        TaskDraft draft = new("Brand new", "Platform", null, Assignee: "Nova");

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Draft), draft);
            builder.CloseComponent();
        });

        Assert.Equal("Save & notify Nova", FindButton(cut, "Save & notify Nova").TextContent.Trim());
        Assert.Equal("Nova is asleep and will be notified.", TextOf(cut, ".task-detail-wake-notice"));
        Assert.Equal("Save & notify Nova", cut.FindComponent<TaskDetail>().Instance.FooterSaveLabel);
    }
}
