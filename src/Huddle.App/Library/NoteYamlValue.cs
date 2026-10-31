using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Library;

/// <summary>Reads one YAML entry's value into a <see cref="NoteProperty"/> (D-2 step 3, D-6, D-10). Anything it cannot read as a scalar or a list of scalars comes back Unsupported, with the entry's own lines as its source.</summary>
internal static partial class NoteYamlValue
{
    [GeneratedRegex(@"^[|>][+\-]?[1-9]?[+\-]?\s*(#.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockScalarHeader();

    /// <summary>Reads <paramref name="entry"/>; never throws.</summary>
    /// <param name="entry">The entry cut out by <see cref="NoteYamlEntries.Segment"/>.</param>
    internal static NoteProperty Parse(NoteYamlEntry entry)
    {
        string rest = entry.Rest.StartsWith('#') ? string.Empty : entry.Rest;
        if (BlockScalarHeader().IsMatch(rest))
        {
            return ParseBlockScalar(entry, rest);
        }

        List<string> significant = [.. entry.Tail.Where(static line => line.Trim().Length > 0 && line.TrimStart()[0] != '#')];
        if (significant.Exists(HasTabIndent))
        {
            return Unsupported(entry);
        }

        if (rest.Length == 0)
        {
            return significant.Count == 0 ? Scalar(entry, string.Empty, quoted: false) : ParseBelow(entry, significant);
        }

        string text = significant.Count == 0 ? rest : string.Concat(rest, " ", string.Join(' ', significant.Select(static line => line.Trim())));
        return ParseInline(entry, text);
    }

    /// <summary>The value written on the lines below an empty key line: a block list, a multi-line scalar, or a nested map.</summary>
    private static NoteProperty ParseBelow(NoteYamlEntry entry, List<string> significant)
    {
        if (NoteYamlEntries.IsListMarker(significant[0].TrimStart()))
        {
            return ParseBlockList(entry, significant);
        }

        if (significant.Exists(static line => NoteYamlScalars.HasKeyColon(line.Trim())))
        {
            return Unsupported(entry);
        }

        return ParseInline(entry, string.Join(' ', significant.Select(static line => line.Trim())));
    }

    /// <summary>A value that starts on the key's own line: a flow list, a flow map, a quoted scalar or a plain one.</summary>
    private static NoteProperty ParseInline(NoteYamlEntry entry, string text)
    {
        switch (text[0])
        {
            case '[':
                return NoteYamlFlowList.TryRead(text, out List<string> items) ? List(entry, items) : Unsupported(entry);
            case '"' or '\'':
                return ParseQuoted(entry, text);
            case '{' or '&' or '*' or '!' or '%' or '@' or '`':
                return Unsupported(entry);
            default:
                string plain = NoteYamlScalars.StripComment(text).Trim();
                return NoteYamlScalars.HasKeyColon(plain) ? Unsupported(entry) : Scalar(entry, plain, quoted: false);
        }
    }

    private static NoteProperty ParseQuoted(NoteYamlEntry entry, string text)
    {
        if (!NoteYamlScalars.TryReadQuoted(text, 0, out string value, out int end))
        {
            return Unsupported(entry);
        }

        string after = text[end..].Trim();
        return after.Length == 0 || after[0] == '#' ? Scalar(entry, value, quoted: true) : Unsupported(entry);
    }

    /// <summary>A block list: every item at one indentation, each a plain or quoted scalar. An item that is a map or a list, or that continues on a deeper line, makes the whole entry Unsupported.</summary>
    private static NoteProperty ParseBlockList(NoteYamlEntry entry, List<string> significant)
    {
        int indent = LeadingSpaces(significant[0]);
        List<string> items = [];
        foreach (string line in significant)
        {
            string trimmed = line.TrimStart();
            if (LeadingSpaces(line) != indent || !NoteYamlEntries.IsListMarker(trimmed))
            {
                return Unsupported(entry);
            }

            string item = trimmed.Length == 1 ? string.Empty : trimmed[1..].Trim();
            if (item.Length == 0 || item[0] == '#')
            {
                continue;
            }

            if (!TryReadListItem(item, out string? value))
            {
                return Unsupported(entry);
            }

            if (value is not null)
            {
                items.Add(value);
            }
        }

        return List(entry, items);
    }

    /// <summary>Reads one block list item. <paramref name="value"/> is <see langword="null"/> for a YAML null, which is dropped.</summary>
    private static bool TryReadListItem(string item, out string? value)
    {
        value = null;
        if (item[0] is '"' or '\'')
        {
            if (!NoteYamlScalars.TryReadQuoted(item, 0, out string quoted, out int end))
            {
                return false;
            }

            string after = item[end..].Trim();
            value = quoted;
            return after.Length == 0 || after[0] == '#';
        }

        if (item[0] is '[' or '{' or '&' or '*' or '!' or '%' or '@' or '`' || NoteYamlEntries.IsListMarker(item))
        {
            return false;
        }

        string plain = NoteYamlScalars.StripComment(item).Trim();
        if (NoteYamlScalars.HasKeyColon(plain))
        {
            return false;
        }

        value = NoteYamlScalars.IsNull(plain) ? null : plain;
        return true;
    }

    /// <summary>A <c>|</c> or <c>&gt;</c> block scalar: the lines below, less their common indentation. Literal keeps the line breaks; folded joins the lines of a paragraph with a space.</summary>
    private static NoteProperty ParseBlockScalar(NoteYamlEntry entry, string header)
    {
        List<string> lines = [.. entry.Tail];
        int indent = lines.Where(static line => line.Trim().Length > 0).Select(LeadingSpaces).DefaultIfEmpty(0).First();
        List<string> content = [.. lines.Select(line => Dedent(line, indent))];
        string value = header[0] == '|' ? string.Join('\n', content) : Fold(content);
        return value.Length == 0
            ? Make(entry, NotePropertyKind.Text, NotePropertyShape.Empty, string.Empty, [])
            : Make(entry, NotePropertyKind.Text, NotePropertyShape.Scalar, value, []);
    }

    /// <summary>Removes the block scalar's indentation from a line; a blank line is empty, and a line indented less than the block keeps no indentation.</summary>
    private static string Dedent(string line, int indent)
    {
        if (line.Trim().Length == 0)
        {
            return string.Empty;
        }

        return LeadingSpaces(line) >= indent ? line[indent..] : line.TrimStart();
    }

    /// <summary>Folds <paramref name="lines"/>: the lines of a paragraph are joined with a space, and a blank line is a line break.</summary>
    private static string Fold(List<string> lines)
    {
        StringBuilder builder = new();
        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                _ = builder.Append('\n');
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '\n')
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(line);
        }

        return builder.ToString();
    }

