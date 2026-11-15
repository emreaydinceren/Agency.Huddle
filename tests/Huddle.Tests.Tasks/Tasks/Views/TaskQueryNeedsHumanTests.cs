using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// Tests for <see cref="TaskQuery.WaitsOnHuman"/> and <see cref="TaskQuery.NeedsHuman"/>, the rules behind the Tasks page's
/// "need you" count and the Top bar's "N Tasks need you" pill: a gated Task still waiting in Needs You counts whoever
/// it is assigned to, and an open Task assigned to the Human counts too.
/// </summary>
public sealed class TaskQueryNeedsHumanTests
{
    /// <summary>A Task that is gated and in Needs You with no acceptance waits on the Human, whatever its assignee.</summary>
    /// <param name="action">The Task's Action, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Send the invoice")]
    public void WaitsOnHuman_GatedInNeedsYouNotAccepted_IsTrue(string? action)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.NeedsYou, assignee: "Nova") with { HumanGate = true, Action = action };

        Assert.True(TaskQuery.WaitsOnHuman(task));
    }

    /// <summary>A Task accepted at its current content no longer waits, though it is still in Needs You.</summary>
    [Fact]
    public void WaitsOnHuman_AcceptedAtCurrentContent_IsFalse()
    {
        TaskItem waiting = TestTasks.Make(id: "PLAT-0001", status: TaskState.NeedsYou, assignee: "Nova") with { HumanGate = true, Action = "Send the invoice" };
        string fingerprint = ContentFingerprint.Of(waiting.Title, waiting.Action, waiting.Description);
        TaskItem accepted = waiting with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", "status: Review → Needs You (Proof 4/4)"), TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "accepted " + fingerprint)] };

        Assert.False(TaskQuery.WaitsOnHuman(accepted));
    }

    /// <summary>A Task in Needs You whose gate is off does not wait.</summary>
    [Fact]
    public void WaitsOnHuman_UngatedInNeedsYou_IsFalse()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.NeedsYou, assignee: "Nova");

        Assert.False(TaskQuery.WaitsOnHuman(task));
    }

    /// <summary>A gated Task outside Needs You does not wait.</summary>
    [Fact]
    public void WaitsOnHuman_GatedButNotInNeedsYou_IsFalse()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.Review, assignee: "Nova") with { HumanGate = true };

        Assert.False(TaskQuery.WaitsOnHuman(task));
    }

    /// <summary>A gated Agent-assigned Task waiting in Needs You needs the Human.</summary>
    [Fact]
    public void NeedsHuman_GatedAgentTaskWaitingInNeedsYou_IsTrue()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.NeedsYou, assignee: "Nova") with { HumanGate = true };

        Assert.True(TaskQuery.NeedsHuman(task, "Sam"));
    }

    /// <summary>An open Task assigned to the Human needs them, as before.</summary>
    [Fact]
    public void NeedsHuman_OpenTaskAssignedToTheHuman_IsTrue()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Sam");

        Assert.True(TaskQuery.NeedsHuman(task, "Sam"));
    }

    /// <summary>An ordinary Agent-assigned To Do Task does not need the Human.</summary>
    [Fact]
    public void NeedsHuman_AgentToDoTask_IsFalse()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "Nova");

        Assert.False(TaskQuery.NeedsHuman(task, "Sam"));
    }

    /// <summary>An accepted Agent-assigned Task still in Needs You does not need the Human.</summary>
    [Fact]
    public void NeedsHuman_AcceptedAgentTaskInNeedsYou_IsFalse()
    {
        TaskItem waiting = TestTasks.Make(id: "PLAT-0001", status: TaskState.NeedsYou, assignee: "Nova") with { HumanGate = true };
        string fingerprint = ContentFingerprint.Of(waiting.Title, waiting.Action, waiting.Description);
        TaskItem accepted = waiting with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", "status: Review → Needs You (Proof 4/4)"), TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "accepted " + fingerprint)] };

        Assert.False(TaskQuery.NeedsHuman(accepted, "Sam"));
    }
}
