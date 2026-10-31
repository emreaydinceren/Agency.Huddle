using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchMatch"/>: the match tiers of spec §7.4, and the first-field rule (OP-6), ordinal and ignoring case (D-6).</summary>
public sealed class SearchMatchTests
{
    /// <summary>Each candidate and phrase gives the one tier the table names, or no match at all.</summary>
    [Theory]
    [InlineData("Marketing", "marketing", "ExactName")]
    [InlineData("Marketing", "MARK", "NamePrefix")]
    [InlineData("Marketing", "ket", "NameContains")]
    [InlineData("Marketing", "sales", null)]
    [InlineData("Mark", "Marketing", null)]
    [InlineData("Marketing", "", null)]
    public void Tier_EachRow_GivesItsTier(string candidate, string phrase, string? expected)
    {
        Assert.Equal(expected, SearchMatch.Tier(candidate, phrase)?.ToString());
    }

    /// <summary>An Agent whose Name misses is found by its Alias, at the Alias's tier.</summary>
    [Fact]
    public void FirstTier_NameMissesAliasMatches_ReturnsAliasTier()
    {
        MatchTier? tier = SearchMatch.FirstTier("nv", "Nova", "nv", "Senior Engineer");

        Assert.Equal<MatchTier?>(MatchTier.ExactName, tier);
    }

    /// <summary>A null field is skipped rather than taken as the answer.</summary>
    [Fact]
    public void FirstTier_NullFieldsAreSkipped()
    {
        MatchTier? tier = SearchMatch.FirstTier("nova", null, null, "Nova");

        Assert.Equal<MatchTier?>(MatchTier.ExactName, tier);
    }

    /// <summary>The first field that matches decides the tier, even when a later field would match better.</summary>
    [Fact]
    public void FirstTier_FirstMatchingFieldDecides_EvenWhenALaterFieldIsExact()
    {
        MatchTier? tier = SearchMatch.FirstTier("mark", "Marketing", "Mark");

        Assert.Equal<MatchTier?>(MatchTier.NamePrefix, tier);
    }

    /// <summary>No field matching gives no tier at all.</summary>
    [Fact]
    public void FirstTier_NoFieldMatches_ReturnsNull()
    {
        MatchTier? tier = SearchMatch.FirstTier("sales", "Nova", null, "Senior Engineer");

        Assert.Null(tier);
    }
}
