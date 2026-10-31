namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Covers roadmap item 8's <c>follow_room</c> tool: asking to be woken by every Message in a Room
/// without being Mentioned in each one.
/// </summary>
public sealed class FollowRoomToolTests
{
    /// <summary>A Member of the Room can follow it, and the follow is recorded in <see cref="RoomFollows"/> itself.</summary>
    [Fact]
    public async Task FollowRoom_CallerIsMember_StartsFollowingAndSaysSo()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var follows = new RoomFollows();
        var tool = new FollowRoomTool(follows, directory, caller.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Now following", result, StringComparison.Ordinal);
        Assert.Contains(room.Name, result, StringComparison.Ordinal);
        Assert.True(follows.IsFollowing(caller.Id, room.Id));
    }

    /// <summary>Following a Room a second time is a no-op that says so, and does not change the recorded state.</summary>
    [Fact]
    public async Task FollowRoom_AlreadyFollowing_SaysSoWithoutChangingState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
        var room = await chat.EnsureRoomForAsync(caller, ct);
        var follows = new RoomFollows();
        follows.Follow(caller.Id, room.Id);
        var tool = new FollowRoomTool(follows, directory, caller.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Already following", result, StringComparison.Ordinal);
        Assert.True(follows.IsFollowing(caller.Id, room.Id));
    }

    /// <summary>An unknown Room id is reported as text, never thrown, and records no follow.</summary>
    [Fact]
    public async Task FollowRoom_UnknownRoom_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var follows = new RoomFollows();
        var tool = new FollowRoomTool(follows, directory, caller.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = "no-such-room" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Unknown room", result, StringComparison.Ordinal);
        Assert.False(follows.IsFollowing(caller.Id, "no-such-room"));
    }

    /// <summary>
    /// A caller that is not a Member of the target Room is refused, because delivery only ever reaches
    /// Members and the follow would otherwise silently do nothing.
    /// </summary>
    [Fact]
    public async Task FollowRoom_CallerNotAMember_RefusesAndExplainsWhy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var owner = await directory.UpsertAgentUserAsync("owner", null, ct);
        var outsider = await directory.UpsertAgentUserAsync("outsider", null, ct);
        Assert.NotNull(owner);
        Assert.NotNull(outsider);
        var aliasSource = new FakeMentionAliasSource();
        var chat = ChatServiceFactory.Create(directory, dir.Options(), aliases: aliasSource).Chat;
        var room = await chat.EnsureRoomForAsync(owner, ct);
        var follows = new RoomFollows();
        var tool = new FollowRoomTool(follows, directory, outsider.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("not a member", result, StringComparison.Ordinal);
        Assert.False(follows.IsFollowing(outsider.Id, room.Id));
    }

    /// <summary>A missing <c>roomId</c> argument returns error text rather than throwing.</summary>
    [Fact]
    public async Task FollowRoom_MissingArgument_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var follows = new RoomFollows();
        var tool = new FollowRoomTool(follows, directory, "caller-id", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("required", result, StringComparison.Ordinal);
    }

    /// <summary>A <c>roomId</c> that is not JSON text is refused with the <c>'roomId' must be text.</c> refusal rather than throwing an <see cref="InvalidCastException"/>.</summary>
    [Fact]
    public async Task FollowRoom_NonStringRoomId_RefusesAsText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var tool = new FollowRoomTool(new RoomFollows(), directory, "caller-id", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["roomId"] = 42 }, ct);

        Assert.Equal("'roomId' must be text.", result);
    }
}