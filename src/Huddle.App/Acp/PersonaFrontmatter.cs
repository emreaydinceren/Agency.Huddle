namespace Agency.Huddle.App.Acp;

/// <summary>
/// Splits a Persona file's optional leading YAML frontmatter block into an ordered list of
/// top-level fields and the body that follows it, and composes those fields into the job
/// description <c>mcp__team__list_agents</c> shows. Supports shallow YAML — quoted and unquoted
/// scalars, block scalars (<c>&gt;</c> folded, <c>|</c> literal), block lists (<c>- item</c>
/// lines), and bracketed flow lists (<c>[a, b]</c>) — with no third-party YAML library, ported
/// from the narrower <c>Agency.Harness.Markdown.FrontmatterParser</c> (a sibling repo, not
/// referenced by this one). Unlike that parser, a field counts as a list only when it actually
/// uses one of those two explicit list syntaxes: Team's persona frontmatter has quoted scalars
/// containing commas (for example <c>role: 'Router, triage, and cross-workstation
/// continuity'</c>), so guessing a list from comma- or space-separated scalar content, as the
/// source parser does, would misparse them.
/// </summary>
internal static class PersonaFrontmatter
{
    private const string FrontmatterDelimiter = "---";

    /// <summary>
    /// Composes a Persona's job description from its frontmatter: every top-level field whose
    /// key does not start with <c>_</c> becomes one "Key: Value" line, in file order, with the
    /// key title-cased (<c>consult_when</c> becomes <c>Consult When</c>). Fields starting with
    /// <c>_</c> are reserved for future programmatic use and never appear here. Returns
    /// <see cref="string.Empty"/>, never <see langword="null"/>, when the Persona has no
    /// frontmatter or only <c>_</c>-prefixed fields.
    /// </summary>
    /// <param name="personaText">The Persona's raw file text.</param>
    internal static string ComposeJobDescription(string personaText)
    {
        var (fields, _) = Parse(personaText);

        var lines = fields
            .Where(field => !field.Key.StartsWith('_'))
            .Select(field => $"{TitleCase(field.Key)}: {field.Value}");

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Splits <paramref name="personaText"/> into its ordered frontmatter fields and the body
    /// text that follows the closing <c>---</c>. Returns an empty field list and the whole
    /// (trimmed) text as body when no frontmatter block is present, or when the opening
    /// delimiter is never closed — this never throws. A block scalar's embedded line breaks are
    /// collapsed to spaces and a block list's items are joined with <c>"; "</c>, so every
    /// field's value is always exactly one line.
    /// </summary>
    /// <param name="personaText">The Persona's raw file text.</param>
    internal static (IReadOnlyList<PersonaFrontmatterField> Fields, string Body) Parse(string personaText)
    {
        var normalized = personaText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');

        if (lines.Length < 2 || lines[0].Trim() != FrontmatterDelimiter)
        {
            return ([], personaText.TrimStart());
        }

        var closeIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == FrontmatterDelimiter)
            {
                closeIndex = i;
                break;
            }
        }

        if (closeIndex < 0)
        {
            return ([], personaText.TrimStart());
        }

        var frontmatterLines = lines[1..closeIndex];
        var body = string.Join('\n', lines[(closeIndex + 1)..]).TrimStart('\n');

