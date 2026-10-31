using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Data;

/// <summary>
/// One text override per Persona, keyed by Persona name, in its own <c>team.db</c> table: the shape
/// <see cref="PersonaModelStore"/>, <see cref="PersonaEffortStore"/> and
/// <see cref="PersonaWorkModeStore"/> share. A blank value is never stored; setting one deletes the
/// row, since a stored blank would be a row that exists but never resolves to a usable value.
/// </summary>
internal sealed class PersonaOverrideTable
{
    private readonly string connectionString;
    private readonly PersonaOverrideSql sql;

    /// <summary>Opens <c>team.db</c> and creates the table when the database does not have it yet.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="sql">The store's own statements.</param>
    public PersonaOverrideTable(TeamOptions options, PersonaOverrideSql sql)
    {
        this.connectionString = SqliteTable.Open(options, sql.Create);
        this.sql = sql;
    }

    /// <summary>Returns the stored value for a Persona, or <c>null</c> if none is set.</summary>
    /// <param name="personaName">The Persona's name, matched as the table's collation says.</param>
    /// <returns>The value, or <c>null</c>.</returns>
    public string? Get(string personaName)
    {
        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = this.sql.Select;
        command.Parameters.AddWithValue("$name", personaName);

        return command.ExecuteScalar() as string;
    }

    /// <summary>Stores the value for a Persona, or deletes the row when <paramref name="value"/> is <c>null</c> or blank.</summary>
    /// <param name="personaName">The Persona's name.</param>
    /// <param name="value">The value to store, or <c>null</c> to clear it.</param>
    public void Set(string personaName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            this.Remove(personaName);
            return;
        }

        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = this.sql.Upsert;
        command.Parameters.AddWithValue("$name", personaName);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    /// <summary>Removes any stored value for a Persona, leaving no row behind.</summary>
    /// <param name="personaName">The Persona's name.</param>
    public void Remove(string personaName)
    {
        using SqliteConnection connection = this.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = this.sql.Delete;
        command.Parameters.AddWithValue("$name", personaName);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(this.connectionString);
        connection.Open();
        return connection;
    }
}
