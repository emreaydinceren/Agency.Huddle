using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// The view-model for one prompt's row in the Settings surface: everything <c>PromptsPanel</c> needs to
/// show a single field, resolved once by <see cref="PromptFieldFactory"/> so the Razor markup never
/// touches <see cref="PromptCatalog"/> or <see cref="IPromptSource"/> directly. Public because Razor
/// generates the component that consumes it (<c>PromptsPanel</c>) as a public type, and a
/// <c>[Parameter]</c> cannot be typed <c>internal</c> without a compiler error (CS0053).
/// </summary>
/// <remarks>
/// A field has three distinct values, and conflating any two of them is the main way this type is
/// misused: <see cref="DefaultValue"/> is <c>PromptCatalog</c>'s shipped text; the prompt's <i>stored</i>
/// value is whatever <c>prompts.json</c> currently resolves to, i.e. <c>IPromptSource.Raw</c>'s answer
/// before this edit; and <see cref="Value"/> is the <i>pending</i> value — what the user has typed but
/// not yet saved, which may equal either, both, or neither of the other two. <see cref="IsModified"/>
/// compares the pending value against the default; <see cref="HasUnsavedChange"/> compares it against
/// the stored value; the two are independent; for example resetting a field that had no override
/// stages the default over an already-default stored value, making <see cref="HasUnsavedChange"/> true
/// while <see cref="IsModified"/> stays false.
/// </remarks>
/// <param name="Key">The prompt's stable identity, e.g. <c>"turn.roomLabel"</c>.</param>
/// <param name="Label">A short human name for the prompt, shown as this row's field label.</param>
/// <param name="HelperText">One plain-language sentence explaining what the prompt controls.</param>
/// <param name="Value">
/// The prompt's pending text: an uncommitted edit when one is staged, otherwise the stored value (the
/// configured override, or <see cref="DefaultValue"/> when there is none).
/// </param>
/// <param name="DefaultValue">The prompt's built-in text from <see cref="PromptCatalog"/>.</param>
/// <param name="Placeholders">Every <c>{{name}}</c> token this prompt's text may contain.</param>
/// <param name="Timing">When an edit to this prompt's text reaches a running teammate.</param>
/// <param name="IsModified">Whether <see cref="Value"/> differs from <see cref="DefaultValue"/>. Drives the Reset button's enabled state and the "Modified" badge.</param>
/// <param name="HasUnsavedChange">Whether <see cref="Value"/> differs from the prompt's currently stored value. Drives the Save button and the "Unsaved" badge.</param>
/// <param name="Issues">Validation findings for <see cref="Value"/>, from <c>PromptValidator.Validate</c>. Never blocks a save — see <c>PromptValidator</c>'s remarks.</param>
public sealed record PromptFieldState(
    string Key,
    string Label,
    string HelperText,
    string Value,
    string DefaultValue,
    IReadOnlyList<string> Placeholders,
    PromptTiming Timing,
    bool IsModified,
    bool HasUnsavedChange,
    IReadOnlyList<PromptIssue> Issues);

/// <summary>
/// One heading's worth of <see cref="PromptFieldState"/> rows on the Settings page, grouped by the
/// prompt key's prefix (e.g. every <c>systemPrompt.*</c> prompt under "System prompt").
/// </summary>
/// <param name="Label">The heading shown above this group's fields.</param>
/// <param name="Fields">Every field in this group, in <see cref="PromptCatalog.All"/>'s order.</param>
public sealed record PromptFieldGroup(string Label, IReadOnlyList<PromptFieldState> Fields);
