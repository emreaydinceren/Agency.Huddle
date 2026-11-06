using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// The stored flag behind the one-time notice on the Agents page that told existing installs their Agents lost the account
/// connectors (docs/Huddle.AgentConnectors-Specifications.md section 13). Three states: no row (not decided yet), <c>show</c>
/// (an Agent existed at upgrade and the Human has not dismissed it) and <c>done</c> (nothing to show, or dismissed). Lives in
/// <c>team.db</c> in its own table.
/// </summary>
public sealed class ConnectorsNoticeStore
{
    private const string Show = "show";
    private const string Done = "done";
    private const string Create = """
        CREATE TABLE IF NOT EXISTS connectors_notice (
            id    INTEGER PRIMARY KEY CHECK (id = 1),
            state TEXT NOT NULL
        );
        """;

    private readonly string connectionString;

    /// <summary>Opens the store and creates its table when the database does not have it yet.</summary>
    /// <param name="options">The Team options, which name the data directory.</param>
    public ConnectorsNoticeStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, Create);
    }

    /// <summary>Whether the notice should be shown now: decided at upgrade, and not yet dismissed.</summary>
    public bool IsShown => string.Equals(this.Read(), Show, StringComparison.Ordinal);

    /// <summary>
    /// Decides, once, whether this install gets the notice: it does when an Agent already existed at the first start of this
    /// build. A later call changes nothing, so a restart never shows a dismissed notice again.
    /// </summary>
    /// <param name="agentExists">Whether at least one Persona exists at this start.</param>
    public void DecideOnce(bool agentExists)
    {
        if (this.Read() is not null)
        {
            return;
        }

        this.Write(agentExists ? Show : Done);
    }

    /// <summary>Marks the notice dismissed, so it is not shown again.</summary>
    public void Dismiss() => this.Write(Done);

    private string? Read()
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT state FROM connectors_notice WHERE id = 1;";
        return command.ExecuteScalar() as string;
    }

    private void Write(string state)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO connectors_notice(id, state) VALUES(1, $state)
            ON CONFLICT(id) DO UPDATE SET state = excluded.state;
            """;
        command.Parameters.AddWithValue("$state", state);
        command.ExecuteNonQuery();
    }
}
