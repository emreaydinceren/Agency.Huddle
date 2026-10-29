namespace Agency.Huddle.App.Search;

/// <summary>A Team hit: its type label and Agent count are what its card shows.</summary>
public sealed record TeamHit(string Name, string? TypeLabel, int AgentCount, MatchTier Tier)
    : SearchHit(Name, Name, false, Tier);
