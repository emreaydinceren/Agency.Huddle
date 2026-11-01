using Agency.Huddle.App.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// The Human's choice per Helper, one all-or-nothing row each in <c>team.db</c> (Helpers Spec §6.6). With no row the
/// configuration applies; Reset to defaults deletes the row.
/// </summary>
/// <remarks>The SQLite parameter values are boxed (<c>HAA0601</c>); this runs when the Human presses Save, not on a hot path.</remarks>
internal sealed class HelperSettingsStore
{
    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS helper_settings (
            helper_id  TEXT PRIMARY KEY COLLATE NOCASE,
            enabled    INTEGER NOT NULL,
            adapter    TEXT,
            model      TEXT,
            effort     TEXT,
            daily_cap  INTEGER NOT NULL
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens <c>team.db</c> under the data directory and creates the table.</summary>
    /// <param name="options">The bound options, read for <see cref="TeamOptions.DataDir"/>.</param>
    public HelperSettingsStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);
    }

    /// <summary>The Human's row, or <see langword="null"/> when they have not saved one.</summary>
    /// <param name="id">The Helper.</param>
    public HelperSettingsRow? Get(HelperId id)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT enabled, adapter, model, effort, daily_cap FROM helper_settings WHERE helper_id = $id;";
        command.Parameters.AddWithValue("$id", id.Value);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new HelperSettingsRow(
            reader.GetInt32(0) != 0,
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetInt32(4));
    }

    /// <summary>Saves the Human's choice, replacing any earlier row.</summary>
    /// <param name="id">The Helper.</param>
    /// <param name="row">The choice.</param>
    public void Save(HelperId id, HelperSettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO helper_settings(helper_id, enabled, adapter, model, effort, daily_cap) VALUES($id, $enabled, $adapter, $model, $effort, $cap)
            ON CONFLICT(helper_id) DO UPDATE SET enabled = excluded.enabled, adapter = excluded.adapter, model = excluded.model,
                effort = excluded.effort, daily_cap = excluded.daily_cap;
            """;
        command.Parameters.AddWithValue("$id", id.Value);
#pragma warning disable HAA0601 // Cold path: the Human pressed Save.
        command.Parameters.AddWithValue("$enabled", row.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$cap", row.DailyCap);
#pragma warning restore HAA0601
        command.Parameters.AddWithValue("$adapter", (object?)NullIfBlank(row.Adapter) ?? DBNull.Value);
        command.Parameters.AddWithValue("$model", (object?)NullIfBlank(row.Model) ?? DBNull.Value);
        command.Parameters.AddWithValue("$effort", (object?)NullIfBlank(row.Effort) ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>Reset to defaults: deletes the row.</summary>
    /// <param name="id">The Helper.</param>
    public void Delete(HelperId id)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM helper_settings WHERE helper_id = $id;";
        command.Parameters.AddWithValue("$id", id.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>A blank value is stored as <see langword="null"/>, meaning "the default".</summary>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
