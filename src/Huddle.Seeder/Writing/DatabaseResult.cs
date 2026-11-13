namespace Agency.Huddle.Seeder.Writing;

/// <summary>The ids the database assigned, which Task files and the manifest need.</summary>
/// <param name="UserIds">Agent Name to user id.</param>
/// <param name="RoomIds">Room key to Room id.</param>
internal sealed record DatabaseResult(IReadOnlyDictionary<string, string> UserIds, IReadOnlyDictionary<string, string> RoomIds)
{
    /// <summary>Job id to the id of its Job Room, for each Job that has run; written by <see cref="JobSeeder"/>.</summary>
    public IReadOnlyDictionary<string, string> JobRoomIds { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
