namespace Agency.Huddle.App.Acp;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Agency.Huddle.Contracts;

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
/// source parser does, would misparse them. <see cref="TryReadIdentity"/> is the one deliberate
/// exception: it splits the <c>Teams</c> field on commas, but only that field, and only after
/// this generic parse has already run.
/// </summary>
internal static class PersonaFrontmatter
{
    private const string FrontmatterDelimiter = "---";
    // Case here is cosmetic, not functional: every lookup against these compares with
    // StringComparison.OrdinalIgnoreCase, so "Name" matches a file's "name:" line just as well.
    // PascalCase is used because these constants also flow straight into error messages (a
    // duplicate-key message names the key as written here), and the new frontmatter spec writes
    // them capitalized.
    private const string NameKey = "Name";
    private const string TitleKey = "Title";
    private const string AliasKey = "Alias";
    private const string TeamsKey = "Teams";
    private const string AdapterKey = "Adapter";

    /// <summary>
    /// Frontmatter keys excluded from <see cref="ComposeJobDescription"/> beyond the <c>_</c>-prefix
    /// rule. <c>Name</c> is excluded because <c>list_agents</c> already prints the Persona's name
    /// on the bullet line above the job description, and repeating it as "Name: Jarvis" immediately
    /// under that bullet is noise. <c>Title</c>, <c>Alias</c> and <c>Teams</c> stay: they are useful
    /// to a reading agent, and <c>Alias</c> in particular tells one that <c>@jar</c> is a working
    /// handle. <c>Adapter</c> is excluded too (Spec §7.2, §12 E-6): <c>mcp__team__list_agents</c>
    /// describes a Teammate to other Agents, and which Adapter runs it is not something any Agent
    /// can act on — without this exclusion, every Teammate's job description would gain a line
    /// reading "Adapter: agency", model-facing text about Huddle's own plumbing.
    /// </summary>
    private static readonly HashSet<string> JobDescriptionExcludedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        NameKey,
        AdapterKey,
    };

    /// <summary>
    /// Composes a Persona's job description from its frontmatter: every top-level field whose
    /// key does not start with <c>_</c>, and is not in <see cref="JobDescriptionExcludedKeys"/>,
    /// becomes one "Key: Value" line, in file order, with the key title-cased (<c>consult_when</c>
    /// becomes <c>Consult When</c>). Fields starting with <c>_</c> are reserved for future
    /// programmatic use and never appear here. Returns <see cref="string.Empty"/>, never
    /// <see langword="null"/>, when the Persona has no frontmatter or only excluded fields.
    /// </summary>
    /// <param name="personaText">The Persona's raw file text.</param>
    internal static string ComposeJobDescription(string personaText)
    {
        var (fields, _) = Parse(personaText);

        var lines = fields
            .Where(field => !field.Key.StartsWith('_') && !JobDescriptionExcludedKeys.Contains(field.Key))
            .Select(field => $"{TitleCase(field.Key)}: {field.Value}");

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Reads and validates the structural identity fields (<see cref="PersonaIdentity.Name"/>,
    /// <see cref="PersonaIdentity.Title"/>, <see cref="PersonaIdentity.Alias"/> and
    /// <see cref="PersonaIdentity.Teams"/>) out of a Persona's raw file text, reusing
    /// <see cref="Parse"/>. A malformed Persona file — a required field that is missing, blank, or
    /// repeated, or a Name/Alias that fails <see cref="NameRules.IsValidAgentName(string?)"/> — is
    /// an expected outcome here, not an exceptional one, so this never throws: it returns
    /// <see langword="false"/> and an <paramref name="error"/> naming the offending field instead.
    /// Key lookup is case-insensitive, so both <c>Name:</c> and <c>name:</c> are recognised.
    /// </summary>
    /// <param name="personaText">The Persona's raw file text.</param>
    /// <param name="identity">
    /// The parsed, validated identity when this returns <see langword="true"/>; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <param name="error">
    /// A message naming the field that failed when this returns <see langword="false"/>;
    /// otherwise <see cref="string.Empty"/>.
    /// </param>
    /// <returns><see langword="true"/> if every required field was present and valid.</returns>
    internal static bool TryReadIdentity(
        string personaText,
        [NotNullWhen(true)] out PersonaIdentity? identity,
        out string error)
    {
        identity = null;
        var (fields, _) = Parse(personaText);

        if (!TryGetField(fields, NameKey, out var rawName, out error)
            || !TryGetField(fields, TitleKey, out var rawTitle, out error)
            || !TryGetField(fields, AliasKey, out var rawAlias, out error)
            || !TryGetField(fields, TeamsKey, out var rawTeams, out error)
            || !TryGetField(fields, AdapterKey, out var rawAdapter, out error))
        {
            return false;
        }

        if (!TryRequireNonBlank(rawName, "Name", out var name, out error)
            || !TryRequireNonBlank(rawTitle, "Title", out var title, out error)
            || !TryRequireNonBlank(rawAlias, "Alias", out var alias, out error))
        {
            return false;
        }

        if (!NameRules.IsValidAgentName(name))
        {
            error = $"Persona frontmatter field 'Name' has an invalid value: '{name}'.";
            return false;
        }

        if (!NameRules.IsValidAgentName(alias))
        {
            error = $"Persona frontmatter field 'Alias' has an invalid value: '{alias}'.";
            return false;
        }

        var adapter = string.IsNullOrWhiteSpace(rawAdapter) ? null : rawAdapter;

        identity = new PersonaIdentity(name, title, alias, SplitTeams(rawTeams), adapter);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Composes a brand-new Persona file's full text: a canonical frontmatter block built from
    /// <paramref name="identity"/>'s fields, followed by <paramref name="body"/> unchanged.
    /// The write-side counterpart to <see cref="TryReadIdentity"/> - <see cref="PersonaStore.Add"/>
    /// calls this so a saved Persona's file always agrees with the identity its caller collected,
    /// rather than composing frontmatter of its own that could drift from what this parser accepts.
    /// Every scalar is single-quoted, with YAML's own escape for an embedded apostrophe (<c>''</c>)
    /// applied, and every key is lowercase - the style real Persona files already use.
    /// <see cref="PersonaIdentity.Teams"/> becomes a bracketed flow list, and the whole
    /// <c>teams:</c> line is omitted when it is empty: <see cref="PersonaIdentity.Teams"/> is
    /// optional, and an empty field is not how a person would write "no Teams" by hand. Emitted
    /// key order is stable - <c>name</c>, <c>title</c>, <c>alias</c>, <c>teams</c>, <c>adapter</c> -
    /// because <c>PromptGoldenTests</c> and any file round-trip depend on it. The <c>adapter:</c>
    /// line is written only when <see cref="PersonaIdentity.Adapter"/> is non-null (Spec §7.2,
    /// §12 E-5): without this, a value written at Create time would be silently destroyed, since
    /// this method previously emitted only the four identity keys.
    /// </summary>
    /// <param name="identity">The Persona's Name, Title, Alias, Teams and Adapter to write as frontmatter.</param>
    /// <param name="body">The Persona's system-prompt body, written back unchanged after the frontmatter.</param>
    internal static string Compose(PersonaIdentity identity, string body)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(body);

        var lines = new List<string>
        {
            FrontmatterDelimiter,
            $"name: {QuoteScalar(identity.Name)}",
            $"title: {QuoteScalar(identity.Title)}",
            $"alias: {QuoteScalar(identity.Alias)}",
        };

        if (identity.Teams.Count > 0)
        {
            lines.Add($"teams: [{string.Join(", ", identity.Teams.Select(QuoteScalar))}]");
        }

        if (identity.Adapter is not null)
        {
            lines.Add($"adapter: {QuoteScalar(identity.Adapter)}");
        }

        lines.Add(FrontmatterDelimiter);
        lines.Add(body);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Rewrites one top-level scalar field's value inside <paramref name="personaText"/>'s
    /// leading YAML frontmatter block, leaving every other frontmatter line - and the whole body
    /// after the closing delimiter - byte-identical, including the original line-ending style.
    /// </summary>
    /// <param name="personaText">The Persona's raw file text.</param>
    /// <param name="key">
    /// The field's key. Matched against each top-level line case-insensitively, the same way
    /// <see cref="TryGetField"/> compares a parsed field's key, so <c>name:</c> and <c>Name:</c>
    /// both hit. When absent from an otherwise valid frontmatter block, a new
    /// <c>key: value</c> line is inserted immediately before the closing delimiter.
    /// </param>
    /// <param name="value">The field's new value, written through <see cref="QuoteScalar"/>.</param>
    /// <returns>
    /// <paramref name="personaText"/> with the one line rewritten, or with the new line inserted;
    /// or <paramref name="personaText"/> itself, unchanged, when there is no frontmatter block, or
    /// when the matched key's existing value is a block scalar (<c>&gt;</c>/<c>|</c>) or a block
    /// list (a following <c>- item</c> line).
    /// </returns>
    /// <remarks>
    /// Not built on <see cref="Compose"/> or <see cref="Parse"/>. <see cref="Compose"/> emits only
    /// the four identity keys, so recomposing a real Persona file through it would drop every
    /// other frontmatter field - exactly the fields <see cref="ComposeJobDescription"/> reads for
    /// a Teammate's job description. <see cref="Parse"/> is not round-trip safe either, since it
    /// collapses a block scalar's line breaks to spaces and joins a list's items with <c>"; "</c>.
    /// A multi-line value cannot be replaced by a one-line edit, so this method leaves a block
    /// scalar or block list untouched and returns the input unchanged instead of flattening it -
    /// the caller is expected to detect that and degrade its control to read-only.
    /// </remarks>
    internal static string WriteScalarField(string personaText, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(personaText);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var lines = SplitKeepingLineEndings(personaText);

        if (lines.Count < 2 || lines[0].Text.Trim() != FrontmatterDelimiter)
        {
            return personaText;
        }

        var closeIndex = -1;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].Text.Trim() == FrontmatterDelimiter)
            {
                closeIndex = i;
                break;
            }
        }

        if (closeIndex < 0)
        {
            return personaText;
        }

        for (var i = 1; i < closeIndex; i++)
        {
            var rawLine = lines[i].Text;

            // A top-level key is always at column 0; an indented line is block-scalar or
            // block-list content belonging to whichever key preceded it, never a new key.
            if (rawLine.Length == 0 || rawLine[0] is ' ' or '\t')
            {
                continue;
            }

            var colonIndex = rawLine.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex < 0)
            {
                continue;
            }

            var lineKey = rawLine[..colonIndex].Trim();
            if (!string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existingValue = rawLine[(colonIndex + 1)..].Trim();

            if (IsBlockScalarIndicator(existingValue, out _))
            {
                return personaText;
            }

            if (existingValue.Length == 0 && IsFollowedByBlockListItems(lines, i + 1, closeIndex))
            {
                return personaText;
            }

            lines[i] = ($"{rawLine[..colonIndex]}: {QuoteScalar(value)}", lines[i].Terminator);
            return Join(lines);
        }

        // The key is absent from an otherwise valid block: insert it before the closing delimiter,
        // reusing the terminator already in use inside the block (or the opening line's, when the
        // block is empty) so the file's line-ending style carries over to the new line too.
        var insertedTerminator = closeIndex > 1 ? lines[closeIndex - 1].Terminator : lines[0].Terminator;
        lines.Insert(closeIndex, ($"{key}: {QuoteScalar(value)}", insertedTerminator));
        return Join(lines);
    }

    /// <summary>
    /// Scans forward from <paramref name="startIndex"/>, skipping blank lines, and reports whether
    /// the first non-blank line found before <paramref name="closeIndex"/> is a YAML block-list
    /// item (<c>- item</c>). Used by <see cref="WriteScalarField"/> to tell a genuinely blank
    /// scalar value apart from a block list's header line, which also has nothing after its colon.
    /// </summary>
    private static bool IsFollowedByBlockListItems(
        List<(string Text, string Terminator)> lines,
        int startIndex,
        int closeIndex)
    {
        for (var i = startIndex; i < closeIndex; i++)
        {
            var trimmed = lines[i].Text.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            return trimmed.StartsWith("- ", StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines, pairing each one with the exact line-ending
    /// sequence that followed it (<c>"\r\n"</c>, <c>"\n"</c>, or <see cref="string.Empty"/> for a
    /// final line with no trailing newline) so <see cref="Join"/> can reassemble the original text
    /// byte-for-byte apart from the one edited or inserted line.
    /// Unlike <see cref="Parse"/>, this never normalizes <c>"\r\n"</c> to <c>"\n"</c> - a Persona
    /// file is edited by a Human in a text editor, not sent to a model, so its line endings are
    /// the Human's to keep.
    /// </summary>
    private static List<(string Text, string Terminator)> SplitKeepingLineEndings(string text)
    {
        var lines = new List<(string Text, string Terminator)>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            var hasCarriageReturn = i > start && text[i - 1] == '\r';
            var textEnd = hasCarriageReturn ? i - 1 : i;
            lines.Add((text[start..textEnd], hasCarriageReturn ? "\r\n" : "\n"));
            start = i + 1;
        }

        if (start < text.Length)
        {
            lines.Add((text[start..], string.Empty));
        }

        return lines;
    }

    /// <summary>Rejoins lines produced by <see cref="SplitKeepingLineEndings"/> back into one string, each with its own terminator.</summary>
    private static string Join(List<(string Text, string Terminator)> lines)
    {
        StringBuilder builder = new();

        foreach (var (text, terminator) in lines)
        {
            builder.Append(text).Append(terminator);
        }

        return builder.ToString();
    }

    /// <summary>Single-quotes a scalar, doubling any interior <c>'</c> per YAML's own escape for it - the inverse of <see cref="StripYamlQuotes"/>'s single-quote branch.</summary>
    private static string QuoteScalar(string value) => $"'{value.Replace("'", "''")}'";

    /// <summary>
    /// Looks up one top-level frontmatter field by key, case-insensitively. Fails, naming the key,
    /// if it was written more than once — Team's shallow parser keeps every occurrence in file
    /// order with no de-duplication, so a repeat can only be an authoring mistake, never a silent
    /// "last one wins".
    /// </summary>
    private static bool TryGetField(
        IReadOnlyList<PersonaFrontmatterField> fields,
        string key,
        out string? value,
        out string error)
    {
        value = null;
        var found = false;

        foreach (var field in fields)
        {
            if (!string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (found)
            {
                value = null;
                error = $"Persona frontmatter has a duplicate '{key}' field.";
                return false;
            }

            value = field.Value;
            found = true;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Requires a field's raw value to be present and non-blank. Missing, empty and
    /// whitespace-only are all the same failure — a required field with nothing meaningful in it —
    /// and are reported alike, naming <paramref name="fieldName"/>.
    /// </summary>
    private static bool TryRequireNonBlank(
        string? rawValue,
        string fieldName,
        [NotNullWhen(true)] out string? value,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = null;
            error = $"Persona frontmatter is missing required field '{fieldName}'.";
            return false;
        }

        value = rawValue;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Splits an already-parsed <c>Teams</c> value into individual team names. <see cref="Parse"/>
    /// hands this exactly one line, in one of three shapes: a comma-separated plain scalar (for
    /// example <c>Business, Household</c>), or a bracketed flow list or a block list, both of which
    /// <see cref="Parse"/> has already joined with <c>"; "</c> — so splitting on both <c>,</c> and
    /// <c>;</c> here covers every shape uniformly, and only for this one field: the generic
    /// <see cref="Parse"/> above never guesses a list from comma content, on purpose. Each item is
    /// trimmed and empty items are dropped; a name repeated later, compared case-insensitively, is
    /// collapsed into its first occurrence, so the list carries no duplicates, but order among the
    /// surviving items is otherwise preserved. Returns an empty list, never <see langword="null"/>,
    /// for a missing or blank field.
    /// </summary>
    private static List<string> SplitTeams(string? rawTeams)
    {
        if (string.IsNullOrWhiteSpace(rawTeams))
        {
            return [];
        }

        var teams = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawTeam in rawTeams.Split([',', ';']))
        {
            var team = rawTeam.Trim();
            if (team.Length > 0 && seen.Add(team))
            {
                teams.Add(team);
            }
        }

        return teams;
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
