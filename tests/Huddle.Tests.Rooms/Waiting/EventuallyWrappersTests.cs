using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Sdk;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Waiting;

/// <summary>Pins <see cref="Eventually.HistoryCountAsync"/> and <see cref="Eventually.DirectRoomAsync(IServiceProvider, string, CancellationToken, TimeSpan?)"/>.</summary>
public sealed class EventuallyWrappersTests
{
    private static readonly TimeSpan shortTimeout = TimeSpan.FromMilliseconds(150);

    /// <summary>Appends one Message to <paramref name="store"/>'s <paramref name="roomId"/>.</summary>
    private static Task AppendAsync(FileChatStore store, string roomId, CancellationToken ct) =>
        store.AppendAsync(roomId, new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", "hi"), ct);

    /// <summary>A Transcript that already holds enough Messages returns them without waiting.</summary>
    [Fact]
    public async Task HistoryCountAsync_CountAlreadyReached_ReturnsTheTranscript()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, "room-1", ct);
        await AppendAsync(store, "room-1", ct);

        IReadOnlyList<ChatMessage> history = await Eventually.HistoryCountAsync(store, "room-1", 2, ct);

        Assert.Equal(2, history.Count);
    }

    /// <summary>A Message that arrives while the wait polls is picked up and the full Transcript is returned.</summary>
    [Fact]
    public async Task HistoryCountAsync_MessageArrivesLater_ReturnsOnceCountReached()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, "room-1", ct);

        Task<IReadOnlyList<ChatMessage>> wait = Eventually.HistoryCountAsync(store, "room-1", 2, ct, TestBudgets.Short);
        await Task.Delay(30, ct);
        await AppendAsync(store, "room-1", ct);

        Assert.Equal(2, (await wait).Count);
    }

    /// <summary>The tick callback runs once per unmet poll, which is how a test advances a fake clock.</summary>
    [Fact]
    public async Task HistoryCountAsync_CountNotYetReached_CallsOnTickEachPoll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        int ticks = 0;

        Task<IReadOnlyList<ChatMessage>> wait = Eventually.HistoryCountAsync(
            store, "room-1", 1, ct, TestBudgets.Slow, onTick: () => Interlocked.Increment(ref ticks));
        await Eventually.UntilAsync(() => Volatile.Read(ref ticks) >= 2, ct, TestBudgets.Slow);
        await AppendAsync(store, "room-1", ct);
        _ = await wait;

        Assert.True(Volatile.Read(ref ticks) >= 2);
    }

    /// <summary>A Transcript that never reaches the count fails the wait with an assertion naming the room.</summary>
    [Fact]
    public async Task HistoryCountAsync_NeverReached_FailsNamingTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);

        XunitException ex = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.HistoryCountAsync(store, "room-9", 3, ct, shortTimeout));

        Assert.Contains("room-9", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>An Agent that is already online with a direct Room yields its id and the Room's id.</summary>
    [Fact]
    public async Task DirectRoomAsync_AgentOnlineWithRoom_ReturnsIds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ServiceProvider services, SqliteTeamDirectory directory, FakeAgentGateway gateway) = await BuildAsync(dir, ct);
        await using ServiceProvider provider = services;
        User agent = Assert.IsType<User>(await directory.UpsertAgentUserAsync("nova", null, ct));
        Room room = await directory.CreateRoomAsync("nova", [KnownIds.Human, agent.Id], ct);
        gateway.SetOnline(agent.Id);

        (string agentId, string roomId) = await Eventually.DirectRoomAsync(provider, "nova", ct);

        Assert.Equal((agent.Id, room.Id), (agentId, roomId));
    }

    /// <summary>An Agent that comes online after the wait began is still found.</summary>
    [Fact]
    public async Task DirectRoomAsync_AgentComesOnlineLater_ReturnsIds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ServiceProvider services, SqliteTeamDirectory directory, FakeAgentGateway gateway) = await BuildAsync(dir, ct);
        await using ServiceProvider provider = services;
        User agent = Assert.IsType<User>(await directory.UpsertAgentUserAsync("nova", null, ct));
        Room room = await directory.CreateRoomAsync("nova", [KnownIds.Human, agent.Id], ct);

        Task<(string AgentId, string RoomId)> wait = Eventually.DirectRoomAsync(provider, "nova", ct, TestBudgets.Short);
        await Task.Delay(30, ct);
        gateway.SetOnline(agent.Id);

        Assert.Equal((agent.Id, room.Id), await wait);
    }

    /// <summary>The overload taking a directory looks the Agent up there rather than in the service provider.</summary>
    [Fact]
    public async Task DirectRoomAsync_ExplicitDirectory_UsesThatDirectory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ServiceProvider services, SqliteTeamDirectory directory, FakeAgentGateway gateway) = await BuildAsync(dir, ct);
        await using ServiceProvider provider = services;
        using TempDataDir otherDir = new();
        SqliteTeamDirectory other = new(otherDir.Options());
        await other.InitializeAsync("You", ct);
        User agent = Assert.IsType<User>(await other.UpsertAgentUserAsync("nova", null, ct));
        Room room = await other.CreateRoomAsync("nova", [KnownIds.Human, agent.Id], ct);
        gateway.SetOnline(agent.Id);

        (string agentId, string roomId) = await Eventually.DirectRoomAsync(provider, other, "nova", ct);

        Assert.Equal((agent.Id, room.Id), (agentId, roomId));
        Assert.NotNull(directory);
    }

    /// <summary>An Agent that never appears fails the wait with an assertion naming the Agent.</summary>
    [Fact]
    public async Task DirectRoomAsync_AgentNeverAppears_FailsNamingTheAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ServiceProvider services, _, _) = await BuildAsync(dir, ct);
        await using ServiceProvider provider = services;

        XunitException ex = await Assert.ThrowsAnyAsync<XunitException>(
            () => Eventually.DirectRoomAsync(provider, "ghost", ct, shortTimeout));

        Assert.Contains("ghost", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Builds a service provider holding a fresh directory and a fake gateway.</summary>
    private static async Task<(ServiceProvider Services, SqliteTeamDirectory Directory, FakeAgentGateway Gateway)> BuildAsync(
        TempDataDir dir, CancellationToken ct)
    {
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        FakeAgentGateway gateway = new();
        ServiceCollection collection = new();
        collection.AddSingleton<ITeamDirectory>(directory);
        collection.AddSingleton<IAgentGateway>(gateway);
        return (collection.BuildServiceProvider(), directory, gateway);
    }
}
