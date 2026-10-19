namespace Agency.Huddle.App.References;

/// <summary>A Library file that exists now.</summary>
/// <param name="Path">The absolute path as written.</param>
/// <param name="FileName">The file name.</param>
internal sealed record ResolvedLibraryFile(string Path, string FileName) : ResolvedReference;
