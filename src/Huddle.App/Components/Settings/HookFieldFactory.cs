using Agency.Huddle.App.Hooks;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// Builds the <see cref="HookFieldState"/> rows the Settings page's Hooks tab renders, grouped and
/// ordered for display. Pulled out of the page as a pure static function, the same way
/// <c>Components/Pages/TeammateGrouping.cs</c> is pulled out of <c>Teammates.razor</c>: the read-only
/// component tests this suite has can only assert on rendered HTML — <c>HtmlRenderer</c> cannot
/// dispatch a click or change a <c>&lt;select&gt;</c> — so any behaviour worth a unit test (which
/// hooks appear, what order, whether a value counts as modified) has to live where a plain test can
/// call it directly.
/// </summary>
internal static class HookFieldFactory
{
    private const string SystemPromptPrefix = "systemPrompt.";
    private const string TurnPrefix = "turn.";
    private const string GetHelpPrefix = "getHelp.";
    private const string ToolPrefix = "tool.";

    // RowsFor's clamp: 2 so a one-line hook still reads as a text box rather than a slot, 14 so the
    // longest defaults (getHelp.rooms, systemPrompt.tools) cannot make their own field dominate the
    // whole page.
    private const int MinRows = 2;
    private const int MaxRows = 14;

    // The display order and labels the Settings page's groups appear in. A key that matches no
    // prefix here contributes to no group, which cannot happen while HookCatalog only ever adds
    // keys under one of these four areas.
    private static readonly (string Prefix, string Label)[] GroupOrder =
    [
        (SystemPromptPrefix, "System prompt"),
        (TurnPrefix, "Turn"),
        (GetHelpPrefix, "Get help"),
        (ToolPrefix, "Tool descriptions"),
    ];

    /// <summary>
    /// Builds one <see cref="HookFieldState"/> per <see cref="HookCatalog"/> entry, grouped by key
    /// prefix in <see cref="GroupOrder"/>'s order, keeping <see cref="HookCatalog.All"/>'s order
    /// within each group.
    /// </summary>
    /// <param name="hooks">The current hook source, used to resolve each field's current text.</param>
    /// <returns>Every non-empty group, in display order.</returns>
    internal static IReadOnlyList<HookFieldGroup> Build(IHookSource hooks)
    {
        ArgumentNullException.ThrowIfNull(hooks);

        var groups = new List<HookFieldGroup>();

        foreach (var (prefix, label) in GroupOrder)
        {
            var fields = new List<HookFieldState>();

            foreach (var definition in HookCatalog.All)
            {
                if (definition.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    fields.Add(ToFieldState(definition, hooks));
                }
            }

            if (fields.Count > 0)
            {
                groups.Add(new HookFieldGroup(label, fields));
            }
        }

        return groups;
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

    /// <summary>Resolves one <see cref="HookDefinition"/> into the row its Settings field renders.</summary>
    /// <param name="definition">The catalog entry to resolve.</param>
    /// <param name="hooks">The current hook source.</param>
    private static HookFieldState ToFieldState(HookDefinition definition, IHookSource hooks)
    {
        var value = hooks.Raw(definition.Key);

        return new HookFieldState(
            Key: definition.Key,
            Label: definition.Label,
            HelperText: definition.HelperText,
            Value: value,
            DefaultValue: definition.Default,
            Placeholders: definition.Placeholders,
            Timing: definition.Timing,
            IsModified: !string.Equals(value, definition.Default, StringComparison.Ordinal));
    }
}
