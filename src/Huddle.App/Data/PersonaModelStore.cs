using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores the model override for each Persona, keyed by Persona name, in the same <c>team.db</c>
/// file as <see cref="SqliteTeamDirectory"/> but in its own table. A new table (rather than a
/// column added to an existing one) is deliberate: <see cref="SqliteTeamDirectory"/> creates its
/// schema with <c>CREATE TABLE IF NOT EXISTS</c>, which silently ignores an added column on a
/// database that predates it, whereas <c>CREATE TABLE IF NOT EXISTS persona_models</c> creates
/// cleanly on an existing <c>App_Data</c> without requiring users to delete it.
/// </summary>
/// <remarks>
/// Deliberately synchronous, unlike <see cref="SqliteTeamDirectory"/>: <c>Microsoft.Data.Sqlite</c>'s
/// async methods are synchronous internally against a local file, and the only caller (a later
/// stage wires this into <c>PersonaStore</c>) is synchronous file I/O throughout. Going async here
/// would force <c>async</c> up through <c>PersonaStore.Get</c>, then <c>PersonaSupervisor</c>'s
/// sync <c>void</c> event handler for <c>PersonasChanged</c>, then the razor page that opens a
/// Persona card, for zero benefit.
/// </remarks>
public sealed class PersonaModelStore
{
    private static readonly PersonaOverrideSql Sql = new(
        Create: """
            CREATE TABLE IF NOT EXISTS persona_models (
                persona_name TEXT PRIMARY KEY COLLATE NOCASE,
                model        TEXT NOT NULL
            );
            """,
        Select: "SELECT model FROM persona_models WHERE persona_name = $name;",
        Upsert: """
            INSERT INTO persona_models(persona_name, model) VALUES($name, $value)
            ON CONFLICT(persona_name) DO UPDATE SET model = excluded.model;
            """,
        Delete: "DELETE FROM persona_models WHERE persona_name = $name;");

    private readonly PersonaOverrideTable table;

    public PersonaModelStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.table = new PersonaOverrideTable(options.Value, Sql);
    }

    /// <summary>Returns the stored model for a Persona, or <c>null</c> if none is set (agent default).</summary>
    public string? Get(string personaName)
    {
        return this.table.Get(personaName);
    }

    /// <summary>
    /// Sets the model for a Persona. A <c>null</c> or blank <paramref name="model"/> deletes the
    /// row instead of storing it, meaning "use the agent's default model" — an empty-string model
    /// must never reach the database, since a stored blank would be a row that exists but never
    /// resolves to a usable model.
    /// </summary>
    public void Set(string personaName, string? model)
    {
        this.table.Set(personaName, model);
    }

    /// <summary>Removes any stored model for a Persona, leaving no row behind.</summary>
    public void Remove(string personaName)
    {
        this.table.Remove(personaName);
    }
}
