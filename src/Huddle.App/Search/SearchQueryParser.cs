namespace Agency.Huddle.App.Search;

/// <summary>
/// Reads the box text: which filter its prefix sets, the phrase after it, and whether that phrase searches
/// (spec §6.2, §7.2). Pure string work, no I/O.
/// </summary>
internal static class SearchQueryParser
{
    /// <summary>The five prefixes and the filter each one sets. The colon decides, so <c>team:</c> is not <c>teammate:</c>.</summary>
    private static readonly (string Prefix, SearchFilter Filter)[] Prefixes =
    [
        ("team:", SearchFilter.Teams),
        ("teammate:", SearchFilter.Teammates),
        ("lib:", SearchFilter.Library),
        ("task:", SearchFilter.Tasks),
        ("room:", SearchFilter.Rooms),
    ];

    /// <summary>Works out the filter, the phrase and whether the box searches.</summary>
    /// <param name="boxText">The text in the box, as typed.</param>
    /// <param name="chipFilter">The filter of the chip that is lit when no prefix is typed.</param>
    /// <returns>The filter the prefix sets, or <paramref name="chipFilter"/>; the trimmed phrase; and whether it searches.</returns>
    public static SearchParse Parse(string boxText, SearchFilter chipFilter)
    {
        ArgumentNullException.ThrowIfNull(boxText);

        SearchFilter filter = chipFilter;
        string rest = boxText;
        if (TryReadPrefix(boxText, out SearchFilter prefixFilter, out int prefixLength))
        {
            filter = prefixFilter;
            rest = boxText[prefixLength..];
        }

        string phrase = rest.Trim();
        bool browse = phrase.Length == 0 && (filter is SearchFilter.Teams or SearchFilter.Teammates);
        bool searchable = !phrase.StartsWith('>') && (phrase.Length >= SearchLimits.MinChars || browse);

        return new SearchParse(filter, phrase, searchable);
    }

    /// <summary>Rewrites the prefix at the start of the box for a chip: replaces it, adds one, or removes it for All. The rest of the text stays.</summary>
    /// <param name="boxText">The text in the box, as typed.</param>
    /// <param name="chip">The chip the Human picked.</param>
    /// <returns>The new box text.</returns>
    public static string ApplyChip(string boxText, SearchFilter chip)
    {
        ArgumentNullException.ThrowIfNull(boxText);

        string rest = TryReadPrefix(boxText, out _, out int prefixLength) ? boxText[prefixLength..] : boxText;
        return string.Concat(PrefixFor(chip), rest);
    }

    /// <summary>The prefix a filter is typed as.</summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The prefix, such as <c>team:</c>; the empty string for All.</returns>
    public static string PrefixFor(SearchFilter filter)
    {
        return filter switch
        {
            SearchFilter.All => string.Empty,
            SearchFilter.Teams => "team:",
            SearchFilter.Teammates => "teammate:",
            SearchFilter.Library => "lib:",
            SearchFilter.Tasks => "task:",
            SearchFilter.Rooms => "room:",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown search filter."),
        };
    }

    /// <summary>Finds a known prefix at the very start of the text, ignoring case.</summary>
    /// <param name="boxText">The text to read.</param>
    /// <param name="filter">The filter the prefix sets, when one is found.</param>
    /// <param name="length">The prefix's length in characters, when one is found; otherwise zero.</param>
    /// <returns>Whether a prefix was found.</returns>
    private static bool TryReadPrefix(string boxText, out SearchFilter filter, out int length)
    {
        foreach ((string prefix, SearchFilter prefixFilter) in Prefixes)
        {
            if (boxText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                filter = prefixFilter;
                length = prefix.Length;
                return true;
            }
        }

        filter = SearchFilter.All;
        length = 0;
        return false;
    }
}
