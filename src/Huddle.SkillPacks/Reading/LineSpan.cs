namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>First and last line of a row in SKILLS.md, 1-based and inclusive (§8.1 step 7).</summary>
/// <param name="FirstLine">The row's first line.</param>
/// <param name="LastLine">The row's last line.</param>
public readonly record struct LineSpan(int FirstLine, int LastLine);