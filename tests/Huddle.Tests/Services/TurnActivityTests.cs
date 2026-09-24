using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

/// <summary>Tests for <see cref="TurnActivity"/> (Spec §10.8).</summary>
public sealed class TurnActivityTests
{
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
}
