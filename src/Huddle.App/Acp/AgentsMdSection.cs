namespace Agency.Huddle.App.Acp;

/// <summary>One AGENTS.md file's text, with the level and scope it was read from (Spec §6.2).</summary>
/// <param name="Level">The level the file belongs to.</param>
/// <param name="Team">The Team the file belongs to; null unless the level is <see cref="AgentsMdLevel.Team"/>.</param>
/// <param name="Project">The Project the file belongs to; null unless the level is <see cref="AgentsMdLevel.Project"/>.</param>
/// <param name="Path">The path the file was read from.</param>
/// <param name="Text">The file's text as read.</param>
/// <param name="HadFrontmatter">Whether the file began with a frontmatter block.</param>
internal sealed record AgentsMdSection(AgentsMdLevel Level, string? Team, string? Project, string Path, string Text, bool HadFrontmatter);
