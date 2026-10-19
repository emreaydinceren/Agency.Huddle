namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>One broken folder rule.</summary>
/// <param name="Limit">Which limit.</param>
/// <param name="Path">The full path of the file or folder at fault, or the Skill folder for a size rule.</param>
/// <param name="Message">One line naming the file and the rule.</param>
public sealed record FolderViolation(FolderLimit Limit, string Path, string Message);
