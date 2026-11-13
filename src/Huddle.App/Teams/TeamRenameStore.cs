using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Agency.Huddle.App.Teams;

/// <summary>The <c>team_renames</c> journal in <c>team.db</c> (Spec 6.3). Deliberately synchronous like <c>CadenceStateStore</c>.</summary>
internal sealed class TeamRenameStore : ITeamRenames
{
    private const string DdlCreateTeamRenames = """
        CREATE TABLE IF NOT EXISTS team_renames (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            kind        TEXT NOT NULL,
            team        TEXT NOT NULL COLLATE NOCASE,
            old_name    TEXT NOT NULL COLLATE NOCASE,
            new_name    TEXT NOT NULL COLLATE NOCASE,
            state       TEXT NOT NULL,
            renamed_at  TEXT NOT NULL
        );
        """;

    private const string SelectRows = "SELECT id, kind, team, old_name, new_name, state, renamed_at FROM team_renames ";

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    /// <param name="timeProvider">The clock; the system clock when <see langword="null"/>.</param>
    public TeamRenameStore(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreateTeamRenames);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public long Begin(TeamRenameKind kind, string team, string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO team_renames(kind, team, old_name, new_name, state, renamed_at)
            VALUES($kind, $team, $old, $new, $state, $at)
            RETURNING id;
            """;
        _ = command.Parameters.AddWithValue("$kind", kind == TeamRenameKind.Project ? nameof(TeamRenameKind.Project) : nameof(TeamRenameKind.Team));
        _ = command.Parameters.AddWithValue("$team", team);
        _ = command.Parameters.AddWithValue("$old", oldName);
        _ = command.Parameters.AddWithValue("$new", newName);
        _ = command.Parameters.AddWithValue("$state", nameof(TeamRenameState.Pending));
        _ = command.Parameters.AddWithValue("$at", FormatTime(this.timeProvider.GetUtcNow()));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public void Complete(long id)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE team_renames SET state = $done WHERE id = $id AND state = $pending;";
        _ = command.Parameters.AddWithValue("$done", nameof(TeamRenameState.Done));
        _ = command.Parameters.AddWithValue("$pending", nameof(TeamRenameState.Pending));
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
        _ = command.Parameters.AddWithValue("$id", id);
#pragma warning restore HAA0601
        _ = command.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public void Discard(long id)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM team_renames WHERE id = $id AND state = $pending;";
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
        _ = command.Parameters.AddWithValue("$id", id);
#pragma warning restore HAA0601
        _ = command.Parameters.AddWithValue("$pending", nameof(TeamRenameState.Pending));
        if (command.ExecuteNonQuery() > 0)
        {
            return;
        }

        if (this.Read("WHERE state = 'Done' AND id = $a ", id).Count > 0)
        {
            throw new InvalidOperationException($"Rename {id} is done and cannot be discarded.");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<TeamRename> Pending() => this.Read("WHERE state = 'Pending' ", 0);

    /// <inheritdoc/>
    public IReadOnlyList<TeamRename> DoneAfter(long id) => this.Read("WHERE state = 'Done' AND id > $a ", id);

    /// <inheritdoc/>
    public IReadOnlyList<TeamRename> DoneSince(DateTimeOffset since) =>
        [.. this.Read("WHERE state = 'Done' ", 0).Where(r => r.RenamedAt >= since)];

    /// <inheritdoc/>
    public long LatestDoneBefore(DateTimeOffset at) =>
        this.Read("WHERE state = 'Done' ", 0).Where(r => r.RenamedAt < at).Select(r => r.Id).DefaultIfEmpty(0).Max();

    /// <inheritdoc/>
    public string Resolve(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        return ResolveTeam(this.Read("WHERE state = 'Done' ", 0), team);
    }

    /// <inheritdoc/>
    public string ResolveProject(string team, string project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        List<TeamRename> done = this.Read("WHERE state = 'Done' ", 0);
        string resolvedTeam = ResolveTeam(done, team);
        string current = project;
        foreach (TeamRename row in done)
        {
            if (row.Kind == TeamRenameKind.Project
                && string.Equals(row.OldName, current, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ResolveTeam(done, row.Team), resolvedTeam, StringComparison.OrdinalIgnoreCase))
            {
                current = row.NewName;
            }
        }

        return current;
    }

    /// <inheritdoc/>
    public bool IsReserved(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return this.Read("WHERE kind = 'Team' ", 0).Any(r => Holds(r, name));
    }

    /// <inheritdoc/>
    public bool IsPending(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        return this.Read("WHERE kind = 'Team' AND state = 'Pending' ", 0).Any(r => Holds(r, team));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> EarlierNames(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        List<TeamRename> done = this.Read("WHERE state = 'Done' ", 0);
        string today = ResolveTeam(done, team);
        List<string> names = [];
        if (!string.Equals(today, team, StringComparison.OrdinalIgnoreCase))
        {
            return names;
        }

        foreach (TeamRename row in done)
        {
            if (row.Kind == TeamRenameKind.Team
                && !string.Equals(row.OldName, today, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ResolveTeam(done, row.OldName), today, StringComparison.OrdinalIgnoreCase)
                && !names.Contains(row.OldName, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(row.OldName);
            }
        }

        return names;
    }

    private static bool Holds(TeamRename row, string name) =>
        string.Equals(row.OldName, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(row.NewName, name, StringComparison.OrdinalIgnoreCase);

    private static string ResolveTeam(List<TeamRename> done, string team)
    {
        string current = team;
        foreach (TeamRename row in done)
        {
            if (row.Kind == TeamRenameKind.Team && string.Equals(row.OldName, current, StringComparison.OrdinalIgnoreCase))
            {
                current = row.NewName;
            }
        }

        return current;
    }

    private static string FormatTime(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private List<TeamRename> Read(string where, long argument)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
#pragma warning disable S2077 // Every caller passes a compile-time literal WHERE clause; the value travels as a parameter.
        command.CommandText = SelectRows + where + "ORDER BY id;";
#pragma warning restore S2077
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
        _ = command.Parameters.AddWithValue("$a", argument);
#pragma warning restore HAA0601

        List<TeamRename> rows = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new TeamRename(
                reader.GetInt64(0),
                Enum.Parse<TeamRenameKind>(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                Enum.Parse<TeamRenameState>(reader.GetString(5)),
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)));
        }

        return rows;
    }
}
