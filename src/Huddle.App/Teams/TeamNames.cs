using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>Validation rules for Team and Project names.</summary>
internal static class TeamNames
{
    /// <summary>The folder name reserved for a Team's shared memory.</summary>
    public const string MemoryFolder = "memory";

    /// <summary>The error message when a Project name is the reserved memory folder.</summary>
    public const string MemoryReservedProblem = "\"memory\" is reserved for the Team's shared Memory.";

    /// <summary>
    /// Checks if a name is reserved for Projects: either a reserved folder name (starting with _ or .)
    /// or the memory folder (case-insensitive).
    /// </summary>
    /// <param name="name">The Project name to check.</param>
    /// <returns>True when the name is reserved for Projects.</returns>
    public static bool IsReservedProjectName(string name) =>
        TaskLayout.IsReservedFolderName(name) || string.Equals(name, MemoryFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates a Team name against the naming rules. Returns null if the name is valid,
    /// or a user-facing error message if it violates any rule.
    /// </summary>
    /// <param name="name">The Team name to validate.</param>
    /// <param name="existing">The list of existing Teams to check for duplicates.</param>
    /// <returns>Null if the name is valid; an error message otherwise.</returns>
    public static string? ValidateTeamName(string name, IReadOnlyList<TeamSummary> existing)
    {
        // 1. Check LibraryNames rules first (empty, contains /, \, reserved names, etc.)
        string? libraryProblem = LibraryNames.Validate(name);
        if (libraryProblem is not null)
        {
            return libraryProblem;
        }

        // 2. Check for reserved Team-level prefixes and invalid characters
        if (TaskLayout.IsReservedFolderName(name))
        {
            return "Names starting with \"_\" or \".\" are reserved.";
        }

        if (ContainsInvalidTeamCharacters(name))
        {
            return "A Team name can't contain commas, semicolons or square brackets.";
        }

        // 3. Check for duplicate Teams (case-insensitive)
        foreach (TeamSummary team in existing)
        {
            if (string.Equals(team.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return $"A Team named \"{name}\" already exists.";
            }
        }

        return null;
    }

    /// <summary>
    /// Validates a Project name within a Team. Returns null if the name is valid,
    /// or a user-facing error message if it violates any rule.
    /// </summary>
    /// <param name="name">The Project name to validate.</param>
    /// <param name="team">The Team containing the Project.</param>
    /// <returns>Null if the name is valid; an error message otherwise.</returns>
    public static string? ValidateProjectName(string name, TeamSummary team)
    {
        // 1. Check LibraryNames rules first (empty, contains /, \, reserved names, etc.)
        string? libraryProblem = LibraryNames.Validate(name);
        if (libraryProblem is not null)
        {
            return libraryProblem;
        }

        // 2. Check if the name is the reserved memory folder (case-insensitive)
        if (string.Equals(name, MemoryFolder, StringComparison.OrdinalIgnoreCase))
        {
            return MemoryReservedProblem;
        }

        // 3. Check for reserved Project-level prefixes
        if (TaskLayout.IsReservedFolderName(name))
        {
            return "Names starting with \"_\" or \".\" are reserved.";
        }

        // 4. Check for duplicate Projects within the Team (case-insensitive)
        foreach (string project in team.Projects)
        {
            if (string.Equals(project, name, StringComparison.OrdinalIgnoreCase))
            {
                return $"A Project named \"{name}\" already exists in {team.Name}.";
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a name contains any of the invalid characters for Team names:
    /// comma, semicolon, square brackets, or control characters.
    /// </summary>
    private static bool ContainsInvalidTeamCharacters(string name)
    {
        foreach (char c in name)
        {
            if (c == ',' || c == ';' || c == '[' || c == ']' || char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
