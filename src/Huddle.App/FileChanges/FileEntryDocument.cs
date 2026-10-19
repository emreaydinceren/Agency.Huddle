namespace Agency.Huddle.App.FileChanges;

/// <summary>The JSON shape of a <see cref="FileEntry"/>.</summary>
internal sealed record FileEntryDocument(long Size, DateTimeOffset ModifiedUtc);
