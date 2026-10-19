namespace Agency.Huddle.App.Teammates;

/// <summary>The limit with the least room left for a set of Candidates.</summary>
/// <param name="Kind">Which limit it is.</param>
/// <param name="Team">The Team for <see cref="HireLimitKind.PerTeam"/>; <see langword="null"/> for the live-process limit.</param>
/// <param name="Used">Members of that Team, or live processes, now.</param>
/// <param name="Limit">The limit's value.</param>
internal sealed record HireHeadroom(HireLimitKind Kind, string? Team, int Used, int Limit);
