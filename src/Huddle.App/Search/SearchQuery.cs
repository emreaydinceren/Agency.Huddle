namespace Agency.Huddle.App.Search;

/// <summary>One search as every provider receives it (spec §7.1).</summary>
/// <param name="Text">The phrase, with any prefix already removed and trimmed. Empty means browse.</param>
/// <param name="Filter">The active filter.</param>
/// <param name="OpenTeam">The Team of the focused Tab, or null (spec §7.4).</param>
/// <param name="Scope">Active or Archived.</param>
/// <param name="MaxHits">The per-group cap for this view.</param>
/// <param name="Purpose">Why the search runs; <see cref="SearchPurpose.Box"/> unless the Composer's picker asked (D-4).</param>
public sealed record SearchQuery(string Text, SearchFilter Filter, string? OpenTeam, SearchScope Scope, int MaxHits, SearchPurpose Purpose = SearchPurpose.Box);
