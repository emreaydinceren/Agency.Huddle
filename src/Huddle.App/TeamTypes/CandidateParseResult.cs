namespace Agency.Huddle.App.TeamTypes;

/// <summary>The outcome of parsing a candidate file.</summary>
/// <param name="Template">The template, or <see langword="null"/> when there are problems.</param>
/// <param name="Problems">Why the file is not a valid candidate; empty when it is valid.</param>
internal sealed record CandidateParseResult(CandidateTemplate? Template, IReadOnlyList<string> Problems);
