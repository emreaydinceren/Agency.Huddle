namespace Agency.Huddle.App.Prompts;

/// <summary>
/// When an edit to a prompt's text can reach a running Agent. This is a property of where the prompt's
/// value is read, not a setting a prompt author chooses: it tells the settings UI whether to warn that
/// a save needs a restart to take effect.
/// </summary>
public enum PromptTiming
{
    /// <summary>
    /// Rendered fresh on every invocation, so an edit reaches the very next turn of every Persona,
    /// with no restart required.
    /// </summary>
    Live,

    /// <summary>
    /// Baked into a Persona's system prompt at <c>session/new</c> and never re-read afterwards, so an
    /// edit reaches only teammates started after the change; a Persona already running keeps the text
    /// it started with until its session restarts.
    /// </summary>
    NextSession,
}
