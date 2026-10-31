using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores the effort override for each Persona, keyed by Persona name, in the same <c>team.db</c>
/// file as <see cref="SqliteTeamDirectory"/> but in its own table. A new table (rather than a
/// column added to an existing one) is deliberate: <see cref="SqliteTeamDirectory"/> creates its
/// schema with <c>CREATE TABLE IF NOT EXISTS</c>, which silently ignores an added column on a
/// database that predates it, whereas <c>CREATE TABLE IF NOT EXISTS persona_efforts</c> creates
/// cleanly on an existing <c>App_Data</c> without requiring users to delete it. This store proves
/// that rule a second time: <see cref="PersonaModelStore"/>'s <c>persona_models</c> table is now
/// itself a table that predates a wanted new field, and a column added to it would have suffered
/// exactly the same silent failure it was created to avoid.
/// </summary>
/// <remarks>
/// Deliberately synchronous, unlike <see cref="SqliteTeamDirectory"/>: <c>Microsoft.Data.Sqlite</c>'s
/// async methods are synchronous internally against a local file, and the only caller (<see
/// cref="Agency.Huddle.App.Acp.PersonaStore"/>) is synchronous file I/O throughout. Going async here would
/// force <c>async</c> up through <c>PersonaStore.Get</c>, then <c>PersonaSupervisor</c>'s sync
/// <c>void</c> event handler for <c>PersonasChanged</c>, then the razor page that opens a Persona
/// card, for zero benefit.
/// </remarks>
public sealed class PersonaEffortStore
{
    private static readonly PersonaOverrideSql Sql = new(
        Create: """
            CREATE TABLE IF NOT EXISTS persona_efforts (
                persona_name TEXT PRIMARY KEY COLLATE NOCASE,
                effort       TEXT NOT NULL
            );
            """,
        Select: "SELECT effort FROM persona_efforts WHERE persona_name = $name;",
        Upsert: """
            INSERT INTO persona_efforts(persona_name, effort) VALUES($name, $value)
            ON CONFLICT(persona_name) DO UPDATE SET effort = excluded.effort;
            """,
        Delete: "DELETE FROM persona_efforts WHERE persona_name = $name;");

    private readonly PersonaOverrideTable table;

    public PersonaEffortStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.table = new PersonaOverrideTable(options.Value, Sql);
    }

    /// <summary>Returns the stored effort for a Persona, or <c>null</c> if none is set (model default).</summary>
    public string? Get(string personaName)
    {
        return this.table.Get(personaName);
    }

    /// <summary>
    /// Sets the effort for a Persona. A <c>null</c> or blank <paramref name="effort"/> deletes the
    /// row instead of storing it, meaning "use whatever effort the model normally uses" — an
    /// empty-string effort must never reach the database, since a stored blank would be a row that
    /// exists but never resolves to a usable effort.
    /// </summary>
    public void Set(string personaName, string? effort)
    {
        this.table.Set(personaName, effort);
    }

    /// <summary>Removes any stored effort for a Persona, leaving no row behind.</summary>
    public void Remove(string personaName)
    {
        this.table.Remove(personaName);
    }
}
