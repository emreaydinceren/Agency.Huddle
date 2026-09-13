namespace Agency.Huddle.App.Hooks;

/// <summary>
/// One entry in the <see cref="HookCatalog"/>: the fixed shape of a single piece of model-facing
/// text — its identity, its default wording, and the placeholders that wording may use.
/// </summary>
/// <remarks>
/// The catalog is the authority for what hooks exist and what their defaults are; a later
/// configuration file only ever carries an override of <see cref="Default"/>; it never adds or
/// removes a hook or changes a hook's <see cref="Placeholders"/> or <see cref="Timing"/>.
/// </remarks>
/// <param name="Key">
/// The hook's stable identity, dotted by area, e.g. <c>"turn.roomLabel"</c>. Used to look the hook up
/// in the catalog and to key an override in configuration; never shown to a model.
/// </param>
/// <param name="Label">A short human name for the hook, shown as a form field's label in the settings UI.</param>
/// <param name="HelperText">
/// One plain-language sentence explaining what the hook controls and any constraint its wording must
/// keep, shown as the hint text under the field named by <see cref="Label"/>.
/// </param>
/// <param name="Default">
/// The hook's built-in text, verbatim from the source this hook was lifted from, as a non-interpolated
/// raw string literal so that <c>{{placeholder}}</c> tokens survive unescaped. Used whenever no
/// override is configured.
/// </param>
/// <param name="Placeholders">
/// Every <c>{{name}}</c> token this hook's text is allowed to contain, in the order a reader would
/// meet them. An override may use any subset of these and no others.
/// </param>
/// <param name="RequiredPlaceholders">
/// The subset of <see cref="Placeholders"/> that must appear in the rendered text for it to make
/// sense at all (e.g. the one placeholder that carries a Room's id). Always a subset of
/// <see cref="Placeholders"/>.
/// </param>
/// <param name="Timing">When an edit to this hook's text reaches a model; see <see cref="HookTiming"/>.</param>
internal sealed record HookDefinition(
    string Key,
    string Label,
    string HelperText,
    string Default,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<string> RequiredPlaceholders,
    HookTiming Timing);
