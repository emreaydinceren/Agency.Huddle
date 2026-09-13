using Agency.Huddle.App.Hooks;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// The view-model for one hook's row in the Settings surface: everything <c>HooksPanel</c> needs to
/// show a single field, resolved once by <see cref="HookFieldFactory"/> so the Razor markup never
/// touches <see cref="HookCatalog"/> or <see cref="IHookSource"/> directly. Public because Razor
/// generates the component that consumes it (<c>HooksPanel</c>) as a public type, and a
/// <c>[Parameter]</c> cannot be typed <c>internal</c> without a compiler error (CS0053).
/// </summary>
/// <param name="Key">The hook's stable identity, e.g. <c>"turn.roomLabel"</c>.</param>
/// <param name="Label">A short human name for the hook, shown as this row's field label.</param>
/// <param name="HelperText">One plain-language sentence explaining what the hook controls.</param>
/// <param name="Value">The hook's current text: the configured override when one exists, otherwise <see cref="DefaultValue"/>.</param>
/// <param name="DefaultValue">The hook's built-in text from <see cref="HookCatalog"/>.</param>
/// <param name="Placeholders">Every <c>{{name}}</c> token this hook's text may contain.</param>
/// <param name="Timing">When an edit to this hook's text reaches a running teammate.</param>
/// <param name="IsModified">Whether <see cref="Value"/> differs from <see cref="DefaultValue"/>.</param>
public sealed record HookFieldState(
    string Key,
    string Label,
    string HelperText,
    string Value,
    string DefaultValue,
    IReadOnlyList<string> Placeholders,
    HookTiming Timing,
    bool IsModified);

/// <summary>
/// One heading's worth of <see cref="HookFieldState"/> rows on the Settings page, grouped by the
/// hook key's prefix (e.g. every <c>systemPrompt.*</c> hook under "System prompt").
/// </summary>
/// <param name="Label">The heading shown above this group's fields.</param>
/// <param name="Fields">Every field in this group, in <see cref="HookCatalog.All"/>'s order.</param>
public sealed record HookFieldGroup(string Label, IReadOnlyList<HookFieldState> Fields);
