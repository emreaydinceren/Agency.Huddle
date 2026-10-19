namespace Agency.Huddle.App.Teammates;

/// <summary>The result of <see cref="CandidateChecker.CheckAsync"/>: every problem found across every Candidate checked, and whether there were none.</summary>
/// <param name="IsValid"><see langword="true"/> when <paramref name="Problems"/> is empty.</param>
/// <param name="Problems">Every problem found, in the order <see cref="CandidateChecker.CheckAsync"/> found them, with exact duplicates removed.</param>
public sealed record CandidateCheck(bool IsValid, IReadOnlyList<string> Problems);
