using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores, per Team, when its digest and retro last fired, in the same <c>team.db</c> as the other stores but in its
/// own table, so a restart repeats nothing (NF-8). Deliberately synchronous like <see cref="PersonaModelStore"/>.
/// </summary>
public sealed class CadenceStateStore
{
    private const string DdlCreateCadenceState = """
        CREATE TABLE IF NOT EXISTS cadence_state (
            team                TEXT PRIMARY KEY COLLATE NOCASE,
            last_digest_utc     TEXT,
            last_retro_utc      TEXT,
            done_at_last_retro  INTEGER
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    public CadenceStateStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreateCadenceState);
    }

    /// <summary>Reads the stored state of a Team.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The state, or <see langword="null"/> when the Team was never seen.</returns>
    public CadenceState? Get(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT last_digest_utc, last_retro_utc, done_at_last_retro FROM cadence_state WHERE team = $team;";
        _ = command.Parameters.AddWithValue("$team", team);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new CadenceState(
            team,
            reader.IsDBNull(0) ? null : ParseTime(reader.GetString(0)),
            reader.IsDBNull(1) ? null : ParseTime(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetInt32(2));
    }

    /// <summary>Stores the state of a Team, replacing what was there.</summary>
    /// <param name="state">The state to store.</param>
    public void Set(CadenceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO cadence_state(team, last_digest_utc, last_retro_utc, done_at_last_retro)
            VALUES($team, $digest, $retro, $done)
            ON CONFLICT(team) DO UPDATE SET
                last_digest_utc = excluded.last_digest_utc,
                last_retro_utc = excluded.last_retro_utc,
                done_at_last_retro = excluded.done_at_last_retro;
            """;
        _ = command.Parameters.AddWithValue("$team", state.Team);
        _ = command.Parameters.AddWithValue("$digest", FormatTime(state.LastDigestUtc));
        _ = command.Parameters.AddWithValue("$retro", FormatTime(state.LastRetroUtc));
#pragma warning disable HAA0601 // AddWithValue takes object, so the box cannot be avoided.
        _ = command.Parameters.AddWithValue("$done", state.DoneAtLastRetro is { } done ? done : DBNull.Value);
#pragma warning restore HAA0601
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets the stored state of a Team, so its next tick baselines at that moment instead of catching up.</summary>
    /// <param name="team">The Team's name.</param>
    public void Remove(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM cadence_state WHERE team = $team;";
        _ = command.Parameters.AddWithValue("$team", team);
        _ = command.ExecuteNonQuery();
    }

    private static object FormatTime(DateTimeOffset? time) =>
        time is { } value ? value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value;

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
