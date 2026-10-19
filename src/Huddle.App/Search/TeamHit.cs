namespace Agency.Huddle.App.Search;

/// <summary>A Team hit: its type label and Teammate count are what its card shows.</summary>
public sealed record TeamHit(string Name, string? TypeLabel, int TeammateCount, MatchTier Tier)
    : SearchHit(Name, Name, false, Tier);
