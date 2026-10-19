namespace Agency.Huddle.App.Search;

/// <summary>
/// A Room hit. A Room has no Team in the base hit (OP-4), so its <see cref="SearchHit.TeamName"/> is always null.
/// <see cref="Team"/> and <see cref="IsTeamRoom"/> carry the Room's Scope for the picker's note (D-27).
/// </summary>
/// <param name="RoomId">The Room's Id.</param>
/// <param name="RoomName">The Room's Name, which is the hit's title.</param>
/// <param name="MatchCount">How many Messages matched; zero for a name-only hit.</param>
/// <param name="MessageId">The newest matching Message's Id, or null.</param>
/// <param name="SenderName">The newest matching Message's sender, or null.</param>
/// <param name="Snippet">The text around the match in the newest matching Message, or null.</param>
/// <param name="Time">The newest matching Message's time, or null.</param>
/// <param name="IsArchivedRoom">Whether the Room is Archived.</param>
/// <param name="Tier">How well the Room matched.</param>
/// <param name="Team">The Room's Team, or null for a root Room.</param>
/// <param name="IsTeamRoom">Whether the Room is its Team's Team Room.</param>
public sealed record RoomHit(string RoomId, string RoomName, int MatchCount, string? MessageId, string? SenderName, string? Snippet, DateTimeOffset? Time, bool IsArchivedRoom, MatchTier Tier, string? Team = null, bool IsTeamRoom = false)
    : SearchHit(RoomName, null, IsArchivedRoom, Tier);
