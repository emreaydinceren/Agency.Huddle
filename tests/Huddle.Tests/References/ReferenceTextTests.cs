using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins <see cref="ReferenceText"/>: the label, the address and the insert text of a reference (spec §6, D-9, D-10, D-26).</summary>
public sealed class ReferenceTextTests
{
    /// <summary>An @ in a label becomes a full-width @, so the label can never be read as a Mention (D-9 rule 2).</summary>
    [Fact]
    public void LabelFor_ReplacesAtWithFullwidthAt()
    {
        string label = ReferenceText.LabelFor("Q@Nova", ReferenceKind.Room);

        Assert.Equal("Q＠Nova", label);
    }

    /// <summary>Each Markdown special is backslash-escaped in a label (D-9 rule 3).</summary>
    [Theory]
    [InlineData("\\")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("`")]
    [InlineData("*")]
    [InlineData("_")]
    public void LabelFor_EscapesMarkdownSpecials(string special)
    {
        string label = ReferenceText.LabelFor("a" + special + "b", ReferenceKind.Room);

        Assert.Equal("a\\" + special + "b", label);
    }

    /// <summary>Characters that CommonMark reads before a link bracket (autolink, entity, image, strikethrough) are escaped too (finding 7b).</summary>
    [Theory]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("&")]
    [InlineData("!")]
    [InlineData("~")]
    public void LabelFor_EscapesAutolinkAndEntityStarters(string special)
    {
        string label = ReferenceText.LabelFor("a" + special + "b", ReferenceKind.Room);

        Assert.Equal("a\\" + special + "b", label);
    }

    /// <summary>The plain label has no backslashes: it is what a chip draws and what the snapshot stores (finding 7a).</summary>
    [Fact]
    public void PlainLabel_IsUnescaped()
    {
        Assert.Equal("Q3_plan [x] & <y>", ReferenceText.PlainLabelFor("Q3_plan  [x] & <y>", ReferenceKind.Room));
        Assert.Equal("Q3_plan (Growth)", ReferenceText.RoomPlainLabelFor("Q3_plan", "Growth", false));
    }

    /// <summary>Runs of whitespace collapse to one space and the label is trimmed (D-9 rule 4).</summary>
    [Fact]
    public void LabelFor_CollapsesWhitespace()
    {
        string label = ReferenceText.LabelFor("  a \t b\n\nc  ", ReferenceKind.Room);

        Assert.Equal("a b c", label);
    }

    /// <summary>A label is cut at 60 text elements and gets an ellipsis; an emoji on the boundary is not split (D-9 rule 4).</summary>
    [Fact]
    public void LabelFor_CutsAt60TextElements()
    {
        string name = new string('a', 59) + "\U0001F600" + "b";

        string label = ReferenceText.LabelFor(name, ReferenceKind.Room);

        Assert.Equal(new string('a', 59) + "\U0001F600" + "…", label);
    }

    /// <summary>An empty label falls back to Room for a Room and Team for a Team (D-9 rule 5).</summary>
    [Fact]
    public void LabelFor_EmptyBecomesRoomOrTeam()
    {
        Assert.Equal("Room", ReferenceText.LabelFor("   ", ReferenceKind.Room));
        Assert.Equal("Team", ReferenceText.LabelFor(string.Empty, ReferenceKind.Team));
    }

    /// <summary>A label built from a Name with an @ never makes a Mention, whatever the Room is called (D-9).</summary>
    [Fact]
    public void LabelFor_NeverMakesAMention()
    {
        string text = "[" + ReferenceText.LabelFor("Q@Nova", ReferenceKind.Room) + "](/rooms/x)";
        User nova = new("1", "Nova", UserKind.Agent, null);

        Assert.Empty(MentionParser.Parse(text, [nova]));
    }

    /// <summary>A Room's label says its Scope: a root Room is its Name, a scoped Room adds its Team, a Team Room is its Team's (D-26).</summary>
    [Theory]
    [InlineData("Nova", null, false, "Nova")]
    [InlineData("Nova", "Marketing", false, "Nova (Marketing)")]
    [InlineData("Marketing", "Marketing", true, "Marketing Team Room")]
    public void RoomLabelFor_EachScope(string roomName, string? team, bool isTeamRoom, string expected)
    {
        string label = ReferenceText.RoomLabelFor(roomName, team, isTeamRoom);

        Assert.Equal(expected, label);
    }

    /// <summary>A Team's address escapes its Name, so a space becomes %20 (D-10).</summary>
    [Fact]
    public void TeamHref_EscapesSpaces()
    {
        string href = ReferenceText.TeamHref("Growth Ops");

        Assert.Equal("/teams/Growth%20Ops", href);
    }

    /// <summary>Only /rooms/ followed by one valid id is a Room address; anything with a separator, query or fragment is refused (D-10).</summary>
    [Theory]
    [InlineData("/rooms/abc", true)]
    [InlineData("/rooms/../x", false)]
    [InlineData("/rooms/a/b", false)]
    [InlineData("/rooms/a?x=1", false)]
    [InlineData("/rooms/a#f", false)]
    [InlineData("/rooms/", false)]
    public void TryParseRoomHref_AcceptsOnlyAValidIdAndNothingElse(string url, bool accepted)
    {
        bool ok = ReferenceText.TryParseRoomHref(url, out string? roomId);

        Assert.Equal(accepted, ok);
        Assert.Equal(accepted ? "abc" : null, roomId);
    }

    /// <summary>A Team address decodes to its Name only when the Name is a legal Team Name and is not the reserved word new (D-10).</summary>
    [Theory]
    [InlineData("/teams/Growth%20Ops", "Growth Ops")]
    [InlineData("/teams/a%2Fb", null)]
    [InlineData("/teams/new", null)]
    [InlineData("/teams/NEW", null)]
    [InlineData("/teams/_x", null)]
    [InlineData("/teams/a%5Bb", null)]
    [InlineData("/teams/%E0%A4%A", null)]
    [InlineData("/teams/a/b", null)]
    [InlineData("/teams/a?x=1", null)]
    [InlineData("/teams/a#f", null)]
    public void TryParseTeamHref_Cases(string url, string? expected)
    {
        bool ok = ReferenceText.TryParseTeamHref(url, out string? teamName);

        Assert.Equal(expected is not null, ok);
        Assert.Equal(expected, teamName);
    }

    /// <summary>Each kind of hit inserts its own text: a Teammate as a Mention, a Task as its id, a Room and a Team as a link (spec §6).</summary>
    [Fact]
    public void InsertFor_EachKind()
    {
        SearchHit teammate = new TeammateHit("Nova", "Router", "nova", ["Marketing"], MatchTier.ExactName);
        SearchHit task = new TaskHit(new TaskId("GROW", 2), "Title", TaskState.ToDo, null, "Growth", false, MatchTier.ExactName);
        SearchHit room = new RoomHit("abc", "Nova", 0, null, null, null, null, false, MatchTier.ExactName, "Marketing", false);
        SearchHit team = new TeamHit("Growth Ops", null, 0, MatchTier.ExactName);

        Assert.Equal("@Nova", ReferenceText.InsertFor(teammate));
        Assert.Equal("GROW-0002", ReferenceText.InsertFor(task));
        Assert.Equal("[Nova (Marketing)](/rooms/abc)", ReferenceText.InsertFor(room));
        Assert.Equal("[Growth Ops](/teams/Growth%20Ops)", ReferenceText.InsertFor(team));
    }
}
