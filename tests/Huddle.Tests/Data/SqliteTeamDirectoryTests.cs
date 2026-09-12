using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Data;

public sealed class SqliteTeamDirectoryTests
{
    [Fact]
    public async Task Initialize_IsIdempotent_AndSeedsHuman()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        await directory.InitializeAsync("Someone Else", ct);

        var human = await directory.GetHumanAsync(ct);
        Assert.Equal(KnownIds.Human, human.Id);
        Assert.Equal("Someone Else", human.Name);

        var users = await directory.GetUsersAsync(ct);
        Assert.Single(users);
    }

    [Fact]
    public async Task UpsertAgent_ReturnsAgentUser_WithGeneratedId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var agent = await directory.UpsertAgentUserAsync("echo", "an echo agent", ct);

        Assert.NotNull(agent);
        Assert.Equal(UserKind.Agent, agent.Kind);
        Assert.False(string.IsNullOrWhiteSpace(agent.Id));
        Assert.Equal("an echo agent", agent.Description);
    }

    [Fact]
    public async Task UpsertAgent_SameNameDifferentCase_ReturnsSameId_UpdatesDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var first = await directory.UpsertAgentUserAsync("Echo", "first description", ct);
        var second = await directory.UpsertAgentUserAsync("echo", "second description", ct);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("second description", second.Description);
    }

    [Fact]
    public async Task UpsertAgent_WithHumanName_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var result = await directory.UpsertAgentUserAsync("You", null, ct);

        Assert.Null(result);
        var human = await directory.GetHumanAsync(ct);
        Assert.Equal("You", human.Name);
        Assert.Equal(UserKind.Human, human.Kind);
    }

    [Fact]
    public async Task FindUserByName_IsCaseInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(agent);

        var found = await directory.FindUserByNameAsync("ECHO", ct);

        Assert.NotNull(found);
        Assert.Equal(agent.Id, found.Id);
    }

    [Fact]
    public async Task CreateRoom_AddMember_GetMembers_InInsertionOrder()
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
        await directory.AddMemberAsync(room.Id, alpha.Id, ct);

        var members = await directory.GetRoomMembersAsync(room.Id, ct);

        Assert.Equal(3, members.Count);
        Assert.Equal(KnownIds.Human, members[0].Id);
        Assert.Equal(echo.Id, members[1].Id);
        Assert.Equal(alpha.Id, members[2].Id);
    }

    [Fact]
    public async Task AddMember_Twice_ReturnsFalseSecondTime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);

        var first = await directory.AddMemberAsync(room.Id, echo.Id, ct);
        var second = await directory.AddMemberAsync(room.Id, echo.Id, ct);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task GetRoomsForUser_ReturnsOnlyMemberRooms()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);
        var echoRoom = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        await directory.CreateRoomAsync("alpha", [KnownIds.Human, alpha.Id], ct);

        var rooms = await directory.GetRoomsForUserAsync(echo.Id, ct);

        Assert.Single(rooms);
        Assert.Equal(echoRoom.Id, rooms[0].Id);
    }

    [Fact]
    public async Task RenameRoom_UpdatesName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human], ct);

        await directory.RenameRoomAsync(room.Id, "echo, alpha", ct);

        var updated = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("echo, alpha", updated.Name);
    }

    [Fact]
    public async Task FindDirectRoom_FoundOnlyWhenExactlyTwoMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);

        var missing = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
        Assert.Null(missing);

        var room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        var found = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
        Assert.NotNull(found);
        Assert.Equal(room.Id, found.Id);

        await directory.AddMemberAsync(room.Id, alpha.Id, ct);
        var afterThirdMember = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
        Assert.Null(afterThirdMember);
    }
}