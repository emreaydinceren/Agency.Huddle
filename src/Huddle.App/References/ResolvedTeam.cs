namespace Agency.Huddle.App.References;

/// <summary>A Team that exists.</summary>
/// <param name="Name">The Team's Name as the catalog spells it.</param>
/// <param name="TypeLabel">The Team type's display name (or its id when the type is gone), or null for none.</param>
/// <param name="Members">The Names of its Teammates.</param>
/// <param name="TeamRoomId">The id of its Team Room, archived or not, or null when it has none.</param>
internal sealed record ResolvedTeam(string Name, string? TypeLabel, IReadOnlyList<string> Members, string? TeamRoomId) : ResolvedReference;
