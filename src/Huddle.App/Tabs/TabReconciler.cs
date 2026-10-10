using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// Brings Room Tabs in step with the Rooms that exist: a renamed Room retitles its Tab, and a Room
/// that is archived or gone closes it. Pure over <see cref="TabService"/> so it needs no renderer.
/// </summary>
internal static class TabReconciler
{
    private const string RoomKeyPrefix = "room:";
    private const string TeamKeyPrefix = "team:";
    private const string ProjectKeyPrefix = "project:";

    /// <summary>
    /// Closes Team and Project Tabs whose Team or Project is gone. Does nothing while the catalog lists
    /// no Team at all: the catalog is built from stores that may still be scanning when a stored Tab is
    /// restored, and an empty list then means "not loaded yet" far more often than "everything was
    /// removed". The accepted cost is that removing the very last Team leaves its Tab open.
    /// </summary>
    /// <param name="tabs">The Tabs to bring in step.</param>
    /// <param name="catalog">The Teams and Projects that exist now.</param>
    internal static void ReconcileTeams(TabService tabs, Teams.ITeamCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.Teams.Count == 0)
        {
            return;
        }

        foreach (Tab tab in tabs.Tabs)
        {
            if (tab.Key.StartsWith(TeamKeyPrefix, StringComparison.Ordinal))
            {
                if (catalog.Find(tab.Key[TeamKeyPrefix.Length..]) is null)
                {
                    tabs.CloseByKey(tab.Key);
                }
            }
            else if (tab.Key.StartsWith(ProjectKeyPrefix, StringComparison.Ordinal))
            {
                string[] parts = tab.Key[ProjectKeyPrefix.Length..].Split('/', 2);
                if (parts.Length != 2 || !catalog.ProjectExists(parts[0], parts[1]))
                {
                    tabs.CloseByKey(tab.Key);
                }
            }
        }
    }

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
