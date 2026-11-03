namespace Agency.Huddle.App.Teams;

/// <summary>One document in an <c>outputs/</c> folder, as the outputs index lists it (Spec §6.3).</summary>
/// <param name="RelativePath">The document's path under the Teams root, with <c>/</c> separators on every system.</param>
/// <param name="Description">The frontmatter <c>description</c>, clipped to 200 characters; <see langword="null"/> when the file has none, so the index lists the bare path.</param>
internal sealed record OutputsEntry(string RelativePath, string? Description);
