using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins the production wiring of the Elicitation bridge (decision E-5): the app container hands out one
/// <see cref="ElicitationStore"/> that the bridge, the service and <see cref="ChatService"/> all share,
/// and <see cref="PersonaSupervisor"/> passes the bridge to the runner it starts, so a question a real
/// Room Session is asked reaches the Human instead of being answered as cancelled.
/// </summary>
public sealed class ElicitationWiringTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// In the app container, a request made through <see cref="IElicitationBridge"/> becomes a card in the
    /// one store, the container's <see cref="ElicitationService"/> answers it, and a Message typed through
    /// the container's <see cref="ChatService"/> cancels another: the three are wired to the same store.
    /// </summary>
    [Fact]
    public async Task Container_WiresBridgeServiceAndChatServiceToOneStore()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(ct);
        IServiceProvider services = fixture.Services;
        ITeamDirectory directory = services.GetRequiredService<ITeamDirectory>();
        User? coach = await directory.UpsertAgentUserAsync("Coach", null, ct);
        Assert.NotNull(coach);
        Room room = await directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        IElicitationBridge bridge = services.GetRequiredService<IElicitationBridge>();
        ElicitationStore store = services.GetRequiredService<ElicitationStore>();
        RoomEvents events = services.GetRequiredService<RoomEvents>();
        ElicitationContext context = new(room.Id, coach.Id);

        Assert.IsType<RoomElicitationBridge>(bridge);
        Task<ElicitationResult> answeredRequest = bridge.RequestAsync(context, Request(SingleQuestionSchema, SingleQuestionMessage), ct);
        PendingElicitation first = await NextCardAsync(store, events, room.Id, 1, ct);
        _ = await services.GetRequiredService<ElicitationService>().AnswerAsync(room.Id, first.Id, Values(Pair("question_0", "Postgres")), ct);
        Assert.IsType<ElicitationAccepted>(await answeredRequest.WaitAsync(BoundedWait, ct));

        Task<ElicitationResult> typedOverRequest = bridge.RequestAsync(context, Request(SingleQuestionSchema, SingleQuestionMessage), ct);
        _ = await NextCardAsync(store, events, room.Id, 1, ct);
        _ = await services.GetRequiredService<ChatService>().PostAsync(room.Id, KnownIds.Human, "never mind", ct: ct);
        Assert.IsType<ElicitationCancelled>(await typedOverRequest.WaitAsync(BoundedWait, ct));
    }

    /// <summary>
    /// <see cref="PersonaSupervisor"/> passes its own trailing <see cref="IElicitationBridge"/> to the
    /// runner it starts, which reaches the Room Session it opens: a question asked of the scope that session
    /// bound, mid-Turn, becomes a card in the bridge's store rather than being answered as cancelled.
    /// </summary>
    [Fact]
    public async Task Supervisor_PassesTheElicitationBridge()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        IOptions<TeamOptions> options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        PersonaStore personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Session.EnqueueGatedReply(release.Task, "ok");
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        ElicitationStore store = new(events);
        RoomElicitationBridge bridge = new(store, directory, NullLogger<RoomElicitationBridge>.Instance);
        AdapterProfileResolver resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using SkillStore skillStore = new(options, NullLogger<SkillStore>.Instance);
        using PersonaSupervisor supervisor = new(
            options,
            personaStore,
            factory,
            resolver,
            new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance),
            new FakePromptSource(),
            new RoomFollows(),
            NullLoggerFactory.Instance,
            NullLogger<PersonaSupervisor>.Instance,
            skillStore,
            elicitationBridge: bridge);
        try
        {
            personaStore.Add(new PersonaIdentity("nova", "nova", "nova", []), "You are Nova.");
            await supervisor.StartAsync(ct);
            (string agentId, string roomId) = await WaitForDirectRoomAsync(fixture, directory, "nova", ct);
            await fixture.Services.GetRequiredService<ChatService>().PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
            await WaitUntilAsync(() => factory.Session.Prompts.Count == 1 && factory.Session.BoundScopes.Count == 1, ct);
            IElicitationScope scope = Assert.Single(factory.Session.BoundScopes);

            Task<ElicitationResult> pending = scope.ElicitAsync(Request(SingleQuestionSchema, SingleQuestionMessage), ct);
            PendingElicitation card = await NextCardAsync(store, events, roomId, 1, ct);

            Assert.Equal(agentId, card.AskerAgentId);
            Assert.Equal("nova", card.AskerName);
            _ = store.Drop(roomId);
            Assert.IsType<ElicitationCancelled>(await pending.WaitAsync(BoundedWait, ct));
        }
        finally
        {
            release.TrySetResult();
            await supervisor.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Waits until <paramref name="roomId"/> holds at least <paramref name="count"/> cards, then returns the newest, bounded so a regression fails rather than hangs.</summary>
    /// <param name="store">The store to watch.</param>
    /// <param name="events">The hub the store raises its change on.</param>
    /// <param name="roomId">The Room to watch.</param>
    /// <param name="count">How many cards to wait for.</param>
    /// <param name="ct">Cancels the wait.</param>
    private static async Task<PendingElicitation> NextCardAsync(ElicitationStore store, RoomEvents events, string roomId, int count, CancellationToken ct)
    {
        TaskCompletionSource appeared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<string> check = room =>
        {
            if (string.Equals(room, roomId, StringComparison.Ordinal) && store.Get(roomId).Count >= count)
            {
                _ = appeared.TrySetResult();
            }
        };
        events.ElicitationsChanged += check;
        try
        {
            check(roomId);
            await appeared.Task.WaitAsync(BoundedWait, ct);
        }
        finally
        {
            events.ElicitationsChanged -= check;
        }

        return store.Get(roomId)[^1];
    }

    /// <summary>Waits until <paramref name="agentName"/> is registered and online, then returns its id and its direct Room with the Human.</summary>
    /// <param name="fixture">The running app.</param>
    /// <param name="directory">The Team Directory to look in.</param>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="ct">Bounds the wait.</param>
    private static async Task<(string AgentId, string RoomId)> WaitForDirectRoomAsync(PipeHostFixture fixture, ITeamDirectory directory, string agentName, CancellationToken ct)
    {
        Agency.Huddle.App.Pipes.IAgentGateway gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        while (true)
        {
            User? user = await directory.FindUserByNameAsync(agentName, ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                Room? room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
                if (room is not null)
                {
                    return (user.Id, room.Id);
                }
            }

            await Task.Delay(50, ct);
        }
    }

    /// <summary>Polls <paramref name="condition"/> until it is true, or the test's <paramref name="ct"/> fires.</summary>
    /// <param name="condition">The condition to poll.</param>
    /// <param name="ct">Bounds the poll.</param>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            await Task.Delay(20, ct);
        }
    }
}
