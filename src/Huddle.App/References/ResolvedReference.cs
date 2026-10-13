using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>A reference after the resolver looked it up now (spec §8.2 step 2).</summary>
internal abstract record ResolvedReference;

/// <summary>A Task that exists.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">Its title now.</param>
/// <param name="Status">Its status now.</param>
/// <param name="Assignee">Its assignee, or null when unassigned.</param>
/// <param name="Team">The Team folder it lives in.</param>
internal sealed record ResolvedTask(TaskId Id, string Title, TaskState Status, string? Assignee, string Team) : ResolvedReference;

/// <summary>A Room that exists; an Archived Room still does.</summary>
/// <param name="Id">The Room's id.</param>
/// <param name="Name">Its Name.</param>
/// <param name="MemberCount">How many Members it has.</param>
/// <param name="Team">The Team it is scoped to, or null for a root Room.</param>
/// <param name="IsTeamRoom">Whether it is its Team's Team Room.</param>
internal sealed record ResolvedRoom(string Id, string Name, int MemberCount, string? Team, bool IsTeamRoom) : ResolvedReference;

/// <summary>A Team that exists.</summary>
/// <param name="Name">The Team's Name as the catalog spells it.</param>
/// <param name="TypeLabel">The Team type's display name (or its id when the type is gone), or null for none.</param>
/// <param name="Members">The Names of its Teammates.</param>
/// <param name="TeamRoomId">The id of its Team Room, archived or not, or null when it has none.</param>
internal sealed record ResolvedTeam(string Name, string? TypeLabel, IReadOnlyList<string> Members, string? TeamRoomId) : ResolvedReference;

/// <summary>A Library file that exists now.</summary>
/// <param name="Path">The absolute path as written.</param>
/// <param name="FileName">The file name.</param>
internal sealed record ResolvedLibraryFile(string Path, string FileName) : ResolvedReference;

/// <summary>What a scan of a Message's text resolved to (finding 2).</summary>
/// <param name="Items">The references after the caps of D-19, in candidate order.</param>
/// <param name="EligibleCount">How many Task, Room and Team references resolved before the cap, so the header can say how many it left out.</param>
internal sealed record ResolvedSet(IReadOnlyList<ResolvedReference> Items, int EligibleCount);

/// <summary>A reference whose target no longer exists, or never did.</summary>
/// <param name="Kind">What it named.</param>
/// <param name="Key">The key it named.</param>
internal sealed record GoneReference(ReferenceKind Kind, string Key) : ResolvedReference;
