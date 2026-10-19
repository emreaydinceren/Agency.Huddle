namespace Agency.Huddle.App.Library;

/// <summary>What <c>LibraryFileService.SearchTextAsync</c> found under one scope.</summary>
/// <param name="Hits">The hits in walk order.</param>
/// <param name="Truncated">Whether the hit cap, the visit budget or a skipped over-size file cut the search short.</param>
internal sealed record LibraryTextSearchResult(IReadOnlyList<LibraryTextHit> Hits, bool Truncated);
