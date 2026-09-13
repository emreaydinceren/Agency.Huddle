namespace Agency.Huddle.App.Hooks;

/// <summary>
/// When an edit to a hook's text can reach a running Agent. This is a property of where the hook's
/// value is read, not a setting a hook author chooses: it tells the settings UI whether to warn that
/// a save needs a restart to take effect.
/// </summary>
internal enum HookTiming
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
