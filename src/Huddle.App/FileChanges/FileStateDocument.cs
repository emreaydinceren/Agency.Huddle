namespace Agency.Huddle.App.FileChanges;

/// <summary>The JSON shape of a <see cref="FileState"/> file, per FC §6.6.</summary>
/// <param name="Subscribed">Entries added by <c>watch_folder</c>, in call order.</param>
/// <param name="Rooms">Keyed by Room id.</param>
/// <param name="Writers">Keyed by entry, then by relative path.</param>
internal sealed record FileStateDocument(
    List<string> Subscribed,
    Dictionary<string, RoomBaselineDocument> Rooms,
    Dictionary<string, Dictionary<string, FileWriterDocument>> Writers);
