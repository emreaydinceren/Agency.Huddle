namespace Agency.Huddle.App.Library;

/// <summary>A Library Root: Teams folder collection, Teammate folders, or a pinned user folder.</summary>
/// <param name="Id">Identifier: <c>teams</c>, <c>teammates</c>, or a pinned root's slug.</param>
/// <param name="DisplayName">The display name shown in the UI.</param>
/// <param name="FullPath">The absolute file system path.</param>
/// <param name="Kind">The root kind: Teams, Teammates, or Pinned.</param>
public sealed record LibraryRoot(string Id, string DisplayName, string FullPath, LibraryRootKind Kind);
