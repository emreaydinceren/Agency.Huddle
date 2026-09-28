namespace Agency.Huddle.App.Library;

/// <summary>A file or folder entry in the Library.</summary>
/// <param name="Path">The path inside a Library Root.</param>
/// <param name="IsFolder">Whether this entry is a folder; false if it is a file.</param>
/// <param name="Length">The file size in bytes (zero for folders).</param>
/// <param name="LastWriteUtc">The last write time in UTC.</param>
public sealed record LibraryEntry(LibraryPath Path, bool IsFolder, long Length, DateTimeOffset LastWriteUtc);
