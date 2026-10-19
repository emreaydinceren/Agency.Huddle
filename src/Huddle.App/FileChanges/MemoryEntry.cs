namespace Agency.Huddle.App.FileChanges;

/// <summary>One Memory file's indexed summary, per FC §6.15.</summary>
/// <param name="Summary">The file's first non-blank line, with a leading <c>#</c> and spaces removed and cut at 200 characters, or the file name when there is no non-blank line (E-15).</param>
/// <param name="FullPath">The file's full path.</param>
internal sealed record MemoryEntry(string Summary, string FullPath);
