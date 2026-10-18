using Agency.Huddle.SkillPacks.Folders;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Validates one candidate Skill's files against Spec §8.1. The rules live in the library's
/// <see cref="SkillFolderValidator"/>, which <c>huddle-packs</c> shares; this passes the app's
/// <see cref="SkillGrants.Grantable"/> and maps the result to the app's <see cref="SkillValidation"/>.
/// </summary>
internal static class SkillValidator
{
    /// <summary>Validates one candidate Skill's files against Spec §8.1 rules 1–7.</summary>
    /// <param name="folderName">The Skill's folder name; a valid <c>name</c> field must equal this, ordinally.</param>
    /// <param name="files">Every file found in the Skill's folder, keyed by file name (a relative path with <c>/</c> separators for an imported Skill's sub-folder files).</param>
    /// <param name="allowSupportingFiles"><see langword="true"/> only when the folder has a valid <c>.source.json</c>: sub-folder and non-<c>.md</c> files are then accepted, up to 200 files.</param>
    /// <returns>What parsed, what survived, and every problem found.</returns>
    internal static SkillValidation Validate(string folderName, IReadOnlyDictionary<string, string> files, bool allowSupportingFiles = false)
    {
        SkillFolderValidation result = SkillFolderValidator.Validate(folderName, files, SkillGrants.Grantable, allowSupportingFiles);
        return new SkillValidation(
            result.Name,
            result.Description,
            result.Tools,
            result.Files,
            [.. result.Issues.Select(static issue => new SkillIssue(issue.Skill, issue.Message, ToSeverity(issue.Severity)))]);
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
