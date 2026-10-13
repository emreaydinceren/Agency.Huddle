using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.References;

/// <summary>
/// The text the Room header's and the sidebar row's Copy link put on the clipboard (spec §6.1, D-30): the reference
/// the picker would insert for that Room, <c>[label](/rooms/id)</c>, built by the same <see cref="ReferenceText"/>
/// functions and without the picker's trailing space.
/// </summary>
internal static class RoomLinkText
{
    /// <summary>Builds the reference for <paramref name="room"/>.</summary>
    /// <param name="room">The Room to link.</param>
    /// <returns>The Markdown reference to that Room.</returns>
    public static string For(Room room) =>
        string.Concat("[", ReferenceText.RoomLabelFor(room.Name, room.Team, room.IsTeamRoom), "](", ReferenceText.RoomHref(room.Id), ")");
}
