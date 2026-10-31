namespace Agency.Huddle.App.Elicitation;

/// <summary>One choice of a select field. All three texts are the agent's own, carried verbatim.</summary>
/// <param name="Value">What goes on the wire when it is chosen: the schema's <c>const</c> (or the enum string).</param>
/// <param name="Label">What the Human reads: the option's <c>title</c>, or <paramref name="Value"/> when it has none.</param>
/// <param name="Description">The option's secondary text, or <see langword="null"/>.</param>
public sealed record ElicitationOption(string Value, string Label, string? Description);
