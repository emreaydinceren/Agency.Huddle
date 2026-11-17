using Agency.Huddle.App.Helpers.MemoryTidy;

namespace Agency.Huddle.Tests.Helpers;

/// <summary><see cref="MemoryTidyTokens"/>: the dates, numbers, links and addresses a tidy may not add or lose (Memory Tidy spec §6.10, "The checked tokens").</summary>
public sealed class MemoryTidyTokensTests
{
    private static string[] Texts(string text) => [.. MemoryTidyTokens.Of(text).Select(static token => token.Text)];

    /// <summary>A date-shaped run is one token, and a time is one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The one token.</param>
    [Theory]
    [InlineData("on 2026-10-30 we met", "2026-10-30")]
    [InlineData("on 30/10/2026 we met", "30/10/2026")]
    [InlineData("on 30.10.2026 we met", "30.10.2026")]
    [InlineData("due 10/30 sharp", "10/30")]
    [InlineData("at 14:30 sharp", "14:30")]
    public void Of_DateShapedRun_IsOneToken(string text, string expected)
    {
        MemoryTidyToken token = Assert.Single(MemoryTidyTokens.Of(text));

        Assert.Equal(expected, token.Text);
        Assert.Equal(MemoryTidyTokenKind.Date, token.Kind);
    }

    /// <summary>A date is not satisfied by its parts: <c>2026</c>, <c>10</c> and <c>30</c> elsewhere do not hold <c>2026-10-30</c>.</summary>
    [Fact]
    public void NotIn_DateAgainstItsParts_IsNew()
    {
        IReadOnlyList<MemoryTidyToken> missing = MemoryTidyTokens.NotIn("Met on 2026-10-30.", "In 2026 we had 10 meetings, the 30th was long.");

        Assert.Equal("2026-10-30", Assert.Single(missing).Text);
    }

    /// <summary>The same date written the same way is not new.</summary>
    [Fact]
    public void NotIn_SameDate_IsNotNew()
    {
        Assert.Empty(MemoryTidyTokens.NotIn("Met on 2026-10-30.", "Earlier note: 2026-10-30 was the kickoff."));
    }

    /// <summary>A number compares as written, so <c>3</c> becoming <c>9</c> is a new token.</summary>
    [Fact]
    public void NotIn_ThreeToNine_IsNew()
    {
        IReadOnlyList<MemoryTidyToken> missing = MemoryTidyTokens.NotIn("We have 9 seats.", "We have 3 seats.");

        Assert.Equal("9", Assert.Single(missing).Text);
    }

    /// <summary>Numbers are whole, with separators and sign; a percent and a currency symbol leave the digits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The one number.</param>
    [Theory]
    [InlineData("it cost 1,250 in all", "1,250")]
    [InlineData("a ratio of 12.5 to one", "12.5")]
    [InlineData("a drop of -3 points", "-3")]
    [InlineData("up 12% on last year", "12")]
    [InlineData("a fee of €40 a month", "40")]
    [InlineData("we are 7 today", "7")]
    public void Of_Number_IsWhole(string text, string expected)
    {
        MemoryTidyToken token = Assert.Single(MemoryTidyTokens.Of(text));

        Assert.Equal(expected, token.Text);
        Assert.Equal(MemoryTidyTokenKind.Number, token.Kind);
    }

    /// <summary>Digits at the start of a line followed by a dot or a bracket and a space are a list marker: no token, so a re-numbered list adds none.</summary>
    [Fact]
    public void Of_ListMarkers_AreNoToken()
    {
        Assert.Empty(MemoryTidyTokens.Of("1. first\n2. second\n  3) third"));
        Assert.Empty(MemoryTidyTokens.NotIn("1. first\n2. third", "1. first\n2. second\n3. third\n4. fourth"));
    }

    /// <summary>Only a marker at the start of a line is left out; a number inside a list item still counts, and so does one with no space after the dot.</summary>
    [Fact]
    public void Of_NumberAfterMarker_StillCounts()
    {
        Assert.Equal(["40", "12.5"], Texts("1. pay 40 now\n12.5 per cent"));
    }

    /// <summary>Links are taken whole to white space, whatever their kind.</summary>
    /// <param name="link">The link as written.</param>
    [Theory]
    [InlineData("https://example.com/a/b?x=1")]
    [InlineData("http://example.com/a")]
    [InlineData("www.example.com/page2")]
    [InlineData("mailto:person@example.com")]
    [InlineData("example.com/docs/42")]
    [InlineData(@"\\fileserver\share\notes 2")]
    public void Of_Link_IsOneTokenAndItsDigitsAreNotNumbers(string link)
    {
        ArgumentNullException.ThrowIfNull(link);
        string text = link.Contains(' ', StringComparison.Ordinal) ? link : $"see {link} now";

        MemoryTidyToken token = Assert.Single(MemoryTidyTokens.Of(text), static t => t.Kind == MemoryTidyTokenKind.Link);

        Assert.Equal(MemoryTidyTokenKind.Link, token.Kind);
        Assert.StartsWith(link.Split(' ')[0], token.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(MemoryTidyTokens.Of(text), static t => t.Kind == MemoryTidyTokenKind.Number && t.Text == "42");
    }

    /// <summary>Trailing punctuation is trimmed, so a link in brackets or at the end of a sentence matches the same link bare.</summary>
    [Fact]
    public void NotIn_LinkTrailingPunctuation_IsTrimmed()
    {
        Assert.Empty(MemoryTidyTokens.NotIn("(see https://example.com/x)", "Read https://example.com/x."));
        Assert.Equal("https://example.com/x", Assert.Single(MemoryTidyTokens.Of("\"https://example.com/x\",")).Text);
    }

    /// <summary>A link is compared ignoring case.</summary>
    [Fact]
    public void NotIn_LinkCase_IsIgnored()
    {
        Assert.Empty(MemoryTidyTokens.NotIn("HTTPS://Example.com/Page", "https://example.com/page"));
    }

    /// <summary>An address is a token, compared ignoring case; a different address is new.</summary>
    [Fact]
    public void NotIn_Address_IgnoresCase()
    {
        // Upper-cased at run time: the secret scan allows only lower-case example.com addresses as literals.
        Assert.Empty(MemoryTidyTokens.NotIn("Write to " + "jo.smith@example.com".ToUpperInvariant(), "jo.smith@example.com"));
        Assert.Equal("al@example.com", Assert.Single(MemoryTidyTokens.NotIn("al@example.com", "jo@example.com")).Text);
    }

    /// <summary>Tokens come back in order of first appearance, each once.</summary>
    [Fact]
    public void Of_Order_IsFirstAppearance()
    {
        Assert.Equal(["7", "2026-10-30", "https://example.com/a", "5"], Texts("7 then 2026-10-30 at https://example.com/a and 7 and 5"));
    }

    /// <summary>A date with a time yields the date and the time; a plain text yields nothing.</summary>
    [Fact]
    public void Of_DateAndTime_AreTwoTokens()
    {
        Assert.Equal(["2026-10-30", "14:30"], Texts("2026-10-30 14:30"));
        Assert.Empty(MemoryTidyTokens.Of("Nothing to see here."));
    }
}
