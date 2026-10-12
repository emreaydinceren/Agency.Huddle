namespace Agency.Huddle.App.Search;

/// <summary>Cuts the first matching line of a text down to a fixed width around the match (spec §8.3).</summary>
internal static class SearchSnippet
{
    /// <summary>
    /// The first line containing the phrase, trimmed, and cut to <paramref name="width"/> characters
    /// centred on the match when it is longer. Ellipses mark each cut end.
    /// </summary>
    /// <param name="text">The text to search, possibly many lines.</param>
    /// <param name="phrase">The phrase to find, ignoring case.</param>
    /// <param name="width">The window width in characters, before the ellipses.</param>
    /// <returns>The snippet, or null when no line contains the phrase.</returns>
    public static string? Around(string text, string phrase, int width)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return null;
        }

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            int index = line.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            return Window(line, phrase.Length, index, width);
        }

        return null;
    }

    /// <summary>Cuts a line down to a window of <paramref name="width"/> characters around the match at <paramref name="index"/>.</summary>
    /// <param name="line">The trimmed line that contains the match.</param>
    /// <param name="phraseLength">The length of the matched phrase.</param>
    /// <param name="index">Where the match starts in the line.</param>
    /// <param name="width">The window width in characters.</param>
    /// <returns>The window, with an ellipsis at each end that was cut.</returns>
    private static string Window(string line, int phraseLength, int index, int width)
    {
        if (line.Length <= width)
        {
            return line;
        }

        int start = Math.Max(0, index - ((width - phraseLength) / 2));
        start = Math.Min(start, line.Length - width);

        string body = line[start..(start + width)];
        string prefix = start > 0 ? "…" : string.Empty;
        string suffix = start + width < line.Length ? "…" : string.Empty;

        return string.Concat(prefix, body, suffix);
    }
}
