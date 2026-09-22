namespace Agency.Huddle.App.Prompts;

/// <summary>
/// One entry in the <see cref="PromptCatalog"/>: the fixed shape of a single piece of model-facing
/// text — its identity, its default wording, and the placeholders that wording may use.
/// </summary>
/// <remarks>
/// The catalog is the authority for what prompts exist and what their defaults are; a later
/// configuration file only ever carries an override of <see cref="Default"/>; it never adds or
/// removes a prompt or changes a prompt's <see cref="Placeholders"/> or <see cref="Timing"/>.
/// </remarks>
/// <param name="Key">
/// The prompt's stable identity, dotted by area, e.g. <c>"turn.roomLabel"</c>. Used to look the prompt up
/// in the catalog and to key an override in configuration; never shown to a model.
/// </param>
/// <param name="Label">A short human name for the prompt, shown as a form field's label in the settings UI.</param>
/// <param name="HelperText">
/// One plain-language sentence explaining what the prompt controls and any constraint its wording must
/// keep, shown as the hint text under the field named by <see cref="Label"/>.
/// </param>
/// <param name="Default">
/// The prompt's built-in text, verbatim from the source this prompt was lifted from, as a non-interpolated
/// raw string literal so that <c>{{placeholder}}</c> tokens survive unescaped. Used whenever no
/// override is configured. Normalised to <c>\n</c> on construction; see the member declaration below
/// for why.
/// </param>
/// <param name="Placeholders">
/// Every <c>{{name}}</c> token this prompt's text is allowed to contain, in the order a reader would
/// meet them. An override may use any subset of these and no others.
/// </param>
/// <param name="RequiredPlaceholders">
/// The subset of <see cref="Placeholders"/> that must appear in the rendered text for it to make
/// sense at all (e.g. the one placeholder that carries a Room's id). Always a subset of
/// <see cref="Placeholders"/>.
/// </param>
/// <param name="Timing">When an edit to this prompt's text reaches a model; see <see cref="PromptTiming"/>.</param>
internal sealed record PromptDefinition(
    string Key,
    string Label,
    string HelperText,
    string Default,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<string> RequiredPlaceholders,
    PromptTiming Timing)
{
    /// <summary>
    /// The prompt's built-in text, normalised to <c>\n</c> line endings regardless of how the repository
    /// was checked out. A C# raw string literal preserves its source file's own line endings rather
    /// than normalising them, and <c>PromptCatalog.cs</c> is CRLF in this repo's working tree on Windows
    /// (there is no <c>.gitattributes</c>, so <c>core.autocrlf</c> decides) but LF in the Linux
    /// container CI checks it out in — <c>git cat-file blob main:src/Huddle.App/Prompts/PromptCatalog.cs</c>
    /// shows 0 CRLF pairs and 395 bare LF, the bytes git actually stores. Left un-normalised, every
    /// prompt's default — and therefore every prompt sent to a model — would silently depend on which
    /// machine built the assembly. Normalising once here, at construction, means every reader of
    /// <see cref="Default"/> gets the same text regardless of checkout platform, instead of each call
    /// site having to remember to do it.
    /// </summary>
    public string Default { get; init; } = Default.ReplaceLineEndings("\n");
}
