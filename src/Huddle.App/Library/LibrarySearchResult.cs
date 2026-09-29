namespace Agency.Huddle.App.Library;

/// <summary>Search results from a Library query, including the hits and whether more results were truncated.</summary>
internal sealed record LibrarySearchResult(IReadOnlyList<LibraryEntry> Hits, bool Truncated);
