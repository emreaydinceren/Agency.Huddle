using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// A one-time, idempotent start-up step (ADR-0031 *Migration*, Spec §6.15) that moves an old
/// <c>{DataDir}/Teams/</c> + <c>{DataDir}/work/</c> layout into the sibling-folder layout each
/// Teammate now owns: <c>{DataDir}/{Acp:TeammatesDir}/&lt;Name&gt;/&lt;Name&gt;.md</c> plus its
/// Work Dir underneath. It must run before <see cref="PersonaStore"/>, so it is a plain static
/// call from <c>Program.cs</c>, not a hosted service - <see cref="PersonaStore"/> scans its
/// folder in its own constructor. Step 4 moves the retired <c>{DataDir}/Tasks/</c> layout
/// (<c>&lt;Team&gt;/[&lt;Project&gt;/][_closed/]*.md</c>) into <c>{DataDir}/{Teams:Dir}/&lt;Team&gt;/
/// [&lt;Project&gt;/]_tasks/[_closed/]</c>; it is gated by that source existing, not by
/// <see cref="MarkerFileName"/> - an install whose marker already exists from a build before Step 4
/// still migrates a leftover Tasks/ root.
/// </summary>
internal static class TeammateLayoutMigration
{
    private const string UnsortedFolderName = "_unsorted";
    private const string OldTeamsFolderName = "Teams";
    private const string OldWorkFolderName = "work";
    private const string OldTasksFolderName = "Tasks";
    private const string MarkerFileName = ".layout-migrated";

    /// <summary>
    /// Runs the migration against <paramref name="options"/>'s configured <c>DataDir</c>, logging
    /// every move through <paramref name="logger"/>. A missing <c>DataDir</c> is a no-op. Every
    /// move is planned first; only once every planned target is confirmed clear does execution
    /// begin, and the first move that fails - typically because a Teammate process still holds a
    /// folder - stops the migration with <see cref="InvalidOperationException"/> naming the path,
    /// leaving the old layout untouched for anything not already moved.
    /// </summary>
    /// <param name="options">The application's <see cref="TeamOptions"/>.</param>
    /// <param name="logger">The logger every move (and a "nothing to migrate" outcome) is reported to.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a planned move cannot be completed.</exception>
    public static void Run(IOptions<TeamOptions> options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        string dataDir = options.Value.DataDir;
        if (!Directory.Exists(dataDir))
        {
            return;
        }

        string oldTeamsRoot = Path.Combine(dataDir, OldTeamsFolderName);
        string workRoot = Path.Combine(dataDir, OldWorkFolderName);
        string oldTasksRoot = Path.Combine(dataDir, OldTasksFolderName);
        string newTasksTeamsRoot = Path.Combine(dataDir, options.Value.Teams.Dir);
        TeammatePaths paths = new(options);

        string markerPath = Path.Combine(paths.DefinitionsRoot, MarkerFileName);
        bool markerPresent = File.Exists(markerPath);

        List<PlannedMove> moves = [];
        if (!markerPresent)
        {
            moves.AddRange(ResolveAlreadyDoneMoves(PlanMoves(oldTeamsRoot, workRoot, paths)));
        }

        if (Directory.Exists(oldTasksRoot))
        {
            moves.AddRange(ResolveAlreadyDoneMoves(PlanTaskMoves(oldTasksRoot, newTasksTeamsRoot, logger)));
        }

        if (moves.Count == 0)
        {
            if (!markerPresent)
            {
                logger.LogInformation("Teammate layout migration: nothing to migrate.");
                WriteMarker(paths.DefinitionsRoot, markerPath);
            }

            return;
        }

        CheckTargetsAreClear(moves);
        ExecuteMoves(moves, logger);
        RemoveEmptyOldFolders(oldTeamsRoot, workRoot);
        if (Directory.Exists(oldTasksRoot))
        {
            RemoveIfEmptyRecursively(oldTasksRoot);
        }

        if (!markerPresent)
        {
            WriteMarker(paths.DefinitionsRoot, markerPath);
        }
    }

