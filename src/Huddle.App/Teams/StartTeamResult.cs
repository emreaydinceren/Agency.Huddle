namespace Agency.Huddle.App.Teams;

/// <summary>The outcome of starting a Team.</summary>
/// <param name="Ok">Whether every member was hired and the manager is running.</param>
/// <param name="Error">Why the start stopped; <see langword="null"/> when it did not.</param>
/// <param name="Started">The Agents hired before the start finished or stopped, in hire order.</param>
internal sealed record StartTeamResult(bool Ok, string? Error, IReadOnlyList<string> Started);
