using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Builds the outputs index a session carries (Spec §6.3, §8.1 slot 13): the Markdown documents under each Team's and Project's
/// <c>outputs/</c>, ordinal by path, Team by Team in the order given, capped across all of them. The Read when line is the file's
/// frontmatter <c>description</c> only; a file without one is listed by its bare relative path.
/// </summary>
internal static class OutputsIndex
{
    /// <summary>The most characters of a description kept.</summary>
    private const int MaxDescriptionLength = 200;

    /// <summary>The most characters read from a file to find its frontmatter.</summary>
    private const int MaxScannedCharacters = 16 * 1024;

    /// <summary>
    /// Lists the documents under <c>outputs/</c> of every Team and of each of its Projects. A Team or Project folder that is a reparse
    /// point is skipped, and a folder that cannot be read counts as empty. Folders and files whose names start with <c>_</c> or <c>.</c>
    /// are skipped, and only <c>*.md</c> files are indexed.
    /// </summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="teamNames">The Teams' folder names, in the order the Persona lists them; a name given twice counts once.</param>
    /// <param name="maxEntries">The most entries to list across every Team; zero or less lists none.</param>
    /// <param name="warnings">Warns once per file and content about a description that is not one line of text; <see langword="null"/> warns of nothing.</param>
    /// <returns>The listed entries, the number left out and one overflow line per folder that lost documents.</returns>
    public static OutputsSnapshot Build(string teamsRoot, IReadOnlyList<string> teamNames, int maxEntries, MemoryWarnings? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamsRoot);
        ArgumentNullException.ThrowIfNull(teamNames);

        int remaining = Math.Max(0, maxEntries);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<OutputsEntry> listed = [];
        Dictionary<string, int> left = new(StringComparer.Ordinal);
        List<string> leftOrder = [];

        foreach (string team in teamNames)
        {
            if (!seen.Add(team))
            {
                continue;
            }

            List<(string Path, string Folder, string File)> documents = DocumentsOf(teamsRoot, team);
            documents.Sort(static (a, b) => string.CompareOrdinal(a.Path, b.Path));
            foreach ((string path, string folder, string file) in documents)
            {
                if (remaining > 0)
                {
                    remaining--;
                    listed.Add(new OutputsEntry(path, DescriptionOf(file, warnings)));
                }
                else if (left.TryGetValue(folder, out int count))
                {
                    left[folder] = count + 1;
                }
                else
                {
                    left[folder] = 1;
                    leftOrder.Add(folder);
                }
            }
        }

        List<OutputsOverflow> overflows = [.. leftOrder.Select(folder => new OutputsOverflow(folder, left[folder]))];
        return new OutputsSnapshot(listed, left.Values.Sum(), overflows);
    }

    /// <summary>Every indexable document of one Team: its own <c>outputs/</c> and that of each Project folder.</summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="team">The Team's folder name.</param>
    private static List<(string Path, string Folder, string File)> DocumentsOf(string teamsRoot, string team)
    {
        List<(string Path, string Folder, string File)> documents = [];
        string teamFolder = System.IO.Path.Combine(teamsRoot, team);
        if (!Directory.Exists(teamFolder) || TeamMemoryIndex.IsSkipped(teamFolder))
        {
            return documents;
        }

        List<string> scopes = [teamFolder];
        foreach (string project in SafeDirectories(teamFolder))
        {
            string name = System.IO.Path.GetFileName(project);
            if (!TeamNames.IsReservedProjectName(name) && !TeamMemoryIndex.IsSkipped(project))
            {
                scopes.Add(project);
            }
        }

        foreach (string scope in scopes)
        {
            string outputs = System.IO.Path.Combine(scope, TeamNames.OutputsFolder);
            if (!TeamMemoryIndex.IsSkipped(outputs))
            {
                string folder = RelativeTo(teamsRoot, outputs);
                documents.AddRange(MarkdownFiles(outputs).Select(file => (RelativeTo(teamsRoot, file), folder, file)));
            }
        }

        return documents;
    }

    /// <summary>The Markdown files under <paramref name="folder"/>, recursively, skipping <c>_</c> and <c>.</c> names and reparse points.</summary>
    /// <param name="folder">The folder to walk; a missing one gives none.</param>
    private static List<string> MarkdownFiles(string folder)
    {
        List<string> files = [];
        try
        {
            foreach (string file in Directory.EnumerateFiles(folder))
            {
                string name = System.IO.Path.GetFileName(file);
                if (!IsHidden(name) && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Build is a pure static with no logger: a folder that cannot be read lists nothing, so one locked folder does not stop the session prompt.
        }

        foreach (string sub in SafeDirectories(folder))
        {
            if (!IsHidden(System.IO.Path.GetFileName(sub)) && !TeamMemoryIndex.IsSkipped(sub))
            {
                files.AddRange(MarkdownFiles(sub));
            }
        }

        return files;
    }

    /// <summary>The sub folders of <paramref name="folder"/>, or none when it cannot be listed.</summary>
    /// <param name="folder">The folder.</param>
    private static string[] SafeDirectories(string folder)
    {
        try
        {
            return Directory.GetDirectories(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Build is a pure static with no logger: an unreadable folder has no sub folders as far as the index is concerned.
            return [];
        }
    }

    /// <summary>Whether a file or folder name starts with <c>_</c> or <c>.</c>, which the index skips.</summary>
    /// <param name="name">The name.</param>
    private static bool IsHidden(string name) => name.StartsWith('_') || name.StartsWith('.');

    /// <summary>A path under the Teams root as <c>/</c>-separated text.</summary>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="path">The path.</param>
    private static string RelativeTo(string teamsRoot, string path) =>
        System.IO.Path.GetRelativePath(teamsRoot, path).Replace('\\', '/');

    /// <summary>The clipped frontmatter description of a file, or <see langword="null"/> when it has none or cannot be read.</summary>
    /// <param name="file">The document.</param>
    /// <param name="warnings">Warns about a description that is not one line of text, or <see langword="null"/>.</param>
    private static string? DescriptionOf(string file, MemoryWarnings? warnings)
    {
        string text;
        try
        {
            using StreamReader reader = new(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            char[] buffer = new char[MaxScannedCharacters];
            int read = reader.ReadBlock(buffer, 0, buffer.Length);
            text = new string(buffer, 0, read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A document that cannot be read is listed by its bare path, as one without a description.
            return null;
        }

        NoteFrontmatterBlock block = NoteFrontmatter.Locate(text);
        if (block.Text is null)
        {
            return null;
        }

        IReadOnlyList<NoteProperty> properties = NoteProperties.Read(block);
        if (MemoryFileRules.DescriptionIn(properties) is { } description)
        {
            return description.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] + "…" : description;
        }

        if (properties.Any(MemoryFileRules.IsDescriptionKey))
        {
            warnings?.Malformed(file, text, "its description is not a single line of text");
        }

        return null;
    }
}
