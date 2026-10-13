namespace Agency.Huddle.App.Search;

/// <summary>One search as every provider receives it (spec §7.1).</summary>
/// <param name="Text">The phrase, with any prefix already removed and trimmed. Empty means browse.</param>
/// <param name="Filter">The active filter.</param>
/// <param name="OpenTeam">The Team of the focused Tab, or null (spec §7.4).</param>
/// <param name="Scope">Active or Archived.</param>
/// <param name="MaxHits">The per-group cap for this view.</param>
public sealed record SearchQuery(string Text, SearchFilter Filter, string? OpenTeam, SearchScope Scope, int MaxHits);

/// <summary>What the box text means: its filter, its phrase, and whether it searches (spec §7.2). Built by the parser.</summary>
/// <param name="Filter">The filter the prefix sets, or the chip's filter when there is none.</param>
/// <param name="Phrase">The text after any prefix, trimmed.</param>
/// <param name="IsSearchable">Whether the phrase is long enough, or is a browse of Teams or Teammates.</param>
public sealed record SearchParse(SearchFilter Filter, string Phrase, bool IsSearchable);
