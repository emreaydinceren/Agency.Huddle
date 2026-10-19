namespace Agency.Huddle.App.Search;

/// <summary>What the box text means: its filter, its phrase, and whether it searches (spec §7.2). Built by the parser.</summary>
/// <param name="Filter">The filter the prefix sets, or the chip's filter when there is none.</param>
/// <param name="Phrase">The text after any prefix, trimmed.</param>
/// <param name="IsSearchable">Whether the phrase is long enough, or is a browse of Teams or Teammates.</param>
public sealed record SearchParse(SearchFilter Filter, string Phrase, bool IsSearchable);
