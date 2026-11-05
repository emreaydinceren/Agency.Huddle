using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Memory;

/// <summary>A memory folder a call resolved to: the one directory every command of that call works in (Instructions spec §5.3).</summary>
/// <param name="Path">The folder's full path, built from the catalog's spelling of the Team and Project and never from the raw argument.</param>
/// <param name="Label">The folder as the Agent names it: <c>me</c>, <c>memory</c>, <c>Team</c> or <c>Team/Project</c>.</param>
/// <param name="Team">The Team that owns the folder, or <see langword="null"/> for the Agent's own and for Huddle-wide Memory.</param>
/// <param name="HuddleWide">Whether the folder is Huddle-wide Memory, <c>Teams/memory</c>: every Agent reads it and only the Chief of Staff writes it.</param>
internal sealed record MemoryFolder(string Path, string Label, TeamSummary? Team, bool HuddleWide = false);
