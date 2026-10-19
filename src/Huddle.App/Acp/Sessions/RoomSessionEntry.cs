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
internal sealed record RoomSessionEntry(
    string SessionId, string AdapterId, string? Model, string? Effort, string? LastMessageId, DateTimeOffset LastTurnUtc, string? WorkMode = null);
