using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public sealed record User(string Id, string Name, UserKind Kind, string? Description);

public sealed record Room(string Id, string Name, DateTimeOffset Created)
{
    /// <summary>
    /// Whether the Room is archived. This is a display filter only — an archived Room stays fully
    /// live: Agents can still post into it, and nothing about its Members, Transcript or behaviour
    /// changes. Archiving only changes whether the Room is offered in the default sidebar view.
    /// </summary>
    public bool Archived { get; init; }
}