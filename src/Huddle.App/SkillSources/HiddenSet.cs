namespace Agency.Huddle.App.SkillSources;

/// <summary>What <c>hidden.json</c> holds.</summary>
/// <param name="Packs">Hidden Pack ids.</param>
/// <param name="Skills">Hidden Skill names.</param>
/// <param name="Malformed">True when the file exists but cannot be read; then nothing is hidden and nothing is written.</param>
internal sealed record HiddenSet(IReadOnlySet<string> Packs, IReadOnlySet<string> Skills, bool Malformed)
{
    /// <summary>Nothing hidden.</summary>
    public static HiddenSet None { get; } = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.Ordinal), false);
}
