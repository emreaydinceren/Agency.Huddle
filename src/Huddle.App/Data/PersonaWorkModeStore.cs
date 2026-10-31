using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores the Work Mode for each Persona, keyed by Persona name, in the same <c>team.db</c> file as
/// <see cref="SqliteTeamDirectory"/> but in its own table. A new table rather than a column is
/// deliberate, for the reason <see cref="PersonaEffortStore"/> records: the schema is created with
/// <c>CREATE TABLE IF NOT EXISTS</c>, which silently ignores an added column on a database that
/// predates it, whereas a new table is created cleanly on an existing <c>App_Data</c>.
/// </summary>
/// <remarks>
/// Deliberately synchronous, as <see cref="PersonaEffortStore"/> is: its only caller,
/// <see cref="Agency.Huddle.App.Acp.PersonaStore"/>, is synchronous file I/O throughout.
/// </remarks>
public sealed class PersonaWorkModeStore
{
    private static readonly PersonaOverrideSql Sql = new(
        Create: """
            CREATE TABLE IF NOT EXISTS persona_work_modes (
                persona_name TEXT PRIMARY KEY COLLATE NOCASE,
                work_mode    TEXT NOT NULL
            );
            """,
        Select: "SELECT work_mode FROM persona_work_modes WHERE persona_name = $name;",
        Upsert: """
            INSERT INTO persona_work_modes(persona_name, work_mode) VALUES($name, $value)
            ON CONFLICT(persona_name) DO UPDATE SET work_mode = excluded.work_mode;
            """,
        Delete: "DELETE FROM persona_work_modes WHERE persona_name = $name;");

    private readonly PersonaOverrideTable table;

    /// <summary>Opens the store and creates its table when the database does not have it yet.</summary>
    /// <param name="options">The Team options, which name the data directory.</param>
    public PersonaWorkModeStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.table = new PersonaOverrideTable(options.Value, Sql);
    }

    /// <summary>Returns the stored Work Mode for a Persona, or <c>null</c> if none is set (the Adapter's default).</summary>
    /// <param name="personaName">The Persona's name, matched case-insensitively.</param>
    /// <returns>The mode id, or <c>null</c>.</returns>
    public string? Get(string personaName)
    {
        return this.table.Get(personaName);
    }

    /// <summary>
    /// Sets the Work Mode for a Persona. A <c>null</c> or blank <paramref name="workMode"/> deletes
    /// the row instead of storing it, meaning "use the Adapter's own mode": a stored blank would be a
    /// row that exists but never resolves to a mode the Adapter advertises.
    /// </summary>
    /// <param name="personaName">The Persona's name.</param>
    /// <param name="workMode">The mode id to store, or <c>null</c> to clear it.</param>
    public void Set(string personaName, string? workMode)
    {
        this.table.Set(personaName, workMode);
    }

    /// <summary>Removes any stored Work Mode for a Persona, leaving no row behind.</summary>
    /// <param name="personaName">The Persona's name.</param>
    public void Remove(string personaName)
    {
        this.table.Remove(personaName);
    }
}
