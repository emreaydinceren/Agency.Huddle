namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Covers the Invitation issued by an Agent. The Human's two routes into the same behaviour — the
/// <c>/invite</c> command and the Add teammate control — are covered where they live.
/// </summary>
public sealed class InviteAgentToolTests
{
    [Fact]
    public async Task InviteAgent_AddsTheAgentAndRenamesTheRoom()
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
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id, ["agent"] = "alpha" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Invited alpha", result, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.Id == alpha.Id);

        // A Room is named after its Agents however the Invitation was issued.
        var renamed = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(renamed);
        Assert.Contains("alpha", renamed.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InviteAgent_ResolvesANameContainingSpaces()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var chief = await directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(chief);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id, ["agent"] = "Chief of Staff" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Invited Chief of Staff", result, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, m => m.Id == chief.Id);
    }

    [Fact]
    public async Task InviteAgent_ResolvesAnAliasToItsOwningAgent()
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
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id, ["agent"] = "jar" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Invited Jarvis", result, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, m => m.Id == jarvis.Id);
    }

    [Fact]
    public async Task InviteAgent_UnknownAgent_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id, ["agent"] = "nobody" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Unknown agent", result, StringComparison.Ordinal);
        Assert.Contains("caller", result, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task InviteAgent_UnknownAlias_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        var jarvis = await directory.UpsertAgentUserAsync("Jarvis", null, ct);
        Assert.NotNull(caller);
        Assert.NotNull(jarvis);

        // A frontmatter Alias that resolves to no live Agent (deleted, or never registered) must
        // fall through to the same helpful listing as any other unknown handle, never an exception.
        var aliasSource = new FakeMentionAliasSource { Aliases = [new MentionAlias("ghost", "Nobody")] };
        var chat = CreateChatService(dir, directory, aliasSource);
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id, ["agent"] = "ghost" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Unknown agent", result, StringComparison.Ordinal);
        Assert.Contains("Jarvis", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InviteAgent_UnknownRoom_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(alpha);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = "no-such-room", ["agent"] = "alpha" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Could not invite", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InviteAgent_AlreadyAMember_SaysSoWithoutChangingTheRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id, ["agent"] = "caller" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("already a member", result, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task InviteAgent_MissingArgument_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
        var tool = new InviteAgentTool(chat, directory, aliasSource, new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["roomId"] = "some-room" }, ct);

        Assert.Contains("required", result, StringComparison.Ordinal);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory, IMentionAliasSource aliasSource)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        return new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), NullLogger<ChatService>.Instance);
    }
}