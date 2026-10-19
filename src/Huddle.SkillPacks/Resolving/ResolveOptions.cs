namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>Switches for one resolve.</summary>
/// <param name="IgnoreGrace">True for <b>Reload</b>: a file that fails to parse is reported even when it was written in
/// the last two seconds (§6.9.5).</param>
public sealed record ResolveOptions(bool IgnoreGrace = false);
