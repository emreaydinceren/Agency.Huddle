namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>What validating one Skill folder found.</summary>
/// <param name="Name">The parsed <c>name</c>, or null when missing or blank.</param>
/// <param name="Description">The parsed <c>description</c>, or null when missing or blank.</param>
/// <param name="Tools">The <c>tools</c> entries that are in the grantable set.</param>
/// <param name="AllTools">Every <c>tools</c> entry as written, split on <c>,</c> and <c>;</c> (S011 reads these).</param>
/// <param name="Files">The files that passed rule 7, <c>SKILL.md</c> first, then ordinal order.</param>
/// <param name="Fields">The frontmatter fields.</param>
/// <param name="Issues">Every problem found.</param>
public sealed record SkillFolderValidation(
    string? Name,
    string? Description,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> AllTools,
    IReadOnlyList<string> Files,
    IReadOnlyList<SkillFrontmatterField> Fields,
    IReadOnlyList<FolderIssue> Issues)
{
    /// <summary>Whether any issue is an error.</summary>
    public bool HasErrors => this.Issues.Any(static issue => issue.Severity == FolderIssueSeverity.Error);
}
