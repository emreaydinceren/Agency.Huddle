namespace Agency.Huddle.App.Memory;

/// <summary>The file one Helper step writes (Memory Tidy spec §6.11).</summary>
/// <param name="File">The file name.</param>
/// <param name="Description">The new description, or <see langword="null"/> to keep the file's own.</param>
/// <param name="Text">The new body, or <see langword="null"/> to keep the body byte for byte (a describe).</param>
/// <param name="ExpectedVersion">The version the reader saw.</param>
internal sealed record MemoryTidyWrite(string File, string? Description, string? Text, string ExpectedVersion);
