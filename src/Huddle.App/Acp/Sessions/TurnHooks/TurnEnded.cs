namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>A Turn whose prompt was sent.</summary>
/// <param name="Scope">Who the pipeline serves.</param>
/// <param name="RoomId">The Room the Turn belongs to.</param>
/// <param name="State">The hook's state for this session.</param>
/// <param name="Touched">A copy of the touched paths, taken under the session's gate.</param>
internal sealed record TurnEnded(TurnHookScope Scope, string RoomId, string? State, IReadOnlyCollection<string> Touched);
