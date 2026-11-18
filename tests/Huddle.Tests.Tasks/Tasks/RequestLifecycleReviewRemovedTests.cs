using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Slice 1 of #669 (decision D-18): Review is no longer a status in which an accepted Request can be worked.</summary>
public sealed class RequestLifecycleReviewRemovedTests
{
    /// <summary>Working an accepted Request whose status reads Review is refused, as for any status that is not To Do or In Progress.</summary>
    [Fact]
    public void Work_AcceptedRequestInReview_IsRefused()
    {
        RequestTransition transition = RequestLifecycle.Apply(new RequestState("Review", TriageState.Accepted), RequestEvent.Work);

        Assert.False(transition.Allowed);
        Assert.NotNull(transition.Error);
    }

    /// <summary>Working an accepted Request in To Do or In Progress keeps the status and still wakes the requesting manager (D-18: the event is unchanged).</summary>
    [Theory]
    [InlineData("To Do")]
    [InlineData("In Progress")]
    public void Work_AcceptedRequestInWorkableStatus_KeepsStatus(string status)
    {
        RequestTransition transition = RequestLifecycle.Apply(new RequestState(status, TriageState.Accepted), RequestEvent.Work);

        Assert.True(transition.Allowed);
        Assert.Equal(new RequestState(status, TriageState.Accepted), transition.Next);
    }
}
