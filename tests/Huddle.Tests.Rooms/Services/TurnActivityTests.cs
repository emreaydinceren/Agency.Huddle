using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Services;

/// <summary>Tests for <see cref="TurnActivity"/> (Spec §10.8).</summary>
public sealed class TurnActivityTests
{
    private static readonly TaskId First = new("PLAT", 1);
    private static readonly TaskId Second = new("PLAT", 2);

    /// <summary>After <see cref="TurnActivity.Begin"/>, both <see cref="TurnActivity.IsBusy"/> and <see cref="TurnActivity.IsBusyIn"/> report the Turn.</summary>
    [Fact]
    public void Begin_ThenIsBusyAndIsBusyIn()
    {
        TurnActivity activity = new();

        activity.Begin("agent-1", "room-a");

        Assert.True(activity.IsBusy("agent-1"));
        Assert.True(activity.IsBusyIn("agent-1", "room-a"));
        Assert.False(activity.IsBusyIn("agent-1", "room-b"));
    }

    /// <summary>
    /// <see cref="TurnActivity.SoleRoom"/> names the Room only while the Agent has exactly one Turn running: none, or Turns in
    /// two Rooms at once, give no answer, because then a tool call cannot be tied to one Room.
    /// </summary>
    [Fact]
    public void SoleRoom_NamesTheRoomOnlyForExactlyOneRunningRoom()
    {
        TurnActivity activity = new();
        Assert.Null(activity.SoleRoom("agent-1"));

        activity.Begin("agent-1", "room-a");
        activity.Begin("agent-2", "room-b");
        Assert.Equal("room-a", activity.SoleRoom("agent-1"));

        activity.Begin("agent-1", "room-b");
        Assert.Null(activity.SoleRoom("agent-1"));

        activity.End("agent-1", "room-a");
        Assert.Equal("room-b", activity.SoleRoom("agent-1"));
    }

    /// <summary>Ending a Turn in one Room leaves a Turn in another Room for the same Agent untouched.</summary>
    [Fact]
    public void End_ClearsOnlyThatRoom()
    {
        TurnActivity activity = new();
        activity.Begin("agent-1", "room-a");
        activity.Begin("agent-1", "room-b");

        activity.End("agent-1", "room-a");

        Assert.False(activity.IsBusyIn("agent-1", "room-a"));
        Assert.True(activity.IsBusyIn("agent-1", "room-b"));
        Assert.True(activity.IsBusy("agent-1"));
    }

    /// <summary>Ending a Turn that was never begun neither throws nor raises <see cref="TurnActivity.Changed"/>.</summary>
    [Fact]
    public void End_Unknown_NoThrowNoEvent()
    {
        TurnActivity activity = new();
        var raised = false;
        activity.Changed += () => raised = true;

        activity.End("agent-1", "room-a");

        Assert.False(raised);
        Assert.False(activity.IsBusy("agent-1"));
    }

    /// <summary>A single Begin/End pair raises <see cref="TurnActivity.Changed"/> exactly twice, once per transition.</summary>
    [Fact]
    public void Changed_RaisedOncePerTransition()
    {
        TurnActivity activity = new();
        var count = 0;
        activity.Changed += () => count++;

        activity.Begin("agent-1", "room-a");
        activity.End("agent-1", "room-a");

        Assert.Equal(2, count);
    }

    /// <summary>
    /// A <see cref="TurnActivity.Changed"/> handler that calls back into <see cref="TurnActivity.IsBusy"/>
    /// from another thread does not deadlock, proving the event is raised outside the lock.
    /// </summary>
    [Fact]
    public void Changed_HandlerCanCallBackIn()
    {
        TurnActivity activity = new();
        activity.Changed += () =>
        {
            var completed = Task.Run(() => activity.IsBusy("agent-1")).Wait(TimeSpan.FromSeconds(2));
            Assert.True(completed);
        };

        activity.Begin("agent-1", "room-a");
    }

    /// <summary>
    /// D8 correction 7: a per-(Agent, Room) counter, not a single mark - a second <see cref="TurnActivity.Begin"/>
    /// for the same pair (a new session's Turn) must not be cleared by an <see cref="TurnActivity.End"/>
    /// that belongs to an earlier, already-superseded Turn (a late signal from an old session).
    /// </summary>
    [Fact]
    public void End_AfterASecondBegin_LeavesItBusy()
    {
        TurnActivity activity = new();
        activity.Begin("agent-1", "room-a");
        activity.Begin("agent-1", "room-a");

        activity.End("agent-1", "room-a");

        Assert.True(activity.IsBusyIn("agent-1", "room-a"));

        activity.End("agent-1", "room-a");

        Assert.False(activity.IsBusyIn("agent-1", "room-a"));
    }

