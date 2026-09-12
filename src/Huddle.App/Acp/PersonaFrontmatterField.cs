namespace Agency.Huddle.App.Acp;

/// <summary>One top-level frontmatter field parsed from a Persona file, in file order.</summary>
/// <param name="Key">The field's YAML key, exactly as written (not title-cased).</param>
/// <param name="Value">
/// The field's value as a single display line: a quote-stripped scalar, a semicolon-joined
/// list, or a block scalar with any embedded line breaks collapsed to spaces.
/// </param>
public sealed record PersonaFrontmatterField(string Key, string Value);
