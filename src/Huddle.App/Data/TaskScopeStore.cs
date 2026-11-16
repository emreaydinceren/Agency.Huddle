using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Remembers, per Task, the scope Huddle placed it in (issue #602): the Team and Project ids (<see cref="ScopeIdStore"/>) it was created in or
/// the Human last moved it to. A Needs You level applies to a Task by the stricter of this scope and the folder the file sits in now, so
/// neither an Agent's <c>update_task</c> nor a shell <c>mv</c> can put a Task under a laxer level.
/// </summary>
/// <remarks>Deliberately synchronous, in the style of <see cref="CadenceStateStore"/>.</remarks>
internal sealed class TaskScopeStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS task_scopes (
            task_id    TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
            team_id    TEXT NOT NULL,
            project_id TEXT NULL
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the table in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public TaskScopeStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>The scope a Task was placed in.</summary>
    /// <param name="TeamId">The Team's id.</param>
    /// <param name="ProjectId">The Project's id, or <see langword="null"/> for work directly under the Team.</param>
    internal sealed record Placement(string TeamId, string? ProjectId);

    /// <summary>Records the scope of a Task, replacing any earlier one.</summary>
    /// <param name="taskId">The Task's id as written, for example <c>DEV-12</c>.</param>
    /// <param name="teamId">The Team's id.</param>
    /// <param name="projectId">The Project's id, or <see langword="null"/> for none.</param>
    public void Set(string taskId, string teamId, string? projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO task_scopes(task_id, team_id, project_id) VALUES($task, $team, $project)
            ON CONFLICT(task_id) DO UPDATE SET team_id = excluded.team_id, project_id = excluded.project_id;
            """;
        _ = command.Parameters.AddWithValue("$task", taskId);
        _ = command.Parameters.AddWithValue("$team", teamId);
        _ = command.Parameters.AddWithValue("$project", (object?)projectId ?? DBNull.Value);
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Reads the scope recorded for a Task.</summary>
    /// <param name="taskId">The Task's id as written.</param>
    /// <returns>The scope, or <see langword="null"/> for a Task Huddle never placed (for example a file written by a shell).</returns>
    public Placement? Get(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT team_id, project_id FROM task_scopes WHERE task_id = $task;";
        _ = command.Parameters.AddWithValue("$task", taskId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        string? projectId = reader.IsDBNull(1) ? null : reader.GetString(1);
        return new Placement(reader.GetString(0), projectId);
    }
}
