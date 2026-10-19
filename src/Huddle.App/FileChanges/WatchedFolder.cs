namespace Agency.Huddle.App.FileChanges;

/// <summary>A Watched Folder entry, resolved to a full path, per FC §6.2-§6.3.</summary>
/// <param name="Entry">The entry as written (or as passed to <see cref="WatchedFolderResolver.TryResolve"/>).</param>
/// <param name="FullPath">The entry's resolved full path.</param>
/// <param name="PruneUnderscore">
/// True when this folder sits inside the Teams root, per Spec §6.13: its scan prunes any
/// <c>_</c>-prefixed sub-folder (a Team's Tasks or drafts folder), so Task files never appear in
/// File Changes.
/// </param>
internal sealed record WatchedFolder(string Entry, string FullPath, bool PruneUnderscore = false);
