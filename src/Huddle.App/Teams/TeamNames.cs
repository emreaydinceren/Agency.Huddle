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

    /// <summary>The folder name reserved for files brought into a Team.</summary>
    public const string RawFolder = "raw";

    /// <summary>The error message when a Project name is the reserved raw folder.</summary>
    public const string RawReservedProblem = "\"raw\" is reserved for files brought into the Team.";

    /// <summary>The folder name reserved for work a Team produces.</summary>
    public const string OutputsFolder = "outputs";

    /// <summary>The error message when a Project name is the reserved outputs folder.</summary>
    public const string OutputsReservedProblem = "\"outputs\" is reserved for work the Team produces.";

    /// <summary>
    /// Checks if a name is reserved for Projects: either a reserved folder name (starting with _ or .),
    /// the memory, raw or outputs folder (case-insensitive).
    /// </summary>
    /// <param name="name">The Project name to check.</param>
    /// <returns>True when the name is reserved for Projects.</returns>
    public static bool IsReservedProjectName(string name) =>
        TaskLayout.IsReservedFolderName(name)
        || string.Equals(name, MemoryFolder, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, RawFolder, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, OutputsFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the refusal text for a Project name that is the memory, raw or outputs folder (case-insensitive).</summary>
    /// <param name="project">The proposed Project name.</param>
    /// <returns>The matching reserved-name problem, or <see langword="null"/> when the name is none of the three.</returns>
    internal static string? ReservedProjectProblem(string project)
    {
        if (string.Equals(project, MemoryFolder, StringComparison.OrdinalIgnoreCase))
        {
            return MemoryReservedProblem;
        }

        if (string.Equals(project, RawFolder, StringComparison.OrdinalIgnoreCase))
        {
            return RawReservedProblem;
        }

        return string.Equals(project, OutputsFolder, StringComparison.OrdinalIgnoreCase) ? OutputsReservedProblem : null;
    }

    /// <summary>Checks if a name is one of the folders reserved for a Team's own use: memory, raw or outputs (case-insensitive).</summary>
    /// <param name="name">The Team name to check.</param>
    /// <returns>True when the name is reserved for a Team's own folders.</returns>
    public static bool IsReservedTeamName(string name) =>
        string.Equals(name, MemoryFolder, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, RawFolder, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, OutputsFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the error message for a Team name that is reserved for a Team's own folders.</summary>
    /// <param name="name">The Team name as the user typed it.</param>
    /// <returns>The user-facing error message quoting the name.</returns>
    public static string TeamReservedProblem(string name) => $"\"{name}\" is reserved for a Team's own folders.";

    /// <summary>
    /// Validates a Team name against the naming rules. Returns null if the name is valid,
    /// or a user-facing error message if it violates any rule.
    /// </summary>
    /// <param name="name">The Team name to validate.</param>
    /// <param name="existing">The list of existing Teams to check for duplicates.</param>
    /// <returns>Null if the name is valid; an error message otherwise.</returns>
    /// <param name="archivedNames">The names of the archived Teams (<c>Archive/&lt;T&gt;/_team.md</c> exists), compared ignoring case; null when the caller has none to check.</param>
    public static string? ValidateTeamName(string name, IReadOnlyList<TeamSummary> existing, IReadOnlyList<string>? archivedNames = null)
    {
        // 0. Refuse the folders a Team reserves for itself (memory, raw, outputs), ignoring case
        if (IsReservedTeamName(name))
        {
            return TeamReservedProblem(name);
        }

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

        // 4. Check for an archived Team of the same name (case-insensitive); the text names the archived spelling
        string? archived = archivedNames?.FirstOrDefault(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return archived is null ? null : $"A Team named {archived} is archived. Unarchive it or pick another name.";
    }

    /// <summary>
    /// Validates a Project name within a Team. Returns null if the name is valid,
    /// or a user-facing error message if it violates any rule.
    /// </summary>
    /// <param name="name">The Project name to validate.</param>
    /// <param name="team">The Team containing the Project.</param>
    /// <returns>Null if the name is valid; an error message otherwise.</returns>
    /// <param name="archivedNames">The names of the Team's archived Projects (<c>Archive/&lt;Team&gt;/&lt;Project&gt;</c>), compared ignoring case; null when the caller has none to check.</param>
    public static string? ValidateProjectName(string name, TeamSummary team, IReadOnlyList<string>? archivedNames = null)
    {
        // 1. Check LibraryNames rules first (empty, contains /, \, reserved names, etc.)
        string? libraryProblem = LibraryNames.Validate(name);
        if (libraryProblem is not null)
        {
            return libraryProblem;
        }

        // 2. Check if the name is the reserved memory, raw or outputs folder (case-insensitive)
        string? reservedProblem = ReservedProjectProblem(name);
        if (reservedProblem is not null)
        {
            return reservedProblem;
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

        // 5. Check for an archived Project of the same name (case-insensitive); the text names the archived spelling
        string? archived = archivedNames?.FirstOrDefault(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return archived is null ? null : $"A Project named {archived} is archived in {team.Name}. Unarchive it or pick another name.";
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
