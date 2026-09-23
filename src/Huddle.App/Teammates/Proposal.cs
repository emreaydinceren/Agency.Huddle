namespace Agency.Huddle.App.Teammates;

/// <summary>
/// A roster of new Teammates one Agent has asked the Human to create, held in memory by
/// <see cref="ProposalStore"/> (Spec §6.9). At most one is pending per Room. Public because
/// <c>ProposalCard.razor</c> renders it directly, the same reason <see cref="Candidate"/> is
/// public.
/// </summary>
/// <param name="Id">This Proposal's own id, a Guid in <c>"N"</c> format.</param>
/// <param name="RoomId">The Room this Proposal is pending in - the store's key.</param>
/// <param name="ProposerAgentId">
/// The stable user id of the Agent that proposed this - the identity <see cref="ProposalStore.TryPut"/>
/// compares against to tell "the same proposer again" from "a different Agent", since a Name can
/// be renamed out from under it.
/// </param>
/// <param name="ProposerName">The proposer's Name at the moment of proposing, for display and for the Mention the outcome Message sends.</param>
/// <param name="Candidates">Between one and four proposed Teammates.</param>
/// <param name="ProposedAt">When this Proposal was made, from the injected <see cref="TimeProvider"/> - never <see cref="DateTimeOffset.Now"/>.</param>
public sealed record Proposal(
    string Id,
    string RoomId,
    string ProposerAgentId,
    string ProposerName,
    IReadOnlyList<Candidate> Candidates,
    DateTimeOffset ProposedAt);

/// <summary>The outcome of <see cref="ProposalStore.TryPut"/> against Spec §8.4's replacement table.</summary>
internal enum ProposalPutResult
{
    /// <summary>No Proposal was pending in the Room; the new one is now stored.</summary>
    Stored,

    /// <summary>The same Agent's earlier Proposal in this Room was replaced by the new one.</summary>
    Replaced,

    /// <summary>Another Agent's Proposal is already pending in this Room; the new one was refused.</summary>
    Refused,
}

/// <summary>
/// The result of <see cref="ProposalStore.TryPut"/>: what happened, and - only meaningful for
/// <see cref="ProposalPutResult.Refused"/> - the Proposal already pending, so the caller can name
/// its proposer.
/// </summary>
/// <param name="Result">Which of Spec §8.4's three outcomes occurred.</param>
/// <param name="Existing">The Proposal already pending in the Room when <see cref="Result"/> is <see cref="ProposalPutResult.Refused"/>; otherwise <see langword="null"/>.</param>
internal sealed record ProposalPut(ProposalPutResult Result, Proposal? Existing);
