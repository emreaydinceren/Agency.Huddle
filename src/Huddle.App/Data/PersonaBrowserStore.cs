using System.Globalization;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Stores which Personas the Human switched the browser off for (the Playwright MCP server, issue #551),
/// keyed by Persona name, in its own <c>team.db</c> table. A row present means off, and its value is the
/// moment the Human switched it off; no row means on, which is the v1 default. A new table rather than a
/// column, for the reason <see cref="PersonaWorkModeStore"/> records.
/// </summary>
/// <remarks>
/// <see cref="Agency.Huddle.App.Acp.PersonaStore.SetBrowser"/> is the only writer: no Agent Tool, proposal,
/// Pack update or frontmatter key reaches <see cref="Set"/> or <see cref="Remove"/>.
/// </remarks>
public sealed class PersonaBrowserStore
{
    private static readonly PersonaOverrideSql Sql = new(
        Create: """
            CREATE TABLE IF NOT EXISTS persona_browser_off (
                persona_name TEXT PRIMARY KEY COLLATE NOCASE,
                off_at       TEXT NOT NULL
            );
            """,
        Select: "SELECT off_at FROM persona_browser_off WHERE persona_name = $name;",
        Upsert: """
            INSERT INTO persona_browser_off(persona_name, off_at) VALUES($name, $value)
            ON CONFLICT(persona_name) DO UPDATE SET off_at = excluded.off_at;
            """,
        Delete: "DELETE FROM persona_browser_off WHERE persona_name = $name;");

    private readonly PersonaOverrideTable table;
    private readonly TimeProvider timeProvider;

    /// <summary>Opens the store and creates its table when the database does not have it yet.</summary>
    /// <param name="options">The Team options, which name the data directory.</param>
    /// <param name="timeProvider">The clock that stamps <c>off_at</c>; defaults to the system clock.</param>
    public PersonaBrowserStore(IOptions<TeamOptions> options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.table = new PersonaOverrideTable(options.Value, Sql);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Whether a Persona has a row, that is, whether the Human switched its browser off.</summary>
    /// <param name="personaName">The Persona's name, matched case-insensitively.</param>
    /// <returns><see langword="true"/> when off.</returns>
    public bool IsOff(string personaName) => this.table.Get(personaName) is not null;

    /// <summary>Switches the browser off, writing now as <c>off_at</c>, or on, deleting the row.</summary>
    /// <param name="personaName">The Persona's name.</param>
    /// <param name="on"><see langword="true"/> to delete the row (on), <see langword="false"/> to write it (off).</param>
    public void Set(string personaName, bool on)
    {
        this.table.Set(personaName, on ? null : this.timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>Removes any row for a Persona, leaving its browser on.</summary>
    /// <param name="personaName">The Persona's name.</param>
    public void Remove(string personaName)
    {
        this.table.Remove(personaName);
    }
}
