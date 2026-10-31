using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="RequestLifecycle"/>: the pure state machine of the Request lifecycle table (RQ-3 to RQ-7, Spec §Lifecycle, Task 67).
/// </summary>
public sealed class RequestLifecycleTests
{
    /// <summary>Applies an event to a state given as plain strings.</summary>
    /// <param name="status">The Task status wire name; empty before the Request exists.</param>
    /// <param name="triage">The <see cref="TriageState"/> name.</param>
    /// <param name="ev">The <see cref="RequestEvent"/> name.</param>
    /// <param name="qa">Whether the requesting Team's QA confirms.</param>
    /// <returns>The transition.</returns>
    private static RequestTransition Run(string status, string triage, string ev, bool qa = false) =>
        RequestLifecycle.Apply(new RequestState(status, Enum.Parse<TriageState>(triage)), Enum.Parse<RequestEvent>(ev), qa);

    /// <summary>Asserts an allowed transition and returns it.</summary>
    /// <param name="transition">The transition under test.</param>
    /// <param name="status">The expected next status.</param>
    /// <param name="triage">The expected next triage state.</param>
    /// <param name="wake">The expected wake targets in order.</param>
    private static void AssertAllowed(RequestTransition transition, string status, TriageState triage, params WakeTarget[] wake)
    {
        Assert.True(transition.Allowed);
        Assert.Null(transition.Error);
        Assert.Equal(new RequestState(status, triage), transition.Next);
        Assert.Equal(wake, transition.Wake);
        Assert.False(transition.Closes);
    }

    /// <summary>Filing a Request creates it in Backlog with no triage and wakes the receiving manager.</summary>
    [Fact]
    public void Filed_FromNothing_BacklogWakesReceivingManager()
    {
        AssertAllowed(Run(string.Empty, "None", "Filed"), "Backlog", TriageState.None, WakeTarget.ReceivingManager);
    }

    /// <summary>Needs-info keeps the Request in Backlog and wakes the requesting manager.</summary>
    [Fact]
    public void MarkNeedsInfo_FromBacklog_NeedsInfoWakesRequester()
    {
        AssertAllowed(Run("Backlog", "None", "MarkNeedsInfo"), "Backlog", TriageState.NeedsInfo, WakeTarget.RequestingManager);
    }

    /// <summary>Adding the information clears the triage and wakes the receiving manager.</summary>
    [Fact]
    public void InfoAdded_FromNeedsInfo_BacklogWakesReceiver()
    {
        AssertAllowed(Run("Backlog", "NeedsInfo", "InfoAdded"), "Backlog", TriageState.None, WakeTarget.ReceivingManager);
    }

    /// <summary>Accepting moves to To Do, accepted, and wakes the requesting manager.</summary>
    [Fact]
    public void Accept_FromBacklog_ToDoAcceptedWakesRequester()
    {
        AssertAllowed(Run("Backlog", "None", "Accept"), "To Do", TriageState.Accepted, WakeTarget.RequestingManager);
    }

    /// <summary>Declining moves to Rejected, declined, and wakes the requesting manager and the Chief of Staff (RQ-7).</summary>
    [Fact]
    public void Decline_FromBacklog_RejectedWakesRequesterAndChief()
    {
        AssertAllowed(
            Run("Backlog", "None", "Decline"),
            "Rejected",
            TriageState.Declined,
            WakeTarget.RequestingManager,
            WakeTarget.ChiefOfStaff);
    }

    /// <summary>Working an accepted Request in any of To Do, In Progress or Review keeps the status and wakes the requesting manager (RQ-4).</summary>
    /// <param name="status">The Task status wire name.</param>
    [Theory]
    [InlineData("To Do")]
    [InlineData("In Progress")]
    [InlineData("Review")]
    public void Work_InWorkingStatuses_KeepsStatusWakesRequester(string status)
    {
        AssertAllowed(Run(status, "Accepted", "Work"), status, TriageState.Accepted, WakeTarget.RequestingManager);
    }

    /// <summary>Evidence attached in Review moves the Request to Done and wakes the requesting manager.</summary>
    [Fact]
    public void EvidenceAttached_FromReview_DoneWakesRequester()
    {
        AssertAllowed(Run("Review", "Accepted", "EvidenceAttached"), "Done", TriageState.Accepted, WakeTarget.RequestingManager);
    }

    /// <summary>The requesting Team's QA confirming a Done Request closes it and wakes the receiving manager (RQ-5).</summary>
    [Fact]
    public void Confirm_FromDoneWithQa_ClosesWakesReceiver()
    {
        RequestTransition transition = Run("Done", "Accepted", "Confirm", qa: true);

        Assert.True(transition.Allowed);
        Assert.Null(transition.Error);
        Assert.Equal(new RequestState("Done", TriageState.Accepted), transition.Next);
        Assert.Equal([WakeTarget.ReceivingManager], transition.Wake);
        Assert.True(transition.Closes);
    }

    /// <summary>Confirming without the requester's QA is refused with a message and does not close.</summary>
    [Fact]
    public void Confirm_FromDoneWithoutQa_Refused()
    {
        RequestTransition transition = Run("Done", "Accepted", "Confirm");

        Assert.False(transition.Allowed);
        Assert.Equal("Only the requesting Team's QA can confirm a Request.", transition.Error);
        Assert.Null(transition.Next);
        Assert.Empty(transition.Wake);
        Assert.False(transition.Closes);
    }

    /// <summary>An illegal pair is refused with an error naming the status and the event, and wakes nobody.</summary>
    [Fact]
    public void Accept_FromDone_RefusedWithExactMessage()
    {
        RequestTransition transition = Run("Done", "Accepted", "Accept");

        Assert.False(transition.Allowed);
        Assert.Equal("A Request in Done (triage Accepted) cannot take the Accept event.", transition.Error);
        Assert.Null(transition.Next);
        Assert.Empty(transition.Wake);
        Assert.False(transition.Closes);
    }

    /// <summary>Every illegal (state, event) pair is refused and the error names both the status and the event.</summary>
    /// <param name="status">The Task status wire name.</param>
    /// <param name="triage">The triage state name.</param>
    /// <param name="ev">The event name.</param>
    [Theory]
    [InlineData("Done", "Accepted", "Accept")]
    [InlineData("To Do", "Accepted", "Decline")]
    [InlineData("Review", "Accepted", "Decline")]
    [InlineData("Backlog", "None", "Confirm")]
    [InlineData("Backlog", "None", "EvidenceAttached")]
    [InlineData("Backlog", "None", "Work")]
    [InlineData("Backlog", "None", "InfoAdded")]
    [InlineData("Backlog", "NeedsInfo", "Accept")]
    [InlineData("Backlog", "NeedsInfo", "Decline")]
    [InlineData("Backlog", "NeedsInfo", "MarkNeedsInfo")]
    [InlineData("In Progress", "Accepted", "EvidenceAttached")]
    [InlineData("To Do", "Accepted", "Confirm")]
    [InlineData("Done", "Accepted", "Work")]
    [InlineData("Rejected", "Declined", "Accept")]
    [InlineData("Rejected", "Declined", "Work")]
    [InlineData("Backlog", "None", "Filed")]
    public void IllegalPairs_AreRefusedNamingStatusAndEvent(string status, string triage, string ev)
    {
        RequestTransition transition = Run(status, triage, ev, qa: true);

        Assert.False(transition.Allowed);
        Assert.NotNull(transition.Error);
        Assert.Contains(status, transition.Error);
        Assert.Contains(ev, transition.Error);
        Assert.Null(transition.Next);
        Assert.Empty(transition.Wake);
        Assert.False(transition.Closes);
    }
}
