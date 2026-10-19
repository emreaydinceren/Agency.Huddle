namespace Agency.Huddle.App.Prompts;

/// <summary>
/// How serious a single <see cref="PromptIssue"/> is. Neither value is grounds to refuse a save — see
/// the governing rule documented on <see cref="PromptValidator"/>. Public — alongside
/// <see cref="PromptIssue"/> itself — because <c>PromptFieldState.Issues</c> is a public record consumed
/// by the Settings UI, and a public record cannot expose an internal type; this is the same reasoning
/// that made <see cref="PromptTiming"/> public.
/// </summary>
public enum PromptIssueSeverity
{
    /// <summary>Worth a second look — most often an unrecognised placeholder token, likely a typo.</summary>
    Warning,

    /// <summary>
    /// This prompt's text will probably not work as intended once it reaches a model — for example a
    /// required placeholder is missing, or the text is blank. Still only advisory: see
    /// <see cref="PromptValidator"/>.
    /// </summary>
    Error,
}
