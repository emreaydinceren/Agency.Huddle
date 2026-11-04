namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>What a hook adds. Null text adds nothing, and the carry still reaches After.</summary>
/// <param name="Text">The block to add to the prompt, or null for none.</param>
/// <param name="Carry">A typed value passed to the hook's After point.</param>
internal sealed record TurnAddition(string? Text, object? Carry);
