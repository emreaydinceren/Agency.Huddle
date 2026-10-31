namespace Agency.Huddle.App.FileChanges;

/// <summary>One Memory file's indexed summary, per FC §6.15.</summary>
/// <param name="Summary">The file's frontmatter <c>description</c>, else its first non-blank body line, with a leading <c>#</c> and spaces removed; cut at 200 characters, or the file name when there is no non-blank line (E-15).</param>
/// <param name="FullPath">The file's full path.</param>
internal sealed record MemoryEntry(string Summary, string FullPath)
{
    /// <summary>The characters that end a folder in <see cref="FullPath"/>.</summary>
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// The file's name with its extension, which the index line shows. A backslash ends a folder on every platform here, not
    /// only on Windows: a memory file name can never hold one (<c>LibraryNames.Validate</c>), and a Windows-style path in a
    /// snapshot must give the same name on Linux.
    /// </summary>
    public string FileName => this.FullPath[(this.FullPath.LastIndexOfAny(Separators) + 1)..];
}
