namespace Agency.Huddle.App.Acp;

/// <summary>One level of the Human's standing Instructions, as it goes into the system prompt.</summary>
/// <param name="Source">Whose Instructions these are: <c>Huddle</c>, <c>Team &lt;Team&gt;</c> or <c>You</c>.</param>
/// <param name="Text">The file's text, cut at the configured cap with the cut line appended when it was cut.</param>
internal sealed record InstructionsSection(string Source, string Text);
