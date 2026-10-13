using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Search;

/// <summary>One result. The card is chosen by the subtype, never by a kind enum (spec §7.1).</summary>
/// <param name="Title">The card's main text.</param>
/// <param name="TeamName">The Team the hit belongs to, used only for "open Team first" (spec §7.4).</param>
/// <param name="IsArchived">Whether the hit is an Archived Room or a Closed Task.</param>
/// <param name="Tier">How well the hit matched.</param>
public abstract record SearchHit(string Title, string? TeamName, bool IsArchived, MatchTier Tier);

/// <summary>A Team hit: its type label and Teammate count are what its card shows.</summary>
public sealed record TeamHit(string Name, string? TypeLabel, int TeammateCount, MatchTier Tier)
    : SearchHit(Name, Name, false, Tier);

/// <summary>A Teammate hit. Its Title on the card is the Name; <see cref="PersonaTitle"/> is the Persona's title.</summary>
public sealed record TeammateHit(string Name, string PersonaTitle, string Alias, IReadOnlyList<string> Teams, MatchTier Tier)
    : SearchHit(Name, Teams.Count > 0 ? Teams[0] : null, false, Tier);

/// <summary>A Library hit: a file or folder under one Library Root, with a snippet for a content match.</summary>
public sealed record LibraryHit(string RootId, string RootName, string RelativePath, string FolderPath, bool IsFolder, string? Snippet, string? TeamName, MatchTier Tier)
    : SearchHit(RelativePath[(RelativePath.LastIndexOf('/') + 1)..], TeamName, false, Tier);

/// <summary>A Task hit: its id, status and assignee are what its card shows. <see cref="AssigneeTitle"/> is the assignee Persona's title, or null when unassigned or the Persona is gone.</summary>
public sealed record TaskHit(TaskId Id, string TaskTitle, TaskState Status, string? Assignee, string Team, bool IsClosed, MatchTier Tier, string? AssigneeTitle = null)
    : SearchHit(TaskTitle, Team, IsClosed, Tier);

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
