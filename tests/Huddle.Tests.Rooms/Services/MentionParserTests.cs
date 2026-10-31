using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// Covers <see cref="MentionParser"/>'s Name-matching behaviour end to end, plus - from
/// <see cref="Parse_ResolvesAliasToTheOwningMember"/> onward - the Alias resolution Phase 4 adds. Every
/// Name-only fact above that line is the pre-existing regression suite and is intentionally left
/// untouched by that work.
/// </summary>
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
        // Agent cannot be Mentioned by accident every time a longer Name is written.
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

    /// <summary>An Alias resolves to the Member it belongs to, exactly as its owning Name would.</summary>
    [Fact]
    public void Parse_ResolvesAliasToTheOwningMember()
    {
        var jarvis = new User("1", "Jarvis", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("jar", "Jarvis") };

        var result = MentionParser.Parse("@jar, can you help?", [jarvis], aliases);

        Assert.Equal([jarvis], result);
    }

    /// <summary>An Alias resolves case-insensitively, matching every other handle this parser matches.</summary>
    [Fact]
    public void Parse_MatchesAliasCaseInsensitively()
    {
        var jarvis = new User("1", "Jarvis", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("jar", "Jarvis") };

        var result = MentionParser.Parse("@JAR", [jarvis], aliases);

        Assert.Equal([jarvis], result);
    }

    /// <summary>
    /// A Member's Name and one of their own Aliases, both mentioned in the same Message, still
    /// dedupe to one User - the existing seenIds dedupe (keyed by Member.Id, not by which handle
    /// matched) already gives this for free, but it is load-bearing enough for an Alias to earn its
    /// own pinning test.
    /// </summary>
    [Fact]
    public void Parse_NameAndItsOwnAliasInOneMessage_YieldsOneUser()
    {
        var jarvis = new User("1", "Jarvis", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("jar", "Jarvis") };

        var result = MentionParser.Parse("@Jarvis and @jar", [jarvis], aliases);

        Assert.Equal([jarvis], result);
    }

    /// <summary>
    /// An Alias whose owning Name is not a Member of this Room resolves to nothing - Aliases are
    /// unique library-wide (PersonaIndex guarantees this), not Room-wide, so an Alias can legitimately
    /// name someone who simply is not here.
    /// </summary>
    [Fact]
    public void Parse_AliasForMemberNotInTheRoom_ResolvesToNothing()
    {
        var echo = new User("1", "echo", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("jar", "Jarvis") };

        var result = MentionParser.Parse("@jar", [echo], aliases);

        Assert.Empty(result);
    }

    /// <summary>
    /// Mirrors <see cref="Parse_PrefersTheLongestMatchingName"/>, but the longer handle is an Alias
    /// owned by a DIFFERENT Member than the shorter Name it overlaps with. A two-pass implementation
    /// - scan every Name longest-first, then fall back to Aliases only if nothing matched - would
    /// match "Emily" here (the space after it is not a boundary blocker, exactly the "Emily"/"Emily
    /// Lee" trap) before it ever looked at the Alias list. Names and Aliases have to be one sorted
    /// list for the Alias to get a fair shot at winning.
    /// </summary>
    [Fact]
    public void Parse_LongerAliasBeatsAShorterNameBelongingToSomeoneElse()
    {
        var emily = new User("1", "Emily", UserKind.Agent, null);
        var other = new User("2", "Other", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("Emily Lee", "Other") };

        var result = MentionParser.Parse("@Emily Lee", [emily, other], aliases);

        Assert.Equal([other], result);
    }

    /// <summary>
    /// Pins the ordering <see cref="MentionParser.Parse(string, IReadOnlyList{User}, IReadOnlyList{MentionAlias})"/>
    /// relies on to break a length tie: Names are appended to the candidate list before Aliases, and
    /// LINQ's <c>OrderByDescending</c> is a stable sort, so a Name wins a tie in length over an Alias.
    /// Swapping the order of the two appends in <see cref="MentionParser"/> would make this fail with
    /// no compiler signal at all. (PersonaIndex guarantees a real Alias can never equal a DIFFERENT
    /// Persona's Name, so this exact collision cannot arise from real Persona files - but
    /// <see cref="MentionParser"/> cannot see that guarantee and must not depend on it silently holding.)
    /// </summary>
    [Fact]
    public void Parse_NameBeatsAnEqualLengthAlias()
    {
        var byName = new User("1", "Jar", UserKind.Agent, null);
        var byAlias = new User("2", "Jarvis", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("Jar", "Jarvis") };

        var result = MentionParser.Parse("@Jar", [byName, byAlias], aliases);

        Assert.Equal([byName], result);
    }

    /// <summary>An Alias respects the same "@" boundary rule as a Name: "x@jar" is not a Mention.</summary>
    [Fact]
    public void Parse_AliasDoesNotMatchAfterAWordCharacter()
    {
        var jarvis = new User("1", "Jarvis", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("jar", "Jarvis") };

        var result = MentionParser.Parse("x@jar", [jarvis], aliases);

        Assert.Empty(result);
    }

    /// <summary>An Alias respects the same word-boundary rule as a Name: "@jarring" does not Mention "jar".</summary>
    [Fact]
    public void Parse_AliasDoesNotMatchAsAPrefixOfALongerWord()
    {
        var jarvis = new User("1", "Jarvis", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("jar", "Jarvis") };

        var result = MentionParser.Parse("@jarring", [jarvis], aliases);

        Assert.Empty(result);
    }

    /// <summary>"@all" Mentions every Agent in the Room, in Member order, so each of them takes a Turn.</summary>
    [Fact]
    public void Parse_AllMentionsEveryAgent()
    {
        var human = new User("h", "Emre", UserKind.Human, null);
        var a = new User("1", "a", UserKind.Agent, null);
        var b = new User("2", "b", UserKind.Agent, null);

        var result = MentionParser.Parse("@all what do you think?", [human, a, b]);

        Assert.Equal([a, b], result);
    }

    /// <summary>"@all" never Mentions the Human: it asks the Agents for a Turn, and must not raise a Needs You card.</summary>
    [Fact]
    public void Parse_AllDoesNotMentionTheHuman()
    {
        var human = new User("h", "Emre", UserKind.Human, null);
        var a = new User("1", "a", UserKind.Agent, null);

        var result = MentionParser.Parse("@all", [human, a]);

        Assert.DoesNotContain(human, result);
    }

    /// <summary>"@all" is matched case-insensitively and ends at a word boundary, like a Name: "@ALL," counts and "@allison" does not.</summary>
    [Fact]
    public void Parse_AllFollowsTheNameBoundaryRules()
    {
        var a = new User("1", "a", UserKind.Agent, null);

        Assert.Equal([a], MentionParser.Parse("@ALL, please review", [a]));
        Assert.Empty(MentionParser.Parse("@allison", [a]));
        Assert.Empty(MentionParser.Parse("team@all", [a]));
    }

    /// <summary>A Member whose Name begins with "all" still wins over "@all", because the longer handle is read first.</summary>
    [Fact]
    public void Parse_LongerNameStartingWithAllBeatsAll()
    {
        var allHands = new User("1", "All Hands", UserKind.Agent, null);
        var other = new User("2", "other", UserKind.Agent, null);

        var result = MentionParser.Parse("@All Hands", [allHands, other]);

        Assert.Equal([allHands], result);
    }

    /// <summary>A Member Named exactly "All" is Mentioned alone by "@all": on a tie in length a Name wins, so it is not everyone.</summary>
    [Fact]
    public void Parse_MemberNamedAllBeatsAll()
    {
        var all = new User("1", "All", UserKind.Agent, null);
        var other = new User("2", "other", UserKind.Agent, null);

        var result = MentionParser.Parse("@all", [all, other]);

        Assert.Equal([all], result);
    }

    /// <summary>An Alias "all" is Mentioned alone by "@all": on a tie in length an Alias wins too, so it is not everyone.</summary>
    [Fact]
    public void Parse_AliasAllBeatsAll()
    {
        var owner = new User("1", "Allegra", UserKind.Agent, null);
        var other = new User("2", "other", UserKind.Agent, null);
        var aliases = new[] { new MentionAlias("all", "Allegra") };

        var result = MentionParser.Parse("@all", [owner, other], aliases);

        Assert.Equal([owner], result);
    }

    /// <summary>"@all" alongside a Name lists each Agent once, the Named one where it first appears.</summary>
    [Fact]
    public void Parse_AllWithANameCollapsesDuplicates()
    {
        var a = new User("1", "a", UserKind.Agent, null);
        var b = new User("2", "b", UserKind.Agent, null);

        var result = MentionParser.Parse("@b and @all", [a, b]);

        Assert.Equal([b, a], result);
    }
}