namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>Covers roadmap item 8's <c>unfollow_room</c> tool: clearing a follow started by <c>follow_room</c>.</summary>
public sealed class UnfollowRoomToolTests
{
    /// <summary>A followed Room is unfollowed, and the state change is reflected on <see cref="RoomFollows"/> itself.</summary>
    [Fact]
    public async Task UnfollowRoom_WasFollowing_StopsFollowingAndSaysSo()
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
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var caller = await directory.UpsertAgentUserAsync("caller", null, ct);
        Assert.NotNull(caller);
        var aliasSource = new FakeMentionAliasSource();
        var chat = CreateChatService(dir, directory, aliasSource);
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
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
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
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var follows = new RoomFollows();
        var tool = new UnfollowRoomTool(follows, directory, "caller-id", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("required", result, StringComparison.Ordinal);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory, IMentionAliasSource aliasSource)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        return new ChatService(directory, store, events, aliasSource, Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
    }
}
