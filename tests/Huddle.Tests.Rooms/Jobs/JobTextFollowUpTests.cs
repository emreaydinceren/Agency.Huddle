using Agency.Huddle.App.Components.Jobs;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins the Scheduled view's words for a Follow-up (<c>J2</c> row, <c>J4</c> Room line).</summary>
public sealed class JobTextFollowUpTests
{
    /// <summary>The list row of a Follow-up names its owner, the Room and the next run.</summary>
    [Fact]
    public void FollowUpRow_NamesTheOwnerTheRoomAndTheNextRun()
    {
        Assert.Equal("For Nova · Follow-up in Ops · Next: Mon 2 Nov, 09:30", JobText.FollowUpRow("Nova", "Ops", "Next: Mon 2 Nov, 09:30"));
    }

    /// <summary>The detail view's Room line.</summary>
    [Fact]
    public void FollowUpRoomLine_NamesTheRoom()
    {
        Assert.Equal("Follow-up in", JobText.FollowUpRoomLabel);
    }
}
