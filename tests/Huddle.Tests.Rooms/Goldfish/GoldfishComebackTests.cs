using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using GoldfishComeback = Agency.Huddle.App.Goldfish.GoldfishComeback;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>Pins the exact "@Ruth comeback" match (spec 6.7, correction 3): only a whole Message that is a Mention of the possessed Agent and the word comeback.</summary>
public sealed class GoldfishComebackTests
{
    private static readonly User Ruth = new("u-ruth", "Ruth", UserKind.Agent, null);
    private static readonly User Quinn = new("u-quinn", "Quinn", UserKind.Agent, null);
    private static readonly User Emily = new("u-emily", "Emily", UserKind.Agent, null);
    private static readonly User EmilyLee = new("u-emily-lee", "Emily Lee", UserKind.Agent, null);
    private static readonly User Human = new("u-human", "Sam", UserKind.Human, null);

    private static bool Matches(string text, User possessed, bool postedByGoldfish = false, MentionAlias[]? aliases = null, params User[] extra)
    {
        List<User> members = [Ruth, Quinn, Human, .. extra];
        return GoldfishComeback.Matches(text, possessed, members, aliases ?? [], postedByGoldfish);
    }

    /// <summary>The plain form matches.</summary>
    [Fact]
    public void Matches_MentionAndComeback_IsTrue()
    {
        Assert.True(Matches("@Ruth comeback", Ruth));
    }

    /// <summary>Case is ignored for the word and for the Name.</summary>
    [Fact]
    public void Matches_CaseIgnored_IsTrue()
    {
        Assert.True(Matches("@rUTH ComeBack", Ruth));
    }

    /// <summary>Surrounding white space is trimmed and a run of white space counts as one space.</summary>
    [Fact]
    public void Matches_WhiteSpaceRuns_IsTrue()
    {
        Assert.True(Matches("  @Ruth \t  comeback \r\n", Ruth));
    }

    /// <summary>A line break between the Mention and the word is white space too.</summary>
    [Fact]
    public void Matches_NewlineBetween_IsTrue()
    {
        Assert.True(Matches("@Ruth\ncomeback", Ruth));
    }

    /// <summary>A full stop after the word makes it an ordinary Message.</summary>
    [Fact]
    public void Matches_TrailingFullStop_IsFalse()
    {
        Assert.False(Matches("@Ruth comeback.", Ruth));
    }

    /// <summary>A punctuation mark between the Mention and the word makes it an ordinary Message.</summary>
    [Fact]
    public void Matches_CommaAfterMention_IsFalse()
    {
        Assert.False(Matches("@Ruth, comeback", Ruth));
    }

    /// <summary>Text before the Mention makes it a sentence.</summary>
    [Fact]
    public void Matches_SentenceWithLeadingText_IsFalse()
    {
        Assert.False(Matches("please @Ruth comeback", Ruth));
    }

    /// <summary>Text after the word makes it a sentence.</summary>
    [Fact]
    public void Matches_SentenceWithTrailingText_IsFalse()
    {
        Assert.False(Matches("@Ruth comeback please", Ruth));
    }

    /// <summary>The goldfish's own Message never matches.</summary>
    [Fact]
    public void Matches_PostedByGoldfish_IsFalse()
    {
        Assert.False(Matches("@Ruth comeback", Ruth, postedByGoldfish: true));
    }

    /// <summary>A Mention of another Agent is not a comeback for the possessed one.</summary>
    [Fact]
    public void Matches_OtherAgent_IsFalse()
    {
        Assert.False(Matches("@Quinn comeback", Ruth));
    }

    /// <summary>An Alias of the possessed Agent works like the Name.</summary>
    [Fact]
    public void Matches_AliasOfPossessed_IsTrue()
    {
        Assert.True(Matches("@rt comeback", Ruth, aliases: [new MentionAlias("rt", "Ruth")]));
    }

    /// <summary>An Alias of another Agent does not match.</summary>
    [Fact]
    public void Matches_AliasOfOtherAgent_IsFalse()
    {
        Assert.False(Matches("@qn comeback", Ruth, aliases: [new MentionAlias("qn", "Quinn")]));
    }

    /// <summary>A handle that only starts with the Name is a different word.</summary>
    [Fact]
    public void Matches_NamePrefixOnly_IsFalse()
    {
        Assert.False(Matches("@Ruthless comeback", Ruth));
    }

    /// <summary>A Member whose Name is "Ruth comeback" wins, so the Message is a Mention of that Member.</summary>
    [Fact]
    public void Matches_MemberNamedRuthComeback_IsFalse()
    {
        User trap = new("u-trap", "Ruth comeback", UserKind.Human, null);

        Assert.False(Matches("@Ruth comeback", Ruth, extra: trap));
    }

    /// <summary>A multi-word Name resolves longest first, so "@Emily Lee comeback" is for Emily Lee, not Emily.</summary>
    [Fact]
    public void Matches_MultiWordName_IsTrueForTheLongerName()
    {
        Assert.Equal((true, false), (Matches("@Emily Lee comeback", EmilyLee, extra: [Emily, EmilyLee]), Matches("@Emily Lee comeback", Emily, extra: [Emily, EmilyLee])));
    }

    /// <summary>Without a Mention, or without the word, nothing matches.</summary>
    [Theory]
    [InlineData("comeback")]
    [InlineData("@Ruth")]
    [InlineData("@all comeback")]
    [InlineData("Ruth comeback")]
    [InlineData("   ")]
    [InlineData("")]
    public void Matches_NotAMentionPlusWord_IsFalse(string text)
    {
        Assert.False(Matches(text, Ruth));
    }

    /// <summary>A possessed Agent who is not a Member of the Room is never matched.</summary>
    [Fact]
    public void Matches_PossessedNotInMembers_IsFalse()
    {
        User stranger = new("u-x", "Zed", UserKind.Agent, null);

        Assert.False(Matches("@Zed comeback", stranger));
    }
}
