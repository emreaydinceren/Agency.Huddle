using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Agents;

/// <summary>
/// Holds at most one pending <see cref="Proposal"/> per Room, in memory (Spec §6.9), on a
/// <see cref="PendingByRoom{T}"/>. <see cref="RoomEvents.ProposalChanged"/> is always raised after that
/// core's lock is released, for <see cref="TryPut"/> (Stored or Replaced), a successful
/// <see cref="TryTake"/>, and a <see cref="Drop"/> that actually removed something.
/// The class itself is public only so <see cref="Agency.Huddle.App.Services.ChatService"/>'s public
/// constructor can take one (CS0051); every member below stays <see langword="internal"/> - nothing
/// outside this assembly is meant to call any of them.
/// </summary>
/// <param name="events">The hub <see cref="RoomEvents.ProposalChanged"/> is raised on.</param>
public sealed class ProposalStore(RoomEvents events)
{
    private readonly PendingByRoom<Proposal> proposals = new(
        static proposal => proposal.RoomId,
        static proposal => proposal.Id,
        static proposal => proposal.ProposerAgentId);

    /// <summary>Returns the Proposal currently pending in <paramref name="roomId"/>, or <see langword="null"/> when none is.</summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>The pending Proposal, or <see langword="null"/>.</returns>
    internal Proposal? Get(string roomId) => this.proposals.Get(roomId);

    /// <summary>
    /// Stores <paramref name="proposal"/> under Spec §8.4's replacement rules: an empty Room
    /// stores it, the same proposer replaces its own pending Proposal, and any other proposer is
    /// refused, with the Proposal already pending handed back so the caller can name its proposer.
    /// </summary>
    /// <param name="proposal">The Proposal to store.</param>
    /// <returns>What happened, and, only when refused, the Proposal that was already pending.</returns>
    internal PendingPut<Proposal> TryPut(Proposal proposal)
    {
        PendingPut<Proposal> put = this.proposals.TryPut(proposal);

        if (put.Result is not PendingPutResult.Refused)
        {
            events.PublishProposalChanged(proposal.RoomId);
        }

        return put;
    }

    /// <summary>
    /// Removes and returns the pending Proposal for <paramref name="roomId"/>, but only when its
    /// id still matches <paramref name="proposalId"/> - "first click wins" (Spec §6.10): of two
    /// concurrent callers racing the same id, the removal happens inside the lock, so exactly one
    /// gets the Proposal back and the other gets <see langword="null"/>.
    /// </summary>
    /// <param name="roomId">The Room to take from.</param>
    /// <param name="proposalId">The Proposal id the caller expects to still be pending.</param>
    /// <returns>The removed Proposal, or <see langword="null"/> when nothing matched.</returns>
    internal Proposal? TryTake(string roomId, string proposalId)
    {
        Proposal? taken = this.proposals.TryTake(roomId, proposalId);

        if (taken is not null)
        {
            events.PublishProposalChanged(roomId);
        }

        return taken;
    }

    /// <summary>Removes any Proposal pending in <paramref name="roomId"/>, if one exists.</summary>
    /// <param name="roomId">The Room to clear - called when it is archived, deleted, or restarted.</param>
    internal void Drop(string roomId)
    {
        if (this.proposals.Drop(roomId))
        {
            events.PublishProposalChanged(roomId);
        }
    }
}
