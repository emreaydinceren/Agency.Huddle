using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// Builds the <see cref="PromptFieldState"/> rows the Settings page's Prompts tab renders, grouped and
/// ordered for display. Pulled out of the page as a pure static function, the same way
/// <c>Components/Pages/TeammateGrouping.cs</c> is pulled out of <c>Teammates.razor</c>: the read-only
/// component tests this suite has can only assert on rendered HTML — <c>HtmlRenderer</c> cannot
/// dispatch a click or change a <c>&lt;select&gt;</c> — so any behaviour worth a unit test (which
/// prompts appear, what order, whether a value counts as modified) has to live where a plain test can
/// call it directly.
/// </summary>
internal static class PromptFieldFactory
{
    private const string SystemPromptPrefix = "systemPrompt.";
    private const string TurnPrefix = "turn.";
    private const string GetHelpPrefix = "getHelp.";
    private const string ToolPrefix = "tool.";

    // RowsFor's clamp: 2 so a one-line prompt still reads as a text box rather than a slot, 14 so the
    // longest defaults (getHelp.rooms, systemPrompt.tools) cannot make their own field dominate the
    // whole page.
    private const int MinRows = 2;
    private const int MaxRows = 14;

    // The display order and labels the Settings page's groups appear in. A key that matches no
    // prefix here contributes to no group, which cannot happen while PromptCatalog only ever adds
    // keys under one of these four areas.
    private static readonly (string Prefix, string Label)[] GroupOrder =
    [
        (SystemPromptPrefix, "System prompt"),
        (TurnPrefix, "Turn"),
        (GetHelpPrefix, "Get help"),
        (ToolPrefix, "Tool descriptions"),
    ];

    /// <summary>
    /// Builds one <see cref="PromptFieldState"/> per <see cref="PromptCatalog"/> entry, grouped by key
    /// prefix in <see cref="GroupOrder"/>'s order, keeping <see cref="PromptCatalog.All"/>'s order
    /// within each group.
    /// </summary>
    /// <param name="prompts">The current prompt source, used to resolve each field's stored text.</param>
    /// <param name="pendingEdits">
    /// The user's uncommitted edits, keyed by <see cref="PromptDefinition.Key"/> — text typed but not
    /// yet saved. A key present here wins over <paramref name="prompts"/>'s stored value for that
    /// field's <see cref="PromptFieldState.Value"/>; a key absent here falls back to the stored value.
    /// </param>
    /// <returns>Every non-empty group, in display order.</returns>
    internal static IReadOnlyList<PromptFieldGroup> Build(IPromptSource prompts, IReadOnlyDictionary<string, string> pendingEdits)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(pendingEdits);

        var groups = new List<PromptFieldGroup>();

        foreach (var (prefix, label) in GroupOrder)
        {
            var fields = new List<PromptFieldState>();

            foreach (var definition in PromptCatalog.All)
            {
                if (definition.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    fields.Add(ToFieldState(definition, prompts, pendingEdits));
                }
            }

            if (fields.Count > 0)
            {
                groups.Add(new PromptFieldGroup(label, fields));
            }
        }

        return groups;
    }

    /// <summary>
    /// Stages every catalog key's default text into <paramref name="pendingEdits"/> — the pure logic
    /// behind the Settings page's "Reset all to defaults" button, pulled out here for the same reason
    /// <see cref="Build"/> is: a plain test can call it directly, where a rendered click cannot be
    /// simulated. Mirrors a single-field Reset exactly, just for every key at once: it only ever
    /// writes into the in-memory pending-edits map, never <c>PromptStore</c> itself, so nothing reaches
    /// disk until a caller goes on to save.
    /// </summary>
    /// <param name="pendingEdits">The Settings page's uncommitted-edits map to stage every default into.</param>
    internal static void StageAllDefaults(IDictionary<string, string> pendingEdits)
    {
        ArgumentNullException.ThrowIfNull(pendingEdits);

        foreach (var definition in PromptCatalog.All)
        {
            pendingEdits[definition.Key] = definition.Default;
        }
    }

    /// <summary>How many rows a textarea needs to show <paramref name="value"/> without scrolling, within reason.</summary>
    /// <param name="value">The text the textarea will display; <see langword="null"/> or empty counts as one line.</param>
    /// <returns>The line count plus one, clamped to between 2 and 14 rows.</returns>
    internal static int RowsFor(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return MinRows;
        }

        var lineCount = 1;
        foreach (var c in value)
        {
            if (c == '\n')
            {
                lineCount++;
            }
        }

        return Math.Clamp(lineCount + 1, MinRows, MaxRows);
    }

    /// <summary>
    /// Resolves one <see cref="PromptDefinition"/> into the row its Settings field renders. Both
    /// modified/unsaved comparisons normalise line endings first: <see cref="PromptDefinition.Default"/>
    /// carries whatever <c>PromptCatalog.cs</c>'s own line endings were at compile time (raw string
    /// literals preserve them, they do not normalise), while a browser <c>&lt;textarea&gt;</c> always
    /// hands back <c>\n</c>. Comparing the two verbatim leaves a multi-line prompt's "Modified" badge
    /// stuck on even when nothing but the line ending differs.
    /// </summary>
    /// <param name="definition">The catalog entry to resolve.</param>
    /// <param name="prompts">The current prompt source, giving this field's stored value.</param>
    /// <param name="pendingEdits">The user's uncommitted edits; see <see cref="Build"/>.</param>
    private static PromptFieldState ToFieldState(PromptDefinition definition, IPromptSource prompts, IReadOnlyDictionary<string, string> pendingEdits)
    {
        var stored = prompts.Raw(definition.Key);
        var value = pendingEdits.TryGetValue(definition.Key, out var pending) ? pending : stored;

        return new PromptFieldState(
            Key: definition.Key,
            Label: definition.Label,
            HelperText: definition.HelperText,
            Value: value,
            DefaultValue: definition.Default,
            Placeholders: definition.Placeholders,
            Timing: definition.Timing,
            IsModified: !string.Equals(
                value.ReplaceLineEndings("\n"), definition.Default.ReplaceLineEndings("\n"), StringComparison.Ordinal),
            HasUnsavedChange: !string.Equals(
                value.ReplaceLineEndings("\n"), stored.ReplaceLineEndings("\n"), StringComparison.Ordinal),
            Issues: PromptValidator.Validate(definition, value));
    }
}
