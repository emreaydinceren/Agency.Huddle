using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

public sealed class MentionParserTests
{
    [Fact]
    public void Parse_MatchesCaseInsensitively()
    {
        var echo = new User("1", "echo", UserKind.Agent, null);

        var result = MentionParser.Parse("hi @Echo", [echo]);

        Assert.Equal([echo], result);
    }

    [Fact]
    public void Parse_AllowsHyphenAndUnderscore()
    {
        var agent = new User("1", "my-agent_2", UserKind.Agent, null);

        var result = MentionParser.Parse("@my-agent_2,", [agent]);

        Assert.Equal([agent], result);
    }

    [Fact]
    public void Parse_IgnoresTrailingPunctuation()
    {
        var echo = new User("1", "echo", UserKind.Agent, null);

        Assert.Equal([echo], MentionParser.Parse("@echo,", [echo]));
        Assert.Equal([echo], MentionParser.Parse("@echo.", [echo]));
        Assert.Equal([echo], MentionParser.Parse("@echo:", [echo]));
        Assert.Equal([echo], MentionParser.Parse("@echo?", [echo]));
    }

    [Fact]
    public void Parse_DoesNotMatchEmailAddress()
    {
        var example = new User("1", "example", UserKind.Agent, null);

        var result = MentionParser.Parse("me@example.com", [example]);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_IgnoresUnknownNames()
    {
        var echo = new User("1", "echo", UserKind.Agent, null);

        var result = MentionParser.Parse("@ghost", [echo]);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_CollapsesDuplicates_PreservesFirstOrder()
    {
        var a = new User("1", "a", UserKind.Agent, null);
        var b = new User("2", "b", UserKind.Agent, null);

        var result = MentionParser.Parse("@b @a @b", [a, b]);

        Assert.Equal([b, a], result);
    }

    [Fact]
    public void Parse_AtStartMiddleEnd()
    {
        var a = new User("1", "a", UserKind.Agent, null);
        var b = new User("2", "b", UserKind.Agent, null);
        var c = new User("3", "c", UserKind.Agent, null);

        var result = MentionParser.Parse("@a middle @b end @c", [a, b, c]);

        Assert.Equal([a, b, c], result);
    }

    [Fact]
    public void Parse_EmptyMembers_ReturnsEmpty()
    {
        var result = MentionParser.Parse("@echo", []);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MatchesNameContainingSpaces()
    {
        var emily = new User("1", "Emily Lee", UserKind.Agent, null);

        var result = MentionParser.Parse("morning @Emily Lee, any news?", [emily]);

        Assert.Equal([emily], result);
    }

    [Fact]
    public void Parse_PrefersTheLongestMatchingName()
    {
        // With no delimiter, "@Emily Lee" is ambiguous by shape alone. The Room's membership is
        // the only thing that can say where the Name ends, and the longer Name is the better
        // reading: someone who wanted Emily would not have written "Lee" after her Name.
        var emily = new User("1", "Emily", UserKind.Agent, null);
        var emilyLee = new User("2", "Emily Lee", UserKind.Agent, null);

        var result = MentionParser.Parse("@Emily Lee", [emily, emilyLee]);

        Assert.Equal([emilyLee], result);
    }

    [Fact]
    public void Parse_FallsBackToTheShorterNameWhenTheLongerIsNotAMember()
    {
        var emily = new User("1", "Emily", UserKind.Agent, null);

        var result = MentionParser.Parse("@Emily Lee", [emily]);

        Assert.Equal([emily], result);
    }

    [Fact]
    public void Parse_DoesNotMatchAPrefixOfALongerWord()
    {
        // "ech" must not be found inside "@echo": a Mention ends at a word boundary, so that a
        // Teammate cannot be Mentioned by accident every time a longer Name is written.
        var ech = new User("1", "ech", UserKind.Agent, null);

        var result = MentionParser.Parse("@echo", [ech]);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MatchesNameWithSpacesCaseInsensitively()
    {
        var emily = new User("1", "Emily Lee", UserKind.Agent, null);

        var result = MentionParser.Parse("@emily lee", [emily]);

        Assert.Equal([emily], result);
    }

    [Fact]
    public void Parse_DoesNotRunTwoMentionsTogether()
    {
        var chief = new User("1", "Chief of Staff", UserKind.Agent, null);
        var echo = new User("2", "echo", UserKind.Agent, null);

        var result = MentionParser.Parse("@Chief of Staff and @echo", [chief, echo]);

        Assert.Equal([chief, echo], result);
    }

    [Fact]
    public void Parse_StopsAtPunctuationInsideANameWithSpaces()
    {
        var chief = new User("1", "Chief of Staff", UserKind.Agent, null);

        Assert.Equal([chief], MentionParser.Parse("@Chief of Staff.", [chief]));
        Assert.Equal([chief], MentionParser.Parse("thanks @Chief of Staff!", [chief]));
    }

    [Fact]
    public void Parse_IncludesHumanMention()
    {
        var human = new User(KnownIds.Human, "You", UserKind.Human, null);

        var result = MentionParser.Parse("@You", [human]);

        Assert.Equal([human], result);
    }
}