    /// <summary>
    /// Plans every move: Persona definitions (whenever <c>Teams/</c> exists - the marker check
    /// already guards against re-scanning after a completed run, corrections-B2 addendum 3.5.f/F2),
    /// then Work Dirs.
    /// </summary>
    private static List<PlannedMove> PlanMoves(string teamsRoot, string workRoot, TeammatePaths paths)
    {
        List<PlannedMove> moves = [];

        if (Directory.Exists(teamsRoot))
        {
            moves.AddRange(PlanDefinitionMoves(teamsRoot, paths));
        }

        if (Directory.Exists(workRoot))
        {
            moves.AddRange(PlanWorkDirMoves(workRoot, paths));
        }

        return moves;
    }

    /// <summary>
    /// Drops any planned move that is already done (addendum 3.5.f/F3): a file move whose target
    /// already exists with byte-identical content is complete, and its leftover source is removed
    /// so old-folder cleanup still proceeds. Any other existing target is left for
    /// <see cref="CheckTargetsAreClear"/> to refuse.
    /// </summary>
    private static List<PlannedMove> ResolveAlreadyDoneMoves(List<PlannedMove> moves)
    {
        List<PlannedMove> remaining = [];
        foreach (PlannedMove move in moves)
        {
            if (!move.IsDirectory && File.Exists(move.Target) && File.Exists(move.Source)
                && FilesAreByteIdentical(move.Source, move.Target))
            {
                File.Delete(move.Source);
                continue;
            }

            remaining.Add(move);
        }

        return remaining;
    }