        return (ParseFields(frontmatterLines), body);
    }

    /// <summary>Walks the frontmatter's lines with a small state machine, tracking at most one open block scalar or block list at a time.</summary>
    private static List<PersonaFrontmatterField> ParseFields(string[] yamlLines)
    {
        var fields = new List<PersonaFrontmatterField>();

        string? currentListKey = null;
        var currentListValues = new List<string>();

        string? currentScalarKey = null;
        var currentScalarFolded = false;
        int? currentScalarIndent = null;
        var currentScalarLines = new List<string>();

        void FlushList()
        {
            if (currentListKey is not null && currentListValues.Count > 0)
            {
                fields.Add(new PersonaFrontmatterField(currentListKey, string.Join("; ", currentListValues)));
            }

            currentListKey = null;
            currentListValues.Clear();
        }

        void FlushScalar()
        {
            if (currentScalarKey is not null)
            {
                var value = JoinBlockScalar(currentScalarLines, currentScalarFolded);
                fields.Add(new PersonaFrontmatterField(currentScalarKey, value.Replace('\n', ' ')));
            }

            currentScalarKey = null;
            currentScalarIndent = null;
            currentScalarLines.Clear();
        }

        foreach (var rawLine in yamlLines)
        {
            var line = rawLine.TrimEnd();

            // Inside a block scalar (">" folded or "|" literal) — consume indented lines until dedent.
            if (currentScalarKey is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    currentScalarLines.Add(string.Empty);
                    continue;
                }

                // A frontmatter key is always at column 0, so real block-scalar content must be
                // indented by at least one space; a line with none can't be content even on the
                // block's first line, and must not be mistaken for one when establishing its indent
                // — otherwise an empty scalar immediately followed by the next field swallows it.
                var leadingSpaces = line.Length - line.TrimStart(' ').Length;
                if (leadingSpaces > 0 && leadingSpaces >= (currentScalarIndent ??= leadingSpaces))
                {
                    currentScalarLines.Add(line[currentScalarIndent.Value..]);
                    continue;
                }

                FlushScalar();
            }

            // YAML block list item ("  - value").
            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal) && currentListKey is not null)
            {
                currentListValues.Add(StripYamlQuotes(line.TrimStart()[2..].Trim()));
                continue;
            }

            // Any non-list-item line closes the current block list.
            FlushList();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var colonIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex < 0)
            {
                continue;
            }

            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            if (IsBlockScalarIndicator(value, out var folded))
            {
                currentScalarKey = key;
                currentScalarFolded = folded;
            }
            else if (string.IsNullOrEmpty(value))
            {
                currentListKey = key;
            }
            else if (TryParseFlowList(value, out var flowItems))
            {
                if (flowItems.Count > 0)
                {
                    fields.Add(new PersonaFrontmatterField(key, string.Join("; ", flowItems)));
                }
            }
            else
            {
                fields.Add(new PersonaFrontmatterField(key, StripYamlQuotes(value)));
            }
        }

        FlushList();
        FlushScalar();

        return fields;
    }

    /// <summary>
    /// Recognises the YAML block scalar indicators <c>&gt;</c> (folded) and <c>|</c> (literal),
    /// with optional chomping modifiers (<c>-</c> strip, <c>+</c> keep). Explicit indentation
    /// indicators (for example <c>&gt;2</c>) are not supported — a block's indentation is
    /// inferred from its own first line.
    /// </summary>
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

    /// <summary>
    /// Joins the dedented lines of a block scalar into a single string. Trailing blank lines are
    /// stripped (approximating YAML's default "clip" chomping). A folded scalar (<c>&gt;</c>)
    /// joins lines within a paragraph with spaces and separates paragraphs (blank-line-delimited)
    /// with a newline; a literal scalar (<c>|</c>) preserves its line breaks as-is — the caller
    /// collapses them afterward, since a composed job-description field is always one line.
    /// </summary>
    private static string JoinBlockScalar(List<string> lines, bool folded)
    {
        var end = lines.Count;
        while (end > 0 && lines[end - 1].Length == 0)
        {
            end--;
        }

        var trimmed = lines.Take(end).ToList();

        if (!folded)
        {
            return string.Join('\n', trimmed);
        }

        var paragraphs = new List<string>();
        var current = new List<string>();

        foreach (var line in trimmed)
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

    /// <summary>
    /// Strips a single layer of matching <c>'...'</c> or <c>"..."</c> quoting from a plain scalar
    /// or list-item value. For a single-quoted value, YAML's own escape for an embedded
    /// apostrophe (<c>''</c>) is unescaped to <c>'</c>. Values that are not quoted, or whose
    /// quotes do not match, are returned unchanged.
    /// </summary>
    private static string StripYamlQuotes(string raw)
    {
        if (raw.Length >= 2)
        {
            var first = raw[0];
            var last = raw[^1];

            if (first == last && (first == '\'' || first == '"'))
            {
                var inner = raw[1..^1];
                return first == '\'' ? inner.Replace("''", "'") : inner;
            }
        }

        return raw;
    }

    /// <summary>
    /// Recognises a YAML bracketed flow list, for example <c>['Finances', 'Email']</c>, and splits
    /// it into quote-stripped, trimmed items — dropping empty ones, so <c>[]</c> yields no items.
    /// A value that is not wrapped in matching <c>[...]</c> is not a flow list.
    /// </summary>
    private static bool TryParseFlowList(string value, out List<string> items)
    {
        if (value.Length < 2 || value[0] != '[' || value[^1] != ']')
        {
            items = [];
            return false;
        }

        items = SplitFlowListItems(value[1..^1])
            .Select(item => StripYamlQuotes(item.Trim()))
            .Where(item => item.Length > 0)
            .ToList();
        return true;
    }

    /// <summary>
    /// Splits a flow list's inner text on top-level commas, tracking whether a <c>'</c> or <c>"</c>
    /// is currently open so a comma inside a quoted item (for example <c>'Router, triage'</c>) does
    /// not split it — the same real-data concern that keeps plain scalars from being guessed as
    /// lists by their comma content.
    /// </summary>
    private static List<string> SplitFlowListItems(string inner)
    {
        var items = new List<string>();
        var current = string.Empty;
        char? openQuote = null;

        foreach (var ch in inner)
        {
            if (openQuote is not null)
            {
                current += ch;
                if (ch == openQuote.Value)
                {
                    openQuote = null;
                }

                continue;
            }

            if (ch is '\'' or '"')
            {
                openQuote = ch;
                current += ch;
                continue;
            }

            if (ch == ',')
            {
                items.Add(current);
                current = string.Empty;
                continue;
            }

            current += ch;
        }

        items.Add(current);
        return items;
    }

    /// <summary>Splits a frontmatter key on <c>_</c>/<c>-</c> and capitalizes each word.</summary>
    private static string TitleCase(string key)
    {
        var words = key.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
        var titled = words.Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
        return string.Join(' ', titled);
    }
}
