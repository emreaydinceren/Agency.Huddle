namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

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
        var chat = CreateChatService(dir, directory);
        var tool = new CreateRoomTool(chat, directory, caller.Id);
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
        var chat = CreateChatService(dir, directory);
        var tool = new CreateRoomTool(chat, directory, caller.Id);
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
        var chat = CreateChatService(dir, directory);
        var tool = new CreateRoomTool(chat, directory, caller.Id);

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("agents", result, StringComparison.Ordinal);
        var rooms = await directory.GetRoomsAsync(ct);
        Assert.Empty(rooms);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        return new ChatService(directory, store, events, NullLogger<ChatService>.Instance);
    }
}