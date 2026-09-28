namespace Agency.Huddle.App.Library;

/// <summary>A wikilink parsed from a Library note, with its position in the file.</summary>
/// <param name="Target">The link target (path or heading reference).</param>
/// <param name="Heading">An optional heading reference (e.g., "target#heading").</param>
/// <param name="Alias">An optional display alias.</param>
/// <param name="IsEmbed">True for embed syntax <c>![[...]]</c>, false for link syntax <c>[[...]]</c>.</param>
/// <param name="Line">The 1-based line number where the link appears.</param>
/// <param name="Start">The 0-based position of the <c>[[</c> or <c>![[</c> token within the file text.</param>
/// <param name="Length">The length of the entire <c>[[...]]</c> or <c>![[...]]</c> token.</param>
public sealed record WikiLink(string Target, string? Heading, string? Alias, bool IsEmbed, int Line, int Start, int Length);
