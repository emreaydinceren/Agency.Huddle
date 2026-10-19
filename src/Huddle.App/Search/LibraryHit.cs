namespace Agency.Huddle.App.Search;

/// <summary>A Library hit: a file or folder under one Library Root, with a snippet for a content match.</summary>
public sealed record LibraryHit(string RootId, string RootName, string RelativePath, string FolderPath, bool IsFolder, string? Snippet, string? TeamName, MatchTier Tier)
    : SearchHit(RelativePath[(RelativePath.LastIndexOf('/') + 1)..], TeamName, false, Tier);
