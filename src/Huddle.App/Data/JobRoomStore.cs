using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Which Room is a Job's own: the <c>room_jobs</c> table that <see cref="SqliteTeamDirectory"/> also reads, to keep a Job Room
/// out of the one-to-one lookups. A Room's row goes when the Room is deleted, so a Job whose Room is gone gets a new one. A
/// deleted Job's row stays: its Room keeps its history and stays a Job Room, so it can never become an Agent's one-to-one.
/// </summary>
internal sealed class JobRoomStore
{
    private readonly string connectionString;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    public JobRoomStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, SqliteTeamDirectory.DdlCreateRoomJobs);
    }

    /// <summary>The Room a Job posts into.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <returns>The Room's id, or <see langword="null"/> when it has none.</returns>
    public string? RoomOf(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT room_id FROM room_jobs WHERE job_id = $job;";
        _ = command.Parameters.AddWithValue("$job", jobId);
        return command.ExecuteScalar() as string;
    }

    /// <summary>Makes <paramref name="roomId"/> the Job's Room, replacing any earlier one.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <param name="roomId">The Room's id; the Room must exist.</param>
    public void Set(string jobId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM room_jobs WHERE job_id = $job OR room_id = $room;";
            _ = clear.Parameters.AddWithValue("$job", jobId);
            _ = clear.Parameters.AddWithValue("$room", roomId);
            _ = clear.ExecuteNonQuery();
        }

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO room_jobs(room_id, job_id) VALUES($room, $job);";
            _ = insert.Parameters.AddWithValue("$job", jobId);
            _ = insert.Parameters.AddWithValue("$room", roomId);
            _ = insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
