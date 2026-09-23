using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// Pins <see cref="ProposalStore"/> against Spec §6.9 (Inputs / outputs) and Spec §8.4 (Proposal
/// replacement), using a real <see cref="RoomEvents"/> throughout - the store's only collaborator -
/// rather than a fake, since <see cref="RoomEvents"/> is a plain event hub with no external
/// dependencies of its own.
/// </summary>
public sealed class ProposalStoreTests
{
    /// <summary>Spec §8.4 row 1: no Proposal exists yet in the Room, so <c>TryPut</c> stores it.</summary>
    [Fact]
    public void TryPut_Empty_Stored()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var proposal = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");

        var result = store.TryPut(proposal);

        Assert.Equal(ProposalPutResult.Stored, result.Result);
        Assert.Equal(proposal, store.Get("room-1"));
    }

    /// <summary>
    /// Spec §8.4 row 2: the same Agent that owns the pending Proposal proposes again in the same
    /// Room - the second call replaces the first, and the Room now holds only the new Proposal.
    /// </summary>
    [Fact]
    public void TryPut_SameProposer_Replaced()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var first = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        var second = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-2");
        _ = store.TryPut(first);

        var result = store.TryPut(second);

        Assert.Equal(ProposalPutResult.Replaced, result.Result);
        Assert.Equal(second, store.Get("room-1"));
    }

    /// <summary>
    /// Spec §8.4 row 3: a different Agent tries to propose while another Agent's Proposal is
    /// already pending in the same Room - refused, and the existing Proposal is handed back so the
    /// caller can name its proposer.
    /// </summary>
    [Fact]
    public void TryPut_OtherProposer_RefusedWithExisting()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var first = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        var second = MakeProposal(roomId: "room-1", proposerAgentId: "agent-quill", proposalId: "proposal-2");
        _ = store.TryPut(first);

        var result = store.TryPut(second);

        Assert.Equal(ProposalPutResult.Refused, result.Result);
        Assert.Equal(first, result.Existing);
        Assert.Equal(first, store.Get("room-1"));
    }

    /// <summary>
    /// Spec §6.10's Approve flow: <c>TryTake</c> is the "first click wins" primitive - the matching
    /// id returns the Proposal exactly once, and a second call for the same id finds nothing left.
    /// </summary>
    [Fact]
    public void TryTake_MatchingId_ReturnsOnceThenNull()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var proposal = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        _ = store.TryPut(proposal);

        var firstTake = store.TryTake("room-1", "proposal-1");
        var secondTake = store.TryTake("room-1", "proposal-1");

        Assert.Equal(proposal, firstTake);
        Assert.Null(secondTake);
        Assert.Null(store.Get("room-1"));
    }

    /// <summary>
    /// Spec §6.10: a stale or mismatched proposal id - stale because another Proposal has since
    /// replaced it, or simply wrong - takes nothing and leaves the current Proposal untouched.
    /// </summary>
    [Fact]
    public void TryTake_StaleId_ReturnsNull()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var proposal = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        _ = store.TryPut(proposal);

        var take = store.TryTake("room-1", "some-other-proposal-id");

        Assert.Null(take);
        Assert.Equal(proposal, store.Get("room-1"));
    }

    /// <summary>
    /// Spec §6.9 responsibilities: <c>Drop</c> removes the pending Proposal for a Room and raises
    /// <see cref="RoomEvents.ProposalChanged"/> - but the internal-flow note "Drop only if something
    /// was removed" means a second <c>Drop</c> on an already-empty Room must not raise again.
    /// </summary>
    [Fact]
    public void Drop_RemovesAndRaises()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var proposal = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        _ = store.TryPut(proposal);
        var raisedFor = new List<string>();
        events.ProposalChanged += roomId => raisedFor.Add(roomId);

        store.Drop("room-1");

        Assert.Null(store.Get("room-1"));
        Assert.Equal("room-1", Assert.Single(raisedFor));

        store.Drop("room-1");

        Assert.Equal("room-1", Assert.Single(raisedFor));
    }

    /// <summary>
    /// Spec §6.9 responsibilities: a stored Proposal raises <see cref="RoomEvents.ProposalChanged"/>
    /// with the Room's id, so the Room view knows which Room's card to repaint without re-reading
    /// every Room.
    /// </summary>
    [Fact]
    public void TryPut_RaisesProposalChangedWithRoomId()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var proposal = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        var raisedFor = new List<string>();
        events.ProposalChanged += roomId => raisedFor.Add(roomId);

        _ = store.TryPut(proposal);

        Assert.Equal("room-1", Assert.Single(raisedFor));
    }

    /// <summary>
    /// Settled design point (not one of the seven originally named): <c>ProposalChanged</c> is
    /// raised for <c>Stored</c>, <c>Replaced</c>, a matching <c>Take</c>, and a removing
    /// <c>Drop</c> - the store owns the raise for all four, so a caller such as
    /// <c>ProposalService</c> (D11) must never raise it a second time after a successful
    /// <c>TryTake</c>.
    /// </summary>
    [Fact]
    public void TryTake_Matching_RaisesProposalChanged()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var store = new ProposalStore(events);
        var proposal = MakeProposal(roomId: "room-1", proposerAgentId: "agent-vera", proposalId: "proposal-1");
        _ = store.TryPut(proposal);
        var raisedFor = new List<string>();
        events.ProposalChanged += roomId => raisedFor.Add(roomId);

        var taken = store.TryTake("room-1", "proposal-1");

        Assert.Equal(proposal, taken);
        Assert.Equal("room-1", Assert.Single(raisedFor));
    }

    /// <summary>
    /// Builds a minimal, valid <see cref="Proposal"/> carrying one <see cref="Candidate"/>, so each
    /// test above only has to vary the Room id, proposer id and Proposal id it cares about.
    /// </summary>
    private static Proposal MakeProposal(string roomId, string proposerAgentId, string proposalId)
    {
        Candidate candidate = new("Vera", "vee", "Researcher", "You research things.", [], null);
        return new Proposal(proposalId, roomId, proposerAgentId, "Nova", [candidate], DateTimeOffset.UnixEpoch);
    }
}
