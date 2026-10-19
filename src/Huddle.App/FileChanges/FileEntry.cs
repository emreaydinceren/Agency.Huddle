namespace Agency.Huddle.App.FileChanges;

/// <summary>One file's size and modified time, as last observed by a scan.</summary>
/// <param name="Size">The file's length in bytes, from <see cref="FileInfo.Length"/>.</param>
/// <param name="ModifiedUtc">The file's last write time, from <see cref="FileSystemInfo.LastWriteTimeUtc"/>.</param>
internal sealed record FileEntry(long Size, DateTimeOffset ModifiedUtc);
