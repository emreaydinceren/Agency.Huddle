namespace Agency.Huddle.App.Library;

/// <summary>Which of the two front matter spellings <see cref="NoteFrontmatter.Locate"/> found (D-9).</summary>
internal enum NoteFrontmatterForm
{
    /// <summary>Lines between two <c>---</c> markers.</summary>
    Yaml,

    /// <summary>A single JSON object at the very start of the note.</summary>
    Json,
}
