using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Keeps what a deleted Room spent inside its Project, in <c>project_spend_retained</c>, so deleting a Room never lowers the
/// Project's total (and never silently lifts a reached cap). Every method runs inside the caller's transaction on the caller's
/// connection. Amounts are summed in C# as invariant-culture decimal text, never in SQL, as in <see cref="RoomSpendStore"/>.
/// </summary>
internal static class RetainedProjectSpend
{
    /// <summary>
    /// Folds the Room's <c>room_spend</c> into its Project's retained row when it has one, then deletes the Room's
    /// <c>room_spend</c> rows. A Room with no Project simply loses its rows. Must run before the Room's scope rows are deleted.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The caller's transaction.</param>
    /// <param name="roomId">The Room being deleted.</param>
    /// <param name="ct">Cancels the work.</param>
    internal static async Task RetainAndDeleteRoomAsync(SqliteConnection connection, SqliteTransaction transaction, string roomId, CancellationToken ct)
    {
        (string Team, string Project)? scope = await ReadScopeAsync(connection, transaction, roomId, ct).ConfigureAwait(false);
        List<(string Currency, decimal Amount)> spent = await ReadRowsAsync(
            connection,
            transaction,
            "SELECT currency, amount FROM room_spend WHERE room_id = $a;",
            roomId,
            null,
            ct).ConfigureAwait(false);

        if (scope is { } found)
        {
            foreach ((string currency, decimal amount) in spent)
            {
                await AddAsync(connection, transaction, found.Team, found.Project, currency, amount, ct).ConfigureAwait(false);
            }
        }

        await ExecuteAsync(connection, transaction, "DELETE FROM room_spend WHERE room_id = $a;", roomId, null, ct).ConfigureAwait(false);
    }

    /// <summary>Moves the retained rows of <paramref name="oldProject"/> in <paramref name="team"/> to <paramref name="newProject"/>, adding into any row already there.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The caller's transaction.</param>
    /// <param name="team">The Team the Project belongs to.</param>
    /// <param name="oldProject">The Project's old name.</param>
    /// <param name="newProject">The Project's new name.</param>
    /// <param name="ct">Cancels the work.</param>
    internal static async Task MoveProjectAsync(SqliteConnection connection, SqliteTransaction transaction, string team, string oldProject, string newProject, CancellationToken ct)
    {
        if (string.Equals(oldProject, newProject, StringComparison.Ordinal))
        {
            return;
        }

        List<(string Currency, decimal Amount)> rows = await ReadRowsAsync(
            connection,
            transaction,
            "SELECT currency, amount FROM project_spend_retained WHERE team = $a AND project = $b;",
            team,
            oldProject,
            ct).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM project_spend_retained WHERE team = $a AND project = $b;", team, oldProject, ct).ConfigureAwait(false);
        foreach ((string currency, decimal amount) in rows)
        {
            await AddAsync(connection, transaction, team, newProject, currency, amount, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Moves every retained row of Team <paramref name="oldTeam"/> to <paramref name="newTeam"/>, adding into any row already there.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The caller's transaction.</param>
    /// <param name="oldTeam">The Team's old name.</param>
    /// <param name="newTeam">The Team's new name.</param>
    /// <param name="ct">Cancels the work.</param>
    internal static async Task MoveTeamAsync(SqliteConnection connection, SqliteTransaction transaction, string oldTeam, string newTeam, CancellationToken ct)
    {
        if (string.Equals(oldTeam, newTeam, StringComparison.Ordinal))
        {
            return;
        }

        List<(string Project, string Currency, decimal Amount)> rows = [];
        SqliteCommand select = connection.CreateCommand();
        await using (select.ConfigureAwait(false))
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT project, currency, amount FROM project_spend_retained WHERE team = $a;";
            _ = select.Parameters.AddWithValue("$a", oldTeam);
            SqliteDataReader reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    rows.Add((reader.GetString(0), reader.GetString(1), ParseAmount(reader.GetString(2))));
                }
            }
        }

        await ExecuteAsync(connection, transaction, "DELETE FROM project_spend_retained WHERE team = $a;", oldTeam, null, ct).ConfigureAwait(false);
        foreach ((string project, string currency, decimal amount) in rows)
        {
            await AddAsync(connection, transaction, newTeam, project, currency, amount, ct).ConfigureAwait(false);
        }
    }

    private static decimal ParseAmount(string text) => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static async Task<(string Team, string Project)?> ReadScopeAsync(SqliteConnection connection, SqliteTransaction transaction, string roomId, CancellationToken ct)
    {
        SqliteCommand command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT t.team, p.project
                FROM room_teams t
                JOIN room_projects p ON p.room_id = t.room_id
                WHERE t.room_id = $a;
                """;
            _ = command.Parameters.AddWithValue("$a", roomId);
            SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return (reader.GetString(0), reader.GetString(1));
                }
            }
        }

        return null;
    }

    private static async Task<List<(string Currency, decimal Amount)>> ReadRowsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        string first,
        string? second,
        CancellationToken ct)
    {
        List<(string Currency, decimal Amount)> rows = [];
        SqliteCommand command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            _ = command.Parameters.AddWithValue("$a", first);
            if (second is not null)
            {
                _ = command.Parameters.AddWithValue("$b", second);
            }

            SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    rows.Add((reader.GetString(0), ParseAmount(reader.GetString(1))));
                }
            }
        }

        return rows;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        string first,
        string? second,
        CancellationToken ct)
    {
        SqliteCommand command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            _ = command.Parameters.AddWithValue("$a", first);
            if (second is not null)
            {
                _ = command.Parameters.AddWithValue("$b", second);
            }

            _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task AddAsync(SqliteConnection connection, SqliteTransaction transaction, string team, string project, string currency, decimal amount, CancellationToken ct)
    {
        decimal total = amount;
        SqliteCommand select = connection.CreateCommand();
        await using (select.ConfigureAwait(false))
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT amount FROM project_spend_retained WHERE team = $a AND project = $b AND currency = $c;";
            _ = select.Parameters.AddWithValue("$a", team);
            _ = select.Parameters.AddWithValue("$b", project);
            _ = select.Parameters.AddWithValue("$c", currency);
            if (await select.ExecuteScalarAsync(ct).ConfigureAwait(false) is string existing)
            {
                total += ParseAmount(existing);
            }
        }

        SqliteCommand upsert = connection.CreateCommand();
        await using (upsert.ConfigureAwait(false))
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO project_spend_retained(team, project, currency, amount) VALUES($a, $b, $c, $d)
                ON CONFLICT(team, project, currency) DO UPDATE SET amount = excluded.amount;
                """;
            _ = upsert.Parameters.AddWithValue("$a", team);
            _ = upsert.Parameters.AddWithValue("$b", project);
            _ = upsert.Parameters.AddWithValue("$c", currency);
            _ = upsert.Parameters.AddWithValue("$d", total.ToString(CultureInfo.InvariantCulture));
            _ = await upsert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
