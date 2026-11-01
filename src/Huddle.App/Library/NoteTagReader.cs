using System.Globalization;
using System.Text.Json;


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
    /// <param name="form">Which spelling the block is written in; a JSON object is read as JSON only when this says so.</param>
    internal static IReadOnlyList<string> Read(string block, NoteFrontmatterForm form)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (form == NoteFrontmatterForm.Json && TryReadJsonValues(block, out List<string>? jsonValues))
        {
            return Normalise(jsonValues);
        }

        string[] lines = block.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        int keyLine = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (NoteYamlScalars.HasTabIndent(lines[i]))
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

        return Normalise(values);
    }

    /// <summary>Turns the raw values into tags: a leading <c>#</c> is dropped and a value that is not a whole tag is skipped.</summary>
    private static List<string> Normalise(List<string> values)
    {
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

    /// <summary>
    /// JSON front matter (D-9): when the block is one JSON object, its <c>tags</c> member is read as a flow list is. A string is one quoted value, an array
    /// gives its string items (a number, a boolean or a null is not a string, so it is skipped), and any other member gives none. A repeated member keeps the last.
    /// </summary>
    /// <returns><see langword="false"/> when the block is not a JSON object, so the YAML reading applies.</returns>
    private static bool TryReadJsonValues(string block, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out List<string>? values)
    {
        values = null;
        if (!block.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(block);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement? tags = null;
            foreach (JsonProperty member in document.RootElement.EnumerateObject())
            {
                if (string.Equals(member.Name, "tags", StringComparison.Ordinal))
                {
                    tags = member.Value;
                }
            }

            values = tags is { } found ? JsonStrings(found) : [];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<string> JsonStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return [.. new[] { element.GetString() ?? string.Empty }.Where(static value => value.Length > 0)];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. element.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? string.Empty)
            .Where(static value => value.Length > 0)];
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
            return NoteYamlScalars.TryReadQuoted(rest, 0, out string value, out _) && Add(values, value, quoted: true);
        }

        foreach (string piece in NoteYamlScalars.StripComment(rest).Split(','))
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
                if (!NoteYamlScalars.TryReadQuoted(item, 0, out string quoted, out _))
                {
                    return false;
                }

                _ = Add(values, quoted, quoted: true);
            }
            else
            {
                _ = Add(values, NoteYamlScalars.StripComment(item).Trim(), quoted: false);
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
            if (!NoteYamlScalars.TryReadQuoted(rest, index, out string quoted, out int end))
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
}
