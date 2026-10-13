namespace Agency.Huddle.App.Search;

/// <summary>The provider's judgement of how well a hit matched, best first (spec §7.4).</summary>
public enum MatchTier
{
    /// <summary>The phrase equals the name, ignoring case.</summary>
    ExactName,

    /// <summary>The name starts with the phrase.</summary>
    NamePrefix,

    /// <summary>The name contains the phrase.</summary>
    NameContains,

    /// <summary>The phrase is found in the contents of an item, such as a Library file or a Message.</summary>
    ContentMatch,
}
