using System.Text;

namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>One <c>key: value</c> line of a <c>SKILL.md</c>'s frontmatter.</summary>
/// <param name="Key">The key as written.</param>
/// <param name="Value">The value on one line: quotes stripped, a block scalar collapsed, list items joined with <c>"; "</c>.</param>
public sealed record SkillFrontmatterField(string Key, string Value);

/// <summary>
/// Reads the frontmatter of a <c>SKILL.md</c> (§6.3). The library cannot use the app's <c>PersonaFrontmatter</c>, so this
/// is a port of its <c>Parse</c> for the shapes a <c>SKILL.md</c> has: <c>key: value</c> lines between <c>---</c> fences,
/// quoted values, flow and block lists, and folded or literal block scalars collapsed to one line. A parity test holds
/// the two to the same answers.
/// </summary>
public static class SkillFrontmatter
{
    private const string Delimiter = "---";

    /// <summary>The frontmatter fields in file order; empty when there is no frontmatter block or it is never closed.
    /// Never throws.</summary>
    public static IReadOnlyList<SkillFrontmatterField> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != Delimiter)
        {
            return [];
        }

        int close = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == Delimiter)
            {
                close = i;
                break;
            }
        }

        return close < 0 ? [] : ParseFields(lines[1..close]);
    }

    /// <summary>The value of the first field named <paramref name="key"/>, ignoring case, or null.</summary>
    public static string? Get(IReadOnlyList<SkillFrontmatterField> fields, string key)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(key);
        return fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>Walks the frontmatter lines, tracking at most one open block scalar or block list.</summary>
    private static List<SkillFrontmatterField> ParseFields(string[] yamlLines)
    {
        List<SkillFrontmatterField> fields = [];
        string? listKey = null;
        List<string> listValues = [];
        string? scalarKey = null;
        bool scalarFolded = false;
        int? scalarIndent = null;
        List<string> scalarLines = [];

        void FlushList()
        {
            if (listKey is not null && listValues.Count > 0)
            {
                fields.Add(new SkillFrontmatterField(listKey, string.Join("; ", listValues)));
            }

            listKey = null;
            listValues.Clear();
        }

        void FlushScalar()
        {
            if (scalarKey is not null)
            {
                fields.Add(new SkillFrontmatterField(scalarKey, JoinBlockScalar(scalarLines, scalarFolded).Replace('\n', ' ')));
            }

            scalarKey = null;
            scalarIndent = null;
            scalarLines.Clear();
        }

        foreach (string rawLine in yamlLines)
        {
            string line = rawLine.TrimEnd();
            if (scalarKey is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    scalarLines.Add(string.Empty);
                    continue;
                }

                int leadingSpaces = line.Length - line.TrimStart(' ').Length;
                if (leadingSpaces > 0 && leadingSpaces >= (scalarIndent ??= leadingSpaces))
                {
                    scalarLines.Add(line[scalarIndent.Value..]);
                    continue;
                }

                FlushScalar();
            }

            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal) && listKey is not null)
            {
                listValues.Add(StripQuotes(line.TrimStart()[2..].Trim()));
                continue;
            }

            FlushList();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                continue;
            }

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            if (IsBlockScalarIndicator(value, out bool folded))
            {
                scalarKey = key;
                scalarFolded = folded;
            }
            else if (value.Length == 0)
            {
                listKey = key;
            }
            else if (TryParseFlowList(value, out List<string> items))
            {
                if (items.Count > 0)
                {
                    fields.Add(new SkillFrontmatterField(key, string.Join("; ", items)));
                }
            }
            else
            {
                fields.Add(new SkillFrontmatterField(key, StripQuotes(value)));
            }
        }

        FlushList();
        FlushScalar();
        return fields;
    }

    /// <summary>Recognises <c>&gt;</c> (folded) and <c>|</c> (literal), with an optional <c>-</c> or <c>+</c>.</summary>
    private static bool IsBlockScalarIndicator(string value, out bool folded)
    {
        switch (value)
        {
            case ">" or ">-" or ">+":
                folded = true;
                return true;
            case "|" or "|-" or "|+":
                folded = false;
                return true;
            default:
                folded = false;
                return false;
        }
    }

    /// <summary>Joins a block scalar's dedented lines: trailing blank lines dropped; a folded scalar joins lines of a
    /// paragraph with spaces and paragraphs with a newline; a literal scalar keeps its breaks.</summary>
    private static string JoinBlockScalar(List<string> lines, bool folded)
    {
        int end = lines.Count;
        while (end > 0 && lines[end - 1].Length == 0)
        {
            end--;
        }

        List<string> trimmed = [.. lines.Take(end)];
        if (!folded)
        {
            return string.Join('\n', trimmed);
        }

        List<string> paragraphs = [];
        List<string> current = [];
        foreach (string line in trimmed)
        {
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    paragraphs.Add(string.Join(' ', current));
                    current.Clear();
                }

                continue;
            }

            current.Add(line);
        }

        if (current.Count > 0)
        {
            paragraphs.Add(string.Join(' ', current));
        }

        return string.Join('\n', paragraphs);
    }

    /// <summary>Strips one layer of matching quotes; <c>''</c> inside single quotes becomes <c>'</c>.</summary>
    private static string StripQuotes(string raw)
    {
        if (raw.Length >= 2 && raw[0] == raw[^1] && raw[0] is '\'' or '"')
        {
            string inner = raw[1..^1];
            return raw[0] == '\'' ? inner.Replace("''", "'", StringComparison.Ordinal) : inner;
        }

        return raw;
    }

    /// <summary>Recognises a bracketed flow list and splits it on top-level commas.</summary>
    private static bool TryParseFlowList(string value, out List<string> items)
    {
        if (value.Length < 2 || value[0] != '[' || value[^1] != ']')
        {
            items = [];
            return false;
        }

        items = [.. SplitFlowListItems(value[1..^1]).Select(static item => StripQuotes(item.Trim())).Where(static item => item.Length > 0)];
        return true;
    }

    /// <summary>Splits a flow list's inner text on commas outside quotes.</summary>
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
