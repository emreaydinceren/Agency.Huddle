using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.HiringPool;

/// <summary>A role the Human wrote themselves for the Build a team page, kept beside the hiring pool's own jobs.</summary>
/// <param name="Id">The role id, <c>custom-</c> followed by a slug of the title; stable once created.</param>
/// <param name="Title">The title shown on the card.</param>
/// <param name="Description">One sentence on the role; may be empty.</param>
/// <param name="Instructions">What a Teammate hired into this role is told to do; may be empty.</param>
/// <param name="Tier">The model strength the role asks for.</param>
/// <param name="Skills">The names of the Skills a Teammate in this role carries, or <see langword="null"/> when omitted.</param>
internal sealed record CustomRole(string Id, string Title, string Description, string Instructions, ModelTier Tier, IReadOnlyList<string>? Skills = null)
{
    /// <summary>The names of the Skills a Teammate in this role carries; empty when omitted.</summary>
    public IReadOnlyList<string> Skills { get; init; } = Skills ?? [];
}
