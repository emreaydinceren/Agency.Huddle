namespace Agency.Huddle.App.Data;

/// <summary>
/// The four statements one <see cref="PersonaOverrideTable"/> runs, written by each store as
/// constants so no SQL is ever built from strings. Every statement binds <c>$name</c>, and
/// <see cref="Upsert"/> also binds <c>$value</c>.
/// </summary>
/// <param name="Create">A <c>CREATE TABLE IF NOT EXISTS</c> statement for the store's own table.</param>
/// <param name="Select">Selects the one stored value for <c>$name</c>.</param>
/// <param name="Upsert">Inserts <c>$value</c> for <c>$name</c>, replacing any earlier row.</param>
/// <param name="Delete">Deletes the row for <c>$name</c>.</param>
internal sealed record PersonaOverrideSql(string Create, string Select, string Upsert, string Delete);
