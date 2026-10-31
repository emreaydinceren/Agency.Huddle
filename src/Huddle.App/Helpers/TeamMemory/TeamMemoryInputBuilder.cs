using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>
/// Builds what the Team Memory Helper sees for one proposal (Team Memory Helper spec §6.3): the Team's folders, a capped index of
/// their files, and the full text of the files that best match the proposal. The match is a cheap word overlap, not search: the
/// Helper still sees the whole index. It never throws for a folder it cannot read; it builds a smaller input instead.
/// </summary>
/// <param name="access">Finds the Team by name.</param>
/// <param name="paths">Resolves the Team's memory folders.</param>
/// <param name="warnings">Warns once about a file with malformed frontmatter while its description is read.</param>
/// <param name="logger">Records a folder or file that could not be read.</param>
internal sealed partial class TeamMemoryInputBuilder(TeamMemoryAccess access, MemoryPaths paths, MemoryWarnings warnings, ILogger<TeamMemoryInputBuilder> logger)
{
    /// <summary>The most index lines the input carries.</summary>
    internal const int MaxIndexLines = 200;

    /// <summary>The most characters the index lines carry together.</summary>
    internal const int MaxIndexCharacters = 12_000;

    /// <summary>The most files the input carries in full.</summary>
    internal const int MaxCandidates = 2;

    /// <summary>The shortest word the match counts.</summary>
    private const int MinWordLength = 3;

    /// <summary>Builds the input for a proposal.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="attempt">1 for the first judgement, 2 for the one after a stale Verdict.</param>
    /// <param name="forcedFolder">With <paramref name="forcedFile"/>, a file that must be among the candidates when it exists: the one a stale Verdict named.</param>
    /// <param name="forcedFile">The forced file's name.</param>
    /// <returns>The input; with no folders and no files when the Team is gone or cannot be read.</returns>
    public TeamMemoryInput Build(TeamMemoryProposal proposal, int attempt = 1, string? forcedFolder = null, string? forcedFile = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (access.Find(proposal.Team) is not { } team)
        {
            return new TeamMemoryInput(proposal, [], [], [], attempt);
        }

        IReadOnlyList<MemoryFolder> folders = paths.TeamFolders(team);
        List<IndexedFile> files = this.ListFiles(folders);
        List<TeamMemoryCandidate> candidates = this.Candidates(files, proposal, forcedFolder, forcedFile);
        return new TeamMemoryInput(proposal, [.. folders.Select(static folder => folder.Label)], IndexLines(files), candidates, attempt);
    }

    /// <summary>The distinct words of <paramref name="text"/> the match counts: letters and digits, lower-cased, at least three long.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words.</returns>
    internal static HashSet<string> Words(string text)
    {
        HashSet<string> words = new(StringComparer.Ordinal);
        foreach (Match match in WordPattern().Matches(text))
        {
            if (match.Length >= MinWordLength)
            {
                _ = words.Add(match.Value.ToLowerInvariant());
            }
        }

        return words;
    }

