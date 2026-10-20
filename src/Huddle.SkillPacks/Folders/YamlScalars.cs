using System.Text;

namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>
/// The YAML scalar rules both frontmatter parsers apply - <see cref="SkillFrontmatter"/> here and the
/// app's <c>PersonaFrontmatter</c> - so the two cannot drift on how a quoted value or a flow list
/// reads. The parsers stay separate (this library cannot reference the app); only these leaves are
/// shared, and the app reaches them through its reference to this library.
/// </summary>
public static class YamlScalars
{
    /// <summary>
    /// Strips a single layer of matching <c>'...'</c> or <c>"..."</c> quoting from a plain scalar or
    /// list-item value. For a single-quoted value, YAML's own escape for an embedded apostrophe
    /// (<c>''</c>) is unescaped to <c>'</c>. Values that are not quoted, or whose quotes do not
    /// match, are returned unchanged.
    /// </summary>
    /// <param name="raw">The value as written.</param>
    /// <returns>The value without its quotes.</returns>
    public static string StripQuotes(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (raw.Length >= 2 && raw[0] == raw[^1] && raw[0] is '\'' or '"')
        {
            string inner = raw[1..^1];
            return raw[0] == '\'' ? inner.Replace("''", "'", StringComparison.Ordinal) : inner;
        }

        return raw;
    }

    /// <summary>
    /// Recognises a YAML bracketed flow list, for example <c>['Finances', 'Email']</c>, and splits it
    /// into quote-stripped, trimmed items - dropping empty ones, so <c>[]</c> yields no items. A value
    /// that is not wrapped in matching <c>[...]</c> is not a flow list.
    /// </summary>
    /// <param name="value">The value after the key's colon, trimmed.</param>
    /// <param name="items">The items, or an empty list when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a flow list.</returns>
    public static bool TryParseFlowList(string value, out IReadOnlyList<string> items)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length < 2 || value[0] != '[' || value[^1] != ']')
        {
            items = [];
            return false;
        }

        items = [.. SplitFlowListItems(value[1..^1]).Select(static item => StripQuotes(item.Trim())).Where(static item => item.Length > 0)];
        return true;
    }

    /// <summary>
    /// Splits a flow list's inner text on top-level commas, tracking whether a <c>'</c> or <c>"</c> is
    /// currently open so a comma inside a quoted item (for example <c>'Router, triage'</c>) does not
    /// split it.
    /// </summary>
    /// <param name="inner">The text between the brackets.</param>
    private static List<string> SplitFlowListItems(string inner)
    {
        List<string> items = [];
        StringBuilder current = new();
        char? openQuote = null;
        foreach (char ch in inner)
        {
            if (openQuote is not null)
            {
                current.Append(ch);
                if (ch == openQuote.Value)
                {
                    openQuote = null;
                }

                continue;
            }

            if (ch is '\'' or '"')
            {
                openQuote = ch;
                current.Append(ch);
                continue;
            }

            if (ch == ',')
            {
                items.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        items.Add(current.ToString());
        return items;
    }
}
