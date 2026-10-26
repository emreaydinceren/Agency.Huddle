namespace Agency.Huddle.App.Components.Shared;

/// <summary>How the Chats view's "Rooms" group lays out its Rooms; the Human picks it with the "Group by" control above the group.</summary>
internal enum RoomGrouping
{
    /// <summary>One flat list of the Rooms that belong to no Team (the original list).</summary>
    All,

    /// <summary>Every Room, under a heading for its Team, the Rooms of no Team first.</summary>
    Team,
}
