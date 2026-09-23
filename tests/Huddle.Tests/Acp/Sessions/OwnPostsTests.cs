using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>Pins <see cref="OwnPosts"/> (RS §6.7, finding P-7).</summary>
public sealed class OwnPostsTests
{
    /// <summary>A Record then a Take returns exactly what was recorded, oldest first.</summary>
    [Fact]
    public void RecordThenTake_ReturnsInOrder()
    {
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));

        ownPosts.Record("nova", "room-b", "first");
        ownPosts.Record("nova", "room-b", "second");

        var taken = ownPosts.Take("nova", "room-b");

        Assert.Equal(["first", "second"], taken);
    }

    /// <summary>A Take drains the recorded lines: a second Take for the same Agent and Room returns nothing.</summary>
    [Fact]
    public void Take_Clears()
    {
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        ownPosts.Record("nova", "room-b", "first");

        _ = ownPosts.Take("nova", "room-b");
        var second = ownPosts.Take("nova", "room-b");

        Assert.Empty(second);
    }

    /// <summary>More than <see cref="AcpOptions.CatchUpMessages"/> Records for one Room drop the oldest lines.</summary>
    [Fact]
    public void Record_CappedAtCatchUpMessagesPerRoom()
    {
        var options = new TeamOptions { Acp = new AcpOptions { CatchUpMessages = 2 } };
        var ownPosts = new OwnPosts(Options.Create(options));

        ownPosts.Record("nova", "room-b", "first");
        ownPosts.Record("nova", "room-b", "second");
        ownPosts.Record("nova", "room-b", "third");

        var taken = ownPosts.Take("nova", "room-b");

        Assert.Equal(["second", "third"], taken);
    }

    /// <summary>A Record for a Room currently marked Busy by <see cref="OwnPosts.BeginTurn"/> is not recorded: that session made the post itself.</summary>
    [Fact]
    public void Record_WhileThatRoomIsBusy_NotRecorded()
    {
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        ownPosts.BeginTurn("nova", "room-b");

        ownPosts.Record("nova", "room-b", "should not be recorded");

        Assert.Empty(ownPosts.Take("nova", "room-b"));
    }

    /// <summary>A shared session's Busy mark (<c>BeginTurn(agent, null)</c>) refuses a Record into every Room for that Agent.</summary>
    [Fact]
    public void Record_WhileSharedSessionBusy_NotRecorded()
    {
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        ownPosts.BeginTurn("nova", null);

        ownPosts.Record("nova", "room-b", "should not be recorded");
        ownPosts.Record("nova", "room-c", "should not be recorded either");

        Assert.Empty(ownPosts.Take("nova", "room-b"));
        Assert.Empty(ownPosts.Take("nova", "room-c"));
    }

    /// <summary>Once <see cref="OwnPosts.EndTurn"/> clears the Busy mark, a Record for that Room is recorded again.</summary>
    [Fact]
    public void EndTurn_ThenRecord_Recorded()
    {
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        ownPosts.BeginTurn("nova", "room-b");
        ownPosts.EndTurn("nova", "room-b");

        ownPosts.Record("nova", "room-b", "recorded now");

        Assert.Equal(["recorded now"], ownPosts.Take("nova", "room-b"));
    }

    /// <summary><see cref="OwnPosts.ClearAgent"/> drops only the named Agent's entries, leaving another Agent's untouched.</summary>
    [Fact]
    public void ClearAgent_DropsOnlyThatAgent()
    {
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        ownPosts.Record("nova", "room-b", "nova's post");
        ownPosts.Record("friend", "room-b", "friend's post");

        ownPosts.ClearAgent("nova");

        Assert.Empty(ownPosts.Take("nova", "room-b"));
        Assert.Equal(["friend's post"], ownPosts.Take("friend", "room-b"));
    }
}
