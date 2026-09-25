using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// A one-time, idempotent start-up step (ADR-0031 *Migration*, Spec §6.15) that moves an old
/// <c>{DataDir}/Teams/</c> + <c>{DataDir}/work/</c> layout into the sibling-folder layout each
/// Teammate now owns: <c>{DataDir}/{Acp:TeammatesDir}/&lt;Name&gt;/&lt;Name&gt;.md</c> plus its
/// Work Dir underneath. It must run before <see cref="PersonaStore"/>, so it is a plain static
/// call from <c>Program.cs</c>, not a hosted service - <see cref="PersonaStore"/> scans its
/// folder in its own constructor. Step 4 (moving <c>Tasks/</c>) is the Tasks effort's job and is
/// not implemented here (Gate G1).
/// </summary>
internal static class TeammateLayoutMigration
{
    private const string UnsortedFolderName = "_unsorted";
    private const string OldTeamsFolderName = "Teams";
    private const string OldWorkFolderName = "work";
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

        string teamsRoot = Path.Combine(dataDir, OldTeamsFolderName);
        string workRoot = Path.Combine(dataDir, OldWorkFolderName);
        TeammatePaths paths = new(options);

        string markerPath = Path.Combine(paths.DefinitionsRoot, MarkerFileName);
        if (File.Exists(markerPath))
        {
            return;
        }

        List<PlannedMove> moves = ResolveAlreadyDoneMoves(PlanMoves(teamsRoot, workRoot, paths));
        if (moves.Count == 0)
        {
            logger.LogInformation("Teammate layout migration: nothing to migrate.");
            WriteMarker(paths.DefinitionsRoot, markerPath);
            return;
        }

        CheckTargetsAreClear(moves);
        ExecuteMoves(moves, logger);
        RemoveEmptyOldFolders(teamsRoot, workRoot);
        WriteMarker(paths.DefinitionsRoot, markerPath);
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
        List<string> files = [.. Directory.EnumerateFiles(teamsRoot, "*.md", SearchOption.AllDirectories)];
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
}
