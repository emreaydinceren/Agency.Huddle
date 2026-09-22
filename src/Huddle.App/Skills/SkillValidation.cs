namespace Agency.Huddle.App.Skills;

/// <summary>
/// The result of validating one candidate Skill's files against Spec §8.1: what parsed from
/// <c>SKILL.md</c>, which files and tools survived, and every problem found along the way.
/// </summary>
/// <param name="Name">The parsed <c>name</c> field, or <see langword="null"/> when it was missing or blank.</param>
/// <param name="Description">The parsed <c>description</c> field, or <see langword="null"/> when it was missing or blank.</param>
/// <param name="Tools">The <c>tools</c> entries that are in <see cref="SkillValidator.Grantable"/>; an ungrantable entry is dropped and reported as a <see cref="SkillIssueSeverity.Warning"/>.</param>
/// <param name="Files">The input files that passed Spec §8.1 rule 7 (flat, ≤ 65,536 UTF-8 bytes), <c>SKILL.md</c> first, then ordinal order.</param>
/// <param name="Issues">Every problem <see cref="SkillValidator.Validate"/> found.</param>
internal sealed record SkillValidation(
    string? Name,
    string? Description,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Files,
    IReadOnlyList<SkillIssue> Issues)
{
    /// <summary>Whether any entry in <see cref="Issues"/> is a <see cref="SkillIssueSeverity.Error"/>.</summary>
    internal bool HasErrors => this.Issues.Any(issue => issue.Severity == SkillIssueSeverity.Error);
}
