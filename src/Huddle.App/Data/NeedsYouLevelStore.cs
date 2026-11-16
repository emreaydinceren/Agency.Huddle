using Agency.Huddle.App.Teams;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Keeps the Needs You level and spend cap the Human set for each scope (ADR-0088), one row of <c>team.db</c> per scope key: <c>global</c>,
/// <c>team:&lt;teamId&gt;</c> or <c>project:&lt;projectId&gt;</c>, built by <c>NeedsYouCaps</c> from the ids <see cref="ScopeIdStore"/> mints. While a
/// row exists for the global scope it wins over <c>Team:NeedsYouLevel</c>. No tool writes it: only <c>NeedsYouLevels</c> does, for the pages.
/// </summary>
/// <remarks>Deliberately synchronous, in the style of <see cref="PersonaModelStore"/>.</remarks>
internal sealed class NeedsYouLevelStore
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS needs_you_levels (
            scope     TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
            level     TEXT NOT NULL,
            spend_cap TEXT NULL
        );
        """;

    private const string Select = "SELECT level, spend_cap FROM needs_you_levels WHERE scope = $scope;";

    private const string Upsert = """
        INSERT INTO needs_you_levels(scope, level, spend_cap) VALUES($scope, $level, $cap)
        ON CONFLICT(scope) DO UPDATE SET level = excluded.level, spend_cap = excluded.spend_cap;
        """;

    private readonly string connectionString;

    /// <summary>Opens (and if needed creates) the table in <c>team.db</c> under the configured data folder.</summary>
    /// <param name="options">The Team options carrying <see cref="TeamOptions.DataDir"/>.</param>
    public NeedsYouLevelStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Ddl);
    }

    /// <summary>Reads the level and cap a scope itself sets.</summary>
    /// <param name="scope">The scope key, compared ignoring case.</param>
    /// <returns>The saved pair, or <see langword="null"/> when the scope sets none.</returns>
    public SavedNeedsYouLevel? Get(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Select;
        _ = command.Parameters.AddWithValue("$scope", scope);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || NeedsYouLevelText.ParseOrNull(reader.GetString(0)) is not { } level)
        {
            return null;
        }

        return new SavedNeedsYouLevel(level, reader.IsDBNull(1) ? null : NeedsYouLevelText.ParseCapOrNull(reader.GetString(1)));
    }

    /// <summary>Saves a scope's level and cap, replacing any earlier pair.</summary>
    /// <param name="scope">The scope key.</param>
    /// <param name="level">The level.</param>
    /// <param name="spendCap">The cap in USD, or <see langword="null"/> for none; a cap of zero or less is stored as none.</param>
    public void Set(string scope, NeedsYouLevel level, decimal? spendCap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Upsert;
        _ = command.Parameters.AddWithValue("$scope", scope);
        _ = command.Parameters.AddWithValue("$level", level.ToWire());
        object capValue = NeedsYouLevelText.Usable(spendCap) is { } cap ? NeedsYouLevelText.FormatCap(cap) : DBNull.Value;
        _ = command.Parameters.AddWithValue("$cap", capValue);
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets a scope's level and cap, so the scope inherits again; nothing happens when it sets none.</summary>
    /// <param name="scope">The scope key.</param>
    public void Delete(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM needs_you_levels WHERE scope = $scope;";
        _ = command.Parameters.AddWithValue("$scope", scope);
        _ = command.ExecuteNonQuery();
    }
}
