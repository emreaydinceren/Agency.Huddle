using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// Brings Room Tabs in step with the Rooms that exist: a renamed Room retitles its Tab, and a Room
/// that is archived or gone closes it. Pure over <see cref="TabService"/> so it needs no renderer.
/// </summary>
internal static class TabReconciler
{
    private const string RoomKeyPrefix = "room:";

    /// <summary>Retitles and closes Room Tabs against <paramref name="rooms"/>; Tabs for other pages are left alone.</summary>
    /// <param name="tabs">The Tabs to bring in step.</param>
    /// <param name="rooms">Every Room that exists now, archived or not.</param>
    public static void ReconcileRooms(TabService tabs, IReadOnlyList<Room> rooms)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(rooms);

        Dictionary<string, Room> byId = new(StringComparer.Ordinal);
        foreach (Room room in rooms)
        {
            byId[room.Id] = room;
        }

        foreach (Tab tab in tabs.Tabs)
        {
            if (!tab.Key.StartsWith(RoomKeyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (byId.TryGetValue(tab.Key[RoomKeyPrefix.Length..], out Room? room) && !room.Archived)
            {
                if (!string.IsNullOrWhiteSpace(room.Name))
                {
                    tabs.SetTitle(tab.Key, room.Name);
                }
            }
            else
            {
                tabs.CloseByKey(tab.Key);
            }
        }
    }
}
