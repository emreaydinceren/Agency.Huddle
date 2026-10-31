namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>A memory file the Team Memory Helper sees in full, because it may be the one to update (Team Memory Helper spec §6.3).</summary>
/// <param name="Folder">The folder label, <c>Team</c> or <c>Team/Project</c>.</param>
/// <param name="File">The file name.</param>
/// <param name="Version">The file's version when it was read; an update is written only while the file still has it.</param>
/// <param name="Text">The file's whole text, frontmatter included.</param>
internal sealed record TeamMemoryCandidate(string Folder, string File, string Version, string Text);
