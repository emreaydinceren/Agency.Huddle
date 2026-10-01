namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

public sealed class PostMessageToolTests
{
    [Fact]
    public async Task PostMessage_AppendsToTheTranscript()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
        var tool = new PostMessageTool(chat, echo.Id, new FakePromptSource(), new OwnPosts(Options.Create(new TeamOptions())));
        var arguments = new JsonObject { ["roomId"] = room.Id, ["text"] = "hello there" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Posted", result, StringComparison.Ordinal);
        var stored = await store.ReadAllAsync(room.Id, ct);
        var message = Assert.Single(stored);
        Assert.Equal("hello there", message.Text);
        Assert.Equal(echo.Id, message.SenderId);
    }

    [Fact]
    public async Task PostMessage_NonMemberRoom_ReturnsErrorText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
        var tool = new PostMessageTool(chat, echo.Id, new FakePromptSource(), new OwnPosts(Options.Create(new TeamOptions())));
        var arguments = new JsonObject { ["roomId"] = room.Id, ["text"] = "hello" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("Could not post", result, StringComparison.Ordinal);
        var stored = await store.ReadAllAsync(room.Id, ct);
        Assert.Empty(stored);
    }

    /// <summary>A successful post records the text in <see cref="OwnPosts"/> under the caller's id and Room.</summary>
    [Fact]
    public async Task Invoke_Success_RecordsOwnPost()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        var tool = new PostMessageTool(chat, echo.Id, new FakePromptSource(), ownPosts);
        var arguments = new JsonObject { ["roomId"] = room.Id, ["text"] = "hello there" };

        await tool.InvokeAsync(arguments, ct);

        Assert.Equal(["hello there"], ownPosts.Take(echo.Id, room.Id));
    }

    /// <summary>A refused post (a non-Member Room) records nothing in <see cref="OwnPosts"/>.</summary>
    [Fact]
    public async Task Invoke_Refused_RecordsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
        var ownPosts = new OwnPosts(Options.Create(new TeamOptions()));
        var tool = new PostMessageTool(chat, echo.Id, new FakePromptSource(), ownPosts);
        var arguments = new JsonObject { ["roomId"] = room.Id, ["text"] = "hello" };

        await tool.InvokeAsync(arguments, ct);

        Assert.Empty(ownPosts.Take(echo.Id, room.Id));
    }

    [Fact]
    public async Task PostMessage_FromToolThread_ReachesRoomEventsSubscribers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        var chat = new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
        var tool = new PostMessageTool(chat, echo.Id, new FakePromptSource(), new OwnPosts(Options.Create(new TeamOptions())));
        var arguments = new JsonObject { ["roomId"] = room.Id, ["text"] = "from a threadpool thread" };
        var tcs = new TaskCompletionSource<MessagePostedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.MessagePosted += e => tcs.TrySetResult(e);

        var toolThread = Task.Run(
            async () =>
            {
                Assert.Null(SynchronizationContext.Current);
                await tool.InvokeAsync(arguments, ct);
            },
            ct);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5), ct));
        await toolThread;

        Assert.Same(tcs.Task, completed);
        var published = await tcs.Task;
        Assert.Equal(room.Id, published.Room.Id);
        Assert.Equal("from a threadpool thread", published.Message.Text);
    }
}