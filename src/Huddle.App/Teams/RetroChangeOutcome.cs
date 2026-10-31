namespace Agency.Huddle.App.Teams;

/// <summary>The result of accepting or rejecting one retro change.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Text">The text posted in the room, or the reason for a refusal; empty for <see cref="RetroChangeOutcomeKind.Gone"/>.</param>
internal sealed record RetroChangeOutcome(RetroChangeOutcomeKind Kind, string Text);
