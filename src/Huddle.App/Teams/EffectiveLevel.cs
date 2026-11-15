namespace Agency.Huddle.App.Teams;

/// <summary>The Needs You level that applies to a Team, a Project or a Task, and where it came from (ADR-0087).</summary>
/// <param name="Level">The level in force.</param>
/// <param name="Scope">The scope that supplied <paramref name="Level"/>, so a timeline event can say "proceeded under project level Run inside the Team".</param>
/// <param name="ScopeName">The Team's or Project's name when <paramref name="Scope"/> is <see cref="NeedsYouScope.Team"/> or <see cref="NeedsYouScope.Project"/>; <see langword="null"/> for the global scope.</param>
/// <param name="SpendCap">The spend cap in USD of the scope that supplied the level, or <see langword="null"/> for no bound. The cap travels with the level.</param>
/// <param name="CapReached">Whether <paramref name="Level"/> is Ask me because the scope spent its cap running inside the Team, not because a scope says so.</param>
internal sealed record EffectiveLevel(NeedsYouLevel Level, NeedsYouScope Scope, string? ScopeName, decimal? SpendCap, bool CapReached = false);
