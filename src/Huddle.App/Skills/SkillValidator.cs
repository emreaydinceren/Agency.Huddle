using Agency.Huddle.SkillPacks.Folders;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Validates one candidate Skill's files against Spec §8.1. The rules live in the library's
/// <see cref="SkillFolderValidator"/>, which <c>huddle-packs</c> shares; this passes the app's
/// <see cref="SkillGrants.Grantable"/> and maps the result to the app's <see cref="SkillValidation"/>.
/// </summary>
internal static class SkillValidator
{
    private const string SkillFileName = "SKILL.md";

    private static readonly (string Legacy, string Current)[] LegacyToolNames =
    [
        ("validate_teammate", "validate_agent"),
        ("propose_teammates", "propose_agents"),
    ];

    /// <summary>Validates one candidate Skill's files against Spec §8.1 rules 1–7.</summary>
    /// <param name="folderName">The Skill's folder name; a valid <c>name</c> field must equal this, ordinally.</param>
    /// <param name="files">Every file found in the Skill's folder, keyed by file name (a relative path with <c>/</c> separators for an imported Skill's sub-folder files).</param>
    /// <param name="allowSupportingFiles"><see langword="true"/> only when the folder has a valid <c>.source.json</c>: sub-folder and non-<c>.md</c> files are then accepted, up to 200 files.</param>
    /// <returns>What parsed, what survived, and every problem found.</returns>
    internal static SkillValidation Validate(string folderName, IReadOnlyDictionary<string, string> files, bool allowSupportingFiles = false)
    {
        SkillFolderValidation result = SkillFolderValidator.Validate(folderName, UpgradeLegacyToolNames(files), SkillGrants.Grantable, allowSupportingFiles);
        return new SkillValidation(
            result.Name,
            result.Description,
            result.Tools,
            result.Files,
            [.. result.Issues.Select(static issue => new SkillIssue(issue.Skill, issue.Message, ToSeverity(issue.Severity)))]);
    }

    /// <summary>
    /// Rewrites the tool names the product used before the Teammate-to-Agent rename on the
    /// <c>tools:</c> line of <c>SKILL.md</c>, so a Skill written earlier still grants them.
    /// Only that line is touched, and only in a file that names an old tool.
    /// </summary>
    /// <param name="files">The candidate Skill's files, keyed by file name.</param>
    /// <returns><paramref name="files"/> itself when nothing needs rewriting, otherwise a copy with the <c>tools:</c> line updated.</returns>
    private static IReadOnlyDictionary<string, string> UpgradeLegacyToolNames(IReadOnlyDictionary<string, string> files)
    {
        if (!files.TryGetValue(SkillFileName, out string? text))
        {
            return files;
        }

        string[] lines = text.Split('\n');
        bool changed = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("tools:", StringComparison.Ordinal))
            {
                continue;
            }

            foreach ((string legacy, string current) in LegacyToolNames)
            {
                string updated = lines[i].Replace(legacy, current, StringComparison.Ordinal);
                changed |= !string.Equals(updated, lines[i], StringComparison.Ordinal);
                lines[i] = updated;
            }
        }

        return changed ? new Dictionary<string, string>(files, StringComparer.Ordinal) { [SkillFileName] = string.Join('\n', lines) } : files;
    }

    /// <summary>The app's severity for a library severity.</summary>
    private static SkillIssueSeverity ToSeverity(FolderIssueSeverity severity)
    {
        return severity switch
        {
            FolderIssueSeverity.Info => SkillIssueSeverity.Info,
            FolderIssueSeverity.Warning => SkillIssueSeverity.Warning,
            _ => SkillIssueSeverity.Error,
        };
    }
}
