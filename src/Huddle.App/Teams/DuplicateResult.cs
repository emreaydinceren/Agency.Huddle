namespace Agency.Huddle.App.Teams;

/// <summary>The outcome of <see cref="TeamDuplicator.Duplicate"/>.</summary>
/// <param name="Ok">Whether the new Team was created.</param>
/// <param name="Error">The plain refusal when <paramref name="Ok"/> is false; otherwise <see langword="null"/>.</param>
/// <param name="Hired">The names of the new Agents, in hire order.</param>
/// <param name="Skipped">The source members that were not copied.</param>
internal sealed record DuplicateResult(bool Ok, string? Error, IReadOnlyList<string> Hired, IReadOnlyList<SkippedMember> Skipped);
