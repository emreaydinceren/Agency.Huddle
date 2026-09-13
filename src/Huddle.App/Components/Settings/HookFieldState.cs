using Agency.Huddle.App.Hooks;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// The view-model for one hook's row in the Settings surface: everything <c>HooksPanel</c> needs to
/// show a single field, resolved once by <see cref="HookFieldFactory"/> so the Razor markup never
/// touches <see cref="HookCatalog"/> or <see cref="IHookSource"/> directly. Public because Razor
/// generates the component that consumes it (<c>HooksPanel</c>) as a public type, and a
/// <c>[Parameter]</c> cannot be typed <c>internal</c> without a compiler error (CS0053).
/// </summary>
/// <remarks>
/// A field has three distinct values, and conflating any two of them is the main way this type is
/// misused: <see cref="DefaultValue"/> is <c>HookCatalog</c>'s shipped text; the hook's <i>stored</i>
/// value is whatever <c>hooks.json</c> currently resolves to, i.e. <c>IHookSource.Raw</c>'s answer
/// before this edit; and <see cref="Value"/> is the <i>pending</i> value — what the user has typed but
/// not yet saved, which may equal either, both, or neither of the other two. <see cref="IsModified"/>
/// compares the pending value against the default; <see cref="HasUnsavedChange"/> compares it against
/// the stored value; the two are independent; for example resetting a field that had no override
/// stages the default over an already-default stored value, making <see cref="HasUnsavedChange"/> true
/// while <see cref="IsModified"/> stays false.
/// </remarks>
/// <param name="Key">The hook's stable identity, e.g. <c>"turn.roomLabel"</c>.</param>
/// <param name="Label">A short human name for the hook, shown as this row's field label.</param>
/// <param name="HelperText">One plain-language sentence explaining what the hook controls.</param>
/// <param name="Value">
/// The hook's pending text: an uncommitted edit when one is staged, otherwise the stored value (the
/// configured override, or <see cref="DefaultValue"/> when there is none).
/// </param>
/// <param name="DefaultValue">The hook's built-in text from <see cref="HookCatalog"/>.</param>
/// <param name="Placeholders">Every <c>{{name}}</c> token this hook's text may contain.</param>
/// <param name="Timing">When an edit to this hook's text reaches a running teammate.</param>
/// <param name="IsModified">Whether <see cref="Value"/> differs from <see cref="DefaultValue"/>. Drives the Reset button's enabled state and the "Modified" badge.</param>
/// <param name="HasUnsavedChange">Whether <see cref="Value"/> differs from the hook's currently stored value. Drives the Save button and the "Unsaved" badge.</param>
/// <param name="Issues">Validation findings for <see cref="Value"/>, from <c>HookValidator.Validate</c>. Never blocks a save — see <c>HookValidator</c>'s remarks.</param>
public sealed record HookFieldState(
    string Key,
    string Label,
    string HelperText,
    string Value,
    string DefaultValue,
    IReadOnlyList<string> Placeholders,
    HookTiming Timing,
    bool IsModified,
    bool HasUnsavedChange,
    IReadOnlyList<HookIssue> Issues);

/// <summary>
/// One heading's worth of <see cref="HookFieldState"/> rows on the Settings page, grouped by the
/// hook key's prefix (e.g. every <c>systemPrompt.*</c> hook under "System prompt").
/// </summary>
/// <param name="Label">The heading shown above this group's fields.</param>
/// <param name="Fields">Every field in this group, in <see cref="HookCatalog.All"/>'s order.</param>
public sealed record HookFieldGroup(string Label, IReadOnlyList<HookFieldState> Fields);
