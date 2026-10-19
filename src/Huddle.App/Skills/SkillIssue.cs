namespace Agency.Huddle.App.Skills;

/// <summary>One problem found while resolving or validating a Skill, naming which Skill it came from.</summary>
/// <param name="Skill">The Skill's folder name.</param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <param name="Severity">How serious the problem is.</param>
internal sealed record SkillIssue(string Skill, string Message, SkillIssueSeverity Severity);
