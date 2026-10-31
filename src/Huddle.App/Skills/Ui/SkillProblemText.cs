using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Words a finding for the Human (Skill Packs spec §6.9.12): <i>"{message} {fix hint} ({code})"</i>, the hint from
/// <see cref="CheckCatalog"/>, the one table of codes, so this class keeps no second list. The code is always last.</summary>
internal static class SkillProblemText
{
    /// <summary>The warning codes that block nothing: the UI adds that sentence to them.</summary>
    private static readonly HashSet<string> NothingBlocked = new(["S007", "S008", "S009", "S010", "S012"], StringComparer.Ordinal);

    /// <summary>The codes whose cause can be a per-computer path with no entry for this computer.</summary>
    private static readonly HashSet<string> PerComputer = new(["R009", "S001"], StringComparer.Ordinal);

    /// <summary>The finding as one line: message, fix hint, the two UI additions where they apply, then the code.</summary>
    /// <param name="finding">The finding.</param>
    /// <param name="noEntryFor">The computer name when the finding's path is per computer and has no entry for it; null otherwise.</param>
    public static string Compose(Finding finding, string? noEntryFor = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        List<string> parts = [Sentence(finding.Message)];
        string hint = CheckCatalog.HintFor(finding.Code);
        if (hint.Length > 0)
        {
            parts.Add(Sentence(hint));
        }

        if (NothingBlocked.Contains(finding.Code))
        {
            parts.Add("Nothing is blocked. Open Details to see the files.");
        }

        if (noEntryFor is not null && PerComputer.Contains(finding.Code))
        {
            parts.Add($"There is no entry for {noEntryFor}.");
        }

        string body = string.Join(' ', parts);
        return $"{body} ({finding.Code})";
    }

    /// <summary>The text with a full stop at the end when it has none.</summary>
    private static string Sentence(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length == 0 || trimmed[^1] is '.' or '!' or '?' ? trimmed : trimmed + ".";
    }
}
