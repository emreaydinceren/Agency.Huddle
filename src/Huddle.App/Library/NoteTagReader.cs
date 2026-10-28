using System.Globalization;
using System.Text;


namespace Agency.Huddle.App.Library;

/// <summary>
/// Reads the frontmatter <c>tags</c> key (Spec §6.17 <i>Frontmatter syntax</i>): a block list, a flow list, a comma string or a single
/// scalar. Only <c>tags</c>, at column 0 and spelled exactly, is read; the other keys are not validated. A malformed <c>tags</c> entry gives
/// no tags, and an unquoted <c>#</c> is a YAML comment, as in Obsidian.
/// </summary>
internal static class NoteTagReader
{
    private const string Key = "tags:";

    /// <summary>The normalised tags in <paramref name="block"/>, in source order, repeats kept; empty when the entry is missing, malformed or holds none.</summary>
    /// <param name="block">The lines between the frontmatter delimiters, from <see cref="NoteFrontmatter.Split"/>.</param>
    internal static IReadOnlyList<string> Read(string block)
    {
        ArgumentNullException.ThrowIfNull(block);

        string[] lines = block.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        int keyLine = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (HasTabIndent(lines[i]))
            {
                return [];
            }

            if (lines[i].StartsWith(Key, StringComparison.Ordinal))
            {
                keyLine = i;
            }
        }

        if (keyLine < 0)
        {
            return [];
        }

        List<string> values = [];
        if (!TryReadValues(lines, keyLine, values))
        {
            return [];
        }

        List<string> tags = [];
        foreach (string value in values)
        {
            string text = value.StartsWith('#') ? value[1..] : value;
            if (NoteTagText.IsAllTagRunes(text) && NoteTagText.TryNormalise(text, out string tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    private static bool HasTabIndent(string line)
    {
        foreach (char c in line)
        {
            if (c == '\t')
            {
                return true;
            }

            if (c != ' ')
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Collects the raw values of the <c>tags</c> entry; <see langword="false"/> when the entry is malformed.</summary>
    private static bool TryReadValues(string[] lines, int keyLine, List<string> values)
    {
        string rest = lines[keyLine][Key.Length..].Trim();
        if (rest.Length == 0 || rest[0] == '#')
        {
            return TryReadBlockList(lines, keyLine + 1, values);
        }

        if (rest[0] == '[')
        {
            return TryReadFlowAcrossLines(lines, keyLine, rest, values);
        }

        if (rest[0] == '{')
        {
            return true;
        }

        if (rest[0] is '"' or '\'')
        {
            return TryReadQuoted(rest, 0, out string value, out _) && Add(values, value, quoted: true);
        }

        foreach (string piece in StripComment(rest).Split(','))
        {
            _ = Add(values, piece.Trim(), quoted: false);
        }

        return true;
    }

    /// <summary>A flow list may continue on the next lines; keeps joining lines until the list closes. Still open at the end of the block is malformed.</summary>
    private static bool TryReadFlowAcrossLines(string[] lines, int keyLine, string first, List<string> values)
    {
        string flow = first;
        for (int next = keyLine + 1; ; next++)
        {
            List<string> attempt = [];
            if (TryReadFlowList(flow, attempt))
            {
                values.AddRange(attempt);
                return true;
            }

            if (next >= lines.Length)
            {
                return false;
            }

            flow = string.Concat(flow, " ", lines[next].Trim());
        }
    }

    /// <summary>Adds a non-empty value. An unquoted YAML null, boolean or number is not a string, so it is not a tag.</summary>
    private static bool Add(List<string> values, string value, bool quoted)
    {
        if (value.Length > 0 && (quoted || !IsYamlNonString(value)))
        {
            values.Add(value);
        }

        return true;
    }

    private static bool IsYamlNonString(string value) =>
        value is "~" or "null" or "Null" or "NULL" or "true" or "True" or "TRUE" or "false" or "False" or "FALSE"
        || (value[0] is (>= '0' and <= '9') or '.' or '+' or '-'
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _));

    private static bool TryReadBlockList(string[] lines, int first, List<string> values)
    {
        for (int i = first; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#' || line == "-")
            {
                continue;
            }

            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                return true;
            }

            string item = line[2..].Trim();
            if (item.Length == 0 || item[0] == '#')
            {
                continue;
            }

            if (item[0] is '"' or '\'')
            {
                if (!TryReadQuoted(item, 0, out string quoted, out _))
                {
                    return false;
                }

                _ = Add(values, quoted, quoted: true);
            }
            else
            {
                _ = Add(values, StripComment(item).Trim(), quoted: false);
            }
        }

        return true;
    }

    private static bool TryReadFlowList(string rest, List<string> values)
    {
        int index = 1;
        while (index < rest.Length)
        {
            while (index < rest.Length && rest[index] == ' ')
            {
                index++;
            }

            if (index >= rest.Length)
            {
                return false;
            }

            if (rest[index] == ']')
            {
                return true;
            }

            if (rest[index] == '#')
            {
                return false;
            }

            if (!TryReadFlowItem(rest, ref index, values))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Reads one flow item and the comma after it; leaves <paramref name="index"/> on the next item or the closing bracket.</summary>
    private static bool TryReadFlowItem(string rest, ref int index, List<string> values)
    {
        if (rest[index] is '"' or '\'')
        {
            if (!TryReadQuoted(rest, index, out string quoted, out int end))
            {
                return false;
            }

            _ = Add(values, quoted, quoted: true);
            index = end;
            while (end < rest.Length && rest[end] == ' ')
            {
                end++;
            }

            if (end >= rest.Length || rest[end] is not (',' or ']'))
            {
                return false;
            }
        }
        else
        {
            int stop = index;
            while (stop < rest.Length && rest[stop] is not (',' or ']'))
            {
                stop++;
            }

            _ = Add(values, rest[index..stop].Trim(), quoted: false);
            index = stop;
        }

        while (index < rest.Length && rest[index] == ' ')
        {
            index++;
        }

        if (index < rest.Length && rest[index] == ',')
        {
            index++;
        }

        return true;
    }

    /// <summary>Reads a quoted scalar starting at <paramref name="start"/>; <see langword="false"/> when its closing quote is missing.</summary>
    private static bool TryReadQuoted(string text, int start, out string value, out int endExclusive)
    {
        char quote = text[start];
        StringBuilder builder = new();
        int i = start + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (quote == '"' && c == '\\' && i + 1 < text.Length)
            {
                _ = builder.Append(text[i + 1]);
                i += 2;
                continue;
            }

            if (c == quote)
            {
                if (quote == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    _ = builder.Append('\'');
                    i += 2;
                    continue;
                }

                value = builder.ToString();
                endExclusive = i + 1;
                return true;
            }

            _ = builder.Append(c);
            i++;
        }

        value = string.Empty;
        endExclusive = text.Length;
        return false;
    }

    /// <summary>Drops a YAML comment: an unquoted <c>#</c> that follows whitespace.</summary>
    private static string StripComment(string text)
    {
        for (int i = 1; i < text.Length; i++)
        {
            if (text[i] == '#' && text[i - 1] is ' ' or '\t')
            {
                return text[..i];
            }
        }

        return text;
    }
}
