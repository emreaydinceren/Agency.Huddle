namespace Agency.Huddle.App.Library;

/// <summary>Cuts a YAML front matter block into its top-level entries (D-1): a key line at column 0 and the lines below it that belong to it.</summary>
internal static class NoteYamlEntries
{
    /// <summary>Segments <paramref name="block"/> into entries, in file order. A line at column 0 that is neither a key nor a continuation is ignored.</summary>
    /// <param name="block">The YAML lines between the delimiters.</param>
    internal static List<NoteYamlEntry> Segment(string block)
    {
        string[] lines = block.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        List<NoteYamlEntry> entries = [];
        string? key = null;
        string rest = string.Empty;
        List<string> tail = [];
        foreach (string line in lines)
        {
            if (TryParseKeyLine(line, out string newKey, out string newRest))
            {
                Flush(entries, key, rest, tail);
                key = newKey;
                rest = newRest;
                tail = [];
            }
            else if (key is not null && Continues(line, rest))
            {
                tail.Add(line);
            }
        }

        Flush(entries, key, rest, tail);
        return entries;
    }

    /// <summary>Whether <paramref name="line"/> at column 0 belongs to the entry whose key line ended with <paramref name="rest"/>.</summary>
    private static bool Continues(string line, string rest) =>
        line.Length == 0
        || line[0] is ' ' or '\t' or '#'
        || IsListMarker(line)
        || (rest.Length > 0 && rest[0] is '[' or '{' or '"' or '\'');

    /// <summary>Whether <paramref name="line"/> is a block list item, <c>- x</c> or a lone dash.</summary>
    internal static bool IsListMarker(string line) => line.Length > 0 && line[0] == '-' && (line.Length == 1 || line[1] is ' ' or '\t');

    private static void Flush(List<NoteYamlEntry> entries, string? key, string rest, List<string> tail)
    {
        if (key is null)
        {
            return;
        }

        int count = tail.Count;
        while (count > 0 && tail[count - 1].Trim().Length == 0)
        {
            count--;
        }

        entries.Add(new NoteYamlEntry(key, rest, tail.GetRange(0, count)));
    }

    /// <summary>Reads <c>key: rest</c>. The key may be quoted; a plain key ends at the first colon followed by a space or the end.</summary>
    private static bool TryParseKeyLine(string line, out string key, out string rest)
    {
        key = string.Empty;
        rest = string.Empty;
        if (line.Length == 0 || line[0] is ' ' or '\t' or '#' or '[' or '{' or '|' or '>' or '&' or '*' or '!' or '%' or '@' or '`' or '?' || IsListMarker(line))
        {
            return false;
        }

        int colon;
        if (line[0] is '"' or '\'')
        {
            if (!NoteYamlScalars.TryReadQuoted(line, 0, out key, out int end))
            {
                return false;
            }

            colon = end;
            while (colon < line.Length && line[colon] == ' ')
            {
                colon++;
            }

            if (!IsKeyColon(line, colon))
            {
                return false;
            }
        }
        else
        {
            colon = 0;
            while (colon < line.Length && !IsKeyColon(line, colon))
            {
                colon++;
            }

            if (colon >= line.Length)
            {
                return false;
            }

            key = line[..colon].TrimEnd();
        }

        rest = line[(colon + 1)..].Trim();
        return key.Length > 0;
    }

    private static bool IsKeyColon(string line, int index) =>
        index < line.Length && line[index] == ':' && (index + 1 == line.Length || line[index + 1] is ' ' or '\t');
}
