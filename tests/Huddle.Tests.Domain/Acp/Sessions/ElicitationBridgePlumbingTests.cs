using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins that the <see cref="IElicitationBridge"/> handed to a <see cref="PersonaRunner"/> is the one
/// every Room Session it builds through its <c>RoomSessionPool</c> answers questions with, so the real
/// bridge can be supplied by DI without touching either. Harness: a real runner over a hand-scripted
/// pipe server (<see cref="PipeRunnerServer"/>).
/// </summary>
public sealed class ElicitationBridgePlumbingTests
{
    private static readonly ElicitationRequest Request = new("session-1", null, "Which one?", """{"type":"object","properties":{}}""");

    /// <summary>A question asked of the scope the runner's session bound reaches the runner's bridge, with the Turn's Room and the runner's Agent id.</summary>
    [Fact]
    public async Task PersonaRunner_PassesItsBridgeToTheRoomSessionsItBuilds()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;

        await using PipeRunnerServer server = new("elicitation-bridge-plumbing-tests", recordReceived: false);
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        FakeElicitationBridge bridge = new();
        Persona persona = new("nova", "You are Nova.");
        RoomInfo room = new(
            "room-1",
            "Direct",
            [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo("agent-1", "nova", UserKind.Agent)]);
        IOptions<TeamOptions> options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        await using PersonaRunner runner = new(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, elicitationBridge: bridge);
        await server.HandshakeAsync(runner, [room], ct);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Session.EnqueueGatedReply(release.Task);
        try
        {
            await server.SendAsync(
                new MessagePosted(
                    room.Id,
                    room.Name,
                    new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", "hi"),
                    Mentioned: true,
                    Mentions: [],
                    Members: room.Members,
                    AgentMessagesSinceHuman: 0,
                    Budget: 0),
                ct);
            await Eventually.UntilAsync(() => factory.Session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

            IElicitationScope scope = Assert.Single(factory.Session.BoundScopes);
            Task<ElicitationResult> pending = scope.ElicitAsync(Request, ct);
            FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];
            call.Answer.SetResult(new ElicitationDeclined());

            Assert.Equal(new ElicitationContext("room-1", "agent-1"), call.Context);
            Assert.IsType<ElicitationDeclined>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
        }
        finally
        {
            release.TrySetResult();
        }
    }
}
