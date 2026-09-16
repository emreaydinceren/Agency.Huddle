using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Agency.Huddle.App;
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
        Assert.NotNull(echo);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
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
        Assert.NotNull(echo);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
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
        Assert.NotNull(echo);
        var emily = await directory.UpsertAgentUserAsync("Emily Lee", null, ct);
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
        Assert.NotNull(echo);
        var chief = await directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
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
        Assert.NotNull(echo);
        var jarvis = await directory.UpsertAgentUserAsync("Jarvis", null, ct);
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

    /// <summary>
    /// The whole feature: a Room the Human hand-renamed away from its auto-derived name must keep that
    /// name across a later Invitation, or the rename the Human just made is thrown away on the very
    /// next <c>/invite</c>, <b>Add teammate</b>, or <c>mcp__team__invite_agent</c> call.
    /// </summary>
    [Fact]
    public async Task Invite_IntoAHandRenamedRoom_LeavesTheNameAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(alpha);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, _) = CreateService(dir, directory);
        await service.RenameRoomAsync(room.Id, "Support Squad", ct);

        var updated = await service.InviteAsync(room.Id, "alpha", ct);

        Assert.Equal("Support Squad", updated.Name);
    }

    /// <summary>An invite into a Room still carrying its auto-derived name re-derives that name.</summary>
    [Fact]
    public async Task Invite_IntoAnAutoNamedRoom_StillRenamesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(alpha);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var (service, _) = CreateService(dir, directory);

        var updated = await service.InviteAsync(room.Id, "alpha", ct);

        Assert.Equal("echo, alpha", updated.Name);
    }

    /// <summary>Renaming a Room publishes <see cref="RoomEvents.RoomsChanged"/> so the sidebar repaints.</summary>
    [Fact]
    public async Task RenameRoom_PublishesRoomsChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, events) = CreateService(dir, directory);
        var roomsChangedCount = 0;
        events.RoomsChanged += () => roomsChangedCount++;

        var updated = await service.RenameRoomAsync(room.Id, "New Name", ct);

        Assert.Equal("New Name", updated.Name);
        Assert.Equal(1, roomsChangedCount);
    }

    /// <summary>A blank name is rejected without reaching the Team Directory.</summary>
    [Fact]
    public async Task RenameRoom_BlankName_ThrowsBadMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.RenameRoomAsync(room.Id, "   ", ct));

        Assert.Equal(ErrorCodes.BadMessage, exception.Code);
    }

    /// <summary>Renaming a Room that does not exist throws <see cref="ErrorCodes.UnknownRoom"/>.</summary>
    [Fact]
    public async Task RenameRoom_UnknownRoom_ThrowsUnknownRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var (service, _) = CreateService(dir, directory);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.RenameRoomAsync("no-such-room", "New Name", ct));

        Assert.Equal(ErrorCodes.UnknownRoom, exception.Code);
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
        Assert.NotNull(echo);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
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
        Assert.NotNull(alpha);
        var beta = await directory.UpsertAgentUserAsync("beta", null, ct);
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

    [Fact]
    public async Task Post_ByAgent_IncrementsTheRoomBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory);

        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);
        await service.PostAsync(room.Id, agent.Id, "two", ct: ct);

        Assert.Equal(2, service.GetBudget(room.Id).Used);
    }

    [Fact]
    public async Task Post_ByHuman_ResetsTheRoomBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);

        await service.PostAsync(room.Id, KnownIds.Human, "hello", ct: ct);

        Assert.Equal(0, service.GetBudget(room.Id).Used);
    }

    [Fact]
    public async Task Post_ByAgentAtBudget_ThrowsBudgetExhausted_AndAppendsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 2);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);
        await service.PostAsync(room.Id, agent.Id, "two", ct: ct);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.PostAsync(room.Id, agent.Id, "three", ct: ct));

        Assert.Equal(ErrorCodes.BudgetExhausted, exception.Code);
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var stored = await store.ReadAllAsync(room.Id, ct);
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task Post_ByHumanAtBudget_IsNeverRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);

        var posted = await service.PostAsync(room.Id, KnownIds.Human, "carry on", ct: ct);

        Assert.Equal("carry on", posted.Text);
    }

    [Fact]
    public async Task Post_ByAgentAfterAHumanMessage_IsAllowedAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);
        await service.PostAsync(room.Id, KnownIds.Human, "carry on", ct: ct);

        var posted = await service.PostAsync(room.Id, agent.Id, "two", ct: ct);

        Assert.Equal("two", posted.Text);
    }

    [Fact]
    public async Task Budget_IsPerRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var first = await directory.CreateRoomAsync("one", [KnownIds.Human, agent.Id], ct);
        var second = await directory.CreateRoomAsync("two", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(first.Id, agent.Id, "spends the first room", ct: ct);

        var posted = await service.PostAsync(second.Id, agent.Id, "the second is untouched", ct: ct);

        Assert.Equal("the second is untouched", posted.Text);
        Assert.True(service.GetBudget(first.Id).Exhausted);
    }

    // A model that reads a refusal as transient retries, spending the very Turn the refusal exists to
    // save. rules.md makes the terminal wording binding, so pin it rather than trusting prose.
    [Fact]
    public async Task BudgetExhausted_MessageTellsTheAgentNotToRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);

        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.PostAsync(room.Id, agent.Id, "two", ct: ct));

        Assert.Contains("Do not retry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZeroBudget_NeverRefusesAnAgentPost()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 0);

        for (var i = 0; i < 25; i++)
        {
            await service.PostAsync(room.Id, agent.Id, $"message {i}", ct: ct);
        }

        Assert.False(service.GetBudget(room.Id).Exhausted);
    }

    [Fact]
    public async Task Post_PublishesTheBudgetOnTheEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, events) = CreateService(dir, directory, agentMessageBudget: 5);
        RoomBudget? published = null;
        events.MessagePosted += e => published = e.Budget;

        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);

        Assert.Equal(new RoomBudget(1, 5), published);
    }

    [Fact]
    public async Task GetBudget_ForARoomWithNoAgentMessages_ReturnsZeroUsed()
    {
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 7);

        var budget = service.GetBudget("a-room-nobody-has-posted-to");

        Assert.Equal(new RoomBudget(0, 7), budget);
    }

    // The test that fails if the check or the increment ever leaves the per-Room semaphore:
    // check-then-act on a ConcurrentDictionary is still a race, and the Room would then take more
    // Messages than it granted.
    [Fact]
    public async Task ConcurrentAgentPosts_StopExactlyAtTheBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 10);
        var refused = 0;

        await Parallel.ForEachAsync(Enumerable.Range(0, 50), ct, async (i, token) =>
        {
            try
            {
                await service.PostAsync(room.Id, agent.Id, $"message {i}", ct: token);
            }
            catch (ChatException ex) when (ex.Code == ErrorCodes.BudgetExhausted)
            {
                Interlocked.Increment(ref refused);
            }
        });

        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var stored = await store.ReadAllAsync(room.Id, ct);
        Assert.Equal(10, stored.Count);
        Assert.Equal(40, refused);
    }

    [Fact]
    public async Task Extend_RaisesTheGrantByOneBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 2);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);
        await service.PostAsync(room.Id, agent.Id, "two", ct: ct);

        var extended = await service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extended.Result);
        Assert.Equal(new RoomBudget(2, 4), service.GetBudget(room.Id));
    }

    [Fact]
    public async Task Extend_AllowsExactlyOneMoreBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);
        await service.ExtendBudgetAsync(room.Id, ct);

        await service.PostAsync(room.Id, agent.Id, "two", ct: ct);
        var exception = await Assert.ThrowsAsync<ChatException>(
            () => service.PostAsync(room.Id, agent.Id, "three", ct: ct));

        Assert.Equal(ErrorCodes.BudgetExhausted, exception.Code);
    }

    // The re-delivery is the whole of the extension: raising the allowance alone changes a number
    // nothing reads, because a Turn only ever begins with a delivered Message.
    [Fact]
    public async Task Extend_RepublishesTheLastMessageForRedeliveryOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, events) = CreateService(dir, directory, agentMessageBudget: 1);
        var last = await service.PostAsync(room.Id, agent.Id, "the message that spent it", ct: ct);
        var posted = new List<string>();
        var redelivered = new List<MessagePostedEvent>();
        events.MessagePosted += e => posted.Add(e.Message.Id);
        events.MessageRedelivered += redelivered.Add;

        await service.ExtendBudgetAsync(room.Id, ct);

        Assert.Empty(posted);
        var only = Assert.Single(redelivered);
        Assert.Equal(last.Id, only.Message.Id);
        Assert.Equal(new RoomBudget(1, 2), only.Budget);
    }

    // The Mentions are re-parsed rather than remembered, so whoever the Message woke the first time is
    // woken again - otherwise Continue would resume the Room but not the conversation.
    [Fact]
    public async Task Extend_PreservesTheOriginalMentions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var author = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(author);
        var mentioned = await directory.UpsertAgentUserAsync("Jarvis", null, ct);
        Assert.NotNull(mentioned);
        var room = await directory.CreateRoomAsync("echo, Jarvis", [KnownIds.Human, author.Id, mentioned.Id], ct);
        var (service, events) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(room.Id, author.Id, "over to you @Jarvis", ct: ct);
        MessagePostedEvent? redelivered = null;
        events.MessageRedelivered += e => redelivered = e;

        await service.ExtendBudgetAsync(room.Id, ct);

        Assert.NotNull(redelivered);
        var only = Assert.Single(redelivered.Mentions);
        Assert.Equal(mentioned.Id, only.Id);
    }

    /// <summary>
    /// The caller is handed the same delivery the Agents received: <see cref="BudgetExtension.Redelivered"/>
    /// is built from the same Members and sender as the re-published Message, not assembled separately.
    /// </summary>
    [Fact]
    public async Task Extend_ReturnsTheFactsItPublished()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        var last = await service.PostAsync(room.Id, agent.Id, "the message that spent it", ct: ct);

        var extended = await service.ExtendBudgetAsync(room.Id, ct);

        Assert.NotNull(extended.Redelivered);
        Assert.Equal(last.SenderId, extended.Redelivered.SenderId);
        Assert.Equal(2, extended.Redelivered.Members.Count);
    }

    // A second click must grant nothing, or the prompt becomes a way to spend without deciding to.
    [Fact]
    public async Task Extend_OnARoomThatIsNotPaused_DoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 5);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);

        var extended = await service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.NotPaused, extended.Result);
        Assert.Equal(new RoomBudget(1, 5), service.GetBudget(room.Id));
    }

    [Fact]
    public async Task Extend_OnAnEmptyRoom_DoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);

        var extended = await service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.NothingToRedeliver, extended.Result);
    }

    /// <summary>
    /// The <see cref="ExtendResult.NothingToRedeliver"/> return still carries the Room's Budget, read
    /// before the per-Room semaphore rather than left at a default - guards the explicit
    /// <c>CurrentBudget</c> call on that early return, which nothing else in this file exercises.
    /// </summary>
    [Fact]
    public async Task Extend_WithNothingToRedeliver_StillReportsTheBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 7);

        var extended = await service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.NothingToRedeliver, extended.Result);
        Assert.Equal(new RoomBudget(0, 7), extended.Budget);
    }

    // An extension is granted for one unattended run. Speaking ends that run, so the next one starts
    // from the configured Budget rather than from whatever the Human last allowed.
    [Fact]
    public async Task HumanMessage_ResetsTheGrantToOneBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, agent.Id], ct);
        var (service, _) = CreateService(dir, directory, agentMessageBudget: 1);
        await service.PostAsync(room.Id, agent.Id, "one", ct: ct);
        await service.ExtendBudgetAsync(room.Id, ct);

        await service.PostAsync(room.Id, KnownIds.Human, "hello", ct: ct);

        Assert.Equal(new RoomBudget(0, 1), service.GetBudget(room.Id));
    }

    // Defaults to the production Budget so every test written before it stays a test about something
    // else; the Budget's own tests pass a small number so they do not have to post forty Messages.
    private static (ChatService Service, RoomEvents Events) CreateService(
        TempDataDir dir,
        ITeamDirectory directory,
        IReadOnlyList<MentionAlias>? aliases = null,
        int agentMessageBudget = 40)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var aliasSource = new FakeMentionAliasSource { Aliases = aliases ?? [] };
        var options = Options.Create(new TeamOptions { AgentMessageBudget = agentMessageBudget });
        var service = new ChatService(directory, store, events, aliasSource, options, NullLogger<ChatService>.Instance);
        return (service, events);
    }
}