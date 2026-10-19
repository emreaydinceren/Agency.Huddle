namespace Agency.Huddle.App.Prompts;

/// <summary>
/// One finding reported by <see cref="PromptValidator"/>, against either a single prompt's text or a fully
/// rendered and composed system prompt. Public for the same reason <see cref="PromptIssueSeverity"/> is.
/// </summary>
/// <param name="Key">
/// The <see cref="PromptDefinition.Key"/> this finding is about, or the tool name it names when it comes
/// from <see cref="PromptValidator.ValidateSystemPrompt"/>.
/// </param>
/// <param name="Severity">How serious this finding is.</param>
/// <param name="Message">
/// A human-readable explanation of what was found and, for an <see cref="PromptIssueSeverity.Error"/>,
/// what breaks because of it.
/// </param>
public sealed record PromptIssue(string Key, PromptIssueSeverity Severity, string Message);
