using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// Covers <see cref="Agency.Huddle.App.Pipes.AgentConnection"/>'s handling of
/// <see cref="ReadTranscript"/> (RS §6.5) against a real <see cref="ChatService"/> over
/// <see cref="PipeHostFixture"/>: slicing, membership, bad input, the P-21 line-length trim, and
/// that the answer goes only to the client that asked.
/// </summary>
public sealed class AgentConnectionReadTranscriptTests
{
    /// <summary>After <see langword="null"/>, before m5: reads m1 through m4, oldest first.</summary>
    [Fact]
    public async Task Read_AfterNullBeforeM5_ReturnsM1ToM4()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        TranscriptTail tail = await scenario.ReadAsync("r1", null, "m5", 20, ct);

        Assert.Equal(["m1", "m2", "m3", "m4"], tail.Messages.Select(m => m.Id));
        Assert.Equal(0, tail.Omitted);
    }

    /// <summary>After m2, before m5: reads only m3 and m4.</summary>
    [Fact]
    public async Task Read_AfterM2BeforeM5_ReturnsM3M4()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        TranscriptTail tail = await scenario.ReadAsync("r1", "m2", "m5", 20, ct);

        Assert.Equal(["m3", "m4"], tail.Messages.Select(m => m.Id));
        Assert.Equal(0, tail.Omitted);
    }

    /// <summary>After <see langword="null"/>, before m5, Max 2: the latest two of the four (m3, m4), with the earlier two counted as omitted.</summary>
    [Fact]
    public async Task Read_Max2_ReturnsLatestTwoAndOmitted()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        TranscriptTail tail = await scenario.ReadAsync("r1", null, "m5", 2, ct);

        Assert.Equal(["m3", "m4"], tail.Messages.Select(m => m.Id));
        Assert.Equal(2, tail.Omitted);
    }

    /// <summary>A <see cref="ReadTranscript.BeforeMessageId"/> not present in the Transcript reads to the end.</summary>
    [Fact]
    public async Task Read_BeforeIdNotInTranscript_ReadsToTheEnd()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        TranscriptTail tail = await scenario.ReadAsync("r1", null, "not-a-real-id", 20, ct);

        Assert.Equal(["m1", "m2", "m3", "m4", "m5", "m6"], tail.Messages.Select(m => m.Id));
        Assert.Equal(0, tail.Omitted);
    }

    /// <summary>A <see cref="ReadTranscript.AfterMessageId"/> not present in the Transcript is treated as <see langword="null"/> (plan-settled: the spec is silent; the latest Messages are the safe reading).</summary>
    [Fact]
    public async Task Read_AfterIdNotInTranscript_TreatedAsNull()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        TranscriptTail tail = await scenario.ReadAsync("r1", "not-a-real-id", "m5", 20, ct);

        Assert.Equal(["m1", "m2", "m3", "m4"], tail.Messages.Select(m => m.Id));
    }

    /// <summary>A Room the asker is not a Member of answers <c>notMember</c>, carrying the <see cref="ReadTranscript.RequestId"/> as <see cref="ProtocolError.RelatedMessageId"/>.</summary>
    [Fact]
    public async Task Read_NotMember_ProtocolErrorNotMemberWithRelatedIdRequestId()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);
        await using JsonLineStream outsider = await scenario.Fixture.ConnectClientAsync(ct);
        await outsider.WriteAsync(new Hello("Outsider", null), ct);
        _ = Assert.IsType<Welcome>(await outsider.ReadAsync(ct));

        await outsider.WriteAsync(new ReadTranscript("r1", scenario.RoomId, null, "m5", 20), ct);
        ProtocolError error = Assert.IsType<ProtocolError>(await outsider.ReadAsync(ct));

        Assert.Equal(ErrorCodes.NotMember, error.Code);
        Assert.Equal("r1", error.RelatedMessageId);
    }

    /// <summary>An unknown Room id answers <c>unknownRoom</c>.</summary>
    [Fact]
    public async Task Read_UnknownRoom_ProtocolErrorUnknownRoom()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        await scenario.Asker.WriteAsync(new ReadTranscript("r1", "no-such-room", null, "m5", 20), ct);
        ProtocolError error = Assert.IsType<ProtocolError>(await scenario.Asker.ReadAsync(ct));

        Assert.Equal(ErrorCodes.UnknownRoom, error.Code);
    }

    /// <summary>A <see cref="ReadTranscript.Max"/> below one is <c>badMessage</c>.</summary>
    [Fact]
    public async Task Read_MaxBelowOne_BadMessage()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        await scenario.Asker.WriteAsync(new ReadTranscript("r1", scenario.RoomId, null, "m5", 0), ct);
        ProtocolError error = Assert.IsType<ProtocolError>(await scenario.Asker.ReadAsync(ct));

        Assert.Equal(ErrorCodes.BadMessage, error.Code);
    }

    /// <summary>
    /// Five 300,000-character Messages: the answer arrives, parses, and holds only the latest that
    /// fit under half of <see cref="JsonLineStream.MaxLineBytes"/>, counting the rest in
    /// <see cref="TranscriptTail.Omitted"/> (finding P-21).
    /// </summary>
    [Fact]
    public async Task Read_LongMessages_TrimmedOldestFirstToFitTheLineLimit()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        ChatService chat;
        JsonLineStream asker;
        string roomId;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        chat = fixture.Services.GetRequiredService<ChatService>();
        await using (asker = await fixture.ConnectClientAsync(ct))
        {
            await asker.WriteAsync(new Hello("Nova", null), ct);
            Welcome welcome = Assert.IsType<Welcome>(await asker.ReadAsync(ct));
            roomId = Assert.Single(welcome.Rooms).Id;

            string longText = new string('a', 300_000);
            for (int i = 1; i <= 5; i++)
            {
                await chat.PostAsync(roomId, KnownIds.Human, longText + i.ToString(System.Globalization.CultureInfo.InvariantCulture), "l" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
                _ = await asker.ReadAsync(ct);
            }

            await asker.WriteAsync(new ReadTranscript("r1", roomId, null, "not-the-trigger", 20), ct);
            TranscriptTail tail = Assert.IsType<TranscriptTail>(await asker.ReadAsync(ct));

            Assert.Single(tail.Messages);
            Assert.Equal("l5", tail.Messages[0].Id);
            Assert.Equal(4, tail.Omitted);
        }
    }

    /// <summary>A second Member of the same Room receives no <see cref="TranscriptTail"/>: the server answers only the client that asked.</summary>
    [Fact]
    public async Task Read_AnswerGoesOnlyToTheAsker()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();

        await using JsonLineStream asker = await fixture.ConnectClientAsync(ct);
        await asker.WriteAsync(new Hello("Nova", null), ct);
        Welcome askerWelcome = Assert.IsType<Welcome>(await asker.ReadAsync(ct));
        string roomId = Assert.Single(askerWelcome.Rooms).Id;

        await using JsonLineStream other = await fixture.ConnectClientAsync(ct);
        await other.WriteAsync(new Hello("Rio", null), ct);
        _ = Assert.IsType<Welcome>(await other.ReadAsync(ct));

        await chat.SubmitFromComposerAsync(roomId, KnownIds.Human, "/invite @Rio", ct);

        await chat.PostAsync(roomId, KnownIds.Human, "hello both", ct: ct);
        _ = await asker.ReadAsync(ct); // drains Nova's push
        _ = await other.ReadAsync(ct); // drains Rio's push

        await asker.WriteAsync(new ReadTranscript("r1", roomId, null, "not-the-trigger", 20), ct);
        TranscriptTail tail = Assert.IsType<TranscriptTail>(await asker.ReadAsync(ct));
        Assert.Equal("r1", tail.RequestId);

        using CancellationTokenSource noAnswerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        noAnswerCts.CancelAfter(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => other.ReadAsync(noAnswerCts.Token));
    }

    /// <summary>The asker's own Messages, posted under its own Name, are included in its Transcript range.</summary>
    [Fact]
    public async Task Read_IncludesTheAskersOwnMessagesUnderItsName()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(ct);

        TranscriptTail tail = await scenario.ReadAsync("r1", null, "not-the-trigger", 20, ct);

        ChatMessage own = Assert.Single(tail.Messages, m => m.Id == "m5");
        Assert.Equal("Nova", own.SenderName);
        Assert.Equal(scenario.AskerId, own.SenderId);
    }

    /// <summary>
    /// A connected "Nova" client, in its own two-Member Room with the Human, with six Messages
    /// already posted: m1-m4 and m6 from the Human, m5 from Nova itself (the asker's own Message).
    /// </summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private Scenario(PipeHostFixture fixture, JsonLineStream asker, string roomId, string askerId)
        {
            this.Fixture = fixture;
            this.Asker = asker;
            this.RoomId = roomId;
            this.AskerId = askerId;
        }

        internal PipeHostFixture Fixture { get; }

        internal JsonLineStream Asker { get; }

        internal string RoomId { get; }

        internal string AskerId { get; }

        internal static async Task<Scenario> StartAsync(CancellationToken ct)
        {
            PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
            ChatService chat = fixture.Services.GetRequiredService<ChatService>();

            JsonLineStream asker = await fixture.ConnectClientAsync(ct);
            await asker.WriteAsync(new Hello("Nova", null), ct);
            Welcome welcome = Assert.IsType<Welcome>(await asker.ReadAsync(ct));
            string roomId = Assert.Single(welcome.Rooms).Id;

            await Scenario.PostAndDrainAsync(chat, asker, roomId, KnownIds.Human, "m1", ct);
            await Scenario.PostAndDrainAsync(chat, asker, roomId, KnownIds.Human, "m2", ct);
            await Scenario.PostAndDrainAsync(chat, asker, roomId, KnownIds.Human, "m3", ct);
            await Scenario.PostAndDrainAsync(chat, asker, roomId, KnownIds.Human, "m4", ct);

            // m5 is the asker's OWN post: AgentGateway.DeliverAsync never delivers a Message back to
            // its own sender, so unlike m1-m4 and m6 there is no push here to drain.
            await chat.PostAsync(roomId, welcome.AgentId, "m5 text", "m5", ct);

            await Scenario.PostAndDrainAsync(chat, asker, roomId, KnownIds.Human, "m6", ct);

            return new Scenario(fixture, asker, roomId, welcome.AgentId);
        }

        /// <summary>Sends one <see cref="ReadTranscript"/> from <see cref="Asker"/> and returns the matching <see cref="TranscriptTail"/>.</summary>
        internal async Task<TranscriptTail> ReadAsync(string requestId, string? afterMessageId, string beforeMessageId, int max, CancellationToken ct)
        {
            await this.Asker.WriteAsync(new ReadTranscript(requestId, this.RoomId, afterMessageId, beforeMessageId, max), ct);
            return Assert.IsType<TranscriptTail>(await this.Asker.ReadAsync(ct));
        }

        /// <summary>Posts one Message from the Human and drains the push it delivers back to the asker's connection.</summary>
        private static async Task PostAndDrainAsync(ChatService chat, JsonLineStream asker, string roomId, string senderId, string messageId, CancellationToken ct)
        {
            await chat.PostAsync(roomId, senderId, messageId + " text", messageId, ct);
            _ = await asker.ReadAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Asker.DisposeAsync();
            await this.Fixture.DisposeAsync();
        }
    }
}
