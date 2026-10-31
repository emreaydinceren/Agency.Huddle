namespace Agency.Huddle.SkillPacks.Folders;

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
        FieldWalker walker = new();
        foreach (string rawLine in yamlLines)
        {
            walker.Feed(rawLine.TrimEnd());
        }

        return walker.Finish();
    }

    /// <summary>The state of one walk over the frontmatter lines: the fields so far and at most one open block list or block scalar.</summary>
    private sealed class FieldWalker
    {
        private readonly List<SkillFrontmatterField> fields = [];
        private readonly List<string> listValues = [];
        private readonly List<string> scalarLines = [];
        private string? listKey;
        private string? scalarKey;
        private bool scalarFolded;
        private int? scalarIndent;

        /// <summary>Takes the next line, already trimmed at its end.</summary>
        public void Feed(string line)
        {
            if (this.scalarKey is not null)
            {
                if (this.TryTakeScalarLine(line))
                {
                    return;
                }

                this.FlushScalar();
            }

            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal) && this.listKey is not null)
            {
                this.listValues.Add(YamlScalars.StripQuotes(line.TrimStart()[2..].Trim()));
                return;
            }

            this.FlushList();
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                return;
            }

            this.StartField(line[..colon].Trim(), line[(colon + 1)..].Trim());
        }

        /// <summary>Closes any open list or scalar and returns the fields.</summary>
        public List<SkillFrontmatterField> Finish()
        {
            this.FlushList();
            this.FlushScalar();
            return this.fields;
        }

        /// <summary>Adds the line to the open block scalar; false when the line is outside it.</summary>
        private bool TryTakeScalarLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                this.scalarLines.Add(string.Empty);
                return true;
            }

            int leadingSpaces = line.Length - line.TrimStart(' ').Length;
            if (leadingSpaces > 0 && leadingSpaces >= (this.scalarIndent ??= leadingSpaces))
            {
                this.scalarLines.Add(line[this.scalarIndent.Value..]);
                return true;
            }

            return false;
        }

        /// <summary>Handles a <c>key: value</c> line: opens a block scalar or list, or adds a field.</summary>
        private void StartField(string key, string value)
        {
            if (IsBlockScalarIndicator(value, out bool folded))
            {
                this.scalarKey = key;
                this.scalarFolded = folded;
            }
            else if (value.Length == 0)
            {
                this.listKey = key;
            }
            else if (YamlScalars.TryParseFlowList(value, out IReadOnlyList<string> items))
            {
                if (items.Count > 0)
                {
                    this.fields.Add(new SkillFrontmatterField(key, string.Join("; ", items)));
                }
            }
            else
            {
                this.fields.Add(new SkillFrontmatterField(key, YamlScalars.StripQuotes(value)));
            }
        }

        /// <summary>Emits the open block list as one field and clears it.</summary>
        private void FlushList()
        {
            if (this.listKey is not null && this.listValues.Count > 0)
            {
                this.fields.Add(new SkillFrontmatterField(this.listKey, string.Join("; ", this.listValues)));
            }

            this.listKey = null;
            this.listValues.Clear();
        }

        /// <summary>Emits the open block scalar as one field and clears it.</summary>
        private void FlushScalar()
        {
            if (this.scalarKey is not null)
            {
                this.fields.Add(new SkillFrontmatterField(this.scalarKey, JoinBlockScalar(this.scalarLines, this.scalarFolded).Replace('\n', ' ')));
            }

            this.scalarKey = null;
            this.scalarIndent = null;
            this.scalarLines.Clear();
        }
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

}
