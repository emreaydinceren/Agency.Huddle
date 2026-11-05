using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>One level's <c>AGENTS.md</c> in scope for an Agent, whether or not the file exists (Spec §6.1).</summary>
/// <param name="Level">The level the file belongs to.</param>
/// <param name="Team">The Team folder name, or <see langword="null"/> for Huddle-wide.</param>
/// <param name="Project">The Project folder name, or <see langword="null"/> above Project level.</param>
/// <param name="FullPath">The file's full path.</param>
/// <param name="RelativePath">The file relative to the Teams root, with <c>/</c> on every OS.</param>
internal sealed record AgentsMdFile(AgentsMdLevel Level, string? Team, string? Project, string FullPath, string RelativePath);
