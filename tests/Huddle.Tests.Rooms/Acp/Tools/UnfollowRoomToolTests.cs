namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

/// <summary>Covers roadmap item 8's <c>unfollow_room</c> tool: clearing a follow started by <c>follow_room</c>.</summary>
public sealed class UnfollowRoomToolTests
{
    /// <summary>A followed Room is unfollowed, and the state change is reflected on <see cref="RoomFollows"/> itself.</summary>
    [Fact]
    public async Task UnfollowRoom_WasFollowing_StopsFollowingAndSaysSo()
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
        var tool = new UnfollowRoomTool(follows, directory, caller.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("No longer following", result, StringComparison.Ordinal);
        Assert.False(follows.IsFollowing(caller.Id, room.Id));
    }

    /// <summary>Unfollowing a Room never followed is a no-op that says so.</summary>
    [Fact]
    public async Task UnfollowRoom_WasNotFollowing_SaysSoWithoutChangingState()
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
        var tool = new UnfollowRoomTool(follows, directory, caller.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = room.Id };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("were not following", result, StringComparison.Ordinal);
        Assert.False(follows.IsFollowing(caller.Id, room.Id));
    }

    /// <summary>An unknown Room id is reported as text, never thrown.</summary>
    [Fact]
    public async Task UnfollowRoom_UnknownRoom_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var follows = new RoomFollows();
        var tool = new UnfollowRoomTool(follows, directory, caller.Id, new FakePromptSource());
        var arguments = new JsonObject { ["roomId"] = "no-such-room" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Unknown room", result, StringComparison.Ordinal);
    }

    /// <summary>A missing <c>roomId</c> argument returns error text rather than throwing.</summary>
    [Fact]
    public async Task UnfollowRoom_MissingArgument_ReturnsErrorTextNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var follows = new RoomFollows();
        var tool = new UnfollowRoomTool(follows, directory, "caller-id", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("required", result, StringComparison.Ordinal);
    }

    /// <summary>A <c>roomId</c> that is not JSON text is refused with the <c>'roomId' must be text.</c> refusal rather than throwing an <see cref="InvalidCastException"/>.</summary>
    [Fact]
    public async Task UnfollowRoom_NonStringRoomId_RefusesAsText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var tool = new UnfollowRoomTool(new RoomFollows(), directory, "caller-id", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["roomId"] = 42 }, ct);

        Assert.Equal("'roomId' must be text.", result);
    }
}