namespace Agency.Huddle.App.FileChanges;

/// <summary>What this Agent was last shown in one Room.</summary>
/// <param name="Folders">The Agent's Watched Folders, keyed by entry, never by full path.</param>
internal sealed record RoomBaseline(IReadOnlyDictionary<string, FolderSnapshot> Folders);
