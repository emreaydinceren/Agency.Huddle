namespace Agency.Huddle.App.Search;

/// <summary>Decides how well one field matches a phrase, ordinal and ignoring case (D-6, spec §7.4).</summary>
internal static class SearchMatch
{
    /// <summary>The tier of one candidate against one phrase, or null when it does not match. A blank phrase matches nothing.</summary>
    /// <param name="candidate">The field to test.</param>
    /// <param name="phrase">The phrase the user typed, with any prefix removed.</param>
    /// <returns>The tier, or null.</returns>
    public static MatchTier? Tier(string candidate, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return null;
        }

        if (string.Equals(candidate, phrase, StringComparison.OrdinalIgnoreCase))
        {
            return MatchTier.ExactName;
        }

        if (candidate.StartsWith(phrase, StringComparison.OrdinalIgnoreCase))
        {
            return MatchTier.NamePrefix;
        }

        if (candidate.Contains(phrase, StringComparison.OrdinalIgnoreCase))
        {
            return MatchTier.NameContains;
        }

        return null;
    }

    /// <summary>The tier of the first non-null field that matches, in the order given (OP-6), or null when none does.</summary>
    /// <param name="phrase">The phrase the user typed, with any prefix removed.</param>
    /// <param name="fields">The fields to test, best field first. Null fields are skipped.</param>
    /// <returns>The tier of the first matching field, or null.</returns>
    public static MatchTier? FirstTier(string phrase, params ReadOnlySpan<string?> fields)
    {
        foreach (string? field in fields)
        {
            if (field is null)
            {
                continue;
            }

            MatchTier? tier = Tier(field, phrase);
            if (tier is not null)
            {
                return tier;
            }
        }

        return null;
    }
}
