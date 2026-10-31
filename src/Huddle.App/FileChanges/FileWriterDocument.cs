namespace Agency.Huddle.App.FileChanges;

/// <summary>The JSON shape of a <see cref="FileWriter"/>: its own entry's <see cref="FileEntry"/> fields flattened alongside the Room id.</summary>
internal sealed record FileWriterDocument(string RoomId, long Size, DateTimeOffset ModifiedUtc);
