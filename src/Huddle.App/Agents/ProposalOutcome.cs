namespace Agency.Huddle.App.Agents;

/// <summary>
/// What approving or declining a Proposal did (Spec §6.10). Public because <c>ProposalCard.razor</c>
/// reads it directly to show the outcome, the same reason <see cref="Proposal"/> and
/// <see cref="Candidate"/> are public.
/// </summary>
/// <param name="Kind">Which of Spec §6.10's outcomes occurred.</param>
/// <param name="Created">The Names of every Candidate that became an Agent, in Proposal order.</param>
/// <param name="Failed">Every Candidate that did not become an Agent, with why - empty unless <paramref name="Kind"/> is <see cref="ProposalOutcomeKind.PartlyCreated"/> or <see cref="ProposalOutcomeKind.NoneCreated"/>.</param>
/// <param name="PostedText">
/// The Message text that was posted - or, for <see cref="ProposalOutcomeKind.Gone"/>, an empty
/// string, since nothing was posted. Still populated even when the post itself failed after
/// Agents were already created (Spec §6.10's Constraints), so a caller can show what would have
/// been said.
/// </param>
public sealed record ProposalOutcome(
    ProposalOutcomeKind Kind,
    IReadOnlyList<string> Created,
    IReadOnlyList<CandidateFailure> Failed,
    string PostedText);
