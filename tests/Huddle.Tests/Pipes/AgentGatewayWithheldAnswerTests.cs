using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// <see cref="AgentGateway.DeliverAsync"/> and <see cref="MessagePostedEvent.WithheldFromAgentId"/>
/// (elicitation bridge, decision E-4): the Human's answer to a form is delivered to every Agent in the
/// Room except the asker, whose Turn is still open and who gets the answer as the tool's own result.
/// A wake while that Turn is open would queue a second Turn, and the Reply Gate cannot prevent it in a
/// one-to-one Room because it replies there whether or not anyone was mentioned. Runs the full pipe
/// stack, so what each test reads back is exactly the <see cref="MessagePosted"/> Envelope a real
/// Adapter would receive. A delivery that must NOT happen is proved by order, never by waiting for
/// silence: the next Message the Human posts is the next thing the asker reads.
/// </summary>
public sealed class AgentGatewayWithheldAnswerTests
{
    private static readonly TimeSpan BoundedRead = TimeSpan.FromSeconds(5);

    /// <summary>
    /// In a one-to-one Room the asker's connection receives an ordinary Human post but not the
    /// answer: the Messages it reads are "before" and "after", in that order, with the answer between
    /// them in the Transcript and absent from its connection.
    /// </summary>
    [Fact]
    public async Task Answer_NotDeliveredToAsker_Direct()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        await using JsonLineStream coach = await fixture.ConnectClientAsync(ct);
        Welcome coachWelcome = await HelloAsync(coach, "Coach", ct);
        Room room = await chat.CreateRoomForAsync([coachWelcome.AgentId], ct);
        _ = await chat.PostAsync(room.Id, KnownIds.Human, "before", ct: ct);
        MessagePosted first = await ReadDeliveryAsync(coach, ct);
        Assert.Equal("before", first.Message.Text);

        ChatMessage? answer = await chat.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coachWelcome.AgentId, ct);
        _ = await chat.PostAsync(room.Id, KnownIds.Human, "after", ct: ct);

        Assert.NotNull(answer);
        MessagePosted next = await ReadDeliveryAsync(coach, ct);
        Assert.Equal("after", next.Message.Text);
        IReadOnlyList<ChatMessage> transcript = await fixture.Services.GetRequiredService<IChatStore>().ReadAllAsync(room.Id, ct);
        Assert.Equal(["before", "Strength", "after"], transcript.Select(message => message.Text).ToArray());
    }

    /// <summary>
    /// In a Room of more than two the asker is withheld and every other Agent is still delivered the
    /// answer, labelled not Mentioned: the answer names nobody, even when the Human's free text
    /// contains an <c>@</c>, so the Reply Gate reads it as catch-up for each of them.
    /// </summary>
    [Fact]
    public async Task Answer_Group_AskerWithheld_OthersStillDelivered_MentionedFalse()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        await using JsonLineStream coach = await fixture.ConnectClientAsync(ct);
        Welcome coachWelcome = await HelloAsync(coach, "Coach", ct);
        await using JsonLineStream nova = await fixture.ConnectClientAsync(ct);
        Welcome novaWelcome = await HelloAsync(nova, "Nova", ct);
        await using JsonLineStream sable = await fixture.ConnectClientAsync(ct);
        Welcome sableWelcome = await HelloAsync(sable, "Sable", ct);
        Room room = await chat.CreateRoomForAsync([coachWelcome.AgentId, novaWelcome.AgentId, sableWelcome.AgentId], ct);

        _ = await chat.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength, ask @Nova", coachWelcome.AgentId, ct);

        foreach (JsonLineStream other in new[] { nova, sable })
        {
            MessagePosted delivered = await ReadDeliveryAsync(other, ct);
            Assert.Equal("Strength, ask @Nova", delivered.Message.Text);
            Assert.Equal(KnownIds.Human, delivered.Message.SenderId);
            Assert.False(delivered.Mentioned);
            Assert.Empty(delivered.Mentions);
            Assert.Equal(4, delivered.Members.Count);
            Assert.Equal(
                ReplyDecision.CatchUp,
                ReplyGate.Decide(delivered.Mentioned, delivered.Members.Count, delivered.AgentMessagesSinceHuman, delivered.Budget, following: false));
        }

        _ = await chat.PostAsync(room.Id, KnownIds.Human, "after", ct: ct);
        MessagePosted askerNext = await ReadDeliveryAsync(coach, ct);
        Assert.Equal("after", askerNext.Message.Text);
    }

    /// <summary>
    /// The one door that redelivers without the withhold, <see cref="ChatService.ExtendBudgetAsync"/>, is
    /// shut straight after an answer: the Room the agents had exhausted is unpaused by the answer, so
    /// the extension grants nothing and the asker's connection reads the next ordinary Message, not
    /// the answer a second time.
    /// </summary>
    [Fact]
    public async Task Answer_ExtendBudget_DoesNotRedeliverToAsker()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" },
            ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        await using JsonLineStream coach = await fixture.ConnectClientAsync(ct);
        Welcome coachWelcome = await HelloAsync(coach, "Coach", ct);
        Room room = await chat.CreateRoomForAsync([coachWelcome.AgentId], ct);
        _ = await chat.PostAsync(room.Id, coachWelcome.AgentId, "framing", ct: ct);
        Assert.True(chat.GetBudget(room.Id).Exhausted);
        _ = await chat.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coachWelcome.AgentId, ct);

        BudgetExtension extension = await chat.ExtendBudgetAsync(room.Id, ct);
        _ = await chat.PostAsync(room.Id, KnownIds.Human, "marker", ct: ct);

        Assert.Equal(ExtendResult.NotPaused, extension.Result);
        MessagePosted next = await ReadDeliveryAsync(coach, ct);
        Assert.Equal("marker", next.Message.Text);
    }

    /// <summary>Connects the Agent <paramref name="name"/> and returns the Welcome it is registered with.</summary>
    /// <param name="client">The client end of an Agent connection.</param>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="ct">Cancels the handshake.</param>
    private static async Task<Welcome> HelloAsync(JsonLineStream client, string name, CancellationToken ct)
    {
        await client.WriteAsync(new Hello(name, null), ct);
        return Assert.IsType<Welcome>(await client.ReadAsync(ct));
    }

    /// <summary>
    /// Reads the next Envelope from <paramref name="client"/> and requires it to be a delivery. The
    /// read is bounded, so a delivery that never comes fails the test instead of hanging it.
    /// </summary>
    /// <param name="client">The client end of an Agent connection.</param>
    /// <param name="ct">Cancels the read.</param>
    private static async Task<MessagePosted> ReadDeliveryAsync(JsonLineStream client, CancellationToken ct)
    {
        try
        {
            ProtocolMessage? received = await client.ReadAsync(ct).WaitAsync(BoundedRead, ct);
            return Assert.IsType<MessagePosted>(received);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"No delivery arrived within {BoundedRead.TotalSeconds:0} seconds.");
            throw;
        }
    }
}
