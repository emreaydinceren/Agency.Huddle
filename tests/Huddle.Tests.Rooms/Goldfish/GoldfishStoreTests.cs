using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>Pins the goldfish ledger in <c>team.db</c> (correction §3, GoldfishStore): one open possession per Agent, the return claim, the CHECK, pruning, the migration and the Room delete.</summary>
public sealed class GoldfishStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The options default to on, a thousand-character brief and thirty days of history.</summary>
    [Fact]
    public void GoldfishOptions_Defaults_AreEnabledThousandCharsThirtyDays()
    {
        GoldfishOptions options = new();

        Assert.Equal((true, 1000, 30), (options.Enabled, options.MaxBriefChars, options.KeepDays));
    }

    /// <summary>A possession written by <c>TryInsert</c> reads back field for field, and <c>Open</c> lists it.</summary>
    [Fact]
    public void TryInsert_WritesARowThatReadsBackFieldForField()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Possession possession = Make("p1", "a1", "r1") with { TaskId = "t1", Brief = "Check the \"build\" for me", Turns = 2, Spend = 1.25m, Currency = "USD", ResultMessageId = "m9" };

        bool inserted = store.TryInsert(possession);

        Assert.True(inserted);
        Assert.Equal(possession, store.Get("p1"));
        Assert.Equal(possession, Assert.Single(store.Open()));
        Assert.True(possession.IsOpen);
    }

    /// <summary>Every state survives the round trip, with an exit exactly where the CHECK requires one.</summary>
    [Theory]
    [InlineData("Starting")]
    [InlineData("Working")]
    [InlineData("Waiting")]
    [InlineData("Returning")]
    [InlineData("Ended")]
    public void TryInsert_EveryState_RoundTrips(string stateName)
    {
        PossessionState state = Enum.Parse<PossessionState>(stateName);
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        bool hasExit = state is PossessionState.Returning or PossessionState.Ended;
        Possession possession = Make("p1", "a1", "r1") with
        {
            State = state,
            Exit = hasExit ? GoldfishExit.Finished : null,
            ExitBy = hasExit ? "a1" : null,
            EndedAt = state == PossessionState.Ended ? T0.AddMinutes(3) : null,
        };

        Assert.True(store.TryInsert(possession));

        Possession? read = store.Get("p1");
        Assert.Equal(possession, read);
        Assert.Equal(state != PossessionState.Ended, read?.IsOpen);
    }

    /// <summary>Every exit survives the round trip and is stored as its lower-case name.</summary>
    [Theory]
    [InlineData(GoldfishExit.Finished, "finished")]
    [InlineData(GoldfishExit.Stuck, "stuck")]
    [InlineData(GoldfishExit.CalledBack, "calledback")]
    [InlineData(GoldfishExit.Failed, "failed")]
    [InlineData(GoldfishExit.Restarted, "restarted")]
    public void TryInsert_EveryExit_RoundTripsAndIsStoredLowerCase(GoldfishExit exit, string stored)
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Possession possession = Make("p1", "a1", "r1") with { State = PossessionState.Ended, Exit = exit, ExitBy = "u1", EndedAt = T0.AddMinutes(1) };

        Assert.True(store.TryInsert(possession));

        Assert.Equal(exit, store.Get("p1")?.Exit);
        Assert.Equal("ended|" + stored, Scalar(dir, "SELECT state || '|' || exit FROM goldfish_possessions WHERE id = 'p1';"));
    }

    /// <summary>A second open possession for the same Agent is refused and the first stays as it was.</summary>
    [Fact]
    public void TryInsert_SecondOpenRowForTheSameAgent_IsRefused()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Possession first = Make("p1", "a1", "r1");
        Assert.True(store.TryInsert(first));

        bool second = store.TryInsert(Make("p2", "a1", "r2"));

        Assert.False(second);
        Assert.Null(store.Get("p2"));
        Assert.Equal(first, Assert.Single(store.Open()));
    }

    /// <summary>Once the first possession has ended, the same Agent may start another.</summary>
    [Fact]
    public void TryInsert_AfterTheFirstEnded_IsAllowed()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));
        Assert.True(store.BeginReturn("p1", GoldfishExit.Finished, "a1"));
        store.End("p1", T0.AddMinutes(1));

        bool second = store.TryInsert(Make("p2", "a1", "r1"));

        Assert.True(second);
        Assert.Equal("p2", Assert.Single(store.Open()).Id);
    }

    /// <summary>Another Agent may hold an open possession at the same time.</summary>
    [Fact]
    public void TryInsert_AnotherAgent_IsAllowed()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));

        bool second = store.TryInsert(Make("p2", "a2", "r1"));

        Assert.True(second);
        Assert.Equal(["p1", "p2"], store.Open().Select(p => p.Id).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Reading an id nobody wrote gives null, and an empty ledger lists nothing open.</summary>
    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());

        Assert.Null(store.Get("nope"));
        Assert.Empty(store.Open());
    }

    /// <summary>The first <c>BeginReturn</c> claims the row (Returning, with exit and who), the second changes nothing and says false.</summary>
    [Fact]
    public void BeginReturn_ClaimsOnce()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1") with { State = PossessionState.Working }));

        bool first = store.BeginReturn("p1", GoldfishExit.CalledBack, "u1");
        bool second = store.BeginReturn("p1", GoldfishExit.Failed, "u2");

        Assert.True(first);
        Assert.False(second);
        Possession? read = store.Get("p1");
        Assert.Equal((PossessionState.Returning, GoldfishExit.CalledBack, "u1"), (read?.State, read?.Exit, read?.ExitBy));
    }

    /// <summary>A row that has ended cannot be claimed again, and its exit is not rewritten.</summary>
    [Fact]
    public void BeginReturn_OnAnEndedRow_ReturnsFalseAndKeepsTheExit()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));
        Assert.True(store.BeginReturn("p1", GoldfishExit.Finished, "a1"));
        store.End("p1", T0.AddMinutes(2));

        bool again = store.BeginReturn("p1", GoldfishExit.Failed, "system");

        Assert.False(again);
        Possession? read = store.Get("p1");
        Assert.Equal((PossessionState.Ended, GoldfishExit.Finished, "a1"), (read?.State, read?.Exit, read?.ExitBy));
        Assert.False(store.BeginReturn("missing", GoldfishExit.Failed, "system"));
    }

    /// <summary>A Returning row can be claimed again by the start-up pass, which overwrites the exit with Restarted.</summary>
    [Fact]
    public void BeginReturn_OnAReturningRow_OverwritesTheExit()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));
        Assert.True(store.BeginReturn("p1", GoldfishExit.Finished, "a1"));

        bool restarted = store.BeginReturn("p1", GoldfishExit.Restarted, "system");

        Assert.True(restarted);
        Possession? read = store.Get("p1");
        Assert.Equal((PossessionState.Returning, GoldfishExit.Restarted, "system"), (read?.State, read?.Exit, read?.ExitBy));
    }

    /// <summary>The CHECK refuses an exit on a Starting row and an Ended row without one, whatever wrote the SQL.</summary>
    [Theory]
    [InlineData("starting", "'finished'")]
    [InlineData("ended", "NULL")]
    [InlineData("returning", "NULL")]
    public void Check_RefusesAnExitThatDisagreesWithTheState(string state, string exit)
    {
        using TempDataDir dir = new();
        _ = new GoldfishStore(dir.Options());

        SqliteException ex = Assert.Throws<SqliteException>(() => Execute(
            dir,
            $"INSERT INTO goldfish_possessions(id, agent_id, room_id, brief, state, exit, started_at, turns, spend) VALUES('p1', 'a1', 'r1', 'b', '{state}', {exit}, 't', 0, '0');"));

        Assert.Equal(19, ex.SqliteErrorCode);
    }

    /// <summary>A state text nobody wrote through the store fails loudly on read instead of reading as something else.</summary>
    [Fact]
    public void Get_AnUnknownStateText_Throws()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Execute(dir, "INSERT INTO goldfish_possessions(id, agent_id, room_id, brief, state, started_at, turns, spend) VALUES('p1', 'a1', 'r1', 'b', 'bogus', '2026-11-17T08:00:00.0000000+00:00', 0, '0');");

        _ = Assert.Throws<InvalidOperationException>(() => store.Get("p1"));
    }

    /// <summary><c>SetState</c> moves an open row between the working states and leaves the rest of it alone.</summary>
    [Fact]
    public void SetState_ChangesOnlyTheState()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Possession possession = Make("p1", "a1", "r1");
        Assert.True(store.TryInsert(possession));

        bool changed = store.SetState("p1", PossessionState.Waiting, PossessionState.Starting, PossessionState.Working);

        Assert.True(changed);
        Assert.Equal(possession with { State = PossessionState.Waiting }, store.Get("p1"));
    }

    /// <summary><c>SetState</c> refuses a row in a state outside the allowed sources, leaving it unchanged and returning false, so it cannot race the return claim into the CHECK.</summary>
    [Theory]
    [InlineData("Returning")]
    [InlineData("Ended")]
    [InlineData("Waiting")]
    public void SetState_SourceStateNotAllowed_LeavesRowUnchangedAndReturnsFalse(string stateName)
    {
        PossessionState state = Enum.Parse<PossessionState>(stateName);
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        bool hasExit = state is PossessionState.Returning or PossessionState.Ended;
        Possession possession = Make("p1", "a1", "r1") with
        {
            State = state,
            Exit = hasExit ? GoldfishExit.Finished : null,
            ExitBy = hasExit ? "a1" : null,
            EndedAt = state == PossessionState.Ended ? T0.AddMinutes(3) : null,
        };
        Assert.True(store.TryInsert(possession));

        bool changed = store.SetState("p1", PossessionState.Working, PossessionState.Starting, PossessionState.Working);

        Assert.False(changed);
        Assert.Equal(possession, store.Get("p1"));
    }

    /// <summary><c>SetResultMessage</c> remembers the posted result Message so a restart does not post it twice.</summary>
    [Fact]
    public void SetResultMessage_IsRead_Back()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));

        store.SetResultMessage("p1", "m42");

        Assert.Equal("m42", store.Get("p1")?.ResultMessageId);
    }

    /// <summary><c>End</c> closes a Returning row at the given time and keeps its exit.</summary>
    [Fact]
    public void End_ClosesTheRowAtTheGivenTime()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));
        Assert.True(store.BeginReturn("p1", GoldfishExit.Stuck, "a1"));

        store.End("p1", T0.AddMinutes(7));

        Possession? read = store.Get("p1");
        Assert.Equal((PossessionState.Ended, GoldfishExit.Stuck, (DateTimeOffset?)T0.AddMinutes(7)), (read?.State, read?.Exit, read?.EndedAt));
        Assert.Empty(store.Open());
    }

    /// <summary><c>AddTurn</c> counts a Turn and adds its spend; a Turn without spend only counts.</summary>
    [Fact]
    public void AddTurn_CountsTurnsAndAddsSpend()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));

        store.AddTurn("p1", 0.40m, "USD");
        store.AddTurn("p1", 0.35m, "USD");
        store.AddTurn("p1", null, null);

        Possession? read = store.Get("p1");
        Assert.Equal((3, 0.75m, "USD"), (read?.Turns, read?.Spend, read?.Currency));
    }

    /// <summary>Spend is stored as invariant text and reads back exactly under a comma-decimal culture.</summary>
    [Fact]
    public void Spend_RoundTripsUnderGermanCulture()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(store.TryInsert(Make("p1", "a1", "r1") with { Spend = 1.25m, Currency = "EUR" }));
            store.AddTurn("p1", 0.1m, "EUR");

            Assert.Equal(1.35m, store.Get("p1")?.Spend);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal("1.35", Scalar(dir, "SELECT spend FROM goldfish_possessions WHERE id = 'p1';"));
    }

    /// <summary><c>PruneBefore</c> removes ended rows older than the cutoff and never an open one, however old.</summary>
    [Fact]
    public void PruneBefore_RemovesOnlyOldEndedRows()
    {
        using TempDataDir dir = new();
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("old", "a1", "r1") with { State = PossessionState.Ended, Exit = GoldfishExit.Finished, ExitBy = "a1", EndedAt = T0.AddDays(-40) }));
        Assert.True(store.TryInsert(Make("recent", "a2", "r1") with { State = PossessionState.Ended, Exit = GoldfishExit.Failed, ExitBy = "system", EndedAt = T0.AddDays(-2) }));
        Assert.True(store.TryInsert(Make("open", "a3", "r1") with { StartedAt = T0.AddDays(-90) }));

        int removed = store.PruneBefore(T0.AddDays(-30));

        Assert.Equal(1, removed);
        Assert.Null(store.Get("old"));
        Assert.NotNull(store.Get("recent"));
        Assert.NotNull(store.Get("open"));
    }

    /// <summary>A team.db made before the feature gains the table and its partial unique index at start-up, and the index then holds.</summary>
    [Fact]
    public async Task InitializeAsync_OnAPreFeatureDatabase_AddsTheTableAndIndex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        _ = await dir.CreateDirectoryAsync(ct);
        Execute(dir, "DROP TABLE goldfish_possessions;");
        Assert.Equal("0", Scalar(dir, "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('goldfish_possessions', 'goldfish_one_open');"));

        SqliteTeamDirectory reopened = await dir.CreateDirectoryAsync(ct);

        Assert.NotNull(reopened);
        Assert.Equal("2", Scalar(dir, "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('goldfish_possessions', 'goldfish_one_open');"));
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("p1", "a1", "r1")));
        Assert.False(store.TryInsert(Make("p2", "a1", "r1")));
    }

    /// <summary>Deleting a Room removes that Room's possessions, open or ended, and keeps another Room's.</summary>
    [Fact]
    public async Task DeleteRoom_RemovesItsPossessionsAndKeepsOthers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = await dir.CreateDirectoryAsync(ct);
        User? echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        Room room = await directory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        Room other = await directory.CreateRoomAsync("other", [KnownIds.Human, echo.Id], ct);
        GoldfishStore store = new(dir.Options());
        Assert.True(store.TryInsert(Make("ended", "a1", room.Id) with { State = PossessionState.Ended, Exit = GoldfishExit.Finished, ExitBy = "a1", EndedAt = T0 }));
        Assert.True(store.TryInsert(Make("open", "a1", room.Id)));
        Assert.True(store.TryInsert(Make("kept", "a2", other.Id)));

        await directory.DeleteRoomAsync(room.Id, ct);

        Assert.Null(store.Get("ended"));
        Assert.Null(store.Get("open"));
        Assert.Equal("kept", Assert.Single(store.Open()).Id);
    }

    private static Possession Make(string id, string agentId, string roomId) =>
        new(id, agentId, roomId, null, "Look into it", PossessionState.Starting, null, null, T0, null, 0, 0m, null, null);

    private static string DbPath(TempDataDir dir) => Path.Combine(dir.Path, "team.db");

    private static void Execute(TempDataDir dir, string sql)
    {
        using SqliteConnection connection = new($"Data Source={DbPath(dir)}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static string Scalar(TempDataDir dir, string sql)
    {
        using SqliteConnection connection = new($"Data Source={DbPath(dir)}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
