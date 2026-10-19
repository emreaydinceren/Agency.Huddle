namespace Agency.Huddle.Tests.Acp.Tools;

using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

public sealed class PostMessageToolTests
{
    [Fact]
    public async Task PostMessage_AppendsToTheTranscript()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (chat, store, _, _, _) = ChatServiceFactory.Create(directory, dir.Options());
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (chat, store, _, _, _) = ChatServiceFactory.Create(directory, dir.Options());
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (chat, _, _, _, _) = ChatServiceFactory.Create(directory, dir.Options());
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (chat, _, _, _, _) = ChatServiceFactory.Create(directory, dir.Options());
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
        var directory = await dir.CreateDirectoryAsync(ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (chat, _, events, _, _) = ChatServiceFactory.Create(directory, dir.Options());
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

    /// <summary>An argument that is not JSON text is refused with the <c>'x' must be text.</c> refusal, naming it, rather than throwing an <see cref="InvalidCastException"/>.</summary>
    /// <param name="wrongArgument">The argument sent as a number.</param>
    [Theory]
    [InlineData("roomId")]
    [InlineData("text")]
    public async Task PostMessage_NonStringArgument_RefusesAsText(string wrongArgument)
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = await dir.CreateDirectoryAsync(ct);
        var (chat, _, _, _, _) = ChatServiceFactory.Create(directory, dir.Options());
        var tool = new PostMessageTool(chat, "echo-id", new FakePromptSource(), new OwnPosts(Options.Create(new TeamOptions())));
        var arguments = new JsonObject { ["roomId"] = "some-room", ["text"] = "hello" };
        arguments[wrongArgument] = 42;

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"'{wrongArgument}' must be text.", result);
    }
}