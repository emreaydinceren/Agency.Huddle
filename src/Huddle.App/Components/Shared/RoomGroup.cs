using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>One heading and its Rooms in the Chats view's Team grouping; <see cref="Team"/> is <see langword="null"/> for the Rooms that belong to no Team.</summary>
/// <param name="Team">The Team's name, or <see langword="null"/> for the Rooms of no Team.</param>
/// <param name="Rooms">The Group's Rooms, the Team Room first, then the others oldest first.</param>
internal sealed record RoomGroup(string? Team, IReadOnlyList<Room> Rooms);
