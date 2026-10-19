using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The parsed <c>Process.md</c> (PR-6, PR-7, Spec §Process): the frontmatter keys Huddle acts on and the
/// outline headings the body is expected to carry. Unreadable optional values read as absent rather than
/// failing the whole document, so a hand edit never locks a Team out of its process.
/// </summary>
/// <param name="Title">The frontmatter title, else the first level-one heading, else empty.</param>
/// <param name="Template">The template the document was copied from, or <see langword="null"/>.</param>
/// <param name="Gates">The known hard gates the document declares, in file order, without duplicates.</param>
/// <param name="Retro">The retrospective cadence, or <see langword="null"/>.</param>
/// <param name="Digest">The digest cadence, or <see langword="null"/>.</param>
/// <param name="WipLimit">The work-in-progress limit, or <see langword="null"/>.</param>
/// <param name="CheckCommand">The command whose exit code the checks gate looks for, or <see langword="null"/>.</param>
/// <param name="UnknownGates">Gate ids in the frontmatter that Huddle does not know, in file order.</param>
/// <param name="MissingSections">PR-6 outline headings absent from the body, in outline order.</param>
/// <param name="DefinitionOfDone">The items of the optional <c>## Definition of Done</c> section, in file order.</param>
/// <param name="Text">The whole document text, verbatim.</param>
internal sealed record ProcessDocument(
    string Title,
    VersionedRef? Template,
    IReadOnlyList<GateId> Gates,
    RetroCadence? Retro,
    DigestCadence? Digest,
    int? WipLimit,
    string? CheckCommand,
    IReadOnlyList<string> UnknownGates,
    IReadOnlyList<string> MissingSections,
    IReadOnlyList<DoneItem> DefinitionOfDone,
    string Text)
{
    private const string Delimiter = "---";

    private static readonly string[] OutlineHeadings =
    [
        "When it applies",
        "Stages",
        "Who does what",
        "If a role is not hired",
        "Gates",
        "Cadence",
        "Escalation",
    ];

    /// <summary>Parses the text of a <c>Process.md</c> file.</summary>
    /// <param name="text">The file text, with LF or CRLF line endings.</param>
    /// <param name="document">The parsed document, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the text has no closed frontmatter block.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out ProcessDocument? document)
    {
        document = null;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (!string.Equals(lines[0].TrimEnd(), Delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        int closing = Array.FindIndex(lines, 1, static line => string.Equals(line.TrimEnd(), Delimiter, StringComparison.Ordinal));
        if (closing < 0)
        {
            return false;
        }

        Dictionary<string, string> values = ReadFrontmatter(lines, closing);

        string[] body = lines[(closing + 1)..];
        (List<GateId> gates, List<string> unknownGates) = ReadGates(values);

        VersionedRef? template = null;
        if (values.TryGetValue("template", out string? templateText))
        {
            _ = VersionedRef.TryParse(Unquote(templateText), out template);
        }

        string title = values.TryGetValue("title", out string? titleText) ? Unquote(titleText) : string.Empty;
        if (title.Length == 0)
        {
            title = body.FirstOrDefault(static line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? string.Empty;
        }

        int? wip = ReadWip(values);

        string? checkCommand = values.TryGetValue("checkCommand", out string? commandText) ? Unquote(commandText) : null;
        HashSet<string> headings = body
            .Where(static line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(static line => line[3..].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        document = new ProcessDocument(
            title,
            template,
            gates,
            ReadCadence(values, "retro", out int retroEvery, out CadenceUnit retroUnit) ? new RetroCadence(retroEvery, retroUnit) : null,
            ReadCadence(values, "digest", out int digestEvery, out CadenceUnit digestUnit) ? new DigestCadence(digestEvery, digestUnit) : null,
            wip,
            string.IsNullOrWhiteSpace(checkCommand) ? null : checkCommand,
            unknownGates,
            OutlineHeadings.Where(heading => !headings.Contains(heading)).ToList(),
            ReadDefinitionOfDone(body),
            text);
        return true;
    }

    private static List<DoneItem> ReadDefinitionOfDone(string[] body)
    {
        List<DoneItem> items = [];
        bool inSection = false;
        foreach (string raw in body)
        {
            string line = raw.Trim();
            if (line.StartsWith('#'))
            {
                if (inSection)
                {
                    break;
                }

                inSection = string.Equals(line.TrimStart('#').Trim(), "Definition of Done", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection && line.StartsWith("- ", StringComparison.Ordinal))
            {
                items.Add(ParseDoneBullet(line[2..].Trim()));
            }
        }

        return items;
    }

    private static DoneItem ParseDoneBullet(string text)
    {
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return new DoneItem(text, new DoneRule.Unreadable(text), ProofItemSource.Process);
        }

        string label = text[..colon].Trim();
        string rule = text[(colon + 1)..].Trim();
        return new DoneItem(label, ParseDoneRule(rule, text), ProofItemSource.Process);
    }

    private static DoneRule ParseDoneRule(string rule, string text)
    {
        const string evidence = "evidence from ";
        const string tagged = "task tagged ";
        const string reviewed = "document reviewed by ";
        const string commandPrefix = "command `";
        const string commandSuffix = "` exits 0";

        if (rule.StartsWith(evidence, StringComparison.Ordinal) && rule[evidence.Length..].Trim() is { Length: > 0 } step)
        {
            return new DoneRule.EvidenceFrom(step);
        }

        if (rule.StartsWith(tagged, StringComparison.Ordinal) && rule[tagged.Length..].Trim() is { Length: > 0 } tag)
        {
            return new DoneRule.TaskTagged(tag);
        }

        if (rule.StartsWith(reviewed, StringComparison.Ordinal) && rule[reviewed.Length..].Trim() is { Length: > 0 } reviewStep)
        {
            return new DoneRule.DocumentReviewedBy(reviewStep);
        }

        if (rule.StartsWith(commandPrefix, StringComparison.Ordinal)
            && rule.EndsWith(commandSuffix, StringComparison.Ordinal)
            && rule.Length > commandPrefix.Length + commandSuffix.Length)
        {
            return new DoneRule.CommandExits(rule[commandPrefix.Length..^commandSuffix.Length]);
        }

        return new DoneRule.Unreadable(text);
    }

    /// <summary>Reads the <c>key: value</c> lines between the frontmatter delimiters.</summary>
    /// <param name="lines">The document lines.</param>
    /// <param name="closing">The index of the closing delimiter line.</param>
    /// <returns>The values by key, compared without case.</returns>
    private static Dictionary<string, string> ReadFrontmatter(string[] lines, int closing)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index < closing; index++)
        {
            string line = lines[index].TrimEnd();
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && !char.IsWhiteSpace(line[0]))
            {
                values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }

        return values;
    }

    /// <summary>Reads the <c>gates</c> value into the known gates, without duplicates, and the ids it could not read.</summary>
    /// <param name="values">The frontmatter values.</param>
    /// <returns>The known gates and the unknown ids, each in the order written.</returns>
    private static (List<GateId> Gates, List<string> UnknownGates) ReadGates(Dictionary<string, string> values)
    {
        List<GateId> gates = [];
        List<string> unknownGates = [];
        foreach (string id in values.TryGetValue("gates", out string? gateText) ? SplitList(gateText) : [])
        {
            if (!TryParseGate(id, out GateId gate))
            {
                unknownGates.Add(id);
            }
            else if (!gates.Contains(gate))
            {
                gates.Add(gate);
            }
        }

        return (gates, unknownGates);
    }

    /// <summary>Reads the <c>wip</c> value.</summary>
    /// <param name="values">The frontmatter values.</param>
    /// <returns>The limit when it is a positive whole number, else <see langword="null"/>.</returns>
    private static int? ReadWip(Dictionary<string, string> values)
    {
        if (values.TryGetValue("wip", out string? wipText) && int.TryParse(Unquote(wipText), NumberStyles.None, CultureInfo.InvariantCulture, out int wipValue) && wipValue > 0)
        {
            return wipValue;
        }

        return null;
    }

    /// <summary>Rewrites the <c>gates</c> line of the frontmatter, keeping every other line as it is.</summary>
    /// <param name="text">The document text.</param>
    /// <param name="gates">The gates the rewritten line lists.</param>
    /// <returns>The new text.</returns>
    public static string WithGates(string text, IReadOnlyList<GateId> gates)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(gates);

        List<string> lines = [.. text.Split('\n')];
        if (!string.Equals(lines[0].TrimEnd(), Delimiter, StringComparison.Ordinal))
        {
            return text;
        }

        int closing = lines.FindIndex(1, static line => string.Equals(line.TrimEnd(), Delimiter, StringComparison.Ordinal));
        if (closing < 0)
        {
            return text;
        }

        int existing = lines.FindIndex(1, closing - 1, static line => line.StartsWith("gates:", StringComparison.OrdinalIgnoreCase));
        string newLine = string.Concat("gates: [", string.Join(", ", gates.Select(QuotedId)), "]");
        if (existing >= 0)
        {
            lines[existing] = lines[existing].EndsWith('\r') ? newLine + "\r" : newLine;
        }
        else
        {
            lines.Insert(closing, lines[closing].EndsWith('\r') ? newLine + "\r" : newLine);
        }

        return string.Join('\n', lines);
    }

    /// <summary>Reads the lines of the <c>## Onboarding</c> section (PR-5), up to the next heading.</summary>
    /// <param name="text">The document text, with LF or CRLF line endings.</param>
    /// <returns>The section's trimmed lines joined with LF, or <see langword="null"/> when the section is absent or empty.</returns>
    public static string? OnboardingSteps(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<string> steps = [];
        bool inSection = false;
        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith('#'))
            {
                if (inSection)
                {
                    break;
                }

                inSection = string.Equals(line.TrimStart('#').Trim(), "Onboarding", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection)
            {
                steps.Add(line);
            }
        }

        string joined = string.Join('\n', steps).Trim();
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>The four gates a Process.md may declare; <see cref="GateId.HumanGate"/> and <see cref="GateId.ProofIncomplete"/> are refusal ids only.</summary>
    private static readonly GateId[] ProcessGates =
    [
        GateId.QaEvidenceBeforeDone,
        GateId.ChecksBeforeMerge,
        GateId.SpecApprovedBeforeBuild,
        GateId.FailingTestBeforeFix,
    ];

    private static string QuotedId(GateId gate) => string.Concat("'", ToId(gate), "'");

    /// <summary>Returns the frontmatter id of a gate.</summary>
    /// <param name="gate">The gate.</param>
    /// <returns>The kebab-case id, for example <c>qa-evidence-before-done</c>.</returns>
    public static string ToId(GateId gate) => gate switch
    {
        GateId.QaEvidenceBeforeDone => "qa-evidence-before-done",
        GateId.ChecksBeforeMerge => "checks-before-merge",
        GateId.SpecApprovedBeforeBuild => "spec-approved-before-build",
        GateId.FailingTestBeforeFix => "failing-test-before-fix",
        GateId.HumanGate => "human-gate",
        GateId.ProofIncomplete => "proof-incomplete",
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, null),
#pragma warning restore HAA0601
    };

    /// <summary>Returns the name a person reads for a gate, in the Process tab and the override prompt.</summary>
    /// <param name="gate">The gate.</param>
    /// <returns>The sentence-case display name, for example <c>QA evidence before done</c>.</returns>
    public static string DisplayName(GateId gate) => gate switch
    {
        GateId.QaEvidenceBeforeDone => "QA evidence before done",
        GateId.ChecksBeforeMerge => "Checks before merge",
        GateId.SpecApprovedBeforeBuild => "Spec approved before build",
        GateId.FailingTestBeforeFix => "Failing test before fix",
        GateId.HumanGate => "Human gate",
        GateId.ProofIncomplete => "Proof incomplete",
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, null),
#pragma warning restore HAA0601
    };
    /// <summary>Parses a frontmatter gate id.</summary>
    /// <param name="id">The id, compared ordinally.</param>
    /// <param name="gate">The gate when the id is known.</param>
    /// <returns><see langword="true"/> when <paramref name="id"/> names one of the four gates.</returns>
    public static bool TryParseGate(string id, out GateId gate)
    {
        foreach (GateId candidate in ProcessGates)
        {
            if (string.Equals(ToId(candidate), id, StringComparison.Ordinal))
            {
                gate = candidate;
                return true;
            }
        }

        gate = default;
        return false;
    }

    private static bool ReadCadence(Dictionary<string, string> values, string key, out int every, out CadenceUnit unit)
    {
        every = 0;
        unit = default;
        if (!values.TryGetValue(key, out string? raw) || !raw.StartsWith('{') || !raw.EndsWith('}'))
        {
            return false;
        }

        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in raw[1..^1].Split(','))
        {
            int colon = pair.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                fields[pair[..colon].Trim()] = Unquote(pair[(colon + 1)..]);
            }
        }

        if (!fields.TryGetValue("every", out string? everyText)
            || !int.TryParse(everyText, NumberStyles.None, CultureInfo.InvariantCulture, out every)
            || every <= 0
            || !fields.TryGetValue("unit", out string? unitText))
        {
            return false;
        }

        switch (unitText)
        {
            case "done-tasks":
                unit = CadenceUnit.DoneTasks;
                return true;
            case "days":
                unit = CadenceUnit.Days;
                return true;
            default:
                return false;
        }
    }

    private static List<string> SplitList(string raw) =>
        raw.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Unquote)
            .Where(static item => item.Length > 0)
            .ToList();

    private static string Unquote(string raw)
    {
        string value = raw.Trim();
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }
}
