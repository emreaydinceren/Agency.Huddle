using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Gives every Team and every Project a stable id in <c>team.db</c>, minted the first time something needs to key by it. A setting that must
/// outlive a rename (the Needs You level and its spend cap, ADR-0088) is stored under the id, and a rename changes only the name stored here.
/// </summary>
/// <remarks>Deliberately synchronous, in the style of <see cref="CadenceStateStore"/>. Names compare ignoring case.</remarks>
internal sealed class ScopeIdStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS team_ids (
            id   TEXT PRIMARY KEY,
            team TEXT NOT NULL COLLATE NOCASE UNIQUE
        );
        CREATE TABLE IF NOT EXISTS project_ids (
            id      TEXT PRIMARY KEY,
            team_id TEXT NOT NULL,
            project TEXT NOT NULL COLLATE NOCASE,
            UNIQUE(team_id, project)
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the tables in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public ScopeIdStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>The id of a Team, minted when it has none.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The id.</returns>
    public string TeamId(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return EnsureTeam(connection, team);
    }

    /// <summary>The id of a Team, without minting one.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The id, or <see langword="null"/> when nothing has keyed by this Team yet.</returns>
    public string? FindTeamId(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Scalar(connection, "SELECT id FROM team_ids WHERE team = $a;", team);
    }

    /// <summary>The id of a Project, minted when it has none (and the Team's too).</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <returns>The id.</returns>
    public string ProjectId(string team, string project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        string teamId = EnsureTeam(connection, team);
        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO project_ids(id, team_id, project) VALUES($id, $team, $project) ON CONFLICT DO NOTHING;";
        _ = insert.Parameters.AddWithValue("$id", NewId());
        _ = insert.Parameters.AddWithValue("$team", teamId);
        _ = insert.Parameters.AddWithValue("$project", project);
        _ = insert.ExecuteNonQuery();
        return Scalar(connection, "SELECT id FROM project_ids WHERE team_id = $a AND project = $b;", teamId, project)
            ?? throw new InvalidOperationException("The Project id could not be read back.");
    }

    /// <summary>The id of a Project, without minting one.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    /// <returns>The id, or <see langword="null"/> when nothing has keyed by this Project yet.</returns>
    public string? FindProjectId(string team, string project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Scalar(connection, "SELECT p.id FROM project_ids p JOIN team_ids t ON t.id = p.team_id WHERE t.team = $a AND p.project = $b;", team, project);
    }

    /// <summary>The Team's name today.</summary>
    /// <param name="teamId">A Team id.</param>
    /// <returns>The name, or <see langword="null"/> for an id this store never minted.</returns>
    public string? TeamNameOf(string teamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        return Scalar(connection, "SELECT team FROM team_ids WHERE id = $a;", teamId);
    }

    /// <summary>The names of a Project and its Team today.</summary>
    /// <param name="projectId">A Project id.</param>
    /// <returns>The Team's and the Project's names, or <see langword="null"/> for an id this store never minted.</returns>
    public (string Team, string Project)? ProjectNamesOf(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT t.team, p.project FROM project_ids p JOIN team_ids t ON t.id = p.team_id WHERE p.id = $a;";
        _ = command.Parameters.AddWithValue("$a", projectId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    /// <summary>Gives a Team's id its new name; a replayed rename, or a Team that never had an id, changes nothing.</summary>
    /// <param name="oldName">The Team's old name.</param>
    /// <param name="newName">The Team's new name.</param>
    public void RenameTeam(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM team_ids
            WHERE team = $new AND $old <> $new COLLATE NOCASE AND EXISTS (SELECT 1 FROM team_ids WHERE team = $old);
            UPDATE team_ids SET team = $new WHERE team = $old;
            """;
        _ = command.Parameters.AddWithValue("$old", oldName);
        _ = command.Parameters.AddWithValue("$new", newName);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    /// <summary>Gives a Project's id its new name; a replayed rename, or a Project that never had an id, changes nothing.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="oldName">The Project's old name.</param>
    /// <param name="newName">The Project's new name.</param>
    public void RenameProject(string team, string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM project_ids
            WHERE project = $new AND $old <> $new COLLATE NOCASE
                AND team_id = (SELECT id FROM team_ids WHERE team = $team)
                AND EXISTS (SELECT 1 FROM project_ids WHERE project = $old AND team_id = (SELECT id FROM team_ids WHERE team = $team));
            UPDATE project_ids SET project = $new
            WHERE project = $old AND team_id = (SELECT id FROM team_ids WHERE team = $team);
            """;
        _ = command.Parameters.AddWithValue("$team", team);
        _ = command.Parameters.AddWithValue("$old", oldName);
        _ = command.Parameters.AddWithValue("$new", newName);
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string EnsureTeam(SqliteConnection connection, string team)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO team_ids(id, team) VALUES($id, $team) ON CONFLICT DO NOTHING;";
        _ = insert.Parameters.AddWithValue("$id", NewId());
        _ = insert.Parameters.AddWithValue("$team", team);
        _ = insert.ExecuteNonQuery();
        return Scalar(connection, "SELECT id FROM team_ids WHERE team = $a;", team)
            ?? throw new InvalidOperationException("The Team id could not be read back.");
    }

    private static string? Scalar(SqliteConnection connection, string sql, string a, string? b = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.Parameters.AddWithValue("$a", a);
        if (b is not null)
        {
            _ = command.Parameters.AddWithValue("$b", b);
        }

        return command.ExecuteScalar() as string;
    }
}
