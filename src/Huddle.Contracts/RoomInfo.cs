namespace Agency.Huddle.Contracts;

/// <summary>One Room in a <see cref="Welcome"/>, and the Members currently in it.</summary>
/// <param name="Id">The Room's id.</param>
/// <param name="Name">The Room's Name.</param>
/// <param name="Members">Every Member of the Room.</param>
/// <param name="IsEmpty">
/// Whether the Room has no Messages yet. Additive on the wire: absent deserialises as
/// <see langword="false"/> — "not empty" — so a server built before this field existed can never
/// cause a Greeting.
/// </param>
/// <param name="Team">
/// The Team the Room is scoped to, or <see langword="null"/> for a root Room. Additive on the wire:
/// omitted when <see langword="null"/>, and absent deserialises as a root Room.
/// </param>
public sealed record RoomInfo(string Id, string Name, IReadOnlyList<MemberInfo> Members, bool IsEmpty = false, string? Team = null);