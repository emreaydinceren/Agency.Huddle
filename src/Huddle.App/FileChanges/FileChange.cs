namespace Agency.Huddle.App.FileChanges;

/// <summary>One file's change between two snapshots, as reported to an Agent.</summary>
/// <param name="Kind">Whether the file was added, changed or deleted.</param>
/// <param name="FullPath">The file's full path.</param>
/// <param name="ByYouRoomName">
/// The current name of the Room whose Turn last wrote this file, when this Agent's own last
/// recorded write to it still matches the file exactly as it is now, and that Room is not the one
/// this Turn is running in (FC §6.15). <see langword="null"/> when no such match applies - the
/// file was last written by someone else, the match no longer holds (E-19), or that Room no
/// longer exists (E-20).
/// </param>
internal sealed record FileChange(FileChangeKind Kind, string FullPath, string? ByYouRoomName = null);
