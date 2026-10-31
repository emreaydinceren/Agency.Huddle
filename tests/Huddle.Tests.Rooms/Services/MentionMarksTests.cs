using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// Pins <see cref="MentionMarks.For"/>: the handles a Room draws as Mention pills are the ones
/// <see cref="MentionParser"/> counts as Mentions.
/// </summary>
public sealed class MentionMarksTests
{
    private static readonly User Human = new("h", "Sam", UserKind.Human, null);
    private static readonly User Morgan = new("1", "Morgan", UserKind.Agent, null);

    /// <summary>A Room with an Agent draws "@all" as a pill, because "@all" wakes that Agent.</summary>
    [Fact]
    public void For_RoomWithAnAgent_MarksAll()
    {
        MentionMarks marks = MentionMarks.For([MentionMarksTests.Human, MentionMarksTests.Morgan], "Sam", []);

        string html = MarkdownRenderer.ToHtml("@all please look", null, null, marks);

        Assert.Equal("<p><span class=\"mention\">@all</span> please look</p>\n", html);
    }

    /// <summary>A Room with no Agent does not mark "@all": it would Mention nobody, so it is plain text.</summary>
    [Fact]
    public void For_RoomWithNoAgent_DoesNotMarkAll()
    {
        MentionMarks marks = MentionMarks.For([MentionMarksTests.Human], "Sam", []);

        Assert.DoesNotContain(MentionParser.AllHandle, marks.Handles);
    }

    /// <summary>An Agent's Name and the Alias of an Agent in the Room are handles; an Alias of someone elsewhere is not.</summary>
    [Fact]
    public void For_TakesAgentNamesAndTheirAliasesOnly()
    {
        MentionAlias[] aliases = [new("mo", "Morgan"), new("ghost", "Absent")];

        MentionMarks marks = MentionMarks.For([MentionMarksTests.Human, MentionMarksTests.Morgan], "Sam", aliases);

        Assert.Equal(["Morgan", "mo", MentionParser.AllHandle], marks.Handles);
    }

    /// <summary>The Human's Name and Alias are the own handles, so a Mention of them carries <c>mention--me</c>.</summary>
    [Fact]
    public void For_HumanNameAndAliasAreOwnHandles()
    {
        MentionAlias[] aliases = [new("me", "Sam")];

        MentionMarks marks = MentionMarks.For([MentionMarksTests.Human, MentionMarksTests.Morgan], "Sam", aliases);

        Assert.Equal(["Sam", "me"], marks.OwnHandles);
    }
}
