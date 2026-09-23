namespace Agency.Huddle.Tests.Teammates;

using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Pipes;

/// <summary>
/// Proves Spec §14 D-1's claim end to end: the ordinary Reply Gate - not a bespoke delivery of
/// <see cref="ProposalService"/>'s own - is what wakes the proposer once Approve posts its
/// outcome. Runs the full pipe stack (<see cref="PipeHostFixture"/>, the same seam
/// <c>PipeEndToEndTests</c> already uses to assert on <see cref="MessagePosted.Mentioned"/>) so
/// what this test reads back is exactly the <see cref="MessagePosted"/> Envelope a real Adapter
/// would receive, with the per-recipient <see cref="MessagePosted.Mentioned"/> flag
/// <see cref="AgentGateway.DeliverAsync"/> computes - the most realistic seam available, short of
/// a live model.
/// </summary>
public sealed class ProposalWakesProposerTests
{
    /// <summary>
    /// A four-Member Room (the Human, the proposer "Chief of Staff" - a Name with a space - and
    /// two other Agents): approving a Proposal posts the outcome Message, and the delivery
    /// <see cref="AgentGateway"/> actually sends the proposer carries it in
    /// <see cref="MessagePosted.Mentions"/> with <see cref="MessagePosted.Mentioned"/> set, which
    /// the real <see cref="ReplyGate.Decide"/> reads as <see cref="ReplyDecision.Reply"/>. The
    /// negative control proves the same delivery to an unmentioned Agent in the same Room reads
    /// as <see cref="ReplyDecision.CatchUp"/>, not Reply.
    /// </summary>
    [Fact]
    public async Task Approve_InGroupRoom_ProposerDeliveryIsMentioned()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var proposals = fixture.Services.GetRequiredService<ProposalStore>();
        var service = fixture.Services.GetRequiredService<ProposalService>();

        await using var proposer = await fixture.ConnectClientAsync(ct);
        await proposer.WriteAsync(new Hello("Chief of Staff", null), ct);
        var proposerWelcome = Assert.IsType<Welcome>(await proposer.ReadAsync(ct));

        await using var nova = await fixture.ConnectClientAsync(ct);
        await nova.WriteAsync(new Hello("Nova", null), ct);
        var novaWelcome = Assert.IsType<Welcome>(await nova.ReadAsync(ct));

        await using var sable = await fixture.ConnectClientAsync(ct);
        await sable.WriteAsync(new Hello("Sable", null), ct);
        var sableWelcome = Assert.IsType<Welcome>(await sable.ReadAsync(ct));

        var room = await chat.CreateRoomForAsync([proposerWelcome.AgentId, novaWelcome.AgentId, sableWelcome.AgentId], ct);

        // Built directly through ProposalStore, the same technique ProposalServiceTests already
        // uses: propose_teammates runs over the ACP MCP tool server, a channel this fixture (ACP
        // off) never starts, so there is no realistic way to reach this point through the wire
        // without spawning a real Adapter.
        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = new Proposal(
            Guid.CreateVersion7().ToString("N"), room.Id, proposerWelcome.AgentId, "Chief of Staff", candidates, TimeProvider.System.GetUtcNow());
        _ = proposals.TryPut(proposal);

        var outcome = await service.ApproveAsync(room.Id, proposal.Id, ct);
        Assert.Equal(ProposalOutcomeKind.Created, outcome.Kind);
        Assert.Equal("Approved. Created Vera. @Chief of Staff go ahead.", outcome.PostedText);

        var proposerDelivered = Assert.IsType<MessagePosted>(await proposer.ReadAsync(ct));
        Assert.True(proposerDelivered.Mentioned);
        Assert.Contains(proposerDelivered.Mentions, m => m.Id == proposerWelcome.AgentId);

        var proposerDecision = ReplyGate.Decide(
            proposerDelivered.Mentioned,
            proposerDelivered.Members.Count,
            proposerDelivered.AgentMessagesSinceHuman,
            proposerDelivered.Budget,
            following: false);
        Assert.Equal(ReplyDecision.Reply, proposerDecision);

        // Negative control: Nova is a Member of the same Room but the outcome text never names her,
        // so her own delivery must carry no Mention and the same real Reply Gate must not wake her.
        var novaDelivered = Assert.IsType<MessagePosted>(await nova.ReadAsync(ct));
        Assert.False(novaDelivered.Mentioned);
        Assert.DoesNotContain(novaDelivered.Mentions, m => m.Id == novaWelcome.AgentId);

        var novaDecision = ReplyGate.Decide(
            novaDelivered.Mentioned,
            novaDelivered.Members.Count,
            novaDelivered.AgentMessagesSinceHuman,
            novaDelivered.Budget,
            following: false);
        Assert.Equal(ReplyDecision.CatchUp, novaDecision);
    }
}
