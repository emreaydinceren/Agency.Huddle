namespace Agency.Huddle.App.SecondBrain;

/// <summary>How an archive or unarchive ended.</summary>
/// <param name="Done">Whether the Team moved.</param>
/// <param name="Refusal">The user-facing reason when <paramref name="Done"/> is <see langword="false"/>; otherwise <see langword="null"/>.</param>
internal sealed record ArchiveOutcome(bool Done, string? Refusal);
