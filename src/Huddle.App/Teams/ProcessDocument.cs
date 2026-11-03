using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The parsed process prose of a Team's <c>AGENTS.md</c> (PR-6, Spec §Process): the outline headings the body is
/// expected to carry and the Definition of Done. The settings (gates, cadences, check command, template) live in
/// <see cref="TeamFile.Process"/>; a leading frontmatter block is skipped as prose.
/// </summary>
/// <param name="Title">The first level-one heading of the body, else empty.</param>
/// <param name="MissingSections">PR-6 outline headings absent from the body, in outline order.</param>
/// <param name="DefinitionOfDone">The items of the optional <c>## Definition of Done</c> section, in file order.</param>
/// <param name="Text">The whole document text, verbatim.</param>
internal sealed record ProcessDocument(
    string Title,
    IReadOnlyList<string> MissingSections,
    IReadOnlyList<DoneItem> DefinitionOfDone,
    string Text)
{
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
    /// <returns><see langword="false"/> only when the text is empty; frontmatter is optional and a closed leading block is skipped as prose.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out ProcessDocument? document)
    {
        document = null;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int closing = FrontmatterFence.IsOpening(lines[0]) ? FrontmatterFence.FindClosing(lines, 1) : -1;

        string[] body = lines[(closing + 1)..];
        string title = body.FirstOrDefault(static line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? string.Empty;
        HashSet<string> headings = body
            .Where(static line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(static line => line[3..].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        document = new ProcessDocument(
            title,
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
                items.Add(ParseDoneBullet(StripCheckbox(line[2..].Trim())));
            }
        }

        return items;
    }

    private static string StripCheckbox(string text) =>
        text.Length >= 3 && text[0] == '[' && text[2] == ']' && text[1] is ' ' or 'x' or 'X'
            ? text[3..].TrimStart()
            : text;

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

    /// <summary>Reads the <c>gates</c> value into the known gates, without duplicates, and the ids it could not read.</summary>
    /// <param name="values">The frontmatter values.</param>
    /// <returns>The known gates and the unknown ids, each in the order written.</returns>
    internal static (List<GateId> Gates, List<string> UnknownGates) ReadGates(Dictionary<string, string> values)
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
    internal static int? ReadWip(Dictionary<string, string> values)
    {
        if (values.TryGetValue("wip", out string? wipText) && int.TryParse(Unquote(wipText), NumberStyles.None, CultureInfo.InvariantCulture, out int wipValue) && wipValue > 0)
        {
            return wipValue;
        }

        return null;
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

    internal static List<string> SplitList(string raw) =>
        raw.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Unquote)
            .Where(static item => item.Length > 0)
            .ToList();

    internal static string Unquote(string raw)
    {
        string value = raw.Trim();
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }
}
