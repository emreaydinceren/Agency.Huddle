using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>Tests for <see cref="ITeamDirectory.FindRoomWithExactMemberSetAsync"/> (Spec §10.4).</summary>
public sealed class SqliteTeamDirectoryMemberSetTests
{
    /// <summary>A Room whose Members are exactly the requested set is found.</summary>
    [Fact]
    public async Task ExactThreeMemberMatch_IsFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        User? echo = await directory.UpsertAgentUserAsync("Echo", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(echo);
        Room room = await directory.CreateRoomAsync("Nova, Echo", [KnownIds.Human, nova.Id, echo.Id], ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id, echo.Id], ct);

        Assert.NotNull(found);
        Assert.Equal(room.Id, found.Id);
    }

    /// <summary>A Room with more Members than the requested set does not match.</summary>
    [Fact]
    public async Task Superset_IsNotMatched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        User? echo = await directory.UpsertAgentUserAsync("Echo", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(echo);
        await directory.CreateRoomAsync("Nova, Echo", [KnownIds.Human, nova.Id, echo.Id], ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], ct);

        Assert.Null(found);
    }

    /// <summary>A Room with fewer Members than the requested set does not match.</summary>
    [Fact]
    public async Task Subset_IsNotMatched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        User? echo = await directory.UpsertAgentUserAsync("Echo", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(echo);
        await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id, echo.Id], ct);

        Assert.Null(found);
    }

    /// <summary>An Archived Room that otherwise matches exactly is skipped.</summary>
    [Fact]
    public async Task ArchivedExactMatch_IsSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        Room room = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);
        await directory.SetRoomArchivedAsync(room.Id, true, ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], ct);

        Assert.Null(found);
    }

    /// <summary>Of two Rooms with the same exact Member set, the oldest one wins.</summary>
    [Fact]
    public async Task TwoMatches_OldestWins()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        Room older = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);
        await Task.Delay(5, ct);
        await directory.CreateRoomAsync("Nova (2)", [KnownIds.Human, nova.Id], ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], ct);

        Assert.NotNull(found);
        Assert.Equal(older.Id, found.Id);
    }

    /// <summary>An empty set returns null.</summary>
    [Fact]
    public async Task EmptySet_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync([], ct);

        Assert.Null(found);
    }

    /// <summary>Duplicate ids in the requested set are treated as one set member each.</summary>
    [Fact]
    public async Task DuplicateIds_TreatedAsSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        User? echo = await directory.UpsertAgentUserAsync("Echo", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(echo);
        Room room = await directory.CreateRoomAsync("Nova, Echo", [KnownIds.Human, nova.Id, echo.Id], ct);

        Room? found = await directory.FindRoomWithExactMemberSetAsync(
            [KnownIds.Human, KnownIds.Human, nova.Id, nova.Id, echo.Id], ct);

        Assert.NotNull(found);
        Assert.Equal(room.Id, found.Id);
    }
}
