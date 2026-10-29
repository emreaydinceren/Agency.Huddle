using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.App.Search;

/// <summary>A Team hit: its type label and Agent count are what its card shows.</summary>
public sealed record TeamHit(string Name, string? TypeLabel, int AgentCount, MatchTier Tier)
    : SearchHit(Name, Name, false, Tier)
{
    /// <summary>The Team's avatar from its <c>_team.md</c>, which the card draws as a rounded square; <see cref="Avatars.Avatar.None"/> when it has none.</summary>
    public Avatar Avatar { get; init; } = Avatar.None;
}
