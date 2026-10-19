using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// One heading's worth of <see cref="PromptFieldState"/> rows on the Settings page, grouped by the
/// prompt key's prefix (e.g. every <c>systemPrompt.*</c> prompt under "System prompt").
/// </summary>
/// <param name="Label">The heading shown above this group's fields.</param>
/// <param name="Fields">Every field in this group, in <see cref="PromptCatalog.All"/>'s order.</param>
public sealed record PromptFieldGroup(string Label, IReadOnlyList<PromptFieldState> Fields);
