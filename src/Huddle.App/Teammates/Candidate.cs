namespace Agency.Huddle.App.Teammates;

/// <summary>
/// One proposed Teammate (Spec §7.2), parsed from a <c>propose_teammates</c> tool call by
/// <see cref="CandidateJson.TryParse"/>. Carries only descriptive fields (Spec §14 D-5): a
/// Candidate cannot set Model, Effort, Adapter or Skills, because each of those grants tools or
/// spends money, and the Human sets them on the Teammate card once it exists. Public because
/// <c>ProposalCard.razor</c> renders it directly.
/// </summary>
/// <param name="Name">The Teammate's proposed Name. Validated by <see cref="CandidateChecker"/>, not here.</param>
/// <param name="Alias">The Teammate's proposed Alias. Validated by <see cref="CandidateChecker"/>, not here.</param>
/// <param name="Title">The Teammate's proposed job, as free display text.</param>
/// <param name="Body">The proposed Persona body text.</param>
/// <param name="Teams">The Teams this Teammate would belong to. Empty when the Candidate names none.</param>
/// <param name="ConsultWhen">
/// A single line saying when to bring this Teammate in, or <see langword="null"/> when the
/// Candidate left it out or left it blank.
/// </param>
public sealed record Candidate(string Name, string Alias, string Title, string Body, IReadOnlyList<string> Teams, string? ConsultWhen);
