namespace Agency.Huddle.Tests.Questions;

using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Pipes;

/// <summary>
/// Proves Questions spec §1 end to end: the ordinary Reply Gate - not a bespoke delivery of
/// <see cref="QuestionService"/>'s own - is what wakes the asker once its answer is posted, in a
/// Room of more than two. Runs the full pipe stack (<see cref="PipeHostFixture"/>), so what this
/// reads back is exactly the <see cref="MessagePosted"/> Envelope a real Adapter would receive.
/// </summary>
public sealed class QuestionWakesAskerTests
{
    /// <summary>
    /// A four-Member Room (the Human and three Agents): answering Coach's card posts the answer,
    /// and the delivery <see cref="AgentGateway"/> sends Coach carries it in
    /// <see cref="MessagePosted.Mentions"/> with <see cref="MessagePosted.Mentioned"/> set, which
    /// the real <see cref="ReplyGate.Decide"/> reads as <see cref="ReplyDecision.Reply"/>. The
    /// negative control: the same delivery to an unmentioned Agent in the same Room reads as
    /// <see cref="ReplyDecision.CatchUp"/>, so only the asker wakes.
    /// </summary>
    [Fact]
    public async Task Answer_InGroupRoom_ReplyGateWakesOnlyAsker()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        QuestionStore questions = fixture.Services.GetRequiredService<QuestionStore>();
        QuestionService service = fixture.Services.GetRequiredService<QuestionService>();

        await using var coach = await fixture.ConnectClientAsync(ct);
        await coach.WriteAsync(new Hello("Coach", null), ct);
        Welcome coachWelcome = Assert.IsType<Welcome>(await coach.ReadAsync(ct));

        await using var nova = await fixture.ConnectClientAsync(ct);
        await nova.WriteAsync(new Hello("Nova", null), ct);
        Welcome novaWelcome = Assert.IsType<Welcome>(await nova.ReadAsync(ct));

        await using var sable = await fixture.ConnectClientAsync(ct);
        await sable.WriteAsync(new Hello("Sable", null), ct);
        Welcome sableWelcome = Assert.IsType<Welcome>(await sable.ReadAsync(ct));

        Room room = await chat.CreateRoomForAsync([coachWelcome.AgentId, novaWelcome.AgentId, sableWelcome.AgentId], ct);

        // Built directly through QuestionStore, as ProposalWakesProposerTests does for Proposals:
        // ask_human runs over the ACP MCP tool server, a channel this fixture (ACP off) never starts.
        PendingQuestions card = new(
            Guid.CreateVersion7().ToString("N"),
            room.Id,
            coachWelcome.AgentId,
            "Coach",
            [new Question("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)],
            TimeProvider.System.GetUtcNow());
        _ = questions.TryPut(card);

        string? text = await service.AnswerAsync(room.Id, card.Id, [new QuestionAnswer([0])], ct);
        Assert.Equal("> What is your main goal?\n\nStrength\n\n@Coach", text);

        MessagePosted coachDelivered = Assert.IsType<MessagePosted>(await coach.ReadAsync(ct));
        Assert.True(coachDelivered.Mentioned);
        Assert.Contains(coachDelivered.Mentions, mention => mention.Id == coachWelcome.AgentId);
        Assert.Equal(
            ReplyDecision.Reply,
            ReplyGate.Decide(
                coachDelivered.Mentioned,
                coachDelivered.Members.Count,
                coachDelivered.AgentMessagesSinceHuman,
                coachDelivered.Budget,
                following: false));

        MessagePosted novaDelivered = Assert.IsType<MessagePosted>(await nova.ReadAsync(ct));
        Assert.False(novaDelivered.Mentioned);
        Assert.Equal(
            ReplyDecision.CatchUp,
            ReplyGate.Decide(
                novaDelivered.Mentioned,
                novaDelivered.Members.Count,
                novaDelivered.AgentMessagesSinceHuman,
                novaDelivered.Budget,
                following: false));
    }
}
