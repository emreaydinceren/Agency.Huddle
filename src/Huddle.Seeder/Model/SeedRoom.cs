namespace Agency.Huddle.Seeder.Model;

/// <summary>A chat Room and its history.</summary>
/// <param name="Key">A stable key Tasks use to refer to the Room.</param>
/// <param name="Name">The Room name; when it equals the auto-derived name it is left as it is.</param>
/// <param name="Members">The Teammates in the Room; the Human is always added.</param>
/// <param name="Archived">Whether the Room is archived.</param>
/// <param name="Messages">The transcript, oldest first.</param>
/// <param name="Team">The Team the Room is scoped to, or <see langword="null"/> for a root Room.</param>
/// <param name="IsTeamRoom">Whether the Room is its Team's Team Room (at most one per Team).</param>
/// <param name="WithReferences">Whether the Messages carry <c>References</c>; <see langword="false"/> leaves them null so the reader's text scan is exercised.</param>
internal sealed record SeedRoom(
    string Key,
    string Name,
    IReadOnlyList<string> Members,
    bool Archived,
    IReadOnlyList<SeedMessage> Messages,
    string? Team = null,
    bool IsTeamRoom = false,
    bool WithReferences = true);
