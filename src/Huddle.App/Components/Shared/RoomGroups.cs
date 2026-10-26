using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>Builds the groups the Chats view shows under "Group by: Team".</summary>
internal static class RoomGroups
{
    /// <summary>
    /// Splits <paramref name="rooms"/> by Team: the Rooms of no Team first, then one group per Team in name order (ignoring case).
    /// A group with no Room is not made, so a Team with only archived Rooms leaves no empty heading.
    /// </summary>
    /// <param name="rooms">The Rooms to show, already filtered of archived ones.</param>
    public static IReadOnlyList<RoomGroup> ByTeam(IReadOnlyList<Room> rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        List<RoomGroup> groups = [];
        List<Room> unscoped = [.. rooms.Where(static room => room.Team is null).OrderBy(static room => room.Created)];
        if (unscoped.Count > 0)
        {
            groups.Add(new RoomGroup(null, unscoped));
        }

        foreach (IGrouping<string, Room> team in rooms
            .Where(static room => room.Team is not null)
            .GroupBy(static room => room.Team ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static team => team.Key, StringComparer.OrdinalIgnoreCase))
        {
            groups.Add(new RoomGroup(team.Key, [
                .. team.Where(static room => room.IsTeamRoom),
                .. team.Where(static room => !room.IsTeamRoom).OrderBy(static room => room.Created),
            ]));
        }

        return groups;
    }
}
