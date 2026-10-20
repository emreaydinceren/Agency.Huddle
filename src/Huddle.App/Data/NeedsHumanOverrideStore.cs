using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// The Tasks the Human turned the Human gate off on, so the Needs Human Helper never judges them again
/// (Helpers Spec §6.9). The ids are loaded once and written through, so <see cref="Contains"/> touches no file.
/// Rows are never deleted: a Task id never changes, even when the Task moves.
/// </summary>
internal sealed class NeedsHumanOverrideStore
{
    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS needs_human_overrides (
            task_id TEXT PRIMARY KEY COLLATE NOCASE,
            at      TEXT NOT NULL
        );
        """;

    private readonly Lock gate = new();
    private readonly HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    /// <summary>Opens <c>team.db</c> under the data directory, creates the table and loads the ids.</summary>
    /// <param name="options">The bound options, read for <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="timeProvider">The clock, for the row's time.</param>
    public NeedsHumanOverrideStore(IOptions<TeamOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.timeProvider = timeProvider;
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);

        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT task_id FROM needs_human_overrides;";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            this.ids.Add(reader.GetString(0));
        }
    }

    /// <summary>Whether the Human turned the gate off on this Task.</summary>
    /// <param name="taskId">The Task id, such as <c>RSH-0208</c>.</param>
    public bool Contains(string taskId)
    {
        lock (this.gate)
        {
            return this.ids.Contains(taskId);
        }
    }

    /// <summary>Records an override; recording the same Task again changes nothing.</summary>
    /// <param name="taskId">The Task id.</param>
    public void Add(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        lock (this.gate)
        {
            if (this.ids.Contains(taskId))
            {
                return;
            }

            // Written first: if the INSERT throws, the id is not remembered in memory either, so a retry is not skipped.
            using SqliteConnection connection = this.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO needs_human_overrides(task_id, at) VALUES($id, $at);";
            command.Parameters.AddWithValue("$id", taskId);
            command.Parameters.AddWithValue("$at", this.timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
            _ = this.ids.Add(taskId);
        }
    }

    /// <summary>Opens a connection to <c>team.db</c>.</summary>
    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(this.connectionString);
        connection.Open();
        return connection;
    }
}
