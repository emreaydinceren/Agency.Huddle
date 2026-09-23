namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

public sealed class CreateRoomToolTests
{
    [Fact]
    public async Task CreateRoom_ContainsCallerHumanAndNamedAgents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "alpha" } };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Created room", result, StringComparison.Ordinal);
        var rooms = await directory.GetRoomsAsync(ct);
        var room = Assert.Single(rooms);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.Id == KnownIds.Human);
        Assert.Contains(members, m => m.Id == caller.Id);
        Assert.Contains(members, m => m.Id == alpha.Id);
    }

    [Fact]
    public async Task CreateRoom_ResolvesAnAliasToItsOwningAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var jarvis = await directory.UpsertAgentUserAsync("Jarvis", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(jarvis);
        var aliasSource = new FakeMentionAliasSource { Aliases = [new MentionAlias("jar", "Jarvis")] };
        var chat = CreateChatService(dir, directory, aliasSource);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "jar" } };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Created room", result, StringComparison.Ordinal);
        var rooms = await directory.GetRoomsAsync(ct);
        var room = Assert.Single(rooms);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, m => m.Id == jarvis.Id);
    }

    [Fact]
    public async Task CreateRoom_UnknownAgent_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "nobody" } };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Unknown agent", result, StringComparison.Ordinal);
        Assert.Contains("alpha", result, StringComparison.Ordinal);
        var rooms = await directory.GetRoomsAsync(ct);
        Assert.Empty(rooms);
    }

    [Fact]
    public async Task CreateRoom_MissingArgument_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("agents", result, StringComparison.Ordinal);
        var rooms = await directory.GetRoomsAsync(ct);
        Assert.Empty(rooms);
    }

    /// <summary>
    /// ADR-0005: a <c>seed</c> passed to <c>create_room</c> is posted into the new Room, as the calling
    /// Agent, in the same call that creates it, so the named agents learn why they are there without a
    /// second tool call.
    /// </summary>
    [Fact]
    public async Task CreateRoom_WithSeed_PostsItAsTheOpeningMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "alpha" }, ["seed"] = "Let's figure out the release notes." };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Created room", result, StringComparison.Ordinal);
        Assert.Contains("seed message", result, StringComparison.Ordinal);
        var room = Assert.Single(await directory.GetRoomsAsync(ct));
        var transcript = await store.ReadAllAsync(room.Id, ct);
        var message = Assert.Single(transcript);
        Assert.Equal("Let's figure out the release notes.", message.Text);
        Assert.Equal(caller.Id, message.SenderId);
    }

    /// <summary>Omitting <c>seed</c> leaves the newly created Room with no transcript at all.</summary>
    [Fact]
    public async Task CreateRoom_WithoutSeed_LeavesTheRoomEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "alpha" } };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Created room", result, StringComparison.Ordinal);
        Assert.DoesNotContain("seed message", result, StringComparison.Ordinal);
        var room = Assert.Single(await directory.GetRoomsAsync(ct));
        var transcript = await store.ReadAllAsync(room.Id, ct);
        Assert.Empty(transcript);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory, IMentionAliasSource aliasSource)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        return new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);
    }
}