namespace Agency.Huddle.App.Components;

/// <summary>
/// One choice of a <c>SegmentedControl</c>: the value it selects and what its segment shows. Public because it is a
/// component parameter's type; in <c>Agency.Huddle.App.Components</c>, like <see cref="AgentProfileCardMode"/>, because a public
/// type in a namespace ending <c>Shared</c> fails CA1716.
/// </summary>
/// <typeparam name="T">The type of the value the control selects.</typeparam>
/// <param name="Value">The value selected by this segment.</param>
/// <param name="Label">The segment's text.</param>
/// <param name="Icon">An optional icon (an SVG path from <c>Icons</c>), drawn before the label.</param>
/// <param name="Badge">An optional count or short note, drawn after the label in caption type.</param>
public sealed record SegmentedOption<T>(T Value, string Label, string? Icon = null, string? Badge = null);
