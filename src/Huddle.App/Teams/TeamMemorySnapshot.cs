namespace Agency.Huddle.App.Teams;

/// <summary>
/// A Persona's Team Memory snapshot, holding each Team's Memory grouped by Project,
/// per Spec §7.2.
/// </summary>
/// <param name="Groups">Each Team's Memory snapshot, ordered by Team folder name.</param>
/// <param name="NotListed">The total count of Memory files not listed across all Groups, capped by MaxMemoryEntries per Team and Project.</param>
internal sealed record TeamMemorySnapshot(IReadOnlyList<TeamMemoryGroup> Groups, int NotListed);
