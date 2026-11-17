namespace Agency.Huddle.App.Memory;

/// <summary>A file one Helper step removes (Memory Tidy spec §6.11).</summary>
/// <param name="File">The file name.</param>
/// <param name="ExpectedVersion">The version the reader saw.</param>
internal sealed record MemoryTidyRemoval(string File, string ExpectedVersion);
