using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Holds at most one pending <see cref="Proposal"/> per Room, in memory (Spec §6.9). Every
/// operation is O(1) under a single <see cref="Lock"/>; <see cref="RoomEvents.ProposalChanged"/>
/// is always raised after that lock is released, for <see cref="TryPut"/> (Stored or Replaced),
/// a successful <see cref="TryTake"/>, and a <see cref="Drop"/> that actually removed something.
/// The class itself is public only so <see cref="Agency.Huddle.App.Services.ChatService"/>'s public
/// constructor can take one (CS0051); every member below stays <see langword="internal"/> - nothing
/// outside this assembly is meant to call any of them.
/// </summary>
/// <param name="events">The hub <see cref="RoomEvents.ProposalChanged"/> is raised on.</param>
public sealed class ProposalStore(RoomEvents events)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, Proposal> proposals = new(StringComparer.Ordinal);

    /// <summary>Returns the Proposal currently pending in <paramref name="roomId"/>, or <see langword="null"/> when none is.</summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>The pending Proposal, or <see langword="null"/>.</returns>
    internal Proposal? Get(string roomId)
    {
        lock (this.gate)
        {
            return this.proposals.GetValueOrDefault(roomId);
        }
    }

    /// <summary>
    /// Stores <paramref name="proposal"/> under Spec §8.4's replacement rules: an empty Room
    /// stores it, the same proposer replaces its own pending Proposal, and any other proposer is
    /// refused, with the Proposal already pending handed back so the caller can name its proposer.
    /// </summary>
    /// <param name="proposal">The Proposal to store.</param>
    /// <returns>What happened, and, only when refused, the Proposal that was already pending.</returns>
    internal ProposalPut TryPut(Proposal proposal)
    {
        ProposalPutResult result;
        Proposal? existing;

        lock (this.gate)
        {
            if (!this.proposals.TryGetValue(proposal.RoomId, out existing))
            {
                this.proposals[proposal.RoomId] = proposal;
                result = ProposalPutResult.Stored;
                existing = null;
            }
            else if (string.Equals(existing.ProposerAgentId, proposal.ProposerAgentId, StringComparison.Ordinal))
            {
                this.proposals[proposal.RoomId] = proposal;
                result = ProposalPutResult.Replaced;
                existing = null;
            }
            else
            {
                result = ProposalPutResult.Refused;
            }
        }

        if (result is ProposalPutResult.Stored or ProposalPutResult.Replaced)
        {
            events.PublishProposalChanged(proposal.RoomId);
        }

        return new ProposalPut(result, existing);
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
        Proposal? taken;

        lock (this.gate)
        {
            if (this.proposals.TryGetValue(roomId, out var current) &&
                string.Equals(current.Id, proposalId, StringComparison.Ordinal))
            {
                _ = this.proposals.Remove(roomId);
                taken = current;
            }
            else
            {
                taken = null;
            }
        }

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
        bool removed;

        lock (this.gate)
        {
            removed = this.proposals.Remove(roomId);
        }

        if (removed)
        {
            events.PublishProposalChanged(roomId);
        }
    }
}
