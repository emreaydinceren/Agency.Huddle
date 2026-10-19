namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

public sealed class CreateRoomToolTests
{
    [Fact]
    public async Task CreateRoom_ContainsCallerHumanAndNamedAgents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var jarvis = await directory.UpsertAgentUserAsync("Jarvis", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(jarvis);
        var aliasSource = new FakeMentionAliasSource { Aliases = [new MentionAlias("jar", "Jarvis")] };
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var (chat, store, _, _, _) = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource);
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var (chat, store, _, _, _) =ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource);
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "alpha" } };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Created room", result, StringComparison.Ordinal);
        Assert.DoesNotContain("seed message", result, StringComparison.Ordinal);
        var room = Assert.Single(await directory.GetRoomsAsync(ct));
        var transcript = await store.ReadAllAsync(room.Id, ct);
        Assert.Empty(transcript);
    }

    /// <summary>A <c>seed</c> that is not JSON text is refused with the <c>'seed' must be text.</c> refusal, and no Room is half-created first.</summary>
    [Fact]
    public async Task CreateRoom_NonStringSeed_RefusesAsTextAndCreatesNoRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
        var tool = new CreateRoomTool(chat, directory, caller.Id, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["agents"] = new JsonArray { "alpha" }, ["seed"] = 42 };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("'seed' must be text.", result);
        Assert.Empty(await directory.GetRoomsAsync(ct));
    }
}