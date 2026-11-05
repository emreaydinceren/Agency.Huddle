namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>A Message Turn about to be prompted.</summary>
/// <param name="Scope">Who the pipeline serves.</param>
/// <param name="RoomId">The Room the Turn belongs to.</param>
/// <param name="State">The hook's state for this session.</param>
internal sealed record TurnStarting(TurnHookScope Scope, string RoomId, string? State);
