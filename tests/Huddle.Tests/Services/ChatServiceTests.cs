using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task EnsureDirectRoom_CreatesOnce_NamedAfterAgent_AndRaisesRoomsChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var (service, events) = CreateService(dir, directory);
        var roomsChangedCount = 0;
        events.RoomsChanged += () => roomsChangedCount++;

        var first = await service.EnsureRoomForAsync(agent, ct);
        var second = await service.EnsureRoomForAsync(agent, ct);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("echo", first.Name);
        var members = await directory.GetRoomMembersAsync(first.Id, ct);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.Id == KnownIds.Human);
        Assert.Contains(members, m => m.Id == agent.Id);
        Assert.Equal(1, roomsChangedCount);
    }

    [Fact]
    public async Task Post_AppendsToStore_AndPublishesEvent_WithMembersAndMentions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, events) = CreateService(dir, directory);
        MessagePostedEvent? published = null;
        events.MessagePosted += e => published = e;

        var message = await service.PostAsync(room.Id, KnownIds.Human, "hi @echo", ct: ct);

        Assert.Equal("hi @echo", message.Text);
        var stored = await new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance).ReadAllAsync(room.Id, ct);
        Assert.Single(stored);
        Assert.Equal(message.Id, stored[0].Id);
        Assert.NotNull(published);
        Assert.Equal(room.Id, published.Room.Id);
        Assert.Equal(2, published.Members.Count);
        Assert.Single(published.Mentions);
        Assert.Equal(echo.Id, published.Mentions[0].Id);
    }

    [Fact]
    public async Task Post_MentioningOneOfTwoAgents_MentionsOnlyThatAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id, alpha.Id], ct);
        var (service, events) = CreateService(dir, directory);
        MessagePostedEvent? published = null;
        events.MessagePosted += e => published = e;

        await service.PostAsync(room.Id, KnownIds.Human, "hi @echo", ct: ct);

        Assert.NotNull(published);
        Assert.Single(published.Mentions);
        Assert.Equal(echo.Id, published.Mentions[0].Id);
    }

    [Fact]
    public async Task Post_ByNonMember_ThrowsNotMember()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.PostAsync(room.Id, "not-a-member", "hi", ct: ct));

        Assert.Equal(ErrorCodes.NotMember, exception.Code);
        var stored = await new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance).ReadAllAsync(room.Id, ct);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task Post_ToUnknownRoom_ThrowsUnknownRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.PostAsync("no-such-room", KnownIds.Human, "hi", ct: ct));

        Assert.Equal(ErrorCodes.UnknownRoom, exception.Code);
    }

    [Fact]
    public async Task Post_EmptyText_ThrowsBadMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.PostAsync(room.Id, KnownIds.Human, "   ", ct: ct));

        Assert.Equal(ErrorCodes.BadMessage, exception.Code);
    }

    [Fact]
    public async Task Post_WithSuppliedId_KeepsId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var message = await service.PostAsync(room.Id, KnownIds.Human, "hi", "custom-id", ct);

        Assert.Equal("custom-id", message.Id);
    }

    [Fact]
    public async Task Post_WithoutId_GeneratesUniqueIds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var ids = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var message = await service.PostAsync(room.Id, KnownIds.Human, $"hi {i}", ct: ct);
            ids.Add(message.Id);
        }

        Assert.Equal(10, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Equal(32, id.Length));
    }

    [Fact]
    public async Task Submit_PlainText_PostsAsHuman()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var result = await service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "hello there", ct);

        Assert.NotNull(result.Posted);
        Assert.Equal(KnownIds.Human, result.Posted.SenderId);
        Assert.Equal("You", result.Posted.SenderName);
        Assert.Null(result.Info);
    }

    [Fact]
    public async Task Submit_Invite_AddsMember_RenamesRoom_RaisesRoomsChanged_PersistsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, events) = CreateService(dir, directory);
        var roomsChangedCount = 0;
        events.RoomsChanged += () => roomsChangedCount++;

        var result = await service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/invite @alpha", ct);

        Assert.Null(result.Posted);
        Assert.NotNull(result.Info);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        var updatedRoom = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updatedRoom);
        Assert.Equal("echo, alpha", updatedRoom.Name);
        Assert.Equal(1, roomsChangedCount);
        var stored = await new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance).ReadAllAsync(room.Id, ct);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task Submit_Invite_ExistingMember_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, _) = CreateService(dir, directory);

        var result = await service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/invite @echo", ct);

        Assert.Null(result.Posted);
        Assert.NotNull(result.Info);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(2, members.Count);
        var updatedRoom = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updatedRoom);
        Assert.Equal("echo", updatedRoom.Name);
    }

    [Fact]
    public async Task Submit_Invite_NameWithSpaces_IsNotTruncated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var emily = await directory.UpsertAgentUserAsync("Emily Lee", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(emily);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, _) = CreateService(dir, directory);

        // The command used to capture the Name by shape, which stopped at the space and then
        // reported "Emily" as an unknown agent.
        var result = await service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/invite @Emily Lee", ct);

        Assert.Null(result.Posted);
        Assert.NotNull(result.Info);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        var updatedRoom = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updatedRoom);
        Assert.Equal("echo, Emily Lee", updatedRoom.Name);
    }

    [Fact]
    public async Task Submit_Invite_WithoutTheAtSign_AlsoAcceptsSpaces()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var chief = await directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(chief);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, _) = CreateService(dir, directory);

        var result = await service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/invite Chief of Staff", ct);

        Assert.NotNull(result.Info);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
    }

    /// <summary>
    /// Phase 4: <c>/invite</c> resolves a Persona's Alias the same way <see cref="MentionParser"/>
    /// does. <see cref="ChatService.InviteAsync"/> only reaches its Alias fallback after
    /// <see cref="ITeamDirectory.FindUserByNameAsync"/> misses on the typed handle, so this proves the
    /// whole path end to end - the fallback firing, resolving to the real Name, and adding the right
    /// Agent to the Room - rather than just the fallback's own resolution logic in isolation.
    /// </summary>
    [Fact]
    public async Task Submit_Invite_ByAlias_ResolvesToTheOwningAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var jarvis = await directory.UpsertAgentUserAsync("Jarvis", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(jarvis);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, events) = CreateService(dir, directory, [new MentionAlias("jar", "Jarvis")]);
        var roomsChangedCount = 0;
        events.RoomsChanged += () => roomsChangedCount++;

        var result = await service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/invite @jar", ct);

        Assert.Null(result.Posted);
        Assert.NotNull(result.Info);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.Id == jarvis.Id);
        var updatedRoom = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updatedRoom);
        Assert.Equal("echo, Jarvis", updatedRoom.Name);
        Assert.Equal(1, roomsChangedCount);
    }

    [Fact]
    public async Task Submit_Invite_UnknownAgent_ThrowsBadMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/invite @nobody", ct));

        Assert.Equal(ErrorCodes.BadMessage, exception.Code);
        Assert.Contains("@nobody", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_UnknownCommand_ThrowsBadMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.SubmitFromComposerAsync(room.Id, KnownIds.Human, "/frobnicate", ct));

        Assert.Equal(ErrorCodes.BadMessage, exception.Code);
    }

    [Fact]
    public async Task ConcurrentPosts_TranscriptOrderMatchesEventOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, events) = CreateService(dir, directory);
        var publishedIds = new ConcurrentQueue<string>();
        events.MessagePosted += e => publishedIds.Enqueue(e.Message.Id);

        await Parallel.ForEachAsync(Enumerable.Range(0, 50), ct, async (i, token) =>
        {
            await service.PostAsync(room.Id, KnownIds.Human, $"message {i}", ct: token);
        });

        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var stored = await store.ReadAllAsync(room.Id, ct);

        Assert.Equal(50, stored.Count);
        Assert.Equal(50, publishedIds.Count);
        Assert.Equal(stored.Select(m => m.Id), publishedIds);
    }

    [Fact]
    public async Task DirectRoom_AfterInvite_IsRecreatedOnNextEnsure()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);
        var (service, _) = CreateService(dir, directory);
        var firstRoom = await service.EnsureRoomForAsync(echo, ct);
        await service.SubmitFromComposerAsync(firstRoom.Id, KnownIds.Human, "/invite @alpha", ct);

        var secondRoom = await service.EnsureRoomForAsync(echo, ct);

        Assert.NotEqual(firstRoom.Id, secondRoom.Id);
        Assert.Equal("echo", secondRoom.Name);
    }

    [Fact]
    public async Task CreateGroupRoom_CreatesRoomWithHumanAndAgents_NamedAfterAgents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        var beta = await directory.UpsertAgentUserAsync("beta", null, ct);
        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        var (service, events) = CreateService(dir, directory);
        var roomsChangedCount = 0;
        events.RoomsChanged += () => roomsChangedCount++;

        var room = await service.CreateRoomForAsync([alpha.Id, beta.Id], ct);

        Assert.Equal("alpha, beta", room.Name);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.Id == KnownIds.Human);
        Assert.Contains(members, m => m.Id == alpha.Id);
        Assert.Contains(members, m => m.Id == beta.Id);
        Assert.Equal(1, roomsChangedCount);
    }

    [Fact]
    public async Task CreateGroupRoom_SingleAgent_ReusesDirectRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var (service, _) = CreateService(dir, directory);

        var first = await service.CreateRoomForAsync([echo.Id], ct);
        var second = await service.CreateRoomForAsync([echo.Id], ct);

        Assert.Equal(first.Id, second.Id);
        var rooms = await directory.GetRoomsAsync(ct);
        Assert.Single(rooms);
    }

    [Fact]
    public async Task CreateGroupRoom_UnknownAgent_ThrowsBadMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.CreateRoomForAsync(["no-such-id"], ct));

        Assert.Equal(ErrorCodes.BadMessage, exception.Code);
    }

    [Fact]
    public async Task CreateGroupRoom_EmptyList_ThrowsBadMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.CreateRoomForAsync([], ct));

        Assert.Equal(ErrorCodes.BadMessage, exception.Code);
        Assert.Contains("select at least one agent", exception.Message, StringComparison.Ordinal);
    }

    private static (ChatService Service, RoomEvents Events) CreateService(
        TempDataDir dir, ITeamDirectory directory, IReadOnlyList<MentionAlias>? aliases = null)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var aliasSource = new FakeMentionAliasSource { Aliases = aliases ?? [] };
        var service = new ChatService(directory, store, events, aliasSource, NullLogger<ChatService>.Instance);
        return (service, events);
    }
}