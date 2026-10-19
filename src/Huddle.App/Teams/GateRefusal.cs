namespace Agency.Huddle.App.Teams;

/// <summary>A gate's refusal of a change.</summary>
/// <param name="Gate">The gate that refused.</param>
/// <param name="Message">The text returned to the caller, naming the gate.</param>
internal sealed record GateRefusal(GateId Gate, string Message);
