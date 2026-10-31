namespace Agency.Huddle.App.FileChanges;

/// <summary>One Memory file's indexed summary, per FC §6.15.</summary>
/// <param name="Summary">The file's frontmatter <c>description</c>, else its first non-blank body line, with a leading <c>#</c> and spaces removed; cut at 200 characters, or the file name when there is no non-blank line (E-15).</param>
/// <param name="FullPath">The file's full path.</param>
internal sealed record MemoryEntry(string Summary, string FullPath)
{
    /// <summary>The file's name with its extension, which the index line shows.</summary>
    public string FileName => Path.GetFileName(this.FullPath);
}
