namespace Agency.Huddle.App.FileChanges;

/// <summary>The JSON shape of a <see cref="RoomBaseline"/>.</summary>
/// <param name="Folders">Keyed by entry, then by relative path.</param>
internal sealed record RoomBaselineDocument(Dictionary<string, Dictionary<string, FileEntryDocument>> Folders);
