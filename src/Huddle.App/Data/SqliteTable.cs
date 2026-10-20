using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Data;

/// <summary>Opens <c>team.db</c> and creates one table in it, the setup every small store in this folder repeats.</summary>
internal static class SqliteTable
{
    /// <summary>Creates the data directory and the table when missing, and returns the connection string.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="createDdl">A <c>CREATE TABLE IF NOT EXISTS</c> statement.</param>
    public static string Open(TeamOptions options, string createDdl)
    {
        ArgumentNullException.ThrowIfNull(options);

        string dbPath = Path.Combine(options.DataDir, "team.db");
        string connectionString = $"Data Source={dbPath}";
        string? directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using SqliteConnection connection = new(connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = createDdl;
        command.ExecuteNonQuery();
        return connectionString;
    }
}