    private static NoteProperty Scalar(NoteYamlEntry entry, string value, bool quoted)
    {
        (NotePropertyKind kind, NotePropertyShape shape) = NoteYamlScalars.Infer(value, quoted);
        return Make(entry, kind, shape, shape == NotePropertyShape.Empty ? string.Empty : value, []);
    }

    private static NoteProperty List(NoteYamlEntry entry, IReadOnlyList<string> items) =>
        Make(entry, NotePropertyKind.List, NotePropertyShape.List, string.Empty, items);

    /// <summary>An entry that is nested, malformed or otherwise outside the reference app's types: shown as its source (D-6, D-10).</summary>
    private static NoteProperty Unsupported(NoteYamlEntry entry) =>
        Make(entry, NotePropertyKind.Unsupported, NotePropertyShape.Nested, string.Empty, []);

    private static NoteProperty Make(NoteYamlEntry entry, NotePropertyKind kind, NotePropertyShape shape, string value, IReadOnlyList<string> items) =>
        new(entry.Key, kind, shape, value, items, SourceOf(entry));

    /// <summary>The entry's value lines as written: what follows the colon on the key line, then the lines below, without leading or trailing blank lines.</summary>
    private static string SourceOf(NoteYamlEntry entry)
    {
        List<string> parts = [];
        if (entry.Rest.Length > 0)
        {
            parts.Add(entry.Rest);
        }

        parts.AddRange(entry.Tail);
        int first = 0;
        while (first < parts.Count && parts[first].Trim().Length == 0)
        {
            first++;
        }

        return string.Join('\n', parts.Skip(first));
    }

    private static int LeadingSpaces(string line)
    {
        int count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    /// <summary>Whether the line's indentation holds a tab, which YAML forbids.</summary>
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
}
