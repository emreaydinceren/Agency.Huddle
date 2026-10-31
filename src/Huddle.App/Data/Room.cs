namespace Agency.Huddle.App.Data;

public sealed record Room(string Id, string Name, DateTimeOffset Created)
{
    /// <summary>
    /// Whether the Room is archived. This is a display filter only — an archived Room stays fully
    /// live: Agents can still post into it, and nothing about its Members, Transcript or behaviour
    /// changes. Archiving only changes whether the Room is offered in the default sidebar view.
    /// </summary>
    public bool Archived { get; init; }

    /// <summary>
    /// When the Room was archived, or <see langword="null"/> when it is not archived or the instant
    /// was not recorded. Read from the same <c>archived_rooms</c> row that makes <see cref="Archived"/> true.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; init; }

    /// <summary>
    /// The Team this Room is scoped to (ADR-0052), or <see langword="null"/> for a root Room. Chosen when
    /// the Room is created and never changed.
    /// </summary>
    public string? Team { get; init; }

    /// <summary>
    /// Whether this is its Team's Team Room (ADR-0052): the one Room of a Team that holds all its Members
    /// and is named after the Team. Always <see langword="false"/> for a root Room. A Team Room is never
    /// renamed, whatever its name looks like.
    /// </summary>
    public bool IsTeamRoom { get; init; }
}