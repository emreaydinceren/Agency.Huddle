namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>One check: its code, its severity without <c>--strict</c>, what it checks and the fix hint the Human sees.</summary>
/// <param name="Code">The code, such as <c>S004</c>.</param>
/// <param name="Severity">The severity without <see cref="CheckOptions.Strict"/>.</param>
/// <param name="Title">What the check checks.</param>
/// <param name="FixHint">The text the Human sees under the message.</param>
public sealed record CheckInfo(string Code, Severity Severity, string Title, string FixHint);
