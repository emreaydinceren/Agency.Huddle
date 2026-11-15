using Agency.Huddle.App.Teams;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Keeps the global Needs You level and spend cap the Human saved in Settings (ADR-0087), as one row of <c>team.db</c>. While a row exists it
/// wins over <c>Team:NeedsYouLevel</c>. No tool writes it: only the Settings page does, through <c>NeedsYouLevels.SetGlobal</c>.
/// </summary>
/// <remarks>Deliberately synchronous, in the style of <see cref="PersonaModelStore"/>.</remarks>
internal sealed class NeedsYouLevelStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS needs_you_level (
            id        INTEGER PRIMARY KEY CHECK (id = 1),
            level     TEXT NOT NULL,
            spend_cap TEXT NULL
        );
        """;

    private const string Select = "SELECT level, spend_cap FROM needs_you_level WHERE id = 1;";

    private const string Upsert = """
        INSERT INTO needs_you_level(id, level, spend_cap) VALUES(1, $level, $cap)
        ON CONFLICT(id) DO UPDATE SET level = excluded.level, spend_cap = excluded.spend_cap;
        """;

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the table in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public NeedsYouLevelStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>Reads the saved global level and cap.</summary>
    /// <returns>The saved pair, or <see langword="null"/> when Settings has saved none (the configuration then decides).</returns>
    public SavedNeedsYouLevel? Get()
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Select;
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || NeedsYouLevelText.ParseOrNull(reader.GetString(0)) is not { } level)
        {
            return null;
        }

        return new SavedNeedsYouLevel(level, reader.IsDBNull(1) ? null : NeedsYouLevelText.ParseCapOrNull(reader.GetString(1)));
    }

    /// <summary>Saves the global level and cap, replacing any earlier pair.</summary>
    /// <param name="level">The level.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; a cap of zero or less is stored as none.</param>
    public void Set(NeedsYouLevel level, decimal? spendCap)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Upsert;
        _ = command.Parameters.AddWithValue("$level", level.ToWire());
        object capValue = NeedsYouLevelText.Usable(spendCap) is { } cap ? NeedsYouLevelText.FormatCap(cap) : DBNull.Value;
        _ = command.Parameters.AddWithValue("$cap", capValue);
        _ = command.ExecuteNonQuery();
    }
}
