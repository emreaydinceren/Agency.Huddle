using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>Whether a set of Packs and Skills can be handed to Agents right now.</summary>
/// <param name="Ready">Registry Skills, Pack Skills expanded, that are usable and downloaded.</param>
/// <param name="Missing">Items that are not.</param>
internal sealed record SkillReadiness(IReadOnlyList<SkillEntry> Ready, IReadOnlyList<MissingItem> Missing)
{
    /// <summary>True when nothing is missing.</summary>
    public bool IsReady => this.Missing.Count == 0;
}
