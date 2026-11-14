using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>A hire card with every default resolved (HR-6, HR-7).</summary>
/// <param name="RoleId">The role the card hires.</param>
/// <param name="Name">The resolved Agent name.</param>
/// <param name="Pick">The adapter and model, or <see langword="null"/> when no adapter qualified.</param>
/// <param name="Skills">The role's skills plus its packs' skills, without duplicates.</param>
/// <param name="Packs">The role's packs that exist, at their version.</param>
/// <param name="Readiness">Ready, or Needs setup when a name, adapter, Pack or Skill default is missing.</param>
/// <param name="Reasons">Why the card needs setup (name, adapter, Pack or Skill); empty when Ready. Holds one line per <paramref name="SkillSetup"/> item too.</param>
/// <param name="SkillSetup">The Packs and Skills that are not ready to hire (Skill Packs spec §6.10.2); empty when none.</param>
/// <param name="Unconnected">The role's connector ids that are not connected; empty when all are. Informational: never a reason and never blocks a hire.</param>
internal sealed record CardState(string RoleId, string Name, ModelPick? Pick, IReadOnlyList<SkillRef> Skills,
    IReadOnlyList<VersionedRef> Packs, CardReadiness Readiness, IReadOnlyList<string> Reasons, IReadOnlyList<SkillSetupItem> SkillSetup,
    IReadOnlyList<string> Unconnected);
