using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
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
    /// The five real chat tools' names, each carrying its full <c>mcp__team__</c> prefix, in the same
    /// order <see cref="DotAcpAgentHostFactory"/> builds them in.
    /// </summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
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
        factory.Session.EnqueueReply("should not appear");
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

        // Bounded wait: give a (misbehaving) unmentioned agent every chance to reply before asserting it
        // did not (docs/ChatRoom.md records a test that logged 4299 messages in two seconds).
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Single(history);
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
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "first");
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "second");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        var post1 = chat.PostAsync(roomId, KnownIds.Human, "one", ct: ct);
        var post2 = chat.PostAsync(roomId, KnownIds.Human, "two", ct: ct);
        await Task.WhenAll(post1, post2);

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

    [Fact]
    public async Task BlankReply_PostsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("   ");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Single(factory.Session.Prompts);

        // Bounded grace period for a (misbehaving) blank reply to be posted anyway.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        var history = await store.ReadAllAsync(roomId, ct);
        Assert.Single(history);
    }

    [Fact]
    public async Task Untagged_MessagesAreNeverSubmitted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned one", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned two", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned three", ct: ct);

        // Bounded wait: give a (misbehaving) unmentioned agent every chance to reply before asserting it
        // did not (docs/ChatRoom.md records a test that logged 4299 messages in two seconds).
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        Assert.Empty(factory.Session.Prompts);
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
        Assert.DoesNotContain("context only", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemPromptComposer_NamesEveryToolWithMcpPrefix()
    {
        var persona = new Persona("nova", "You are Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakeHookSource(), ToolNames);

        Assert.Contains("mcp__team__get_help", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__list_agents", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__create_room", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__invite_agent", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__post_message", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The canned orientation is what makes progressive discovery work: it has to say what kind of
    /// application this is, and point at get_help, before the Persona's own text begins.
    /// </summary>
    [Fact]
    public void SystemPromptComposer_OpensWithTheCannedOrientationNamingGetHelp()
    {
        var persona = new Persona("nova", "You are Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakeHookSource(), ToolNames);

        var orientation = prompt.IndexOf("chat application", StringComparison.Ordinal);
        var help = prompt.IndexOf("mcp__team__get_help", StringComparison.Ordinal);
        var personaText = prompt.IndexOf("You are Nova.", StringComparison.Ordinal);

        Assert.True(orientation >= 0 && orientation < personaText);
        Assert.True(help >= 0 && help < personaText);
    }

    [Fact]
    public void SystemPromptComposer_IncludesPersonaText()
    {
        var persona = new Persona("nova", "# Nova\nYou are a helpful assistant named Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakeHookSource(), ToolNames);

        Assert.Contains("You are a helpful assistant named Nova.", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the feature this task adds: a configured override for a hook's text reaches the composed
    /// prompt in place of <see cref="HookCatalog"/>'s default.
    /// </summary>
    [Fact]
    public void SystemPromptComposer_HookOverride_ReachesTheComposedPrompt()
    {
        var persona = new Persona("nova", "You are Nova.");
        var hooks = new FakeHookSource();
        hooks.SetOverride("systemPrompt.identity", "You are, unusually, called \"{{personaName}}\" here.");

        var prompt = SystemPromptComposer.Compose(persona, hooks, ToolNames);

        Assert.Contains("You are, unusually, called \"nova\" here.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupRoom_AtBudget_MentionedAgentTakesNoTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("should not appear");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);

        // The friend's message is itself the first - and only - agent message this Room allows, so by
        // the time it reaches Nova the Room is already spent.
        await chat.PostAsync(room.Id, friendId, "@nova hello", ct: ct);

        // Bounded wait: give a misbehaving agent every chance to reply before asserting it did not.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Single(history);
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
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

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
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova hello", ct: ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        Assert.Single(await store.ReadAllAsync(room.Id, ct));

        var extended = await chat.ExtendBudgetAsync(room.Id, ct);

        Assert.True(extended);
        var history = await WaitForHistoryCountAsync(store, room.Id, 2, ct);
        Assert.Equal("resumed", history[^1].Text);
        Assert.Equal(novaId, history[^1].SenderId);
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

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        // Two posts from the friend and one reply: the second turn never ran.
        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Equal(3, history.Count);
        Assert.DoesNotContain(history, m => m.Text == "second reply");
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

    private static PersonaRunner CreateHost(PipeHostFixture fixture, Persona persona, FakeAgentHostFactory factory)
    {
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        return new PersonaRunner(persona, options, factory, NullLogger<PersonaRunner>.Instance);
    }

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

            await Task.Delay(50, ct);
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

            await Task.Delay(50, ct);
        }
    }
}