    /// <summary>Compares two files' contents byte for byte.</summary>
    private static bool FilesAreByteIdentical(string first, string second) =>
        File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second));

    /// <summary>Writes the empty completion marker, creating the Teammates root first if nothing was migrated into it.</summary>
    private static void WriteMarker(string definitionsRoot, string markerPath)
    {
        Directory.CreateDirectory(definitionsRoot);
        using FileStream _ = File.Create(markerPath);
    }

    /// <summary>
    /// Plans a move for every <c>.md</c> file under <paramref name="teamsRoot"/>: a valid Persona
    /// moves to its own Teammate folder, named from its frontmatter; anything <see cref="PersonaIndex.Build"/>
    /// rejects (an invalid file, or one half of a duplicate) moves to <c>Teammates/_unsorted/</c>,
    /// keeping its path relative to <paramref name="teamsRoot"/>.
    /// </summary>
    private static List<PlannedMove> PlanDefinitionMoves(string teamsRoot, TeammatePaths paths)
    {
        // Since Library Task G1.2, Teams/ also doubles as the Tasks scan root
        // ({Team}/_tasks/*.md, {Team}/{Project}/_tasks/*.md): a reserved-name segment there is a
        // Task file, not a stray legacy Persona definition, so it is excluded here rather than fed
        // to PersonaIndex.Build, which would otherwise reject it and sweep it into _unsorted.
        List<string> files = [..
            Directory.EnumerateFiles(teamsRoot, "*.md", SearchOption.AllDirectories)
                .Where(file => !HasReservedSegment(Path.GetRelativePath(teamsRoot, file)))];
        if (files.Count == 0)
        {
            return [];
        }

        List<(string Path, string Text)> candidates = [.. files.Select(file => (file, File.ReadAllText(file)))];
        PersonaIndex index = PersonaIndex.Build(candidates);

        List<PlannedMove> moves = [];
        foreach (PersonaEntry entry in index.Entries)
        {
            moves.Add(new PlannedMove(entry.Path, paths.DefinitionFile(entry.Name), IsDirectory: false));
        }

        foreach (RejectedPersonaFile rejected in index.Rejected)
        {
            string relativePath = Path.GetRelativePath(teamsRoot, rejected.Path);
            string target = Path.Combine(paths.DefinitionsRoot, UnsortedFolderName, relativePath);
            moves.Add(new PlannedMove(rejected.Path, target, IsDirectory: false));
        }

        return moves;
    }

    /// <summary>True when any path segment of <paramref name="relativePath"/> is reserved (<see cref="TaskLayout.IsReservedFolderName"/>) - a Tasks folder, not a legacy Persona definition.</summary>
    private static bool HasReservedSegment(string relativePath) =>
        relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).Any(TaskLayout.IsReservedFolderName);

    /// <summary>
    /// Plans one directory move per Teammate under <paramref name="workRoot"/>, moving its whole
    /// subtree under the Teammate's Work Dir - a Work Dir with no definition still moves.
    /// </summary>
    private static List<PlannedMove> PlanWorkDirMoves(string workRoot, TeammatePaths paths)
    {
        List<PlannedMove> moves = [];
        foreach (string teammateWorkDir in Directory.EnumerateDirectories(workRoot))
        {
            string name = Path.GetFileName(teammateWorkDir);
            moves.Add(new PlannedMove(teammateWorkDir, paths.WorkDir(name), IsDirectory: true));
        }

        return moves;
    }

    /// <summary>
    /// Plans a move for every file under <paramref name="oldTasksRoot"/> that matches the retired
    /// <c>&lt;Team&gt;/[&lt;Project&gt;/][_closed/]*.md</c> Tasks layout (<see cref="LegacyTaskPath"/>),
    /// targeting <paramref name="newTasksTeamsRoot"/>'s <see cref="TaskLayout.TasksFolder"/> layout. A
    /// file the old rules do not recognise is left in place and logged as a warning.
    /// </summary>
    private static List<PlannedMove> PlanTaskMoves(string oldTasksRoot, string newTasksTeamsRoot, ILogger logger)
    {
        List<PlannedMove> moves = [];
        foreach (string file in Directory.EnumerateFiles(oldTasksRoot, "*", SearchOption.AllDirectories))
        {
            if (LegacyTaskPath.TryMap(oldTasksRoot, file, out string? relativeTarget))
            {
                moves.Add(new PlannedMove(file, Path.Combine(newTasksTeamsRoot, relativeTarget), IsDirectory: false));
            }
            else
            {
                logger.LogWarning("Teammate layout migration: '{Path}' under the old Tasks folder does not match the Task layout and was left in place.", file);
            }
        }

        return moves;
    }

    /// <summary>Confirms every planned target is clear before any move executes.</summary>
    private static void CheckTargetsAreClear(IReadOnlyList<PlannedMove> moves)
    {
        foreach (PlannedMove move in moves)
        {
            if (File.Exists(move.Target) || Directory.Exists(move.Target))
            {
                throw new InvalidOperationException($"Could not migrate '{move.Source}' to '{move.Target}': the target already exists.");
            }
        }
    }

    /// <summary>Executes every planned move in order, creating each target's parent folder first, and logs each one.</summary>
    private static void ExecuteMoves(IReadOnlyList<PlannedMove> moves, ILogger logger)
    {
        foreach (PlannedMove move in moves)
        {
            try
            {
                string? targetParent = Path.GetDirectoryName(move.Target);
                if (targetParent is not null)
                {
                    Directory.CreateDirectory(targetParent);
                }

                if (move.IsDirectory)
                {
                    Directory.Move(move.Source, move.Target);
                }
                else
                {
                    File.Move(move.Source, move.Target);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Could not migrate '{move.Source}' to '{move.Target}': {ex.Message}", ex);
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Teammate layout migration: moved '{Source}' to '{Target}'.", move.Source, move.Target);
            }
        }
    }

    /// <summary>
    /// Removes organisational folders left empty under <paramref name="teamsRoot"/> (the root
    /// itself always survives), and removes <paramref name="workRoot"/> itself once it is empty.
    /// </summary>
    private static void RemoveEmptyOldFolders(string teamsRoot, string workRoot)
    {
        if (Directory.Exists(teamsRoot))
        {
            foreach (string subfolder in Directory.GetDirectories(teamsRoot))
            {
                RemoveIfEmptyRecursively(subfolder);
            }
        }

        if (Directory.Exists(workRoot))
        {
            RemoveIfEmptyRecursively(workRoot);
        }
    }

    /// <summary>Recursively removes a folder, and every subfolder under it, once each holds nothing.</summary>
    private static void RemoveIfEmptyRecursively(string folder)
    {
        foreach (string subfolder in Directory.GetDirectories(folder))
        {
            RemoveIfEmptyRecursively(subfolder);
        }

        if (Directory.GetFileSystemEntries(folder).Length == 0)
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>One planned move: a source path to a target path, either a single file or a whole directory subtree.</summary>
    private sealed record PlannedMove(string Source, string Target, bool IsDirectory);

    /// <summary>
    /// A copy of the retired Tasks layout's own path rules, kept inside this migration rather than
    /// shared with <see cref="TaskLayout"/> (whose <c>TryMap</c> parses the current, not the retired,
    /// layout). Recognises <c>&lt;Team&gt;/*.md</c>, <c>&lt;Team&gt;/_closed/*.md</c>,
    /// <c>&lt;Team&gt;/&lt;Project&gt;/*.md</c> and <c>&lt;Team&gt;/&lt;Project&gt;/_closed/*.md</c>, all
    /// relative to the old <c>Tasks/</c> root, and maps each to its path relative to the new Tasks
    /// root, built from <see cref="TaskLayout"/>'s folder-name constants.
    /// </summary>
    private static class LegacyTaskPath
    {
        /// <summary>
        /// Maps <paramref name="fullPath"/> (a file under <paramref name="oldTasksRoot"/>) to its
        /// target path, relative to the new Tasks root. Returns <see langword="false"/>, with
        /// <paramref name="relativeTarget"/> <see langword="null"/>, when the old layout's rules do
        /// not recognise the path - it is not rejected, only left where it is.
        /// </summary>
        internal static bool TryMap(string oldTasksRoot, string fullPath, [NotNullWhen(true)] out string? relativeTarget)
        {
            string relativePath = Path.GetRelativePath(oldTasksRoot, fullPath);
            string[] parts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

            relativeTarget = null;
            if (parts.Length < 2 || TaskLayout.IsReservedFolderName(parts[0]))
            {
                return false;
            }

            string team = parts[0];
            string fileName = parts[^1];

            if (parts.Length == 2)
            {
                relativeTarget = Path.Combine(team, TaskLayout.TasksFolder, fileName);
                return true;
            }

            if (parts.Length == 3)
            {
                if (IsClosedFolder(parts[1]))
                {
                    relativeTarget = Path.Combine(team, TaskLayout.TasksFolder, TaskLayout.ClosedFolder, fileName);
                    return true;
                }

                if (!TaskLayout.IsReservedFolderName(parts[1]))
                {
                    relativeTarget = Path.Combine(team, parts[1], TaskLayout.TasksFolder, fileName);
                    return true;
                }

                return false;
            }

            if (parts.Length == 4 && !TaskLayout.IsReservedFolderName(parts[1]) && IsClosedFolder(parts[2]))
            {
                relativeTarget = Path.Combine(team, parts[1], TaskLayout.TasksFolder, TaskLayout.ClosedFolder, fileName);
                return true;
            }

            return false;
        }

        /// <summary>True when <paramref name="name"/> is the retired layout's closed-tasks folder name.</summary>
        private static bool IsClosedFolder(string name) => FolderSnapshot.PathComparer.Equals(name, TaskLayout.ClosedFolder);
    }
}
