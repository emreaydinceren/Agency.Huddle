namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>A session opening.</summary>
/// <param name="Scope">Who the pipeline serves.</param>
/// <param name="Reason">Why the session is opening.</param>
/// <param name="State">The hook's stored state on a resume or a compaction, else null.</param>
internal sealed record SessionOpening(TurnHookScope Scope, SessionOpenReason Reason, string? State);
