namespace Agency.Huddle.App.Teams;

/// <summary>The effective <c>oneInProgressPerProject</c> rule for one Task, and the level that set it (ADR-0103).</summary>
/// <param name="IsOn">Whether one Task at a time In Progress is asked for.</param>
/// <param name="Source">The level whose value decided <paramref name="IsOn"/>.</param>
internal readonly record struct ResolvedRule(bool IsOn, RuleSource Source);
