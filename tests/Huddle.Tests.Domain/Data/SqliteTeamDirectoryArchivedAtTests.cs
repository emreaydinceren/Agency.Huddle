using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// <see cref="Room.ArchivedAt"/> (UX-E13): the instant a Room was archived, read from the
/// <c>archived_rooms</c> row that already made <see cref="Room.Archived"/> true.
/// </summary>
public sealed class SqliteTeamDirectoryArchivedAtTests
{
    private static readonly DateTimeOffset ArchivedMoment = new(2026, 10, 3, 14, 30, 0, TimeSpan.Zero);

    /// <summary>Archiving stamps the clock's instant, and the Room reads it back through the same lookups the sidebar uses.</summary>
    [Fact]
    public async Task SetRoomArchived_RecordsTheInstant_AndRoomsReadItBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(ArchivedMoment);
        SqliteTeamDirectory directory = new(dir.Options(), clock);
        await directory.InitializeAsync("You", ct);
        Room room = await directory.CreateRoomAsync("Plan", [KnownIds.Human], null, false, ct);

        await directory.SetRoomArchivedAsync(room.Id, archived: true, ct);

        Room? byId = await directory.GetRoomAsync(room.Id, ct);
        Room all = Assert.Single(await directory.GetRoomsAsync(ct), candidate => candidate.Id == room.Id);
        Assert.Equal(ArchivedMoment, byId?.ArchivedAt);
        Assert.Equal(ArchivedMoment, all.ArchivedAt);
    }

    /// <summary>A Room that is not archived has no archive instant, and unarchiving clears it.</summary>
    [Fact]
    public async Task ArchivedAt_IsNull_ForALiveRoom_AndAfterUnarchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options(), new FakeTimeProvider(ArchivedMoment));
        await directory.InitializeAsync("You", ct);
        Room room = await directory.CreateRoomAsync("Plan", [KnownIds.Human], null, false, ct);
        Assert.Null((await directory.GetRoomAsync(room.Id, ct))?.ArchivedAt);

        await directory.SetRoomArchivedAsync(room.Id, archived: true, ct);
        await directory.SetRoomArchivedAsync(room.Id, archived: false, ct);

        Room? after = await directory.GetRoomAsync(room.Id, ct);
        Assert.False(after?.Archived);
        Assert.Null(after?.ArchivedAt);
    }

    /// <summary>A Room archived before the instant was recorded (a value that is not a timestamp) stays archived with a null instant, so the dialog falls back to "Created".</summary>
    [Fact]
    public async Task ArchivedAt_IsNull_WhenTheStoredValueIsNotATimestamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        Room room = await directory.CreateRoomAsync("Plan", [KnownIds.Human], null, false, ct);
        string dbPath = Path.Combine(dir.Options().Value.DataDir, "team.db");
        await using (SqliteConnection connection = new($"Data Source={dbPath};Pooling=False"))
        {
            await connection.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO archived_rooms(room_id, archived) VALUES($id, 'yes');";
            command.Parameters.AddWithValue("$id", room.Id);
            await command.ExecuteNonQueryAsync(ct);
        }

        Room? read = await directory.GetRoomAsync(room.Id, ct);

        Assert.True(read?.Archived);
        Assert.Null(read?.ArchivedAt);
    }
}
