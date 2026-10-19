namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>One problem found in a Skill folder.</summary>
/// <param name="Skill">The Skill's folder name.</param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Rule">Which rule found it.</param>
public sealed record FolderIssue(string Skill, string Message, FolderIssueSeverity Severity, FolderRule Rule);