    /// <summary>The index lines, capped at <see cref="MaxIndexLines"/> and <see cref="MaxIndexCharacters"/>, with a last line counting the rest.</summary>
    /// <param name="files">Every file, in folder then name order.</param>
    private static List<string> IndexLines(List<IndexedFile> files)
    {
        List<string> lines = [];
        int characters = 0;
        foreach (IndexedFile file in files)
        {
            string line = $"{file.Folder.Label}/{file.File} — {file.Summary}";
            if (lines.Count == MaxIndexLines || characters + line.Length > MaxIndexCharacters)
            {
                break;
            }

            lines.Add(line);
            characters += line.Length + 1;
        }

        if (lines.Count < files.Count)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"[…{files.Count - lines.Count} more files not shown]"));
        }

        return lines;
    }

    /// <summary>Every memory file of the folders with its description, skipping a folder that cannot be read.</summary>
    /// <param name="folders">The Team's folders.</param>
    private List<IndexedFile> ListFiles(IReadOnlyList<MemoryFolder> folders)
    {
        List<IndexedFile> files = [];
        foreach (MemoryFolder folder in folders)
        {
            try
            {
                (IReadOnlyList<MemoryEntry> entries, _) = MemoryIndex.Build(folder.Path, int.MaxValue, warnings);
                files.AddRange(entries
                    .Where(static entry => MemoryFileRules.IsValidFileName(Path.GetFileName(entry.FullPath)))
                    .Select(entry => new IndexedFile(folder, Path.GetFileName(entry.FullPath), entry.FullPath, entry.Summary)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not list the Team Memory folder '{Folder}' for a proposal.", folder.Label);
            }
        }

        return files;
    }

    /// <summary>The files to show in full: the forced one first when it exists, then the best matches with a score above zero.</summary>
    /// <param name="files">Every file.</param>
    /// <param name="proposal">The proposal.</param>
    /// <param name="forcedFolder">The forced file's folder label, or <see langword="null"/>.</param>
    /// <param name="forcedFile">The forced file's name, or <see langword="null"/>.</param>
    private List<TeamMemoryCandidate> Candidates(List<IndexedFile> files, TeamMemoryProposal proposal, string? forcedFolder, string? forcedFile)
    {
        HashSet<string> words = Words($"{proposal.Fact} {proposal.Why}");
        IEnumerable<IndexedFile> ranked = files
            .Select(file => (File: file, Score: Score(file, words)))
            .Where(static scored => scored.Score > 0)
            .OrderByDescending(static scored => scored.Score)
            .ThenByDescending(static scored => LastWrite(scored.File.Path))
            .ThenBy(static scored => $"{scored.File.Folder.Label}/{scored.File.File}", StringComparer.Ordinal)
            .Select(static scored => scored.File);

        IndexedFile? forced = files.Find(file =>
            string.Equals(file.Folder.Label, forcedFolder, StringComparison.OrdinalIgnoreCase)
            && string.Equals(file.File, forcedFile, StringComparison.OrdinalIgnoreCase));
        IEnumerable<IndexedFile> chosen = forced is null ? ranked : ranked.Where(file => !ReferenceEquals(file, forced)).Prepend(forced);

        List<TeamMemoryCandidate> candidates = [];
        foreach (IndexedFile file in chosen)
        {
            if (candidates.Count == MaxCandidates)
            {
                break;
            }

            if (this.Read(file) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    /// <summary>Reads a file in full with its version, or <see langword="null"/> when it cannot be read.</summary>
    /// <param name="file">The file.</param>
    private TeamMemoryCandidate? Read(IndexedFile file)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(file.Path);
            return new TeamMemoryCandidate(file.Folder.Label, file.File, MemoryVersion.Of(bytes), Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read the Team Memory file '{File}' for a proposal.", file.Path);
            return null;
        }
    }

    /// <summary>How many of the proposal's words occur in the file's name or description.</summary>
    /// <param name="file">The file.</param>
    /// <param name="words">The proposal's words.</param>
    private static int Score(IndexedFile file, HashSet<string> words)
    {
        HashSet<string> own = Words($"{Path.GetFileNameWithoutExtension(file.File)} {file.Summary}");
        return words.Count(own.Contains);
    }

    /// <summary>When a file was last written, or the earliest time when that cannot be read.</summary>
    /// <param name="path">The file's full path.</param>
    private static DateTime LastWrite(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only a tie-break: a file whose time cannot be read sorts last among equals.
            return DateTime.MinValue;
        }
    }

    /// <summary>A run of letters and digits.</summary>
    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    /// <summary>One memory file of the Team, with the description the index shows.</summary>
    /// <param name="Folder">Its folder.</param>
    /// <param name="File">Its name.</param>
    /// <param name="Path">Its full path.</param>
    /// <param name="Summary">Its index line.</param>
    private sealed record IndexedFile(MemoryFolder Folder, string File, string Path, string Summary);
}
