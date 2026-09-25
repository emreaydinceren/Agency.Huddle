namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins the "AI reacting" chip (Spec §13.8, §10.8): it shows only while one of the watched Tasks has
/// a <see cref="TaskActivity.LastWake"/> whose Room a Turn is currently running in
/// (<c>TurnActivity.IsBusyIn</c>), and it tracks that Turn starting and ending live. Renders against
/// <see cref="TaskToolHarness"/>'s real <see cref="TaskActivity"/>, <c>TurnActivity</c> and
/// <c>ITeamDirectory</c>, because the chip resolves a wake's Assignee Name to an Agent id through the
/// directory - the same "render for real" reasoning <c>MessageListTests</c> uses for its own store.
/// </summary>
public sealed class AiReactingChipTests
{
    /// <summary>No watched Task has a busy Room: the chip renders nothing.</summary>
    [Fact]
    public async Task NotReacting_NoWatchedTaskHasABusyRoom_RendersNothing()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = TaskId.TryParse("PLAT-0001", out TaskId taskId);
        harness.TaskActivity.Record(new WakeRecord(taskId, "Nova", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [taskId]);

        Assert.DoesNotContain("ai-reacting-chip", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A watched Task's last-wake Room has a Turn running: the chip appears.</summary>
    [Fact]
    public async Task Reacting_AWatchedTasksLastWakeRoomIsBusy_ShowsTheChip()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string novaId = RequireNovaId(harness);
        _ = TaskId.TryParse("PLAT-0001", out TaskId taskId);
        harness.TaskActivity.Record(new WakeRecord(taskId, "Nova", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [taskId]);

        harness.TurnActivity.Begin(novaId, "room-1");

        cut.WaitForAssertion(() => Assert.Contains("ai-reacting-chip", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>The Turn ending hides the chip again, live, without a manual re-render.</summary>
    [Fact]
    public async Task StopsReacting_WhenTheTurnEnds_HidesTheChip()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string novaId = RequireNovaId(harness);
        _ = TaskId.TryParse("PLAT-0001", out TaskId taskId);
        harness.TaskActivity.Record(new WakeRecord(taskId, "Nova", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow));
        harness.TurnActivity.Begin(novaId, "room-1");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [taskId]);
        cut.WaitForAssertion(() => Assert.Contains("ai-reacting-chip", cut.Markup, StringComparison.Ordinal));

        harness.TurnActivity.End(novaId, "room-1");

        cut.WaitForAssertion(() => Assert.DoesNotContain("ai-reacting-chip", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>Only one of several watched Tasks needs a busy Room for the chip to show.</summary>
    [Fact]
    public async Task Reacting_OnlyOneOfSeveralWatchedTasksIsBusy_StillShowsTheChip()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string novaId = RequireNovaId(harness);
        _ = TaskId.TryParse("PLAT-0001", out TaskId quietTask);
        _ = TaskId.TryParse("PLAT-0002", out TaskId busyTask);
        harness.TaskActivity.Record(new WakeRecord(quietTask, "Nova", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow));
        harness.TaskActivity.Record(new WakeRecord(busyTask, "Nova", "room-2", "Other", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [quietTask, busyTask]);

        harness.TurnActivity.Begin(novaId, "room-2");

        cut.WaitForAssertion(() => Assert.Contains("ai-reacting-chip", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>A watched Task with no recorded wake at all does not throw and shows no chip.</summary>
    [Fact]
    public async Task NotReacting_WatchedTaskHasNoRecordedWake_RendersNothing()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = TaskId.TryParse("PLAT-0001", out TaskId taskId);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [taskId]);

        Assert.DoesNotContain("ai-reacting-chip", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A wake recorded with no Room chosen (Spec §10.5, <c>Failed</c> with no Room) does not throw and shows no chip.</summary>
    [Fact]
    public async Task NotReacting_LastWakeHasNoRoomId_DoesNotThrowAndRendersNothing()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = TaskId.TryParse("PLAT-0001", out TaskId taskId);
        harness.TaskActivity.Record(new WakeRecord(taskId, "Nova", null, null, WakeOutcome.Failed, DateTimeOffset.UtcNow));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [taskId]);

        Assert.DoesNotContain("ai-reacting-chip", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An Assignee Name the directory doesn't know (an outside-Huddle actor, or a stale Name) does not throw and shows no chip.</summary>
    [Fact]
    public async Task NotReacting_AssigneeIsUnknownToTheDirectory_DoesNotThrowAndRendersNothing()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = TaskId.TryParse("PLAT-0001", out TaskId taskId);
        harness.TaskActivity.Record(new WakeRecord(taskId, "Ghost", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderChip(ctx, harness, [taskId]);

        Assert.DoesNotContain("ai-reacting-chip", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Narrows <see cref="TaskToolHarness.NovaId"/>, seeded by <see cref="TaskToolHarness.CreateAsync"/>, to a non-null Agent id.</summary>
    private static string RequireNovaId(TaskToolHarness harness)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return novaId;
    }

    /// <summary>Registers <paramref name="harness"/>'s real services into <paramref name="ctx"/> and renders the chip watching <paramref name="watched"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderChip(MudBunitContext ctx, TaskToolHarness harness, IReadOnlyList<TaskId> watched)
    {
        harness.AddTo(ctx.Services);

        return ctx.Render(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Tasks.AiReactingChip>(0);
            builder.AddAttribute(1, nameof(Agency.Huddle.App.Components.Tasks.AiReactingChip.Watched), watched);
            builder.CloseComponent();
        });
    }
}
