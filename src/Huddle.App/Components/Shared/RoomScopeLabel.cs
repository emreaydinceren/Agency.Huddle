using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// How a Room's Team (and Project) scope reads next to a one-to-one's name, so the root one-to-one with an Agent and
/// the same Agent's Team one-to-one are told apart. Display only: the stored Room name is never changed.
/// </summary>
internal static class RoomScopeLabel
{
    /// <summary>Whether the Room is a Team-scoped one-to-one: a Team set, not the Team Room, and exactly one Agent member.</summary>
    /// <param name="room">The Room.</param>
    /// <param name="members">The Room's Members.</param>
    public static bool IsScopedOneToOne(Room room, IReadOnlyList<User> members) =>
        !string.IsNullOrEmpty(room.Team)
        && !room.IsTeamRoom
        && members.Count(static member => member.Kind == UserKind.Agent) == 1;

    /// <summary>The scope text: the Team, or <c>Team / Project</c>; <see langword="null"/> for a root Room.</summary>
    /// <param name="room">The Room.</param>
    public static string? Scope(Room room)
    {
        if (string.IsNullOrEmpty(room.Team))
        {
            return null;
        }

        return string.IsNullOrEmpty(room.Project) ? room.Team : $"{room.Team} / {room.Project}";
    }

    /// <summary>
    /// The scope to show after the Room's name: set only for a scoped one-to-one whose Team is live in the catalog (a Team
    /// that has left has no icon to draw), else <see langword="null"/>. Every surface labels a Room by this one rule.
    /// </summary>
    /// <param name="room">The Room.</param>
    /// <param name="members">The Room's Members.</param>
    /// <param name="catalog">The Team catalog.</param>
    public static string? ScopeFor(Room room, IReadOnlyList<User> members, ITeamCatalog catalog) =>
        IsScopedOneToOne(room, members) && room.Team is { } team && catalog.Find(team) is not null ? Scope(room) : null;

    /// <summary>The text that names the Room: <c>Name · Scope</c> when <see cref="ScopeFor"/> gives a scope, else the Room's name.</summary>
    /// <param name="room">The Room.</param>
    /// <param name="members">The Room's Members.</param>
    /// <param name="catalog">The Team catalog.</param>
    public static string Display(Room room, IReadOnlyList<User> members, ITeamCatalog catalog) =>
        ScopeFor(room, members, catalog) is { } scope ? $"{room.Name} · {scope}" : room.Name;
}
