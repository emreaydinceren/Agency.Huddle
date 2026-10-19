namespace Agency.Huddle.Seeder.Model;

/// <summary>A plain file under the data folder: a Library note or a memory fact.</summary>
/// <param name="RelativePath">The path below the data folder, using <c>/</c>.</param>
/// <param name="Content">The Markdown text.</param>
internal sealed record SeedFile(string RelativePath, string Content);
