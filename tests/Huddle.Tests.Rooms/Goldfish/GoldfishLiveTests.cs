using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>Pins the store's <c>Changed</c> event and <c>GoldfishLive</c>, the read model the UI draws goldfish mode from (slice 4, correction B).</summary>
public sealed class GoldfishLiveTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Each kind of write raises the store's Changed event exactly once.</summary>
    [Fact]
    public void Store_EachWriteKind_RaisesChangedOnce()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        int raised = 0;
        store.Changed += () => raised++;
        List<int> counts = [];

        _ = store.TryInsert(Make("p1", "a1", "r1", PossessionState.Starting));
        counts.Add(raised);
        _ = store.SetState("p1", PossessionState.Working, PossessionState.Starting);
        counts.Add(raised);
        store.AddTurn("p1", 0.5m, "USD");
        counts.Add(raised);
        _ = store.BeginReturn("p1", GoldfishExit.Finished, null);
        counts.Add(raised);
        store.End("p1", T0.AddMinutes(1));
        counts.Add(raised);

        Assert.Equal([1, 2, 3, 4, 5], counts);
    }

    /// <summary>A write that changed nothing (a second open row, a move from the wrong state, a second claim) raises nothing.</summary>
    [Fact]
    public void Store_WritesThatChangeNothing_RaiseNothing()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        _ = store.TryInsert(Make("p1", "a1", "r1", PossessionState.Working));
        _ = store.BeginReturn("p1", GoldfishExit.Finished, null);
        int raised = 0;
        store.Changed += () => raised++;

        bool inserted = store.TryInsert(Make("p2", "a1", "r1", PossessionState.Starting));
        bool moved = store.SetState("p1", PossessionState.Working, PossessionState.Starting);
        bool claimed = store.BeginReturn("p1", GoldfishExit.CalledBack, "Quinn");

        Assert.Equal((false, false, false, 0), (inserted, moved, claimed, raised));
    }

    /// <summary>The snapshot lists Starting, Working and Waiting possessions with the Agent's display name, and marks only Waiting as waiting.</summary>
    [Fact]
    public async Task Snapshot_LiveStates_ListsThemWithNamesAndWaiting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, User ruth, User kai, User nova) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        _ = store.TryInsert(Make("p1", ruth.Id, "r1", PossessionState.Starting));
        _ = store.TryInsert(Make("p2", kai.Id, "r2", PossessionState.Working));
        _ = store.TryInsert(Make("p3", nova.Id, "r3", PossessionState.Waiting));

        await live.RefreshAsync(ct);

        Assert.Equal(
            [("p1", "Ruth", false), ("p2", "Kai", false), ("p3", "Nova", true)],
            live.Snapshot.Select(item => (item.Row.Id, item.AgentName, item.Waiting)));
    }

    /// <summary>A Returning or Ended possession shows nothing, and a name the directory does not know falls back to the stored id.</summary>
    [Fact]
    public async Task Snapshot_ReturningEndedAndUnknownAgent_ExcludesAndFallsBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, User ruth, User kai, _) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        _ = store.TryInsert(Make("p1", ruth.Id, "r1", PossessionState.Working));
        _ = store.BeginReturn("p1", GoldfishExit.Finished, null);
        _ = store.TryInsert(Make("p2", kai.Id, "r2", PossessionState.Working));
        _ = store.BeginReturn("p2", GoldfishExit.Failed, null);
        store.End("p2", T0.AddMinutes(1));
        _ = store.TryInsert(Make("p3", "a-gone", "r3", PossessionState.Working));

        await live.RefreshAsync(ct);

        Assert.Equal([("p3", "a-gone")], live.Snapshot.Select(item => (item.Row.Id, item.AgentName)));
    }

    /// <summary>InRoom finds the possession in a Room and ForAgent finds it by display name; neither finds an unrelated Room or Agent.</summary>
    [Fact]
    public async Task InRoomAndForAgent_FindByRoomAndByName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, User ruth, _, _) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        _ = store.TryInsert(Make("p1", ruth.Id, "r1", PossessionState.Working));
        await live.RefreshAsync(ct);

        Assert.Equal(
            ("p1", "p1", null, null),
            (live.InRoom("r1")?.Row.Id, live.ForAgent("Ruth")?.Row.Id, live.InRoom("r2")?.Row.Id, live.ForAgent("Kai")?.Row.Id));
    }

    /// <summary>StampedIfPossessed adds the stamp only for the possessed Agent in the possessed Room.</summary>
    [Fact]
    public async Task StampedIfPossessed_OnlyForThePossessedAgentInItsRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, User ruth, _, _) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        _ = store.TryInsert(Make("p1", ruth.Id, "r1", PossessionState.Working));
        await live.RefreshAsync(ct);

        Assert.Equal(
            ["Ruth · goldfish", "Ruth", "Kai"],
            [live.StampedIfPossessed("Ruth", "r1"), live.StampedIfPossessed("Ruth", "r2"), live.StampedIfPossessed("Kai", "r1")]);
    }

    /// <summary>A store change refreshes the snapshot and raises Changed after the swap, with no explicit refresh.</summary>
    [Fact]
    public async Task Changed_AfterAStoreWrite_RaisesWithTheNewSnapshot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, User ruth, _, _) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        TaskCompletionSource seen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        live.Changed += () =>
        {
            if (live.Snapshot.Count == 1)
            {
                _ = seen.TrySetResult();
            }
        };

        _ = store.TryInsert(Make("p1", ruth.Id, "r1", PossessionState.Working));

        await seen.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        Assert.Equal("p1", Assert.Single(live.Snapshot).Row.Id);
    }

    /// <summary>A store that cannot be read is logged and swallowed: the refresh completes and the snapshot stays empty.</summary>
    [Fact]
    public async Task RefreshAsync_StoreFailure_IsSwallowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, _, _, _) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        using (SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO goldfish_possessions(id, agent_id, room_id, brief, state, started_at, turns, spend) VALUES('bad', 'a1', 'r1', 'x', 'bogus', '2026-11-17T08:00:00.0000000+00:00', 0, '0');";
            _ = command.ExecuteNonQuery();
        }

        await live.RefreshAsync(ct);

        Assert.Empty(live.Snapshot);
    }

    /// <summary>Get reads the row from the store, including an ended one, and is null for an unknown id.</summary>
    [Fact]
    public async Task Get_ReadsTheStoreRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (GoldfishStore store, SqliteTeamDirectory team, User ruth, _, _) = await SeedAsync(dir, ct);
        using GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
        _ = store.TryInsert(Make("p1", ruth.Id, "r1", PossessionState.Working));
        _ = store.BeginReturn("p1", GoldfishExit.Finished, null);
        store.End("p1", T0.AddMinutes(3));

        Assert.Equal((PossessionState.Ended, null), (live.Get("p1")?.State, live.Get("nope")?.State));
    }

    private static async Task<(GoldfishStore Store, SqliteTeamDirectory Team, User Ruth, User Kai, User Nova)> SeedAsync(TempDataDir dir, CancellationToken ct)
    {
        SqliteTeamDirectory team = dir.CreateDirectory();
        await team.InitializeAsync("You", ct);
        User ruth = await AddAgentAsync(team, "Ruth", ct);
        User kai = await AddAgentAsync(team, "Kai", ct);
        User nova = await AddAgentAsync(team, "Nova", ct);
        return (new GoldfishStore(dir.Options()), team, ruth, kai, nova);
    }

    private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
    {
        User? agent = await team.UpsertAgentUserAsync(name, null, ct);
        Assert.NotNull(agent);
        return agent;
    }

    private static Possession Make(string id, string agentId, string roomId, PossessionState state) =>
        new(id, agentId, roomId, null, "Look into it", state, null, null, T0, null, 0, 0m, null, null);
}
