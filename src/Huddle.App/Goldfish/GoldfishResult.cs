namespace Agency.Huddle.App.Goldfish;

/// <summary>What the goldfish reports through <c>goldfish_done</c>.</summary>
/// <param name="Text">The result text.</param>
/// <param name="Findings">The findings, in order.</param>
internal sealed record GoldfishResult(string Text, IReadOnlyList<string> Findings);
