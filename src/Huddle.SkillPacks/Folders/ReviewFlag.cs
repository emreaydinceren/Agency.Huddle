namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>One finding for the user to review before install.</summary>
/// <param name="Kind">What was found.</param>
/// <param name="File">Path relative to the skill folder, or <c>.</c> for the skill as a whole.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Excerpt">The offending text, at most 120 characters.</param>
public sealed record ReviewFlag(ReviewFlagKind Kind, string File, int Line, string Excerpt);
