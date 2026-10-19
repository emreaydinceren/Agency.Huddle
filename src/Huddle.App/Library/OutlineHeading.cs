namespace Agency.Huddle.App.Library;

/// <summary>
/// One heading of a Markdown text as the outline panel shows it.
/// </summary>
/// <param name="Level">The heading level, 1 to 6.</param>
/// <param name="Text">The heading's plain inline text.</param>
/// <param name="Id">The id the rendered heading carries, and the target of a scroll.</param>
/// <param name="Line">The 1-based line of the heading in the whole text, frontmatter included.</param>
internal sealed record OutlineHeading(int Level, string Text, string Id, int Line);
