using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Locates and reads a Team's Process document. It is always <see cref="TeamPaths.ProcessFileName"/> in the Team
/// folder: <c>_team.md</c> does not name it, so the Done gate, cadence, onboarding, the role block and the Process tab
/// all read the same file and a Team cannot have two Processes (issue #312).
/// </summary>
internal static class TeamProcessFile
{
    /// <summary>The path of a Team's Process document, whether or not the file exists yet.</summary>
    /// <param name="folders">Locates the Team folder.</param>
    /// <param name="team">The Team's name.</param>
    /// <returns>The path, or <see langword="null"/> when the Team has no folder.</returns>
    public static string? PathOf(ITeamFolders folders, string team) =>
        folders.FindTeam(team).Value is { } folder ? Path.Combine(folder.FullPath, TeamPaths.ProcessFileName) : null;

    /// <summary>Reads a Team's Process document.</summary>
    /// <param name="folders">Locates the Team folder.</param>
    /// <param name="team">The Team's name.</param>
    /// <returns>The file text, or <see langword="null"/> when the Team has no folder or the file is missing or unreadable.</returns>
    public static string? Read(ITeamFolders folders, string team) =>
        PathOf(folders, team) is { } path ? ReadText(path) : null;

    /// <summary>Reads the Process document at <paramref name="path"/>.</summary>
    /// <param name="path">The file's full path.</param>
    /// <returns>The file text, or <see langword="null"/> when it is missing or unreadable.</returns>
    public static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
