namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>What is kept about one Room Session so that it can be resumed (RS §6.6).</summary>
/// <param name="SessionId">The Adapter's own id for this Room's session.</param>
/// <param name="AdapterId">The Adapter Profile id the session was opened under; a mismatch against the Persona's current one means the entry is stale.</param>
/// <param name="Model">The model the session was pinned to, or <see langword="null"/> for the Adapter's default.</param>
/// <param name="Effort">The effort level the session was pinned to, or <see langword="null"/> for the model's default.</param>
/// <param name="LastMessageId">The id of the last Message this Room Session's Transcript range should resume after, or <see langword="null"/> before any Turn has completed.</param>
/// <param name="LastTurnUtc">When this Room Session last completed a Turn.</param>
/// <param name="WorkMode">
/// The Work Mode the session was opened in (ADR-0033), or <see langword="null"/> for the Adapter's own.
/// Trailing and optional, so every existing construction is unchanged, and a file written before Work
/// Modes existed has no such key and loads as <see langword="null"/>: it still resumes for a Persona that
/// has none, with no migration.
/// </param>
/// <param name="ContextUsed">
/// How full the session's context window was when the last Turn ended, or <see langword="null"/> when no reading
/// was known. Persisted so the context meter survives a restart: a resumed session still has that context.
/// Trailing and optional like <paramref name="WorkMode"/>, so an older file loads it as <see langword="null"/>.
/// </param>
/// <param name="ContextSize">The size of the context window that <paramref name="ContextUsed"/> was measured against; zero when the Adapter did not say.</param>
/// <param name="HookState">
/// Each Turn Hook's own state for this session, keyed by hook name, or <see langword="null"/> when none was stored.
/// Trailing and optional like <paramref name="WorkMode"/>, so a file written before Turn Hooks existed has no such key
/// and loads as <see langword="null"/>. Keys the store does not know are kept as they are.
/// </param>
internal sealed record RoomSessionEntry(
    string SessionId, string AdapterId, string? Model, string? Effort, string? LastMessageId, DateTimeOffset LastTurnUtc, string? WorkMode = null, long? ContextUsed = null, long ContextSize = 0, IReadOnlyDictionary<string, string?>? HookState = null)
{
    /// <summary>
    /// Whether this entry still describes a session the Persona would resume: the Adapter, Model, Effort and
    /// Work Mode all match the current ones (RS §6.1). The host's own <c>CanResume</c> is checked separately.
    /// </summary>
    /// <param name="adapterId">The Adapter Profile id the Persona runs under now.</param>
    /// <param name="persona">The Persona whose current Model, Effort and Work Mode are compared.</param>
    public bool Matches(string adapterId, Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        return string.Equals(this.AdapterId, adapterId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Model, persona.Model, StringComparison.Ordinal)
            && string.Equals(this.Effort, persona.Effort, StringComparison.Ordinal)
            && string.Equals(this.WorkMode, persona.WorkMode, StringComparison.Ordinal);
    }
}
