namespace Agency.Huddle.App.Library;

/// <summary>A note's front matter as <see cref="NoteFrontmatter.Locate"/> found it.</summary>
/// <param name="Text">The block's text (YAML lines with their breaks, or the JSON object); <see langword="null"/> when the note has none.</param>
/// <param name="BodyStart">The offset where the body starts; 0 when there is no block.</param>
/// <param name="Form">Which spelling the block is written in.</param>
internal sealed record NoteFrontmatterBlock(string? Text, int BodyStart, NoteFrontmatterForm Form);
