namespace Agency.Huddle.App.Skills.Ui;

/// <summary>A Skill whose files changed at the pointer: shared by every holder, so it has no per-Teammate choice (§8.4).</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="FromCommit">The copy's commit, short.</param>
/// <param name="ToCommit">The pointer's commit, short, or empty when unknown.</param>
/// <param name="FilesChanged">Files whose content differs plus files on one side only.</param>
/// <param name="ModifiedLocally">Whether the copy was edited on this computer.</param>
/// <param name="AffectedTeammates">The Teammates that hold it.</param>
internal sealed record SkillFileChange(string Name, string FromCommit, string ToCommit, int FilesChanged, bool ModifiedLocally, IReadOnlyList<string> AffectedTeammates);
