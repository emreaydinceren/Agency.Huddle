using System.Globalization;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores which Personas the Human let use the account connectors (the claude.ai sign-in's mail, files
/// and calendar), keyed by Persona name, in its own <c>team.db</c> table. A row present means on, and its
/// value is the moment the Human switched it on; no row means off, so there is no stored "off"
/// (docs/Huddle.AgentConnectors-Specifications.md section 6.1). A new table rather than a column, for the
/// reason <see cref="PersonaWorkModeStore"/> records.
/// </summary>
/// <remarks>
/// <see cref="Agency.Huddle.App.Acp.PersonaStore.SetConnectors"/> is the only caller of
/// <see cref="Set"/> and <see cref="Remove"/>: an Agent must never be able to grant itself access.
/// </remarks>
public sealed class PersonaConnectorsStore
{
    private static readonly PersonaOverrideSql Sql = new(
        Create: """
            CREATE TABLE IF NOT EXISTS persona_connectors (
                persona_name TEXT PRIMARY KEY COLLATE NOCASE,
                enabled_at   TEXT NOT NULL
            );
            """,
        Select: "SELECT enabled_at FROM persona_connectors WHERE persona_name = $name;",
        Upsert: """
            INSERT INTO persona_connectors(persona_name, enabled_at) VALUES($name, $value)
            ON CONFLICT(persona_name) DO UPDATE SET enabled_at = excluded.enabled_at;
            """,
        Delete: "DELETE FROM persona_connectors WHERE persona_name = $name;");

    private readonly PersonaOverrideTable table;
    private readonly TimeProvider timeProvider;

    /// <summary>Opens the store and creates its table when the database does not have it yet.</summary>
    /// <param name="options">The Team options, which name the data directory.</param>
    /// <param name="timeProvider">The clock that stamps <c>enabled_at</c>; defaults to the system clock.</param>
    public PersonaConnectorsStore(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.table = new PersonaOverrideTable(options.Value, Sql);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Whether a Persona has a row, that is, whether the Human switched connectors on for it.</summary>
    /// <param name="personaName">The Persona's name, matched case-insensitively.</param>
    /// <returns><see langword="true"/> when on.</returns>
    public bool IsOn(string personaName) => this.table.Get(personaName) is not null;

    /// <summary>When the Human switched connectors on for a Persona, or <see langword="null"/> when they are off.</summary>
    /// <param name="personaName">The Persona's name, matched case-insensitively.</param>
    /// <returns>The moment, in UTC, or <see langword="null"/>.</returns>
    public DateTimeOffset? EnabledAt(string personaName)
    {
        string? stored = this.table.Get(personaName);
        return stored is not null && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset at)
            ? at
            : null;
    }

    /// <summary>Switches connectors on, writing now as <c>enabled_at</c>, or off, deleting the row.</summary>
    /// <param name="personaName">The Persona's name.</param>
    /// <param name="enabled"><see langword="true"/> to write the row, <see langword="false"/> to delete it.</param>
    public void Set(string personaName, bool enabled)
    {
        this.table.Set(personaName, enabled ? this.timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture) : null);
    }

    /// <summary>Removes any row for a Persona, leaving the Persona off.</summary>
    /// <param name="personaName">The Persona's name.</param>
    public void Remove(string personaName)
    {
        this.table.Remove(personaName);
    }
}
