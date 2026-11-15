namespace Agency.Huddle.App.Teams;

/// <summary>Tells a retro change that would touch a Needs You level or its spend cap from every other, so neither a proposal nor an Accept can change them (ADR-0087).</summary>
internal static class RetroChangeGuard
{
    /// <summary>Whether a change to <paramref name="targetFile"/> names <c>needsYouLevel</c> or <c>spendCap</c> in its find or replace text.</summary>
    /// <param name="targetFile">The file the change edits, by name.</param>
    /// <param name="find">The text to find.</param>
    /// <param name="replace">The replacement.</param>
    /// <returns><see langword="true"/> when the target is a <c>_team.md</c> or <c>_project.md</c> and either text names a key; such a change is refused.</returns>
    public static bool TouchesNeedsYouLevel(string targetFile, string find, string replace)
    {
        ArgumentNullException.ThrowIfNull(targetFile);
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(replace);

        return (string.Equals(targetFile, TeamPaths.TeamFileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetFile, TeamPaths.ProjectFileName, StringComparison.OrdinalIgnoreCase))
            && (NeedsYouLevelText.MentionsKey(find) || NeedsYouLevelText.MentionsKey(replace));
    }
}
