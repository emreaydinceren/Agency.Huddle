using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// One Team's Memory snapshot, holding the Team-wide Memory and each Project's Memory separately,
/// per Spec §6.2 and §7.2.
/// </summary>
/// <param name="Team">The Team folder name.</param>
/// <param name="TeamMemoryPath">The Team folder's memory/ path.</param>
/// <param name="TeamWide">The Team's Memory snapshot (entries from the Team's memory/ folder).</param>
/// <param name="Projects">Each Project folder's Memory snapshot, ordered by folder name.</param>
internal sealed record TeamMemoryGroup(string Team, string TeamMemoryPath, MemorySnapshot TeamWide, IReadOnlyList<(string Project, MemorySnapshot Memory)> Projects);
