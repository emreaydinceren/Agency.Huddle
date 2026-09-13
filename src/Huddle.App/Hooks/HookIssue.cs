namespace Agency.Huddle.App.Hooks;

/// <summary>
/// How serious a single <see cref="HookIssue"/> is. Neither value is grounds to refuse a save — see
/// the governing rule documented on <see cref="HookValidator"/>.
/// </summary>
internal enum HookIssueSeverity
{
    /// <summary>Worth a second look — most often an unrecognised placeholder token, likely a typo.</summary>
    Warning,

    /// <summary>
    /// This hook's text will probably not work as intended once it reaches a model — for example a
    /// required placeholder is missing, or the text is blank. Still only advisory: see
    /// <see cref="HookValidator"/>.
    /// </summary>
    Error,
}

/// <summary>
/// One finding reported by <see cref="HookValidator"/>, against either a single hook's text or a fully
/// rendered and composed system prompt.
/// </summary>
/// <param name="Key">
/// The <see cref="HookDefinition.Key"/> this finding is about, or the tool name it names when it comes
/// from <see cref="HookValidator.ValidateSystemPrompt"/>.
/// </param>
/// <param name="Severity">How serious this finding is.</param>
/// <param name="Message">
/// A human-readable explanation of what was found and, for an <see cref="HookIssueSeverity.Error"/>,
/// what breaks because of it.
/// </param>
internal sealed record HookIssue(string Key, HookIssueSeverity Severity, string Message);