    /// <summary>
    /// <see cref="TurnActivity.ClearAgent"/> drops every outstanding mark for that Agent, across every
    /// Room, and leaves every other Agent's marks untouched - the startup self-heal
    /// <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> calls beside <c>OwnPosts.ClearAgent</c>.
    /// </summary>
    [Fact]
    public void ClearAgent_DropsThatAgentEverywhereAndLeavesOthersAlone()
    {
        TurnActivity activity = new();
        activity.Begin("agent-1", "room-a");
        activity.Begin("agent-1", "room-b");
        activity.Begin("agent-2", "room-a");

        activity.ClearAgent("agent-1");

        Assert.False(activity.IsBusyIn("agent-1", "room-a"));
        Assert.False(activity.IsBusyIn("agent-1", "room-b"));
        Assert.True(activity.IsBusyIn("agent-2", "room-a"));
    }

    /// <summary>A wake recorded for an Agent and Room becomes the Task in hand when the pair's Turn begins.</summary>
    [Fact]
    public void RecordWake_ThenBegin_TaskInHand()
    {
        TurnActivity activity = new();

        activity.RecordWake("agent-1", "room-a", First);
        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
        activity.Begin("agent-1", "room-a");

        Assert.Equal(First, activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>Ending the pair's last Turn clears the Task in hand, so it never leaks into a later, unrelated Turn.</summary>
    [Fact]
    public void End_ClearsTaskInHand()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-a", First);
        activity.Begin("agent-1", "room-a");

        activity.End("agent-1", "room-a");
        activity.Begin("agent-1", "room-a");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>A wake for another Room does not apply to this Room's Turn.</summary>
    [Fact]
    public void RecordWake_OtherRoom_DoesNotApply()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-b", First);

        activity.Begin("agent-1", "room-a");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>A wake recorded while a Turn runs stays pending: the running Turn did not start from it, the next one does.</summary>
    [Fact]
    public void RecordWake_WhileTurnRuns_AppliesToNextTurnOnly()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-a", First);
        activity.Begin("agent-1", "room-a");

        activity.RecordWake("agent-1", "room-a", Second);
        activity.Begin("agent-1", "room-a");
        Assert.Equal(First, activity.TaskInHand("agent-1", "room-a"));

        activity.End("agent-1", "room-a");
        activity.End("agent-1", "room-a");
        activity.Begin("agent-1", "room-a");
        Assert.Equal(Second, activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>Two different Tasks waiting when the Turn begins tie it to neither; the same Task twice is one Task.</summary>
    [Fact]
    public void RecordWake_TwoDifferentTasks_NoTaskInHand_SameTaskTwiceIsFine()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-a", First);
        activity.RecordWake("agent-1", "room-a", Second);
        activity.RecordWake("agent-2", "room-a", First);
        activity.RecordWake("agent-2", "room-a", First);

        activity.Begin("agent-1", "room-a");
        activity.Begin("agent-2", "room-a");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
        Assert.Equal(First, activity.TaskInHand("agent-2", "room-a"));
    }

    /// <summary>A withdrawn wake no longer becomes the Task in hand.</summary>
    [Fact]
    public void ForgetWake_RemovesPendingWake()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-a", First);
        activity.ForgetWake("agent-1", "room-a", First);

        activity.Begin("agent-1", "room-a");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>A wake note older than the pending-wake lifetime is dropped at Begin, so a later unrelated Turn has no Task in hand.</summary>
    [Fact]
    public void Begin_PendingWakeOlderThanWindow_NoTaskInHand()
    {
        FakeTimeProvider clock = new(DateTimeOffset.Parse("2026-01-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        TurnActivity activity = new(timeProvider: clock);
        activity.RecordWake("agent-1", "room-a", First);

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        activity.Begin("agent-1", "room-a");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>A wake note just inside the pending-wake lifetime still becomes the Task in hand.</summary>
    [Fact]
    public void Begin_PendingWakeJustInsideWindow_TaskInHand()
    {
        FakeTimeProvider clock = new(DateTimeOffset.Parse("2026-01-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        TurnActivity activity = new(timeProvider: clock);
        activity.RecordWake("agent-1", "room-a", First);

        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        activity.Begin("agent-1", "room-a");

        Assert.Equal(First, activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>Clearing an Agent drops its pending wake notes, so a Turn after a reconnect starts fresh.</summary>
    [Fact]
    public void ClearAgent_DropsPendingWake()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-a", First);

        activity.ClearAgent("agent-1");
        activity.Begin("agent-1", "room-a");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
    }

    /// <summary>Clearing an Agent drops its Tasks in hand, and leaves another Agent's alone.</summary>
    [Fact]
    public void ClearAgent_ClearsTaskInHand()
    {
        TurnActivity activity = new();
        activity.RecordWake("agent-1", "room-a", First);
        activity.RecordWake("agent-2", "room-a", Second);
        activity.Begin("agent-1", "room-a");
        activity.Begin("agent-2", "room-a");

        activity.ClearAgent("agent-1");

        Assert.Null(activity.TaskInHand("agent-1", "room-a"));
        Assert.Equal(Second, activity.TaskInHand("agent-2", "room-a"));
    }
}
