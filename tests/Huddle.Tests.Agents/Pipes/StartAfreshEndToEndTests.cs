using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// Long-Running Rooms 5.0.t, across the real pipe: the Human's <i>Start afresh</i> forgets one Room's stored session,
/// the next Message there opens a NEW session whose first prompt carries the transcript header, the server receives
/// the <see cref="FreshStartNote"/>, and the same Agent's other Room still resumes its old session (Spec LR-R15, LR-R19, LR-R21).
/// </summary>
public sealed class StartAfreshEndToEndTests
{
    private const string HeaderText = "This is a new session for this Room. Its recent Messages, oldest first, including your own:";

    /// <summary>
    /// One Agent in two Rooms, each with a stored session after a Turn and the runner restarted so neither is live.
    /// <c>StartAfreshAsync</c> for Room A forgets A's entry only; A's next Message opens a new session with the
    /// transcript header, the server holds one "asked" note for A, and B's next Message resumes B's old session.
    /// </summary>
    [Fact]
    public async Task StartAfresh_ForgetsOneRoomsSession_NextMessageIsFresh_OtherRoomStillResumes()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;

        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        RoomSessionStore roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        IOptions<TeamOptions> options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        ILogger<PersonaRunner> logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();
        IChatStore chatStore = fixture.Services.GetRequiredService<IChatStore>();
        ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        Persona persona = TestPersonas.Nova;

        // First run: one Turn in each of two Rooms stores a session per Room. Both Rooms are made after the
        // runner starts, so neither is the Agent's own Human Room that a per-Room runner opens eagerly.
        string agentId;
        string roomA;
        string roomB;
        await using (PersonaRunner firstRunner = new(
            persona, options, new FakeAgentHostFactory { SessionPerRoom = true }, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions))
        {
            await firstRunner.StartAsync(ct);
            (agentId, _) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
            roomA = (await directory.CreateRoomAsync("alpha", [KnownIds.Human, agentId], ct)).Id;
            roomB = (await directory.CreateRoomAsync("beta", [KnownIds.Human, agentId], ct)).Id;

            await chat.PostAsync(roomA, KnownIds.Human, "@nova first in A", ct: ct);
            await chat.PostAsync(roomB, KnownIds.Human, "@nova first in B", ct: ct);
            await Eventually.UntilAsync(
                () => roomSessions.Get("nova", roomA) is not null && roomSessions.Get("nova", roomB) is not null,
                ct,
                timeout: TestBudgets.Standard);
        }

        string oldSessionA = roomSessions.Get("nova", roomA)!.SessionId;
        string oldSessionB = roomSessions.Get("nova", roomB)!.SessionId;
        int messagesInA = (await chatStore.ReadAllAsync(roomA, ct)).Count;

        // Second run: a resume-capable host, so a stored entry is resumed and an absent one opens a new session.
        ConcurrentQueue<FakeAgentSession> resumed = new();
        FakeAgentHostFactory secondFactory = new() { SessionPerRoom = true };
        secondFactory.OnHostStarted = host =>
        {
            host.CanResume = true;
            host.ResumeHandler = id =>
            {
                FakeAgentSession session = new();
                resumed.Enqueue(session);
                return session;
            };
        };

        await using PersonaRunner runner = new(
            persona, options, secondFactory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await runner.StartAsync(ct);
        IAgentGateway gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        await gateway.StartAfreshAsync(agentId, roomA, ct);

        // (1) Room A's stored entry is gone; Room B's remains, untouched.
        await Eventually.UntilAsync(() => roomSessions.Get("nova", roomA) is null, ct, timeout: TestBudgets.Standard);
        Assert.Equal(oldSessionB, roomSessions.Get("nova", roomB)?.SessionId);

        // (2) The next Message in Room A opens a NEW session, not a resumed one, and its first prompt opens with the header.
        await chat.PostAsync(roomA, KnownIds.Human, "@nova second in A", ct: ct);
        await Eventually.UntilAsync(
            () => secondFactory.Host!.Sessions.Any(static session => session.Prompts.Any(static prompt => prompt.Contains("second in A", StringComparison.Ordinal))),
            ct,
            timeout: TestBudgets.Standard);
        string freshPrompt = secondFactory.Host!.Sessions
            .SelectMany(static session => session.Prompts)
            .First(static prompt => prompt.Contains("second in A", StringComparison.Ordinal));
        Assert.DoesNotContain(oldSessionA, secondFactory.Host.ResumeCalls);
        int headerAt = freshPrompt.IndexOf(HeaderText, StringComparison.Ordinal);
        Assert.True(headerAt >= 0, "the first prompt of the new session carries the transcript header: " + freshPrompt);
        Assert.True(headerAt < freshPrompt.IndexOf("second in A", StringComparison.Ordinal), "the header introduces the trigger: " + freshPrompt);

        // (3) The server holds one note for Room A: reason "asked", counts matching what was reread.
        await Eventually.UntilAsync(
            () => fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>().For(roomA).Count == 1,
            ct,
            timeout: TestBudgets.Standard);
        var notes = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>().For(roomA);
        var entry = Assert.Single(notes);
        Assert.Equal("nova", entry.AgentName);
        Assert.Equal(roomA, entry.Note.RoomId);
        Assert.Equal("asked", entry.Note.Reason);
        Assert.Equal(messagesInA, entry.Note.MessageCount);
        Assert.True(entry.Note.EstimatedTokens > 0, "what was reread has a token estimate");
        Assert.False(entry.Note.ReadFailed);
        Assert.DoesNotContain(
            fixture.LogEntries,
            static log => log.Message.Contains("had a message refused", StringComparison.Ordinal)
                && log.Message.Contains(ErrorCodes.BadMessage, StringComparison.Ordinal));

        // (4) A Message in Room B resumes its old session; no new session opens for it.
        await chat.PostAsync(roomB, KnownIds.Human, "@nova second in B", ct: ct);
        await Eventually.UntilAsync(
            () => resumed.Any(static session => session.Prompts.Any(static prompt => prompt.Contains("second in B", StringComparison.Ordinal))),
            ct,
            timeout: TestBudgets.Standard);
        Assert.Contains(oldSessionB, secondFactory.Host.ResumeCalls);
        Assert.DoesNotContain(
            secondFactory.Host.Sessions,
            static session => session.Prompts.Any(static prompt => prompt.Contains("second in B", StringComparison.Ordinal)));
    }
}
