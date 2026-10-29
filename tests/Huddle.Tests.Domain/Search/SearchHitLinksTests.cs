using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchHitLinks.UrlFor"/>: the address each hit opens, per the table in spec §9.</summary>
public sealed class SearchHitLinksTests
{
    /// <summary>A Team opens its page.</summary>
    [Fact]
    public void UrlFor_TeamHit_OpensTheTeamPage()
    {
        string? url = SearchHitLinks.UrlFor(new TeamHit("Marketing", null, 0, MatchTier.ExactName));

        Assert.Equal("/teams/Marketing", url);
    }

    /// <summary>An Agent opens a dialog rather than a page, so it has no URL.</summary>
    [Fact]
    public void UrlFor_AgentHit_HasNoUrl()
    {
        string? url = SearchHitLinks.UrlFor(new AgentHit("Nova", "Router", "nova", ["Marketing"], MatchTier.ExactName));

        Assert.Null(url);
    }

    /// <summary>A Library file opens the Library page with its root and its relative path, escaped.</summary>
    [Fact]
    public void UrlFor_LibraryHit_OpensTheLibraryPage()
    {
        string? url = SearchHitLinks.UrlFor(new LibraryHit("lib-1", "Team Library", "docs/plan.md", "docs", false, null, null, MatchTier.ExactName));

        Assert.Equal("/library?root=lib-1&path=docs%2Fplan.md", url);
    }

    /// <summary>A Task opens its item page under its zero-padded id.</summary>
    [Fact]
    public void UrlFor_TaskHit_OpensTheTaskItem()
    {
        string? url = SearchHitLinks.UrlFor(new TaskHit(new TaskId("PLAT", 42), "Fix login", TaskState.ToDo, null, "Platform", false, MatchTier.NamePrefix));

        Assert.Equal("/tasks/item/PLAT-0042", url);
    }

    /// <summary>A Room with a message opens the Room at that message.</summary>
    [Fact]
    public void UrlFor_RoomHitWithMessage_OpensTheMessage()
    {
        string? url = SearchHitLinks.UrlFor(new RoomHit("room-1", "Kickoff", 2, "msg-9", "Nova", "snippet", null, false, MatchTier.ContentMatch));

        Assert.Equal("/rooms/room-1?message=msg-9", url);
    }

    /// <summary>A Room without a message opens the Room with no query string.</summary>
    [Fact]
    public void UrlFor_RoomHitWithoutMessage_HasNoQuery()
    {
        string? url = SearchHitLinks.UrlFor(new RoomHit("room-1", "Kickoff", 1, null, null, null, null, false, MatchTier.NameContains));

        Assert.Equal("/rooms/room-1", url);
    }

    /// <summary>A Team name with a space is escaped in the address.</summary>
    [Fact]
    public void UrlFor_NameWithSpace_IsEscaped()
    {
        string? url = SearchHitLinks.UrlFor(new TeamHit("Launch Team", null, 0, MatchTier.ExactName));

        Assert.Equal("/teams/Launch%20Team", url);
    }

    /// <summary>A hit subtype the table does not know is refused, not given a made-up address.</summary>
    [Fact]
    public void UrlFor_UnknownHit_Throws()
    {
        UnknownHit hit = new("mystery", null, false, MatchTier.ExactName);

        Assert.Throws<ArgumentOutOfRangeException>(() => SearchHitLinks.UrlFor(hit));
    }

    /// <summary>A hit subtype the link table does not list, used only to reach the refusal branch.</summary>
    /// <param name="Title">The card's main text.</param>
    /// <param name="TeamName">The Team the hit belongs to, or null.</param>
    /// <param name="IsArchived">Whether the hit is archived.</param>
    /// <param name="Tier">How well the hit matched.</param>
    private sealed record UnknownHit(string Title, string? TeamName, bool IsArchived, MatchTier Tier)
        : SearchHit(Title, TeamName, IsArchived, Tier);
}
