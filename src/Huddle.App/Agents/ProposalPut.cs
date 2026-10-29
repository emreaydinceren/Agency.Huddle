namespace Agency.Huddle.App.Agents;

/// <summary>
/// The result of <see cref="ProposalStore.TryPut"/>: what happened, and - only meaningful for
/// <see cref="ProposalPutResult.Refused"/> - the Proposal already pending, so the caller can name
/// its proposer.
/// </summary>
/// <param name="Result">Which of Spec §8.4's three outcomes occurred.</param>
/// <param name="Existing">The Proposal already pending in the Room when <see cref="Result"/> is <see cref="ProposalPutResult.Refused"/>; otherwise <see langword="null"/>.</param>
internal sealed record ProposalPut(ProposalPutResult Result, Proposal? Existing);
