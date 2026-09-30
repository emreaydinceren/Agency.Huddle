using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.IO.Pipes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises <see cref="PersonaRunner"/> end to end over a real pipe (via <see cref="PipeHostFixture"/>)
/// against a <see cref="FakeAgentHostFactory"/>, so no test spends a token or launches <c>node</c>.
/// </summary>
public sealed class PersonaRunnerTests
{
    /// <summary>
    /// The seven real chat tools' names, each carrying its full <c>mcp__team__</c> prefix, in the same
    /// order <see cref="DotAcpAgentHostFactory"/> builds them in.
    /// </summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
        "mcp__team__follow_room",
        "mcp__team__unfollow_room",
    ];

    [Fact]
    public async Task Start_RegistersAgentAndCreatesDirectRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (userId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        Assert.Single(factory.Calls);
        Assert.Equal(userId, factory.Calls[0].AgentId);
        Assert.NotNull(roomId);
    }

    /// <summary>
    /// Regression test for the description being built from the raw file's first line, which is
    /// always "---" once a Persona has frontmatter: BuildDescription must skip the frontmatter
    /// block and use the body's first line instead.
    /// </summary>
    [Fact]
    public async Task Start_PersonaWithFrontmatter_RegistersDescriptionFromTheBodyNotTheDelimiter()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "---\nrole: 'Router'\n---\nYou are Nova, the router.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        await WaitForDirectRoomAsync(fixture, "nova", ct);
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var user = await directory.FindUserByNameAsync("nova", ct);

        Assert.NotNull(user);
        Assert.Equal("You are Nova, the router.", user.Description);
    }

    /// <summary>
    /// The built-in Chief of Staff's empty two-Member Room with the Human gets exactly one Greeting
    /// Turn after the session starts (Spec §6.14): the Turn's prompt is recorded on the fake session,
    /// and it opens with the Room's label - the only way an Agent ever learns a Room's id.
    /// </summary>
    [Fact]
    public async Task Start_BuiltinWithEmptyHumanRoom_RunsOneGreetingTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        await using var runner = CreateHost(fixture, persona, factory);
        await runner.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);
        var prompts = await WaitForPromptCountAsync(factory.Session, 1, ct);

        var prompt = Assert.Single(prompts);
        Assert.StartsWith($"[Room: Chief of Staff (id: {roomId})]", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plain, non-built-in Persona never gets a Greeting, even though its own two-Member Room with
    /// the Human is freshly created and empty (Spec §12 F-31): the built-in check in
    /// <see cref="PersonaRunner.StartAsync"/> gates on <c>_builtin: chief-of-staff</c> in the
    /// Persona's own text, which this Persona's text never carries.
    /// </summary>
    /// <remarks>
    /// There is nothing to wait FOR here - the point under test is that nothing happens - so this
    /// proves the negative the same way <see cref="ABudgetExhaustedRefusal_ReportsNothing"/> does
    /// elsewhere in this file: bound the wait to <see cref="PersonaRunner.StartAsync"/> completing
    /// (which itself deterministically waits through the Welcome handshake and session creation, via
    /// <see cref="WaitForDirectRoomAsync"/> confirming registration) plus a short, fixed grace period
    /// for a wrongly-queued item to reach the fake session - a bounded, explained sleep rather than an
    /// unbounded assertion that could only ever time out instead of failing cleanly.
    /// </remarks>
    [Fact]
    public async Task Start_NotBuiltin_NoGreeting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");

        await using var runner = CreateHost(fixture, persona, factory);
        await runner.StartAsync(ct);
        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        // A Greeting would be queued by StartAsync itself, ahead of anything that arrives later, so
        // the probe Message being the first Turn is the whole proof: no sleep needed.
        var chat = fixture.Services.GetRequiredService<ChatService>();
        await chat.PostAsync(roomId, KnownIds.Human, "probe", ct: ct);
        var prompts = await WaitForPromptCountAsync(factory.Session, 1, ct);

        Assert.Contains("probe", Assert.Single(prompts), StringComparison.Ordinal);
    }

    /// <summary>
    /// The built-in Chief of Staff does not greet a second time once its Room with the Human has
    /// taken a Message: <see cref="PersonaRunner.StartAsync"/>'s Greeting check reads
    /// <see cref="RoomInfo.IsEmpty"/> straight off the <see cref="Welcome"/> for this particular
    /// start, and a Room the Human has already posted into reports it <see langword="false"/> (Task
    /// 16.2).
    /// </summary>
    /// <remarks>
    /// The "later real Message" this test waits on is the Human's own post:
    /// <see cref="WaitForHistoryCountAsync"/> proves it reached the Transcript - and therefore that
    /// the Room's file is non-empty for <see cref="IChatStore.HasMessagesAsync"/> to see - before the
    /// second runner ever says <c>hello</c>. Only the absence that follows (no Greeting on that
    /// second start) needs the same bounded grace period <see cref="Start_NotBuiltin_NoGreeting"/>
    /// uses, since there is no later event to wait for proving something did not happen.
    /// </remarks>
    [Fact]
    public async Task Start_HumanRoomNotEmpty_NoGreeting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        var firstFactory = new FakeAgentHostFactory();
        await using var firstRunner = CreateHost(fixture, persona, firstFactory);
        await firstRunner.StartAsync(ct);
        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 1, ct);
        await firstRunner.StopAsync();

        var secondFactory = new FakeAgentHostFactory();
        await using var secondRunner = CreateHost(fixture, persona, secondFactory);
        await secondRunner.StartAsync(ct);
        await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);

        // A Greeting would be queued by StartAsync itself, ahead of anything that arrives later, so
        // the probe Message being the first Turn is the whole proof: no sleep needed.
        await chat.PostAsync(roomId, KnownIds.Human, "probe", ct: ct);
        var prompts = await WaitForPromptCountAsync(secondFactory.Session, 1, ct);

        Assert.Contains("probe", Assert.Single(prompts), StringComparison.Ordinal);
    }

    /// <summary>
    /// An empty Room the built-in Chief of Staff belongs to does not queue a Greeting when it has
    /// three Members instead of two: the Spec §6.14 check is specific to a Room with exactly two
    /// Members, one of them the Human, and <see cref="RoomInfo.IsEmpty"/> - a three-Member Room fails
    /// that shape test regardless of whether it has taken any Messages.
    /// </summary>
    /// <remarks>
    /// The Room with the Human is made non-empty first, exactly as
    /// <see cref="Start_HumanRoomNotEmpty_NoGreeting"/> does, so this test isolates one variable: an
    /// empty Room of the wrong shape must not itself queue a Greeting even while it sits alongside
    /// that non-empty Room in the same Welcome. The same bounded grace period proves the negative,
    /// for the same reason given there.
    /// </remarks>
    [Fact]
    public async Task Start_EmptyGroupRoomOnly_NoGreeting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        var firstFactory = new FakeAgentHostFactory();
        await using var firstRunner = CreateHost(fixture, persona, firstFactory);
        await firstRunner.StartAsync(ct);
        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 1, ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));
        await chat.CreateRoomForAsync([agentId, friendWelcome.AgentId], ct);

        await firstRunner.StopAsync();

        var secondFactory = new FakeAgentHostFactory();
        await using var secondRunner = CreateHost(fixture, persona, secondFactory);
        await secondRunner.StartAsync(ct);
        await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);

        // A Greeting would be queued by StartAsync itself, ahead of anything that arrives later, so
        // the probe Message being the first Turn is the whole proof: no sleep needed.
        await chat.PostAsync(roomId, KnownIds.Human, "probe", ct: ct);
        var prompts = await WaitForPromptCountAsync(secondFactory.Session, 1, ct);

        Assert.Contains("probe", Assert.Single(prompts), StringComparison.Ordinal);
    }

    /// <summary>
    /// When the session factory itself fails, <see cref="PersonaRunner.StartAsync"/> propagates the
    /// exception before it ever reaches the Greeting check: Spec §6.14 requires the Greeting to be
    /// queued only after <c>factory.StartAsync</c> succeeds, "so a failed start never leaves a
    /// queued Turn with no session". <see cref="FakeAgentSession.Prompts"/> on a fresh, never-returned
    /// session proves nothing reached it.
    /// </summary>
    [Fact]
    public async Task Start_FactoryThrows_NoGreetingQueued()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.FailNextCreateWith(new InvalidOperationException("Session creation failed."));
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        await using var runner = CreateHost(fixture, persona, factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync(ct));

        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        Assert.Empty(factory.Session.Prompts);
    }

    /// <summary>
    /// The Greeting Turn's reply is posted as an ordinary Message from the Chief of Staff's own
    /// Agent, into its Room with the Human - and nothing else lands there. There is no fake
    /// Human-authored Message triggering it (Spec §6.14).
    /// </summary>
    [Fact]
    public async Task Greeting_FakeSessionReplies_PostedAsChiefOfStaffInHumanRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("Welcome aboard! What would you like to get done first?");
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        await using var runner = CreateHost(fixture, persona, factory);
        await runner.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var history = await WaitForHistoryCountAsync(store, roomId, 1, ct);

        var greeting = Assert.Single(history);
        Assert.Equal(agentId, greeting.SenderId);
        Assert.Equal("Chief of Staff", greeting.SenderName);
        Assert.Equal("Welcome aboard! What would you like to get done first?", greeting.Text);
        Assert.DoesNotContain(history, m => m.SenderId == KnownIds.Human);
    }

    /// <summary>
    /// A Greeting Turn that fails posts nothing, and the Room it targeted stays empty exactly as
    /// <see cref="IChatStore.HasMessagesAsync"/> reports it server-side - the same signal
    /// <see cref="Agency.Huddle.App.Pipes.AgentConnection"/> reads to decide whether the next start
    /// queues a Greeting again (Spec §6.14, F-26).
    /// </summary>
    [Fact]
    public async Task Greeting_TurnFails_NothingPostedRoomStillEmpty()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("boom"));
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        await using var runner = CreateHost(fixture, persona, factory);
        await runner.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);
        await WaitForPromptCountAsync(factory.Session, 1, ct);

        // Bounded grace period for the failed prompt's post-processing to finish - the same shape
        // Start_NotBuiltin_NoGreeting above uses: there is nothing further to wait FOR (a failure
        // posts nothing, by design), only a window to let it settle before asserting the negative.
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        var store = fixture.Services.GetRequiredService<IChatStore>();
        var history = await store.ReadAllAsync(roomId, ct);
        Assert.Empty(history);

        var hasMessages = await store.HasMessagesAsync(roomId, ct);
        Assert.False(hasMessages);
    }

    /// <summary>
    /// A Human Message that arrives while the Greeting Turn is still running does not interrupt it
    /// or merge into it: the Greeting Turn is queued on the runner's single consumer loop like any
    /// other Turn, so it posts first, and only then does the Human's Message get its own separate
    /// Turn (Spec §6.14, F-27).
    /// </summary>
    [Fact]
    public async Task Greeting_HumanMessageArrivesDuring_QueuedAndAnsweredNext()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "greeting reply");
        factory.Session.EnqueueReply("pong");
        var persona = new Persona("Chief of Staff", BuiltinTeammate.DefaultText);

        await using var runner = CreateHost(fixture, persona, factory);
        await runner.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "Chief of Staff", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        // Posted well inside the Greeting Turn's 300ms hold, while its own PromptAsync call is still
        // in flight - the runner's single consumer loop can only pick this up once that Turn ends.
        await chat.PostAsync(roomId, KnownIds.Human, "hello, anyone there?", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 3, ct);

        Assert.False(factory.Session.OverlapDetected);
        Assert.Equal(2, factory.Session.Prompts.Count);

        var humanMessage = Assert.Single(history, m => m.SenderId == KnownIds.Human);
        Assert.Equal("hello, anyone there?", humanMessage.Text);

        var greetingReply = Assert.Single(history, m => m.Text == "greeting reply");
        var pongReply = Assert.Single(history, m => m.Text == "pong");
        Assert.Equal(agentId, greetingReply.SenderId);
        Assert.Equal(agentId, pongReply.SenderId);

        var orderedHistory = history.ToList();
        Assert.True(
            orderedHistory.IndexOf(greetingReply) < orderedHistory.IndexOf(pongReply),
            "The Greeting Turn must post before the Turn answering the Human's Message.");
    }

    [Fact]
    public async Task DirectRoom_RepliesWithoutMention()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("pong");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hello there", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 2, ct);

        Assert.Equal("pong", history[^1].Text);
        Assert.Equal(agentId, history[^1].SenderId);
    }

    [Fact]
    public async Task GroupRoom_DoesNotReplyWithoutMention()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "hello group", ct: ct);

        // A sentinel proves the negative without sleeping (docs/ChatRoom.md records a test that logged
        // 4299 messages in two seconds, so a misbehaving agent is a real risk): the mentioned probe is
        // delivered after "hello group" on the same ordered pipe and answered by the runner's single
        // consumer, so if the unmentioned message had been taken as a Turn its reply would be the
        // first Message Nova posts, ahead of the probe's.
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova probe", ct: ct);
        var history = await WaitForMessageFromAsync(store, room.Id, novaId, ct);

        Assert.Equal(["hello group", "@nova probe", "probe reply"], history.Select(m => m.Text).ToArray());
        Assert.Contains("@nova probe", Assert.Single(factory.Session.Prompts), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupRoom_RepliesWhenMentioned()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("mentioned reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova hello", ct: ct);

        var history = await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        Assert.Equal("mentioned reply", history[^1].Text);
        Assert.Equal(novaId, history[^1].SenderId);
    }

    /// <summary>
    /// D16 P0-3 (RS §2 U15, RS D-20): when a group Room is renamed to share its name with this
    /// Agent's direct Room, the Turn's Room label distinguishes them with " #" and the last six
    /// characters of the labelled Room's own id, so a model reading two same-named Rooms can tell
    /// which is which.
    /// </summary>
    [Fact]
    public async Task TwoRoomsSameName_PromptLabelsDiffer()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("group reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);
        await chat.RenameRoomAsync(room.Id, "nova", ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova hello", ct: ct);

        var prompts = await WaitForPromptCountAsync(factory.Session, 1, ct);

        var expectedSuffix = room.Id.Length <= 6 ? room.Id : room.Id[^6..];
        Assert.Contains($"[Room: nova #{expectedSuffix} (id: {room.Id})]", prompts[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// D16 P0-3 (RS §9 E-7): the runner's known-names table is refreshed from every
    /// <see cref="MessagePosted.RoomName"/>, so a Room renamed after its direct Room was created
    /// labels its very next Turn with the new name.
    /// </summary>
    [Fact]
    public async Task RoomRenamed_NextLabelUsesNewName()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "hello", ct: ct);
        var firstPrompts = await WaitForPromptCountAsync(factory.Session, 1, ct);
        Assert.Contains("[Room: nova (id:", firstPrompts[0], StringComparison.Ordinal);

        await chat.RenameRoomAsync(roomId, "Porto trip", ct);
        await chat.PostAsync(roomId, KnownIds.Human, "hello again", ct: ct);
        var secondPrompts = await WaitForPromptCountAsync(factory.Session, 2, ct);
        Assert.Contains("[Room: Porto trip (id:", secondPrompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reply_IsTheConcatenationOfMessageChunks()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("Hello, ", "world!");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 2, ct);

        Assert.Equal("Hello, world!", history[^1].Text);
    }

    [Fact]
    public async Task ConcurrentMessages_AreProcessedOneAtATime()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "first");
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "second");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory, clock: clock);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        var post1 = chat.PostAsync(roomId, KnownIds.Human, "one", ct: ct);
        var post2 = chat.PostAsync(roomId, KnownIds.Human, "two", ct: ct);
        await Task.WhenAll(post1, post2);

        // Each Turn holds its reply for 300 ms of fake time; the second may not start until the first
        // has ended, so the clock is moved once per Turn. The watchdog is the second armed timer.
        await WaitForPromptCountAsync(factory.Session, 1, ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromMilliseconds(300), ct);
        await WaitForPromptCountAsync(factory.Session, 2, ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromMilliseconds(300), ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 4, ct);

        Assert.False(factory.Session.OverlapDetected);
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Contains(history, m => m.Text == "first");
        Assert.Contains(history, m => m.Text == "second");
    }

    [Fact]
    public async Task PromptFailure_DoesNotKillTheReadLoop()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("boom"));
        factory.Session.EnqueueReply("recovered");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "second message", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 3, ct);

        Assert.Equal("recovered", history[^1].Text);
        Assert.Equal(agentId, history[^1].SenderId);
    }

    /// <summary>
    /// Regression test for the crashed-agent hang (roadmap item 3): before <c>RunEventReaderAsync</c>
    /// grew its <c>finally</c>, faulting the event stream mid-turn left <c>ProcessWorkItemAsync</c>
    /// awaiting a <c>TurnCompleted</c> that could now never arrive, so the single consumer never
    /// drained another <see cref="WorkItem"/> - the Agent went deaf in every Room,
    /// permanently. A second message posted after the fault is only ever attempted (recorded in
    /// <see cref="FakeAgentSession.Prompts"/>) if the consumer actually moved past the faulted turn.
    /// </summary>
    [Fact]
    public async Task EventStreamFaulting_UnblocksTheInFlightTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "never posted");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);

        // Give the first turn's PromptAsync a moment to start its 300ms delay before the fault lands,
        // so the fault genuinely arrives mid-turn rather than before the turn even started.
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        factory.Session.FaultEvents(new IOException("adapter process died"));

        await chat.PostAsync(roomId, KnownIds.Human, "second message", ct: ct);

        // Bounded: if the finally were removed, the consumer would stay parked on the first turn's
        // completion source forever, this count would never reach 2, and the test fails on timeout
        // instead of hanging the suite.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 2 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Equal(2, factory.Session.Prompts.Count);
    }

    /// <summary>
    /// Companion to <see cref="EventStreamFaulting_UnblocksTheInFlightTurn"/>: the same fault must also
    /// be logged, not merely survived, so a crashed agent process leaves a trace somewhere. Uses a
    /// capturing logger rather than <see cref="NullLogger{T}"/> because this is the one behaviour
    /// <see cref="NullLogger{T}"/> cannot observe.
    /// </summary>
    [Fact]
    public async Task EventStreamFaulting_IsLogged()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "never posted");
        var persona = new Persona("nova", "You are Nova.");
        var logger = new RecordingLogger<PersonaRunner>();

        await using var agentHost = CreateHost(fixture, persona, factory, logger);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        factory.Session.FaultEvents(new IOException("adapter process died"));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!logger.Entries.Any(entry => entry.Level == LogLevel.Warning) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains(persona.Name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Proves item 2 of task T2.1: <c>ProcessWorkItemAsync</c> mints a Message id and carries it on
    /// <see cref="PostMessage"/> rather than sending <see langword="null"/>. Talks to a hand-rolled
    /// server rather than the usual <see cref="PipeHostFixture"/> so it can inspect the raw envelope
    /// the runner writes, not only whatever id a full <see cref="ChatService"/> would persist - which
    /// would look identical whether or not the runner supplied one, since <c>ChatService.PostAsync</c>
    /// mints its own id when <c>messageId</c> is null.
    /// </summary>
    [Fact]
    public async Task PostMessage_CarriesTheMintedMessageId()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("hi back");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(
            new MessagePosted(
                "room-1",
                "Room",
                new ChatMessage("human-message", DateTimeOffset.UtcNow, "human", "You", "hi"),
                Mentioned: true,
                Mentions: [],
                Members: [],
                AgentMessagesSinceHuman: 0,
                Budget: 0),
            ct);

        // Deltas now precede the post on the wire, so skip past them rather than demanding
        // that the very first envelope be the PostMessage.
        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.NotNull(posted.MessageId);
        Assert.True(NameRules.IsValidId(posted.MessageId));
    }

    /// <summary>
    /// Proves item 1 of task T2.2: each <see cref="MessageChunk"/> the agent emits is published as a
    /// <see cref="MessageDelta"/> carrying the same minted Message id the eventual
    /// <see cref="PostMessage"/> carries.
    /// </summary>
    [Fact]
    public async Task MessageChunks_AreWrittenAsDeltas_UnderTheMintedId()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("Hello, ", "world!");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        var firstDelta = await server.ReceiveAsync<MessageDelta>(ct);
        Assert.Equal("Hello, ", firstDelta.Text);
        Assert.False(firstDelta.IsFinal);

        var secondDelta = await server.ReceiveAsync<MessageDelta>(ct);
        Assert.Equal("world!", secondDelta.Text);
        Assert.False(secondDelta.IsFinal);
        Assert.Equal(firstDelta.MessageId, secondDelta.MessageId);

        var posted = await server.ReceiveAsync<PostMessage>(ct);
        Assert.Equal(firstDelta.MessageId, posted.MessageId);
    }

    /// <summary>
    /// Proves item 3 of task T2.2: whatever ends a Turn - success, an empty reply, a throwing
    /// prompt, or a Stop - <c>ProcessWorkItemAsync</c>'s <c>finally</c> always writes the
    /// terminating <see cref="MessageDelta"/> with <see cref="MessageDelta.IsFinal"/> true, so a
    /// partial reply can never outlive its Turn on screen.
    /// </summary>
    /// <param name="scenario">Which way the Turn ends.</param>
    [Theory]
    [MemberData(nameof(TurnScenarios))]
    public async Task EveryTurn_EndsWithAFinalDelta(TurnScenario scenario)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        switch (scenario)
        {
            case TurnScenario.Success:
                factory.Session.EnqueueReply("hi back");
                break;
            case TurnScenario.EmptyReply:
                factory.Session.EnqueueReply("   ");
                break;
            case TurnScenario.ThrowingPrompt:
                factory.Session.EnqueueFailure(new InvalidOperationException("boom"));
                break;
            case TurnScenario.Stop:
                factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "never delivered");
                break;
        }

        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        if (scenario == TurnScenario.Stop)
        {
            // Give PromptAsync a moment to start its delay before the Stop lands.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(20, ct);
            }

            await server.SendAsync(new StopTurn("room-1"), ct);
        }

        var finalDelta = await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        Assert.Equal(string.Empty, finalDelta.Text);
        Assert.Equal("room-1", finalDelta.RoomId);
        Assert.True(NameRules.IsValidId(finalDelta.MessageId));
    }

    /// <summary>
    /// Proves item 2 of task T2.2: a <see cref="ToolCallStarted"/> and a following
    /// <see cref="ToolCallUpdated"/> each become one <see cref="ToolActivity"/> envelope, carrying
    /// the active Turn's Room and Message id and the mapped <see cref="ToolActivityStatus"/>.
    /// </summary>
    [Fact]
    public async Task ToolCalls_AreWrittenAsToolActivity()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueToolActivity(
            new ToolCallStarted("session-1", "call-1", "Reading a file", ToolKind.Read, ToolCallStatus.Pending, null),
            new ToolCallUpdated("session-1", "call-1", "Reading a file", ToolKind.Read, ToolCallStatus.Completed, null));
        factory.Session.EnqueueReply("done reading");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        var started = await ReceiveUntilAsync<ToolActivity>(server, ct);
        Assert.Equal("call-1", started.ToolCallId);
        Assert.Equal("Reading a file", started.Title);
        Assert.Equal(ToolActivityStatus.Pending, started.Status);
        Assert.Equal("room-1", started.RoomId);

        var updated = await ReceiveUntilAsync<ToolActivity>(server, ct);
        Assert.Equal("call-1", updated.ToolCallId);
        Assert.Equal(ToolActivityStatus.Completed, updated.Status);
        Assert.Equal(started.MessageId, updated.MessageId);
    }

    /// <summary>A cost the Adapter reports reaches the Persona's Spend under the Persona's own Name, through the runner that owns the session.</summary>
    [Fact]
    public async Task CostReportedByTheAdapter_IsAddedToThePersonasSpend()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsageAndCost([100], [new UsageCost(0.05m, "USD")], "done");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        PersonaSpend spend = new(NullLogger<PersonaSpend>.Instance);

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, spend: spend);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);
        await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal([new SpendAmount(0.05m, "USD")], spend.Get("nova"));
    }

    /// <summary>
    /// Proves item 4 of task T2.2: a Stop ends the live Turn - <see cref="IAgentSession.CancelAsync"/>
    /// is called exactly once - and discards every Turn still queued behind it, none of which ever
    /// reaches <see cref="FakeAgentSession.PromptAsync"/>.
    /// </summary>
    [Fact]
    public async Task Stop_EndsTheLiveTurnAndDiscardsTheQueue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "live reply");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        for (var i = 0; i < 3; i++)
        {
            await server.SendAsync(NewMessagePosted("room-1", $"message {i}"), ct);
        }

        // Give the first (live) turn a moment to reach PromptAsync before the Stop lands.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        Assert.Single(factory.Session.Prompts);

        await server.SendAsync(new StopTurn("room-1"), ct);

        var finalDelta = await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);
        Assert.Equal("room-1", finalDelta.RoomId);

        // A sentinel Turn proves the two discarded Turns never ran, without sleeping: Turns run in
        // arrival order on one consumer, so had either survived it would have called PromptAsync (and
        // taken the probe's queued reply) before the probe did.
        await server.SendAsync(NewMessagePosted("room-1", "probe"), ct);
        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("probe reply", posted.Text);
        Assert.Equal(1, factory.Session.CancelCallCount);
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.DoesNotContain(factory.Session.Prompts, prompt => prompt.Contains("message 1", StringComparison.Ordinal) || prompt.Contains("message 2", StringComparison.Ordinal));
    }

    /// <summary>
    /// The regression that matters most: a Stop must not end the single consumer loop. A Message
    /// delivered after a Stop still produces a Turn, proving the consumer moved past the stopped
    /// Turn rather than propagating its cancellation out of <c>RunConsumerAsync</c>.
    /// </summary>
    [Fact]
    public async Task Stop_DoesNotEndTheConsumerLoop()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "never posted");
        factory.Session.EnqueueReply("after the stop");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "first"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(new StopTurn("room-1"), ct);

        // The stopped Turn's own terminator, discarded before proving the next Turn still runs.
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        await server.SendAsync(NewMessagePosted("room-1", "second"), ct);

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);
        Assert.Equal("after the stop", posted.Text);
    }

    /// <summary>
    /// Proves item 6 of task T2.2: a stopped Turn posts no Message, even though text may have
    /// arrived before the Stop landed - the same stance already taken for a Refusal.
    /// </summary>
    [Fact]
    public async Task AStoppedTurn_PostsNoMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "should not be posted");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(new StopTurn("room-1"), ct);

        var finalDelta = await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);
        Assert.Equal(string.Empty, finalDelta.Text);

        // A sentinel Turn proves the negative without sleeping: the pipe is ordered and Turns run one
        // at a time, so if the stopped Turn had posted anything it would arrive before the probe's
        // own reply. The first Message posted is therefore the probe's.
        await server.SendAsync(NewMessagePosted("room-1", "probe"), ct);
        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("probe reply", posted.Text);
    }

    /// <summary>
    /// Proves item 6 of task T2.2: a stopped Turn is logged at Information, never at Warning or
    /// Error - a Stop is a normal outcome, not a failure.
    /// </summary>
    [Fact]
    public async Task AStoppedTurn_IsNotLoggedAsAFailure()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "should not be posted");
        var persona = new Persona("nova", "You are Nova.");
        var logger = new RecordingLogger<PersonaRunner>();
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(new StopTurn("room-1"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        // Bounded grace period for the Information entry, written from the consumer's catch clause,
        // to actually land before asserting against it.
        var logDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!logger.Entries.Any(entry => entry.Level == LogLevel.Information) && DateTimeOffset.UtcNow < logDeadline)
        {
            await Task.Delay(20, ct);
        }

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    /// <summary>
    /// D16 P0-2: <c>StopTurn(roomId)</c> only ends that Room's own live Turn and clears that Room's
    /// own queue. Room A's live Turn is stopped and posts nothing; Room A's queued second Turn is
    /// discarded without ever reaching <see cref="FakeAgentSession.PromptAsync"/>; Room B's queued
    /// Turn, unaffected, still runs and posts.
    /// </summary>
    [Fact]
    public async Task Stop_InRoomA_EndsAsTurnAndClearsAsQueue_BsQueuedTurnStillRuns()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "A1 live reply");
        factory.Session.EnqueueReply("B1 reply");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-a", "a1"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        Assert.Single(factory.Session.Prompts);

        // A2 must never itself reach PromptAsync, so it is never given a queued reply of its own -
        // if it wrongly did run, it would consume B1's queued reply instead, which the assertions
        // below would then catch as a mismatched posted text.
        await server.SendAsync(NewMessagePosted("room-a", "a2"), ct);
        await server.SendAsync(NewMessagePosted("room-b", "b1"), ct);
        await server.SendAsync(new StopTurn("room-a"), ct);

        var aFinal = await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal && delta.RoomId == "room-a", ct);
        Assert.Equal(string.Empty, aFinal.Text);

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);
        Assert.Equal("room-b", posted.RoomId);
        Assert.Equal("B1 reply", posted.Text);

        // A sentinel Turn proves the discarded A2 never reached PromptAsync, without sleeping: Turns
        // run in arrival order on one consumer, so a surviving A2 would have taken the probe's queued
        // reply (and been recorded) before the probe itself ran.
        await server.SendAsync(NewMessagePosted("room-b", "probe"), ct);
        var probePosted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("probe reply", probePosted.Text);
        Assert.Equal(3, factory.Session.Prompts.Count);
        Assert.DoesNotContain(factory.Session.Prompts, prompt => prompt.Contains("a2", StringComparison.Ordinal));
    }

    /// <summary>
    /// D16 P0-2: a Stop naming a Room with no live Turn is a no-op elsewhere. Room A's live Turn
    /// keeps running and posts, and <see cref="FakeAgentSession.CancelAsync"/> is never called.
    /// </summary>
    [Fact]
    public async Task Stop_InRoomB_WhileATurnRuns_LeavesATurnRunning()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "A1 reply");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-a", "a1"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(new StopTurn("room-b"), ct);

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);
        Assert.Equal("room-a", posted.RoomId);
        Assert.Equal("A1 reply", posted.Text);
        Assert.Equal(0, factory.Session.CancelCallCount);
    }

    /// <summary>
    /// D16 P0-2: Room B's live Turn keeps running while Room A's queued Turn is discarded by
    /// <c>StopTurn("room-a")</c> and never reaches <see cref="FakeAgentSession.PromptAsync"/>.
    /// </summary>
    [Fact]
    public async Task Stop_InRoomA_WhileBTurnRuns_ClearsOnlyAsQueue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "B1 live reply");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-b", "b1"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(NewMessagePosted("room-a", "a1"), ct);
        await server.SendAsync(new StopTurn("room-a"), ct);

        // Nothing on the wire says the runner's read loop has handled the Stop, and B1 must not be
        // released before it has (or the consumer could reach A1 first). A short real pause, then the
        // fake clock releases B1's five second hold at once instead of waiting it out.
        await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(5), ct);

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);
        Assert.Equal("room-b", posted.RoomId);
        Assert.Equal("B1 live reply", posted.Text);

        // A sentinel Turn proves the discarded A1 never reached PromptAsync, without sleeping: a
        // surviving A1 would have run (and taken the probe's queued reply) before the probe did.
        await server.SendAsync(NewMessagePosted("room-b", "probe"), ct);
        var probePosted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("probe reply", probePosted.Text);
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.DoesNotContain(factory.Session.Prompts, prompt => prompt.Contains("a1", StringComparison.Ordinal));
    }

    /// <summary>
    /// The race D16's corrections settled: a Stop in Room A must let Room B's queued Turn run only
    /// after the far-side cancel for Room A has settled, and B's Turn must complete normally - posted,
    /// with no failure logged - rather than racing <see cref="FakeAgentSession.CancelAsync"/>.
    /// </summary>
    [Fact]
    public async Task Stop_InRoomA_BQueued_BRunsAfterCancelSettles_AndPosts()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "A1 live reply");
        factory.Session.EnqueueReply("B1 reply");
        var persona = new Persona("nova", "You are Nova.");
        var logger = new RecordingLogger<PersonaRunner>();
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-a", "a1"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(NewMessagePosted("room-b", "b1"), ct);
        await server.SendAsync(new StopTurn("room-a"), ct);

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);
        Assert.Equal("room-b", posted.RoomId);
        Assert.Equal("B1 reply", posted.Text);

        Assert.Equal(1, factory.Session.CancelCallCount);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    /// <summary>
    /// Proves item 5 of task T2.1: a Turn that ends in <see cref="StopReason.Refusal"/> posts nothing,
    /// even though text arrived before the stop - a refusal is not a successful reply.
    /// </summary>
    [Fact]
    public async Task TurnEndingInRefusal_PostsNoMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyEndingIn(StopReason.Refusal, "I can't help with that.");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Single(factory.Session.Prompts);

        // A sentinel Turn proves the negative without sleeping: the probe is answered after the first
        // Turn on the runner's single consumer, so if the refusal had been posted it would be the
        // first Message Nova posts, ahead of the probe's reply.
        await chat.PostAsync(roomId, KnownIds.Human, "probe", ct: ct);
        var history = await WaitForMessageFromAsync(store, roomId, agentId, ct);

        Assert.Equal(["hi", "probe", "probe reply"], history.Select(m => m.Text).ToArray());
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Contains("probe", factory.Session.Prompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlankReply_PostsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("   ");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Single(factory.Session.Prompts);

        // A sentinel Turn proves the negative without sleeping: the probe is answered after the first
        // Turn on the runner's single consumer, so if the blank reply had been posted it would be the
        // first Message Nova posts, ahead of the probe's reply.
        await chat.PostAsync(roomId, KnownIds.Human, "probe", ct: ct);
        var history = await WaitForMessageFromAsync(store, roomId, agentId, ct);

        Assert.Equal(["hi", "probe", "probe reply"], history.Select(m => m.Text).ToArray());
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Contains("probe", factory.Session.Prompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Untagged_MessagesAreNeverSubmitted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned one", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned two", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned three", ct: ct);

        // A mentioned probe follows the three unmentioned Messages on the same ordered pipe. Turns run
        // one at a time in arrival order, so by the time the probe has been answered every earlier
        // Message has already had its chance to become a Turn: the probe's must be the only one.
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova probe", ct: ct);
        await WaitForMessageFromAsync(store, room.Id, novaId, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.Contains("@nova probe", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tagged_PromptIncludesMissedMessages()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("caught up");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "missed one", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "missed two", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "missed three", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova wake up", ct: ct);

        await WaitForHistoryCountAsync(store, room.Id, 5, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.Contains("missed one", prompt, StringComparison.Ordinal);
        Assert.Contains("missed two", prompt, StringComparison.Ordinal);
        Assert.Contains("missed three", prompt, StringComparison.Ordinal);
        Assert.Contains("wake up", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchUp_IsClearedAfterUse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("first catch up");
        factory.Session.EnqueueReply("second catch up");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "batch one missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova first tag", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 3, ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova second tag", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 5, ct);

        Assert.Equal(2, factory.Session.Prompts.Count);
        var secondPrompt = factory.Session.Prompts[1];
        Assert.DoesNotContain("batch one missed", secondPrompt, StringComparison.Ordinal);
        Assert.Contains("second tag", secondPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchUp_IsPerRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("reply for room b");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomA = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);
        var roomB = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(roomA.Id, KnownIds.Human, "room a secret missed message", ct: ct);
        await chat.PostAsync(roomB.Id, KnownIds.Human, "@nova hello in room b", ct: ct);

        await WaitForHistoryCountAsync(store, roomB.Id, 2, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.DoesNotContain("room a secret missed message", prompt, StringComparison.Ordinal);
        Assert.Contains("hello in room b", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchUp_IsBoundedToTheConfiguredCount()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        var additionalConfig = new Dictionary<string, string?>
        {
            ["Team:Acp:CatchUpMessages"] = "2",
        };
        await using var fixture = await PipeHostFixture.StartAsync(additionalConfig, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("bounded reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "oldest missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "middle missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "newest missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova tag", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.DoesNotContain("oldest missed", prompt, StringComparison.Ordinal);
        Assert.Contains("middle missed", prompt, StringComparison.Ordinal);
        Assert.Contains("newest missed", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectRoom_HasNoCatchUp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("pong");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hello there", ct: ct);

        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        var prompt = Assert.Single(factory.Session.Prompts);

        // "context only" used to be a literal in BuildPrompt; it now lives entirely inside the
        // turn.catchUpHeader prompt default. Deriving the expected phrase from PromptCatalog rather than
        // typing it here means a reworded default still fails this test loudly when the header leaks
        // into a no-catch-up prompt, instead of silently asserting against wording nobody owns anymore.
        // Do not "simplify" this back to a literal — see task T1.11.
        var catchUpHeaderDefault = PromptCatalog.Get("turn.catchUpHeader").Default;
        var distinctivePortion = PromptRenderer
            .Render(catchUpHeaderDefault, new Dictionary<string, string> { ["{{roomLabel}}"] = string.Empty })
            .Trim();

        Assert.DoesNotContain(distinctivePortion, prompt, StringComparison.Ordinal);
    }

    // The mcp__team__ five-name pin and the orientation-ordering pin both moved to
    // Prompts/PromptDefaultsTests.cs (task T1.11): rendered against a caller-supplied toolNames argument,
    // they proved only that Compose's own argument came back out of its own output, not anything about
    // the product's shipped wording. PromptDefaultsTests re-anchors both against PromptCatalog's actual
    // defaults, which is the thing that can vary now.

    [Fact]
    public void SystemPromptComposer_IncludesPersonaText()
    {
        var persona = new Persona("nova", "# Nova\nYou are a helpful assistant named Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakePromptSource(), "mcp__team__get_help", ToolNames);

        Assert.Contains("You are a helpful assistant named Nova.", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the feature this task adds: a configured override for a prompt's text reaches the composed
    /// prompt in place of <see cref="PromptCatalog"/>'s default.
    /// </summary>
    [Fact]
    public void SystemPromptComposer_PromptOverride_ReachesTheComposedPrompt()
    {
        var persona = new Persona("nova", "You are Nova.");
        var prompts = new FakePromptSource();
        prompts.SetOverride("systemPrompt.identity", "You are, unusually, called \"{{personaName}}\" here.");

        var prompt = SystemPromptComposer.Compose(persona, prompts, "mcp__team__get_help", ToolNames);

        Assert.Contains("You are, unusually, called \"nova\" here.", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the same feature for the per-turn prompt: a configured override for the Room label prompt
    /// reaches the text <see cref="RoomSession.BuildPrompt"/> produces, in place of the catalog
    /// default.
    /// </summary>
    [Fact]
    public void BuildPrompt_PromptOverride_ReachesTheTurnPrompt()
    {
        var prompts = new FakePromptSource();
        prompts.SetOverride("turn.roomLabel", "<<{{roomName}}/{{roomId}}>>");
        var item = new WorkItem("room-9", "Nova & You", "You", "hello", []);

        var prompt = RoomSession.BuildPrompt(item, prompts);

        Assert.Contains("<<Nova & You/room-9>>", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupRoom_AtBudget_MentionedAgentTakesNoTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);

        // The friend's message is itself the first - and only - agent message this Room allows, so by
        // the time it reaches Nova the Room is already spent.
        await chat.PostAsync(room.Id, friendId, "@nova hello", ct: ct);

        // A Human message resets the Room's Budget, so the probe that follows is a Turn Nova does take.
        // It reaches Nova after the spent Message on the same ordered pipe, and Turns run one at a
        // time: had the spent Message been taken, its reply would be the first thing Nova posts.
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova probe", ct: ct);
        var history = await WaitForMessageFromAsync(store, room.Id, novaId, ct);

        Assert.Equal(["@nova hello", "@nova probe", "probe reply"], history.Select(m => m.Text).ToArray());
        Assert.Contains("@nova probe", Assert.Single(factory.Session.Prompts), StringComparison.Ordinal);
    }

    // The payoff of the three-state gate. A Catch-up Message was missed and rides along later; a
    // Message declined for Budget is being held for re-delivery, so buffering it would show it to the
    // model twice in one prompt.
    [Fact]
    public async Task BudgetExhausted_DoesNotBufferAsCatchUp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("woken by the human");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova declined for budget", ct: ct);

        // No sleep between the two posts: the pipe is ordered, so the runner sees the declined Message
        // before the Human's, which is the order this test is about.
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova carry on", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 3, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.DoesNotContain("declined for budget", prompt, StringComparison.Ordinal);
    }

    // Continue is only meaningful if it wakes the exchange it paused: a Turn never starts on its own.
    [Fact]
    public async Task Redelivery_AfterExtend_ProducesATurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("resumed");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova hello", ct: ct);

        var extended = await chat.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extended.Result);
        var history = await WaitForHistoryCountAsync(store, room.Id, 2, ct);
        Assert.Equal("resumed", history[^1].Text);
        Assert.Equal(novaId, history[^1].SenderId);

        // Exactly one Turn ran for the spent Message, and only after the extend: a sentinel Turn proves
        // it without sleeping. Had the spent Message been taken as a Turn when it first arrived, it
        // would have used "resumed", and the redelivery would have produced a second reply before
        // the probe's, so the Transcript would differ from the one asserted here.
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova probe", ct: ct);
        var settled = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Equal(["@nova hello", "resumed", "@nova probe", "probe reply"], settled.Select(m => m.Text).ToArray());
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Contains("@nova probe", factory.Session.Prompts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenBudget_WhenExhausted_StopsTakingTurns()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        // The message Budget is off so only the token Budget can stop anything.
        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "100",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([150], "first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);

        // The spent-Budget report is what says "two" was refused: the Room's drain loop decides that
        // after the read loop has seen the Message, so probing straight away let the probe's reset
        // land first and "two" took a Turn (main run 733).
        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded && s.Reason!.Contains("token Budget", StringComparison.Ordinal),
            ct);

        // A sentinel proves "two" took no Turn without sleeping. A Human Message resets the token
        // Budget, so the probe that follows on the same ordered pipe is a Turn Nova does take, and it
        // takes the queued "second reply". Had "two" been taken, "second reply" would sit before the
        // probe in the Transcript, not after it.
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova probe", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 5, ct);

        Assert.Equal(["@nova one", "first reply", "@nova two", "@nova probe", "second reply"], history.Select(m => m.Text).ToArray());
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Contains("@nova probe", factory.Session.Prompts[1], StringComparison.Ordinal);
    }

    // UsageUpdated.Used is a level, so a compaction makes it fall. Levels 100, 150, 20, 60 are 190
    // tokens of work; summing the levels themselves would read 330 and trip a 200 budget that should
    // not trip.
    [Fact]
    public async Task TokenBudget_IgnoresDropsInUsed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "200",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([100, 150, 20, 60], "first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Contains(history, m => m.Text == "second reply");
    }

    [Fact]
    public async Task TokenBudget_ResetsOnAHumanMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "100",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([150], "first reply");
        factory.Session.EnqueueReply("after the human spoke");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova carry on", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Equal("after the human spoke", history[^1].Text);
    }

    [Fact]
    public async Task TokenBudget_ZeroDisablesTheCap()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "0",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([999_999], "first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Contains(history, m => m.Text == "second reply");
    }

    /// <summary>
    /// Proves T4.3 item 1: a stored Model absent from a genuinely non-empty catalog is a warning, not
    /// a failure - the session still starts, and <see cref="PersonaState.Degraded"/> is reported naming
    /// the Model.
    /// </summary>
    [Fact]
    public async Task StoredModelNotInTheCatalog_ReportsDegraded()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.Models = [new AgentModelOption("claude-3", "Claude 3", null)];
        var persona = new Persona("nova", "You are Nova.", Model: "claude-99");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        await WaitForDirectRoomAsync(fixture, "nova", ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded && s.Reason!.Contains("claude-99", StringComparison.Ordinal),
            ct);
    }

    /// <summary>
    /// Proves T4.3 item 1's other half: <see cref="IAgentSession.Models"/>' own doc comment says an
    /// EMPTY list means unknown, never "no models" - so it must never produce a Degraded report, even
    /// though a Model is stored.
    /// </summary>
    [Fact]
    public async Task EmptyModelCatalog_ReportsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.", Model: "claude-99");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        await WaitForDirectRoomAsync(fixture, "nova", ct);

        // Bounded grace period: give a misbehaving check every chance to report anyway.
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        Assert.Empty(statuses.Snapshot());
    }

    /// <summary>A Turn whose prompt throws is reported as Degraded, naming it as a single failure.</summary>
    [Fact]
    public async Task AFailedTurn_ReportsDegraded()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("boom"));
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded
                && s.Reason!.Contains("A Turn in Room", StringComparison.Ordinal)
                && s.Reason.Contains("failed —", StringComparison.Ordinal),
            ct);
    }

    /// <summary>
    /// The third consecutive Turn failure escalates the reason's wording, without ever matching on the
    /// Adapter's own (changeable) message text.
    /// </summary>
    [Fact]
    public async Task ThreeConsecutiveFailures_EscalateTheReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("one"));
        factory.Session.EnqueueFailure(new InvalidOperationException("two"));
        factory.Session.EnqueueFailure(new InvalidOperationException("three"));
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "one", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "two", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "three", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 3 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded &&
                s.Reason!.Contains("3 consecutive Turns have failed", StringComparison.Ordinal),
            ct);
    }

    /// <summary>A Turn that completes normally clears whatever Degraded state a prior failure reported.</summary>
    [Fact]
    public async Task ASuccessfulTurn_ClearsADegradedState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("boom"));
        factory.Session.EnqueueReply("recovered");
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "second message", ct: ct);

        // Completed from the StatusChanged handler the moment an Online lands after a Degraded - the
        // recovery this test is about - rather than polled.
        var seen = await statuses.WaitForAsync(
            static history => history.Count > 0 && history[^1].State == PersonaState.Online
                && history.Any(static status => status.State == PersonaState.Degraded),
            ct);

        Assert.Contains(seen, s => s.State == PersonaState.Degraded);
        Assert.NotEmpty(seen);
        Assert.Equal(PersonaState.Online, seen[^1].State);
    }

    /// <summary>An <see cref="AgentDisconnectedException"/> from a Turn is reported as Offline.</summary>
    [Fact]
    public async Task Disconnected_ReportsOffline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new AgentDisconnectedException());
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        await statuses.AssertContainsEventuallyAsync(s => s.State == PersonaState.Offline, ct);
    }

    /// <summary>The three StopReasons a Turn can end in without producing a reply are all reported as Degraded.</summary>
    /// <param name="reason">The StopReason this run of the theory exercises.</param>
    [Theory]
    [MemberData(nameof(IncompleteStopReasons))]
    public async Task EachStopReason_ReportsDegraded(StopReason reason)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyEndingIn(reason, "partial");
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded && s.Reason!.Contains("without a reply", StringComparison.Ordinal),
            ct);
    }

    /// <summary>The three <see cref="StopReason"/> values <see cref="EachStopReason_ReportsDegraded"/> exercises.</summary>
    /// <returns>One row per incomplete-stop reason.</returns>
    public static TheoryData<StopReason> IncompleteStopReasons() => new()
    {
        StopReason.MaxTokens,
        StopReason.MaxTurnRequests,
        StopReason.Refusal,
    };

    /// <summary>A Turn the Human stopped reports nothing at all - a Stop is not a failure.</summary>
    [Fact]
    public async Task AStoppedTurn_ReportsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "never posted");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(new StopTurn("room-1"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        // Bounded grace period for a misbehaving Stop to report something anyway.
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        Assert.Empty(statuses.Snapshot());
    }

    /// <summary>
    /// A Stop between two failures neither counts as a failure itself nor resets the streak: fail,
    /// stop, fail, fail must still escalate on the fourth Turn - if the Stop had reset the counter,
    /// the fourth Turn would only be the second consecutive failure and would not escalate.
    /// </summary>
    [Fact]
    public async Task AStoppedTurn_DoesNotBreakTheFailureStreak()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("one"));
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "never posted");
        factory.Session.EnqueueFailure(new InvalidOperationException("three"));
        factory.Session.EnqueueFailure(new InvalidOperationException("four"));
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        // Turn 1: fails.
        await server.SendAsync(NewMessagePosted("room-1", "one"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        // Turn 2: stopped.
        await server.SendAsync(NewMessagePosted("room-1", "two"), ct);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 2 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        await server.SendAsync(new StopTurn("room-1"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        // Turns 3 and 4: fail again.
        await server.SendAsync(NewMessagePosted("room-1", "three"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);
        await server.SendAsync(NewMessagePosted("room-1", "four"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded &&
                s.Reason!.Contains("3 consecutive Turns have failed", StringComparison.Ordinal),
            ct);
    }

    /// <summary>Spending the per-Persona token Budget is reported as Degraded.</summary>
    [Fact]
    public async Task SpentTokenBudget_ReportsDegraded()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "100",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([150], "first reply");
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded && s.Reason!.Contains("token Budget", StringComparison.Ordinal),
            ct);
    }

    /// <summary>A Human Message clears the Degraded state a spent token Budget reported.</summary>
    [Fact]
    public async Task AHumanMessage_ClearsTheDegradedTokenBudget()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "100",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([150], "first reply");
        factory.Session.EnqueueReply("after the human spoke");
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        await statuses.AssertContainsEventuallyAsync(s => s.State == PersonaState.Degraded, ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova carry on", ct: ct);

        // Five, not four: unlike TokenBudget_ResetsOnAHumanMessage, "two" is also in this Room's
        // history (declined for a spent Budget, but still posted and still counted).
        var history = await WaitForHistoryCountAsync(store, room.Id, 5, ct);

        Assert.Equal("after the human spoke", history[^1].Text);
        var settled = await statuses.WaitForAsync(static history => history.Count > 0 && history[^1].State == PersonaState.Online, ct);
        Assert.Equal(PersonaState.Online, settled[^1].State);
    }

    /// <summary>
    /// TRAP 1 regression: a Human Stop that lands while a Turn is within the idle-timeout window must
    /// still be reported as a Stop, never as a Degraded failure. Both a Stop and the idle-timeout
    /// watchdog firing cancel the same Turn token and surface to the consumer as the identical
    /// <see cref="OperationCanceledException"/> - only the latch each producer writes before it
    /// cancels lets the consumer tell them apart, and a real Stop must win any tie.
    /// </summary>
    [Fact]
    public async Task TurnIdleTimeout_AStopInsideTheWindow_IsStillReportedAsStopped()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "should not be posted");
        var persona = new Persona("nova", "You are Nova.");
        var logger = new RecordingLogger<PersonaRunner>();
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 5 },
        });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger, timeProvider: clock);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        await WaitForPromptCountAsync(factory.Session, 1, ct);

        // Watchdog + the fake session's own delay are the two armed timers. Time moves to just inside
        // the 5 second bound, so the Stop below really does land inside the window rather than at t=0.
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(4), ct);

        await server.SendAsync(new StopTurn("room-1"), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        var logDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!logger.Entries.Any(entry => entry.Message.Contains("was stopped", StringComparison.Ordinal))
            && DateTimeOffset.UtcNow < logDeadline)
        {
            await Task.Delay(20, ct);
        }

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information && entry.Message.Contains("was stopped", StringComparison.Ordinal));
        Assert.DoesNotContain(statuses.Snapshot(), s => s.State == PersonaState.Degraded);
    }

    /// <summary>
    /// Against an unreachable inference endpoint, session/prompt can return nothing for a long time
    /// with no error at all - the Adapter is hung, not merely slow. The idle-timeout bound catches
    /// this and reports Degraded, naming the bound, instead of leaving the Persona reading as healthy
    /// forever. This is the regression that matters most: on today's code, with no such bound, this
    /// test fails because nothing is ever reported.
    /// </summary>
    [Fact]
    public async Task TurnIdleTimeout_AnAdapterThatSaysNothing_ReportsDegradedWithinTheBound()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "should not be posted");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 1 },
        });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        // The Adapter is silent for exactly the one second bound: the fake clock moves that far and no
        // further, so only the watchdog (never the fake session's 30 second delay) can fire.
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(1), ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded && s.Reason!.Contains("for 1 seconds", StringComparison.Ordinal),
            ct, TimeSpan.FromSeconds(8));
    }

    /// <summary>
    /// TRAP 2 regression: the idle-timeout watchdog must reach the far side with an actual
    /// <see cref="IAgentSession.CancelAsync"/> call while the prompt is still in flight - the obvious
    /// "cancel the token, then call CancelAsync" ordering makes <c>DotAcpAgentSession.CancelAsync</c>
    /// silently no-op (src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs:144-171), so counting cancel calls
    /// alone cannot prove this; only observing that a prompt was in flight when the cancel landed can.
    /// </summary>
    [Fact]
    public async Task TurnIdleTimeout_OnFiring_ReachesTheAdapterWithASessionCancel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "should not be posted");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 1 },
        });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(1), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (!factory.Session.CancelObservedPromptInFlight && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(factory.Session.CancelObservedPromptInFlight);
    }

    /// <summary>An idle-timeout firing posts no Message - the same stance already taken for a Stop and for a Refusal.</summary>
    [Fact]
    public async Task TurnIdleTimeout_OnFiring_PostsNoMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "should not be posted");
        factory.Session.EnqueueReply("probe reply");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 1 },
        });

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(1), ct);

        var finalDelta = await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);
        Assert.Equal(string.Empty, finalDelta.Text);

        // A sentinel Turn proves the negative deterministically rather than by sleeping: the runner
        // serialises Turns and the pipe is ordered, so if the timed-out Turn had posted anything it
        // would arrive before the probe's own reply. The first Message posted is therefore the probe's.
        await server.SendAsync(NewMessagePosted("room-1", "probe"), ct);
        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("probe reply", posted.Text);
    }

    /// <summary>Three Turns that each go silent past the bound escalate the Degraded reason, proving the failure streak the watchdog reports through shares <see cref="ThreeConsecutiveFailures_EscalateTheReason"/>'s counter.</summary>
    [Fact]
    public async Task TurnIdleTimeout_ThreeSilentTurns_EscalateTheReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "one");
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "two");
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "three");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 1 },
        });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "one"), ct);
        await WaitForPromptCountAsync(factory.Session, 1, ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(1), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        await server.SendAsync(NewMessagePosted("room-1", "two"), ct);
        await WaitForPromptCountAsync(factory.Session, 2, ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(1), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        await server.SendAsync(NewMessagePosted("room-1", "three"), ct);
        await WaitForPromptCountAsync(factory.Session, 3, ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(1), ct);
        await ReceiveUntilAsync<MessageDelta>(server, delta => delta.IsFinal, ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded &&
                s.Reason!.Contains("3 consecutive Turns have failed", StringComparison.Ordinal),
            ct);
    }

    /// <summary>
    /// Any sign of life restarts the idle clock: a Turn that keeps streaming chunks faster than the
    /// bound never goes idle long enough to trip it, however long the whole Turn runs. Converting the
    /// bound into a whole-turn cap instead of an idle one would make this test fail.
    /// </summary>
    [Fact]
    public async Task TurnIdleTimeout_StreamingActivity_ExtendsTheBound()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        string[] chunks = ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"];
        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDripFedReply(TimeSpan.FromMilliseconds(250), chunks);
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 2 },
        });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        // Twelve chunks, one every 250 ms of fake time: 3 seconds in all, past the 2 second bound. Each
        // step waits until the watchdog and the drip delay are both armed, moves time by exactly one
        // gap, then waits for that chunk's delta to reach the server, so the runner has observed the
        // sign of life before the next step. A bound that measured the whole Turn, not the silence,
        // would fire at the eighth step.
        foreach (var chunk in chunks)
        {
            await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromMilliseconds(250), ct);
            await ReceiveUntilAsync<MessageDelta>(server, delta => string.Equals(delta.Text, chunk, StringComparison.Ordinal), ct);
        }

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal(string.Concat(chunks), posted.Text);
        Assert.DoesNotContain(statuses.Snapshot(), s => s.State == PersonaState.Degraded);
    }

    /// <summary>A bound of zero or less disables the idle timeout entirely, exactly as <see cref="AcpOptions.TokenBudget"/>'s own zero-disables convention.</summary>
    [Fact]
    public async Task TurnIdleTimeout_ZeroDisablesTheBound()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(2), "still here");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 0 },
        });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        // No watchdog is armed, so the fake session's own delay is the only timer. Two fake seconds
        // pass, well past any bound that could have been left switched on.
        await clock.AdvanceWhenArmedAsync(1, TimeSpan.FromSeconds(2), ct);

        var posted = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("still here", posted.Text);
        Assert.DoesNotContain(statuses.Snapshot(), s => s.State == PersonaState.Degraded);
    }

    /// <summary>
    /// A run shutdown that lands while a Turn is inside the idle-timeout window must report nothing -
    /// the same "shutdown reports nothing" stance as <see cref="NormalShutdown_ReportsNothing"/> -
    /// and must itself return promptly, proving the watchdog does not outlive the run token.
    /// </summary>
    [Fact]
    public async Task TurnIdleTimeout_RunShutdownInsideTheWindow_ReportsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        TrackedFakeTimeProvider clock = new();
        var factory = new FakeAgentHostFactory(clock);
        factory.Session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "should not be posted");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions
        {
            PipeName = server.PipeName,
            Acp = new AcpOptions { TurnIdleTimeoutSeconds = 5 },
        });
        StatusRecorder statuses = new();

        var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: clock);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);

        await WaitForPromptCountAsync(factory.Session, 1, ct);
        await clock.AdvanceWhenArmedAsync(2, TimeSpan.FromSeconds(4), ct);

        await runner.StopAsync();

        Assert.Empty(statuses.Snapshot());
    }

    /// <summary>
    /// Proves T4.4 item 1: if the read loop dies for any reason other than the run being cancelled
    /// - here, the server end of the pipe dropping without the runner's own shutdown ever running -
    /// Offline is reported.
    /// </summary>
    [Fact]
    public async Task AReadLoopThatDies_ReportsOffline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        // Kills the server side of the pipe without the runner's own shutdown ever running, so the
        // read loop's stream.ReadAsync throws instead of observing a cooperative cancellation.
        await server.DisposeAsync();

        await statuses.AssertContainsEventuallyAsync(s => s.State == PersonaState.Offline, ct);
    }

    /// <summary>
    /// A <see cref="PersonaRunner.StatusChanged"/> subscriber that throws is logged and skipped, and
    /// the Agent keeps taking Turns. Health reporting must never be able to kill the Agent it
    /// reports on: all three of the runner's loops raise this event, two of them from a
    /// <c>finally</c>, so an unguarded subscriber could end a loop and mask the exception that
    /// ended it — turning a diagnostic into the outage it exists to describe.
    /// </summary>
    [Fact]
    public async Task AThrowingStatusSubscriber_DoesNotKillTheRunner()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("first");
        factory.Session.EnqueueReply("second");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        var logger = new RecordingLogger<PersonaRunner>();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger);
        runner.StatusChanged += _ => throw new InvalidOperationException("Simulated subscriber failure.");
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(NewMessagePosted("room-1", "hi"), ct);
        var first = await ReceiveUntilAsync<PostMessage>(server, ct);

        // The second Turn is the assertion: a throwing subscriber on the first one must not have
        // ended the consumer loop.
        await server.SendAsync(NewMessagePosted("room-1", "again"), ct);
        var second = await ReceiveUntilAsync<PostMessage>(server, ct);

        Assert.Equal("first", first.Text);
        Assert.Equal("second", second.Text);
        Assert.Contains(logger.Entries, e => e.Message.Contains("StatusChanged", StringComparison.Ordinal));
    }

    /// <summary>Proves T4.4 item 1 for the event reader: a faulted agent event stream is reported as Offline.</summary>
    [Fact]
    public async Task EventReaderEnding_ReportsOffline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "never posted");
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        await using var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);

        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        factory.Session.FaultEvents(new IOException("adapter process died"));

        await statuses.AssertContainsEventuallyAsync(s => s.State == PersonaState.Offline, ct);
    }

    /// <summary>An ordinary shutdown reports nothing - the regression that would otherwise paint every tile red on Ctrl-C.</summary>
    [Fact]
    public async Task NormalShutdown_ReportsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");
        StatusRecorder statuses = new();

        var agentHost = CreateHost(fixture, persona, factory);
        agentHost.StatusChanged += statuses.Record;
        await agentHost.StartAsync(ct);
        await WaitForDirectRoomAsync(fixture, "nova", ct);

        await agentHost.DisposeAsync();

        Assert.Empty(statuses.Snapshot());
    }

    /// <summary>A refused post naming a real misconfiguration - not a Member, an unknown Room, or a bad Message - is reported as Degraded.</summary>
    [Fact]
    public async Task ARefusedPost_ReportsDegraded()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(new ProtocolError(ErrorCodes.NotMember, "Not a member of that room."), ct);

        await statuses.AssertContainsEventuallyAsync(
            s => s.State == PersonaState.Degraded && s.Reason!.Contains("Not a member", StringComparison.Ordinal),
            ct);
    }

    /// <summary>ADR-0006 already owns the spent-Budget surface, so that refusal alone must report nothing.</summary>
    [Fact]
    public async Task ABudgetExhaustedRefusal_ReportsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        StatusRecorder statuses = new();

        await using var runner = new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(new ProtocolError(ErrorCodes.BudgetExhausted, "The room has spent its budget."), ct);

        // Bounded grace period for a misbehaving refusal to report something anyway.
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);

        Assert.Empty(statuses.Snapshot());
    }

    /// <summary>
    /// Puts Nova in a Room with a second Agent, so a test can post agent-authored Messages - the only
    /// kind that spends Budget. The friend is a bare pipe client: it never replies, which keeps every
    /// Message in these Rooms one the test asked for.
    /// </summary>
    /// <param name="fixture">The running host.</param>
    /// <param name="novaId">The Agent id of the Persona under test.</param>
    /// <param name="ct">Cancels the handshake and the Room creation.</param>
    /// <returns>The chat service, the store, the Room, and the friend's Agent id.</returns>
    private static async Task<(ChatService Chat, IChatStore Store, Room Room, string FriendId)> CreateGroupWithFriendAsync(
        PipeHostFixture fixture, string novaId, CancellationToken ct)
    {
        var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);
        return (chat, store, room, friendWelcome.AgentId);
    }

    private static PersonaRunner CreateHost(
        PipeHostFixture fixture, Persona persona, FakeAgentHostFactory factory, ILogger<PersonaRunner>? logger = null, TimeProvider? clock = null)
    {
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        return new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger ?? NullLogger<PersonaRunner>.Instance, timeProvider: clock);
    }

    /// <summary>
    /// Builds a Mentioned <see cref="MessagePosted"/> for the <see cref="FakePersonaServer"/> tests,
    /// which talk to a bare <see cref="PersonaRunner"/> directly and so have no <c>ChatService</c> to
    /// mint one through.
    /// </summary>
    /// <param name="roomId">The Room the delivery is attributed to.</param>
    /// <param name="text">The Message text.</param>
    /// <returns>A delivery this Agent is Mentioned in, with no other Members and no Budget cap.</returns>
    private static MessagePosted NewMessagePosted(string roomId, string text) =>
        new(
            roomId,
            "Room",
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "You", text),
            Mentioned: true,
            Mentions: [],
            Members: [],
            AgentMessagesSinceHuman: 0,
            Budget: 0);

    /// <summary>
    /// Reads raw envelopes from <paramref name="server"/> until one of type <typeparamref name="T"/>
    /// arrives, discarding everything else - a <see cref="MessagePosted"/> Turn can write several
    /// envelope types before the one a test cares about.
    /// </summary>
    /// <typeparam name="T">The envelope type to wait for.</typeparam>
    /// <param name="server">The server to read from.</param>
    /// <param name="ct">Bounds the read.</param>
    /// <returns>The first matching envelope.</returns>
    private static Task<T> ReceiveUntilAsync<T>(FakePersonaServer server, CancellationToken ct)
        where T : ProtocolMessage =>
        ReceiveUntilAsync<T>(server, static _ => true, ct);

    /// <summary>
    /// Reads raw envelopes from <paramref name="server"/> until one of type <typeparamref name="T"/>
    /// matching <paramref name="predicate"/> arrives, discarding everything else.
    /// </summary>
    /// <typeparam name="T">The envelope type to wait for.</typeparam>
    /// <param name="server">The server to read from.</param>
    /// <param name="predicate">Which envelope of type <typeparamref name="T"/> to stop on.</param>
    /// <param name="ct">Bounds the read.</param>
    /// <returns>The first matching envelope.</returns>
    private static async Task<T> ReceiveUntilAsync<T>(FakePersonaServer server, Func<T, bool> predicate, CancellationToken ct)
        where T : ProtocolMessage
    {
        while (true)
        {
            var message = await server.ReceiveRawAsync(ct);
            if (message is T typed && predicate(typed))
            {
                return typed;
            }
        }
    }

    /// <summary>The ways a Turn can end, exercised by <see cref="EveryTurn_EndsWithAFinalDelta"/>.</summary>
    public enum TurnScenario
    {
        /// <summary>The agent replies normally.</summary>
        Success,

        /// <summary>The agent replies with only whitespace.</summary>
        EmptyReply,

        /// <summary><see cref="IAgentSession.PromptAsync"/> itself throws.</summary>
        ThrowingPrompt,

        /// <summary>The Human stops the Turn while it is still in flight.</summary>
        Stop,
    }

    /// <summary>The scenarios <see cref="EveryTurn_EndsWithAFinalDelta"/> runs as a Theory.</summary>
    /// <returns>One row per <see cref="TurnScenario"/>.</returns>
    public static TheoryData<TurnScenario> TurnScenarios() => new()
    {
        TurnScenario.Success,
        TurnScenario.EmptyReply,
        TurnScenario.ThrowingPrompt,
        TurnScenario.Stop,
    };

    private static async Task<(string AgentId, string RoomId)> WaitForDirectRoomAsync(
        PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        while (true)
        {
            var user = await directory.FindUserByNameAsync(agentName, ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
                if (room is not null)
                {
                    return (user.Id, room.Id);
                }
            }

            await Task.Delay(5, ct);
        }
    }

    private static async Task<IReadOnlyList<ChatMessage>> WaitForHistoryCountAsync(
        IChatStore store, string roomId, int count, CancellationToken ct)
    {
        while (true)
        {
            var history = await store.ReadAllAsync(roomId, ct);
            if (history.Count >= count)
            {
                return history;
            }

            await Task.Delay(5, ct);
        }
    }

    /// <summary>
    /// Polls <paramref name="store"/> until <paramref name="senderId"/> has posted at least one Message
    /// into <paramref name="roomId"/>, then returns the whole Transcript. Used by the sentinel Turns
    /// that prove a negative: the runner serialises Turns and the pipe is ordered, so when the
    /// sentinel's own reply is the first thing the Agent has said, nothing it was wrongly asked to do
    /// earlier was ever posted.
    /// </summary>
    /// <param name="store">The chat store to read.</param>
    /// <param name="roomId">The Room to read.</param>
    /// <param name="senderId">The Agent whose first Message is awaited.</param>
    /// <param name="ct">Bounds the poll.</param>
    /// <returns>The Room's Messages once <paramref name="senderId"/> has posted one.</returns>
    private static async Task<IReadOnlyList<ChatMessage>> WaitForMessageFromAsync(
        IChatStore store, string roomId, string senderId, CancellationToken ct)
    {
        while (true)
        {
            var history = await store.ReadAllAsync(roomId, ct);
            if (history.Any(m => string.Equals(m.SenderId, senderId, StringComparison.Ordinal)))
            {
                return history;
            }

            await Task.Delay(5, ct);
        }
    }

    /// <summary>
    /// Polls <paramref name="session"/>'s recorded <see cref="FakeAgentSession.Prompts"/> until at
    /// least <paramref name="count"/> have arrived, the same bounded-poll shape as
    /// <see cref="WaitForDirectRoomAsync"/> and <see cref="WaitForHistoryCountAsync"/> above: a
    /// Greeting Turn runs on <see cref="PersonaRunner"/>'s own consumer loop, concurrently with the
    /// test, so there is no single call this test can await for "the prompt has arrived" - only a
    /// condition to poll for, bounded by <paramref name="ct"/> so a runner that never queues the
    /// Greeting fails the test via cancellation rather than hanging forever.
    /// </summary>
    /// <param name="session">The fake session recording prompts.</param>
    /// <param name="count">How many prompts to wait for.</param>
    /// <param name="ct">Bounds the poll.</param>
    /// <returns>The recorded prompts, once at least <paramref name="count"/> have arrived.</returns>
    private static async Task<IReadOnlyList<string>> WaitForPromptCountAsync(FakeAgentSession session, int count, CancellationToken ct)
    {
        while (true)
        {
            var prompts = session.Prompts;
            if (prompts.Count >= count)
            {
                return prompts;
            }

            await Task.Delay(5, ct);
        }
    }

    /// <summary>
    /// A capturing <see cref="ILogger{TCategoryName}"/> fake, for the one test in this file that must
    /// assert against a log call rather than a chat side effect. Every other test in this file uses
    /// <see cref="NullLogger{T}"/>, which by design cannot be asserted against.
    /// </summary>
    /// <remarks>
    /// A <see cref="PersonaRunner"/> under test logs concurrently from three long-lived background
    /// loops - its read loop, its consumer, and its event reader - while the test thread reads
    /// <see cref="Entries"/>. A plain <see cref="List{T}"/> enumerated on one thread while another
    /// appends to it throws "Collection was modified; enumeration operation may not execute.", which
    /// is exactly the intermittent failure this fake once produced. <see cref="gate"/> serialises
    /// every write, and <see cref="Entries"/> hands back a snapshot taken under the same lock, so a
    /// caller enumerating it can never observe a background loop's in-progress mutation. Do not
    /// "simplify" this back to a bare <see cref="List{T}"/> property - that is the bug this type
    /// exists to avoid.
    /// </remarks>
    /// <typeparam name="T">The logger's category type.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly Lock gate = new();

        private readonly List<(LogLevel Level, string Message)> entries = [];

        /// <summary>
        /// An immutable snapshot of every call made to this logger so far, in call order. Each read
        /// takes a fresh copy under <see cref="gate"/>, so two calls in a row may return different
        /// snapshots if a background loop logged in between - which is expected, not a bug.
        /// </summary>
        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.entries];
                }
            }
        }

        /// <summary>Not used by this fake: scoping is irrelevant to the one test that needs it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            lock (this.gate)
            {
                this.entries.Add((logLevel, message));
            }
        }
    }

    /// <summary>
    /// A hand-rolled, single-connection ACP server, used only by
    /// <see cref="PostMessage_CarriesTheMintedMessageId"/> to read the raw <see cref="PostMessage"/>
    /// envelope a <see cref="PersonaRunner"/> writes. Every other test in this file runs the runner
    /// against a real <see cref="PipeHostFixture"/> composition root instead, because most behaviour is
    /// easiest to observe through <see cref="ChatService"/> and <see cref="IChatStore"/> - but the id a
    /// full <see cref="ChatService.PostAsync"/> ultimately persists looks identical whether or not the
    /// runner supplied one, since it mints its own when the supplied id is <see langword="null"/>. Only
    /// the wire envelope itself can prove the runner sent one.
    /// </summary>
    private sealed class FakePersonaServer : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;

        private JsonLineStream? stream;

        /// <summary>Creates the server-side pipe instance, unconnected, under a unique per-test name.</summary>
        public FakePersonaServer()
        {
            this.PipeName = "persona-runner-tests-" + Guid.NewGuid().ToString("N");
            this.pipe = new NamedPipeServerStream(
                this.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        /// <summary>The named pipe this server listens on; hand it to the runner's <see cref="TeamOptions.PipeName"/>.</summary>
        public string PipeName { get; }

        /// <summary>Starts <paramref name="runner"/>, accepts its connection, and completes the Hello/Welcome handshake.</summary>
        /// <param name="runner">The runner to start against this server.</param>
        /// <param name="ct">Cancels the accept, the handshake, and the runner's own start.</param>
        public async Task HandshakeAsync(PersonaRunner runner, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(runner);

            var startTask = runner.StartAsync(ct);
            await this.pipe.WaitForConnectionAsync(ct);

            var lineStream = new JsonLineStream(this.pipe);
            this.stream = lineStream;

            var hello = Assert.IsType<Hello>(await lineStream.ReadAsync(ct));
            await lineStream.WriteAsync(new Welcome("agent-1", hello.Name, []), ct);

            await startTask;
        }

        /// <summary>Writes one envelope to the connected runner.</summary>
        /// <param name="message">The envelope to send.</param>
        /// <param name="ct">Cancels the write.</param>
        public Task SendAsync(ProtocolMessage message, CancellationToken ct)
        {
            return this.stream!.WriteAsync(message, ct);
        }

        /// <summary>Reads the next envelope the runner writes and asserts its concrete type.</summary>
        /// <typeparam name="T">The expected envelope type.</typeparam>
        /// <param name="ct">Cancels the read.</param>
        /// <returns>The envelope, typed as <typeparamref name="T"/>.</returns>
        public async Task<T> ReceiveAsync<T>(CancellationToken ct)
            where T : ProtocolMessage
        {
            var message = await this.stream!.ReadAsync(ct);
            return Assert.IsType<T>(message);
        }

        /// <summary>Reads the next envelope the runner writes, without asserting its type.</summary>
        /// <param name="ct">Cancels the read.</param>
        /// <returns>The envelope, or <see langword="null"/> at end of stream.</returns>
        public Task<ProtocolMessage?> ReceiveRawAsync(CancellationToken ct)
        {
            return this.stream!.ReadAsync(ct);
        }

        /// <summary>Disposes the line stream if the handshake reached it, otherwise the bare pipe.</summary>
        public async ValueTask DisposeAsync()
        {
            if (this.stream is not null)
            {
                await this.stream.DisposeAsync();
            }
            else
            {
                await this.pipe.DisposeAsync();
            }
        }
    }
}