namespace Agency.Huddle.App.Helpers;

/// <summary>The session a Helper call opens and the one prompt it sends.</summary>
/// <param name="Cwd">An empty working folder, <c>{DataDir}/helpers/work</c>.</param>
/// <param name="Model">The Model id, or <see langword="null"/> for the Adapter's default.</param>
/// <param name="Effort">The Effort, or <see langword="null"/> for the Model's default.</param>
/// <param name="SystemPrompt">Sent with <c>SystemPromptMode.Replace</c>.</param>
/// <param name="Prompt">The single user prompt.</param>
/// <param name="Meta">The isolation <c>_meta</c> for an Adapter that isolates user settings, or <see langword="null"/>.</param>
internal sealed record HelperSessionRequest(
    string Cwd,
    string? Model,
    string? Effort,
    string SystemPrompt,
    string Prompt,
    IReadOnlyDictionary<string, object>? Meta = null);
