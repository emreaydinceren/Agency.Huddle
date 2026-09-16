using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="RoomFollows"/>.</summary>
public sealed class RoomFollowsTests
{
    /// <summary>Following a Room makes <see cref="RoomFollows.IsFollowing"/> report true for it.</summary>
    [Fact]
    public void Follow_ThenIsFollowing_IsTrue()
    {
        RoomFollows follows = new();

        follows.Follow("agent-1", "room-1");

        Assert.True(follows.IsFollowing("agent-1", "room-1"));
    }

    /// <summary>A second Follow of the same (Agent, Room) pair returns false: nothing new happened.</summary>
    [Fact]
    public void Follow_Twice_SecondCallReturnsFalse()
    {
        RoomFollows follows = new();

        var first = follows.Follow("agent-1", "room-1");
        var second = follows.Follow("agent-1", "room-1");

        Assert.True(first);
        Assert.False(second);
    }

    /// <summary>Unfollowing a followed Room returns true the first time, then false: there is nothing left to remove.</summary>
    [Fact]
    public void Unfollow_ThenAgain_ReturnsTrueThenFalse()
    {
        RoomFollows follows = new();
        follows.Follow("agent-1", "room-1");

        var first = follows.Unfollow("agent-1", "room-1");
        var second = follows.Unfollow("agent-1", "room-1");

        Assert.True(first);
        Assert.False(second);
    }

    /// <summary><see cref="RoomFollows.FollowersOf"/> returns every Agent following one Room.</summary>
    [Fact]
    public void FollowersOf_ReturnsEveryoneFollowingTheRoom()
    {
        RoomFollows follows = new();
        follows.Follow("agent-1", "room-1");
        follows.Follow("agent-2", "room-1");
        follows.Follow("agent-3", "room-2");

        var followers = follows.FollowersOf("room-1");

        Assert.Equal(2, followers.Count);
        Assert.Contains("agent-1", followers);
        Assert.Contains("agent-2", followers);
    }

    /// <summary>A Room nobody has ever followed reports an empty set, not an exception or a null.</summary>
    [Fact]
    public void FollowersOf_UnknownRoom_IsEmpty()
    {
        RoomFollows follows = new();

        var followers = follows.FollowersOf("room-nobody-follows");

        Assert.Empty(followers);
    }

    /// <summary>
    /// Clearing an Agent drops every Room it follows, and leaves every other Agent's follows
    /// untouched.
    /// </summary>
    [Fact]
    public void ClearAgent_DropsThatAgentEverywhereAndLeavesOthersAlone()
    {
        RoomFollows follows = new();
        follows.Follow("agent-1", "room-1");
        follows.Follow("agent-1", "room-2");
        follows.Follow("agent-2", "room-1");

        follows.ClearAgent("agent-1");

        Assert.False(follows.IsFollowing("agent-1", "room-1"));
        Assert.False(follows.IsFollowing("agent-1", "room-2"));
        Assert.True(follows.IsFollowing("agent-2", "room-1"));
    }

    /// <summary>
    /// <see cref="RoomFollows.FollowersOf"/> hands back a snapshot: mutating the returned set, or
    /// following the same Room afterwards, must never change what was already returned.
    /// </summary>
    [Fact]
    public void FollowersOf_ReturnsASnapshot_LaterChangesDoNotAffectIt()
    {
        RoomFollows follows = new();
        follows.Follow("agent-1", "room-1");

        var snapshot = follows.FollowersOf("room-1");
        follows.Follow("agent-2", "room-1");

        Assert.Single(snapshot);
        Assert.DoesNotContain("agent-2", snapshot);
    }
}
