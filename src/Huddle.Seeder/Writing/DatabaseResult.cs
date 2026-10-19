namespace Agency.Huddle.Seeder.Writing;

/// <summary>The ids the database assigned, which Task files and the manifest need.</summary>
/// <param name="UserIds">Teammate Name to user id.</param>
/// <param name="RoomIds">Room key to Room id.</param>
internal sealed record DatabaseResult(IReadOnlyDictionary<string, string> UserIds, IReadOnlyDictionary<string, string> RoomIds);
