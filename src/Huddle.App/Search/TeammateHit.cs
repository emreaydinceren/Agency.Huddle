namespace Agency.Huddle.App.Search;

/// <summary>A Teammate hit. Its Title on the card is the Name; <see cref="PersonaTitle"/> is the Persona's title.</summary>
public sealed record TeammateHit(string Name, string PersonaTitle, string Alias, IReadOnlyList<string> Teams, MatchTier Tier)
    : SearchHit(Name, Teams.Count > 0 ? Teams[0] : null, false, Tier);
