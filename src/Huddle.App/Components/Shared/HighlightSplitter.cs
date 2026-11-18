namespace Agency.Huddle.App.Components.Shared;

/// <summary>Cuts a text into the runs <c>SearchHighlight</c> draws, by ordinal case-insensitive search so that no match timeout exists to throw.</summary>
internal static class HighlightSplitter
{
    /// <summary>Splits <paramref name="text"/> around every non-overlapping occurrence of <paramref name="term"/>.</summary>
    /// <param name="text">The text to cut. Null or empty gives no runs.</param>
    /// <param name="term">The term to find. Null, empty or whitespace gives the whole text as one unmatched run.</param>
    /// <returns>The runs in order; their text joins back to <paramref name="text"/>.</returns>
    public static IReadOnlyList<HighlightPart> Split(string? text, string? term)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(term))
        {
            return [new HighlightPart(text, false)];
        }

        List<HighlightPart> parts = [];
        int position = 0;
        while (position < text.Length)
        {
            int found = text.IndexOf(term, position, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                break;
            }

            if (found > position)
            {
                parts.Add(new HighlightPart(text[position..found], false));
            }

            parts.Add(new HighlightPart(text.Substring(found, term.Length), true));
            position = found + term.Length;
        }

        if (position < text.Length)
        {
            parts.Add(new HighlightPart(text[position..], false));
        }

        return parts;
    }
}
