using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests.Data;

/// <summary>Pins the Turn Queue's ledger in <c>team.db</c> (spec §5.2): what each write does to which rows, and what it never touches.</summary>
public sealed class TurnQueueStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);
    private static readonly DateTimeOffset T2 = T0.AddMinutes(10);

    /// <summary>A row written by <c>Record</c> reads back field for field as waiting.</summary>
    [Fact]
    public void Record_WritesAWaitingRow()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());

        bool inserted = store.Record("a1", "m1", "r1", T0, T1);

        Assert.True(inserted);
        TurnQueueRow row = Assert.Single(store.Open("a1"));
        Assert.Equal(("a1", "m1", "r1"), (row.AgentId, row.MessageId, row.RoomId));
        Assert.Equal(T0, row.PostedUtc);
        Assert.Equal(TurnQueueStatus.Waiting, row.Status);
        Assert.Null(row.Outcome);
        Assert.Null(row.ReplyMessageId);
        Assert.Null(row.ConnectionId);
        Assert.Equal(0, row.Attempts);
        Assert.Equal(T1, row.CreatedUtc);
        Assert.Equal(T1, row.UpdatedUtc);
    }

    /// <summary>Recording the same Message twice keeps one row and never changes it, even a running or done one.</summary>
    [Fact]
    public void Record_Twice_KeepsOneRowAndNeverChangesIt()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        store.Started("a1", "r1", ["m1"], "reply-1", "c1", T0, T1);

        bool second = store.Record("a1", "m1", "r1", T0, T2);

        Assert.False(second);
        TurnQueueRow row = Assert.Single(store.Open("a1"));
        Assert.Equal(TurnQueueStatus.Running, row.Status);
        Assert.Equal(T1, row.UpdatedUtc);
        _ = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Failed, T1);
        Assert.False(store.Record("a1", "m1", "r1", T0, T2));
        Assert.Empty(store.Open("a1"));
    }

    /// <summary>A Started naming another Room than the one the row is in leaves the row waiting with its attempts, and adds no row.</summary>
    [Fact]
    public void Started_NamingAnotherRoom_LeavesTheRowAlone()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "rB", T0, T0);

        store.Started("a1", "rA", ["m1"], "reply-1", "c1", T0, T1);

        TurnQueueRow row = Assert.Single(store.Open("a1"));
        Assert.Equal(("rB", TurnQueueStatus.Waiting, 0), (row.RoomId, row.Status, row.Attempts));
        Assert.Null(row.ReplyMessageId);
        Assert.Null(row.ConnectionId);
        _ = Assert.Single(TurnQueueRows.Read(dir.Path));
    }

    /// <summary>A Started naming another Room never attaches its reply and connection to a running row without a reply in the row's own Room.</summary>
    [Fact]
    public void Started_NamingAnotherRoom_LeavesARunningRowWithoutReplyAlone()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "rB", T0, T0);
        store.Set(Assert.Single(store.Open("a1")).Seq, TurnQueueStatus.Running, null, T0);

        store.Started("a1", "rA", ["m1"], "reply-1", "c1", T0, T1);

        TurnQueueRow row = Assert.Single(store.Open("a1"));
        Assert.Null(row.ReplyMessageId);
        Assert.Null(row.ConnectionId);
    }

    /// <summary>The same Message owed to two Agents is two rows.</summary>
    [Fact]
    public void Record_TwoAgentsSameMessage_TwoRows()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());

        Assert.True(store.Record("a1", "m1", "r1", T0, T0));
        Assert.True(store.Record("a2", "m1", "r1", T0, T0));

        _ = Assert.Single(store.Open("a1"));
        _ = Assert.Single(store.Open("a2"));
    }

    /// <summary>The sequence number only grows, also after rows are deleted.</summary>
    [Fact]
    public void Seq_StrictlyIncreases_AfterDeletes()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        long last = store.Open("a1")[1].Seq;
        _ = store.DeleteForRoom("r1");

        _ = store.Record("a1", "m3", "r1", T0, T0);

        TurnQueueRow row = Assert.Single(store.Open("a1"));
        Assert.True(row.Seq > last);
    }

    /// <summary>Every outcome is stored under its name and read back as the same outcome.</summary>
    [Fact]
    public void Finish_EveryOutcome_RoundTrips()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        TurnQueueOutcome[] outcomes = Enum.GetValues<TurnQueueOutcome>();
        for (int i = 0; i < outcomes.Length; i++)
        {
            _ = store.Record("a1", $"m{i}", "r1", T0, T0);
            Assert.Equal(1, store.Finish("a1", "r1", [$"m{i}"], outcomes[i], T1));
        }

        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT outcome FROM turn_queue ORDER BY seq;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> stored = [];
        while (reader.Read())
        {
            stored.Add(reader.GetString(0));
        }

        Assert.Equal(
            ["replied", "silent", "failed", "stopped", "declined", "dropped", "expired", "abandoned", "gone"], stored);
    }

    /// <summary>Set writes each status and outcome, and open reads exclude done rows; a row set back to waiting loses its ids.</summary>
    [Fact]
    public void Set_EveryStatus_RoundTrips()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        store.Started("a1", "r1", ["m1"], "reply-1", "c1", T0, T0);
        long seq = store.Open("a1")[0].Seq;

        store.Set(seq, TurnQueueStatus.Waiting, null, T1);
        TurnQueueRow waiting = Assert.Single(store.Open("a1"));
        Assert.Equal(TurnQueueStatus.Waiting, waiting.Status);
        Assert.Null(waiting.ReplyMessageId);
        Assert.Null(waiting.ConnectionId);

        store.Set(seq, TurnQueueStatus.Running, null, T1);
        Assert.Equal(TurnQueueStatus.Running, Assert.Single(store.Open("a1")).Status);

        store.Set(seq, TurnQueueStatus.Done, TurnQueueOutcome.Abandoned, T2);
        Assert.Empty(store.Open("a1"));
        Assert.Equal(1, store.PruneDoneBefore(T2.AddSeconds(1)));
    }

    /// <summary>Delete removes only the named row.</summary>
    [Fact]
    public void Delete_RemovesOnlyThatRow()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);

        store.Delete(store.Open("a1")[0].Seq);

        Assert.Equal("m2", Assert.Single(store.Open("a1")).MessageId);
    }

    /// <summary>Forget deletes every Agent's row for that Message and no other.</summary>
    [Fact]
    public void Forget_DeletesEveryRowOfTheMessage()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a2", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);

        Assert.Equal(2, store.Forget("m1"));

        Assert.Equal("m2", Assert.Single(store.OpenIn("r1")).MessageId);
    }

    /// <summary>Started inserts a missing row as running with one attempt.</summary>
    [Fact]
    public void Started_MissingRow_InsertsRunning()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());

        store.Started("a1", "r1", ["m1"], "reply-1", "c1", T0, T1);

        TurnQueueRow row = Assert.Single(store.Open("a1"));
        Assert.Equal(TurnQueueStatus.Running, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal("reply-1", row.ReplyMessageId);
        Assert.Equal("c1", row.ConnectionId);
        Assert.Equal(T0, row.PostedUtc);
        Assert.Equal("r1", row.RoomId);
    }

    /// <summary>Started moves a waiting row and a running row to running with one more attempt and the new ids.</summary>
    [Fact]
    public void Started_WaitingAndRunning_BecomeRunningWithAnotherAttempt()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        store.Started("a1", "r1", ["m2"], "reply-old", "c-old", T0, T0);

        store.Started("a1", "r1", ["m1", "m2"], "reply-2", "c2", T0, T1);

        IReadOnlyList<TurnQueueRow> rows = store.Open("a1");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(TurnQueueStatus.Running, row.Status));
        Assert.All(rows, row => Assert.Equal("reply-2", row.ReplyMessageId));
        Assert.All(rows, row => Assert.Equal("c2", row.ConnectionId));
        Assert.All(rows, row => Assert.Equal(T1, row.UpdatedUtc));
        Assert.Equal(1, rows[0].Attempts);
        Assert.Equal(2, rows[1].Attempts);
    }

    /// <summary>Started leaves a done row done and does not insert a second one.</summary>
    [Fact]
    public void Started_DoneRow_IsLeftAlone()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Stopped, T1);

        store.Started("a1", "r1", ["m1"], "reply-1", "c1", T0, T2);

        Assert.Empty(store.Open("a1"));
        Assert.Equal(1, store.PruneDoneBefore(T2.AddDays(1)));
    }

    /// <summary>Finish closes only open rows of that Agent in that Room for those Messages.</summary>
    [Fact]
    public void Finish_ClosesOnlyOpenRowsOfThatAgentInThatRoom()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Record("a2", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m3", "r2", T0, T0);

        int closed = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Silent, T1);
        int again = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Failed, T2);
        int wrongRoom = store.Finish("a1", "r2", ["m2"], TurnQueueOutcome.Failed, T2);

        Assert.Equal(1, closed);
        Assert.Equal(0, again);
        Assert.Equal(0, wrongRoom);
        Assert.Equal(["m2", "m3"], store.Open("a1").Select(row => row.MessageId));
        _ = Assert.Single(store.Open("a2"));
        Assert.Equal(1, store.PruneDoneBefore(T1.AddSeconds(1)));
    }

    /// <summary>A Finish with an outcome never rewrites the time of a row that was already closed.</summary>
    [Fact]
    public void Finish_DoneRow_KeepsItsTime()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Silent, T1);

        _ = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Failed, T2);

        Assert.Equal(1, store.PruneDoneBefore(T1.AddSeconds(1)));
    }

    /// <summary>MarkReplied closes waiting, running and done-stopped rows as replied.</summary>
    [Fact]
    public void MarkReplied_WaitingRunningAndStopped_AllBecomeReplied()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Record("a1", "m3", "r1", T0, T0);
        store.Started("a1", "r1", ["m2"], "reply-1", "c1", T0, T0);
        _ = store.Finish("a1", "r1", ["m3"], TurnQueueOutcome.Stopped, T1);

        int changed = store.MarkReplied("a1", "r1", ["m1", "m2", "m3"], T2);

        Assert.Equal(3, changed);
        Assert.Empty(store.Open("a1"));
        Assert.Equal(3, CountOutcome(dir, "replied"));
    }

    /// <summary>MarkReplied does not touch a row already replied, nor another Agent's or Room's row.</summary>
    [Fact]
    public void MarkReplied_RepliedRowAndOtherScopes_AreLeftAlone()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a2", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r2", T0, T0);
        _ = store.MarkReplied("a1", "r1", ["m1"], T1);

        int again = store.MarkReplied("a1", "r1", ["m1"], T2);
        int wrongRoom = store.MarkReplied("a1", "r1", ["m2"], T2);

        Assert.Equal(0, again);
        Assert.Equal(0, wrongRoom);
        Assert.Equal(1, store.PruneDoneBefore(T1.AddSeconds(1)));
        _ = Assert.Single(store.Open("a2"));
        _ = Assert.Single(store.Open("a1"));
    }

    /// <summary>CloseRunning closes the running rows of that connection only.</summary>
    [Fact]
    public void CloseRunning_ClosesOnlyThatConnection()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Record("a1", "m3", "r1", T0, T0);
        store.Started("a1", "r1", ["m1"], "reply-1", "c1", T0, T0);
        store.Started("a1", "r1", ["m2"], "reply-2", "c2", T0, T0);

        int closed = store.CloseRunning("c1", TurnQueueOutcome.Failed, T1);

        Assert.Equal(1, closed);
        Assert.Equal(["m2", "m3"], store.Open("a1").Select(row => row.MessageId));
    }

    /// <summary>CloseRunning leaves a done row of that connection alone.</summary>
    [Fact]
    public void CloseRunning_DoneRow_IsLeftAlone()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        store.Started("a1", "r1", ["m1"], "reply-1", "c1", T0, T0);
        _ = store.MarkReplied("a1", "r1", ["m1"], T1);

        Assert.Equal(0, store.CloseRunning("c1", TurnQueueOutcome.Failed, T2));
        Assert.Equal(1, CountOutcome(dir, "replied"));
    }

    /// <summary>CloseUpTo closes open rows of the Agent in the Room with a seq at or below the mark and keeps later ones.</summary>
    [Fact]
    public void CloseUpTo_ClosesBySeq_AndKeepsLaterRows()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m0", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["m0"], TurnQueueOutcome.Silent, T0);
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Record("a1", "m9", "r2", T0, T0);
        _ = store.Record("a2", "m1", "r1", T0, T0);
        long mark = store.MaxSeq("a1", "r1");
        _ = store.Record("a1", "m3", "r1", T0, T0);

        int closed = store.CloseUpTo("a1", "r1", mark, TurnQueueOutcome.Stopped, T1);

        Assert.Equal(2, closed);
        Assert.Equal(["m9", "m3"], store.Open("a1").Select(row => row.MessageId));
        _ = Assert.Single(store.Open("a2"));
        Assert.Equal(1, CountOutcome(dir, "silent"));
        Assert.Equal(2, CountOutcome(dir, "stopped"));
    }

    /// <summary>MaxSeq is zero when the Agent has no row in the Room, and the highest seq otherwise.</summary>
    [Fact]
    public void MaxSeq_NoRows_IsZero_AndOtherwiseTheHighest()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        Assert.Equal(0, store.MaxSeq("a1", "r1"));
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Record("a1", "m3", "r2", T0, T0);

        Assert.Equal(store.Open("a1")[1].Seq, store.MaxSeq("a1", "r1"));
    }

    /// <summary>CloseForAgent closes every open row of that Agent and no one else's.</summary>
    [Fact]
    public void CloseForAgent_ClosesEveryOpenRowOfThatAgent()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r2", T0, T0);
        _ = store.Record("a2", "m1", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["m1"], TurnQueueOutcome.Silent, T0);

        int closed = store.CloseForAgent("a1", TurnQueueOutcome.Gone, T1);

        Assert.Equal(1, closed);
        Assert.Empty(store.Open("a1"));
        _ = Assert.Single(store.Open("a2"));
        Assert.Equal(1, CountOutcome(dir, "gone"));
    }

    /// <summary>DeleteForRoom removes that Room's rows, of every status, and leaves other Rooms.</summary>
    [Fact]
    public void DeleteForRoom_RemovesThatRoomOnly()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["m2"], TurnQueueOutcome.Silent, T0);
        _ = store.Record("a1", "m3", "r2", T0, T0);

        Assert.Equal(2, store.DeleteForRoom("r1"));

        Assert.Equal("m3", Assert.Single(store.Open("a1")).MessageId);
        Assert.Equal(0, store.PruneDoneBefore(T2));
    }

    /// <summary>OpenIn lists the open rows of one Room in seq order.</summary>
    [Fact]
    public void OpenIn_ListsOpenRowsOfThatRoom()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a2", "m2", "r1", T0, T0);
        _ = store.Record("a1", "m3", "r2", T0, T0);
        _ = store.Record("a1", "m4", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["m4"], TurnQueueOutcome.Silent, T0);

        Assert.Equal(["m1", "m2"], store.OpenIn("r1").Select(row => row.MessageId));
    }

    /// <summary>OpenUpdatedBefore lists open rows strictly older than the cutoff and not done ones.</summary>
    [Fact]
    public void OpenUpdatedBefore_ExcludesRowsAtOrAfterTheCutoff()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "old", "r1", T0, T0);
        _ = store.Record("a1", "edge", "r1", T0, T1);
        _ = store.Record("a1", "new", "r1", T0, T2);
        _ = store.Record("a1", "closed", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["closed"], TurnQueueOutcome.Silent, T0);

        Assert.Equal(["old"], store.OpenUpdatedBefore(T1).Select(row => row.MessageId));
    }

    /// <summary>PruneDoneBefore deletes old done rows and keeps open rows and newer done rows.</summary>
    [Fact]
    public void PruneDoneBefore_KeepsOpenRowsAndNewerDoneRows()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "old", "r1", T0, T0);
        _ = store.Record("a1", "new", "r1", T0, T0);
        _ = store.Record("a1", "open", "r1", T0, T0);
        _ = store.Record("a1", "edge", "r1", T0, T0);
        _ = store.Finish("a1", "r1", ["old"], TurnQueueOutcome.Silent, T0);
        _ = store.Finish("a1", "r1", ["edge"], TurnQueueOutcome.Silent, T1);
        _ = store.Finish("a1", "r1", ["new"], TurnQueueOutcome.Silent, T2);

        Assert.Equal(1, store.PruneDoneBefore(T1));

        Assert.Equal("open", Assert.Single(store.Open("a1")).MessageId);
        Assert.Equal(2, store.PruneDoneBefore(T2.AddSeconds(1)));
    }

    /// <summary>A row with a status or outcome this build does not know is skipped, not thrown on.</summary>
    [Fact]
    public void Open_UnknownValues_SkipsTheRow()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        _ = store.Record("a1", "m1", "r1", T0, T0);
        _ = store.Record("a1", "m2", "r1", T0, T0);
        _ = store.Record("a1", "m3", "r1", T0, T0);
        using (SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE turn_queue SET status = 'paused' WHERE message_id = 'm1'; "
                + "UPDATE turn_queue SET outcome = 'mystery' WHERE message_id = 'm3';";
            _ = command.ExecuteNonQuery();
        }

        Assert.Equal("m2", Assert.Single(store.Open("a1")).MessageId);
    }

    /// <summary>Subscribe adds an Agent once, Unsubscribe removes it, and the set is case-sensitive.</summary>
    [Fact]
    public void Subscribers_SubscribeAndUnsubscribe()
    {
        using TempDataDir dir = new();
        TurnQueueStore store = new(dir.Options());
        Assert.Empty(store.Subscribers());

        store.Subscribe("a1");
        store.Subscribe("a1");
        store.Subscribe("A1");
        Assert.Equal(["A1", "a1"], store.Subscribers().Order(StringComparer.Ordinal));

        store.Unsubscribe("a1");
        Assert.Equal(["A1"], store.Subscribers());
    }

    /// <summary>A team.db built before the feature gains both tables and the index when the store opens, keeping its other tables.</summary>
    [Fact]
    public void Constructor_PreFeatureDatabase_GainsTheTables()
    {
        using TempDataDir dir = new();
        _ = new JobStateStore(dir.Options());

        _ = new TurnQueueStore(dir.Options());

        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE name IN ('job_state', 'turn_queue', 'turn_queue_agents', 'turn_queue_open') ORDER BY name;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> names = [];
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        Assert.Equal(["job_state", "turn_queue", "turn_queue_agents", "turn_queue_open"], names);
    }

    /// <summary>Empty configuration binds the defaults: on, one day, seven days.</summary>
    [Fact]
    public void Options_Defaults()
    {
        TeamOptions options = new();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        configuration.GetSection("Team").Bind(options);

        Assert.True(options.TurnQueue.Enabled);
        Assert.Equal(TimeSpan.FromDays(1), options.TurnQueue.ResumeWithin);
        Assert.Equal(7, options.TurnQueue.KeepDays);
    }

    /// <summary>The <c>Team:TurnQueue</c> keys bind, a <see cref="TimeSpan"/> from its text form.</summary>
    [Fact]
    public void Options_BindFromTeamTurnQueue()
    {
        TeamOptions options = new();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:TurnQueue:Enabled"] = "false",
                ["Team:TurnQueue:ResumeWithin"] = "00:02:00",
                ["Team:TurnQueue:KeepDays"] = "3",
            })
            .Build();
        configuration.GetSection("Team").Bind(options);

        Assert.False(options.TurnQueue.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(2), options.TurnQueue.ResumeWithin);
        Assert.Equal(3, options.TurnQueue.KeepDays);
    }

    private static long CountOutcome(TempDataDir dir, string outcome)
    {
        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM turn_queue WHERE status = 'done' AND outcome = $outcome;";
        _ = command.Parameters.AddWithValue("$outcome", outcome);
        return (long)(command.ExecuteScalar() ?? 0L);
    }
}
