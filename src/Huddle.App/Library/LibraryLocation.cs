namespace Agency.Huddle.App.Library;

/// <summary>A location inside a Library Root, identified by root and folder path.</summary>
/// <param name="RootId">The root identifier: <c>teams</c>, <c>teammates</c>, or a pinned root's slug.</param>
/// <param name="FolderPath">The folder path relative to the root, forward-slash, no leading slash, empty for root.</param>
public sealed record LibraryLocation(string RootId, string FolderPath);
