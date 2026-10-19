namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>One thing that looks like a secret.</summary>
/// <param name="Pattern">The pattern's number in §6.3.</param>
/// <param name="Name">The pattern's name.</param>
/// <param name="Line">The 1-based line it is on.</param>
/// <param name="Preview">At most the first four characters of the match, then <c>…</c>; never the secret.</param>
public sealed record SecretHit(int Pattern, string Name, int Line, string Preview);
