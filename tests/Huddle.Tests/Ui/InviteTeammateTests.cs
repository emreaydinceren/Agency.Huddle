namespace Agency.Huddle.Tests.Ui;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Renders <see cref="InviteTeammate"/> on its own. The control lives behind a click on the Room
/// header, so an HTTP GET of the chat page only ever returns the prerender without it — this is the
/// layer that can see what the panel actually offers.
/// </summary>
public sealed class InviteTeammateTests
{
    [Fact]
    public async Task Panel_OffersOnlyAgentsThatAreNotAlreadyMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var inside = await directory.UpsertAgentUserAsync("inside", null, ct);
        var outside = await directory.UpsertAgentUserAsync("outside", null, ct);
        Assert.NotNull(inside);
        Assert.NotNull(outside);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(inside, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("outside", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">inside", html, StringComparison.Ordinal);
        Assert.Contains("Add teammate", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Panel_SaysSoWhenEveryAgentIsAlreadyInTheRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var only = await directory.UpsertAgentUserAsync("only", null, ct);
        Assert.NotNull(only);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(only, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("Every agent is already in this room.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Panel_StartsClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("agent", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("class=\"invite-panel\" hidden", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control and the Agent's own tool are two doors onto one behaviour. This pins that the
    /// door the Human uses ends in the same rename the Agent's does.
    /// </summary>
    [Fact]
    public async Task Invite_RenamesTheRoomAfterItsAgents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var first = await directory.UpsertAgentUserAsync("first", null, ct);
        var second = await directory.UpsertAgentUserAsync("second", null, ct);
        Assert.NotNull(first);
        Assert.NotNull(second);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(first, ct);

        await chat.InviteAsync(room.Id, "second", ct);

        var renamed = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(renamed);
        Assert.Contains("first", renamed.Name, StringComparison.Ordinal);
        Assert.Contains("second", renamed.Name, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        return new ChatService(directory, store, events, NullLogger<ChatService>.Instance);
    }

    private static async Task<string> RenderAsync(
        TempDataDir dir, ITeamDirectory directory, ChatService chat, string roomId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dir.Options());
        services.AddSingleton(directory);
        services.AddSingleton(chat);
        services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        await using var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<InviteTeammate>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["RoomId"] = roomId }));

            return output.ToHtmlString();
        });
    }
}
