namespace Agency.Huddle.App.Library;

/// <summary>The outcome of <see cref="WikiLinkResolver.Resolve(IReadOnlyList{string}, string, string)"/>.</summary>
/// <param name="Path">The resolved root-relative note path, or <see langword="null"/> when nothing matches.</param>
/// <param name="IsAmbiguous">Whether several notes tied at the shortest distance from the linking note.</param>
internal sealed record WikiLinkResolution(string? Path, bool IsAmbiguous);

/// <summary>
/// Pure Spec §6.5 wikilink resolution ("Resolution follows Obsidian"), over a flat list of
/// root-relative note paths. Holds no state and touches no disk; the caller supplies the note
/// list (typically from <c>WikiLinkIndex</c>).
/// Link text (name and path segment matching) is compared case-insensitively on every OS, by
/// design, unlike file-system path comparison elsewhere in the Library.
/// </summary>
internal static class WikiLinkResolver
{
    /// <summary>
    /// Resolves <paramref name="target"/> as written inside <c>[[...]]</c> from the note at
    /// <paramref name="fromNote"/>, against the candidate <paramref name="notes"/>.
    /// </summary>
    /// <param name="notes">Every root-relative note path the link could resolve to.</param>
    /// <param name="fromNote">The root-relative path of the note containing the link.</param>
    /// <param name="target">The link target, without heading or alias.</param>
    internal static WikiLinkResolution Resolve(IReadOnlyList<string> notes, string fromNote, string target)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(fromNote);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Length == 0)
        {
            return new WikiLinkResolution(fromNote, false);
        }

        List<string> candidates = target.Contains('/', StringComparison.Ordinal)
            ? MatchByPathSuffix(notes, target)
            : MatchByName(notes, target);

        if (candidates.Count == 0)
        {
            return new WikiLinkResolution(null, false);
        }

        string[] fromFolder = FolderSegments(fromNote);
        string winner = candidates[0];
        int winnerDistance = Distance(fromFolder, FolderSegments(winner));
        int tieCount = 1;

        for (int i = 1; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            int distance = Distance(fromFolder, FolderSegments(candidate));
            if (distance < winnerDistance)
            {
                winner = candidate;
                winnerDistance = distance;
                tieCount = 1;
            }
            else if (distance == winnerDistance)
            {
                tieCount++;
                if (string.CompareOrdinal(candidate, winner) < 0)
                {
                    winner = candidate;
                }
            }
        }

        return new WikiLinkResolution(winner, tieCount > 1);
    }

    /// <summary>
    /// The shortest link target that resolves from <paramref name="fromNote"/> back to
    /// <paramref name="targetPath"/> unambiguously: the bare name first, then one more leading
    /// folder at a time, the full path as the last resort.
    /// </summary>
    /// <param name="notes">Every root-relative note path that could compete for the target.</param>
    /// <param name="fromNote">The root-relative path of the note that will hold the link.</param>
    /// <param name="targetPath">The root-relative path of the note being linked to.</param>
    internal static string ShortestTarget(IReadOnlyList<string> notes, string fromNote, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(fromNote);
        ArgumentNullException.ThrowIfNull(targetPath);

        string fileName = System.IO.Path.GetFileName(targetPath);
        string bareName = fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".md".Length]
            : fileName;

        string[] folders = targetPath[..^fileName.Length].Split('/', StringSplitOptions.RemoveEmptyEntries);

        string attempt = bareName;
        if (IsUniqueMatch(notes, fromNote, attempt, targetPath))
        {
            return attempt;
        }

        for (int start = folders.Length - 1; start >= 0; start--)
        {
            attempt = string.Join('/', folders[start..]) + "/" + bareName;
            if (IsUniqueMatch(notes, fromNote, attempt, targetPath))
            {
                return attempt;
            }
        }

        return attempt;
    }

    private static bool IsUniqueMatch(IReadOnlyList<string> notes, string fromNote, string attempt, string targetPath)
    {
        WikiLinkResolution resolution = Resolve(notes, fromNote, attempt);
        return !resolution.IsAmbiguous && string.Equals(resolution.Path, targetPath, StringComparison.Ordinal);
    }

    /// <summary>Rule 2: the notes whose file name (with <c>.md</c> appended when <paramref name="target"/> has no known extension) matches <paramref name="target"/>.</summary>
    private static List<string> MatchByName(IReadOnlyList<string> notes, string target)
    {
        string candidateName = WithMarkdownExtension(target);
        List<string> matches = [];
        foreach (string note in notes)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(System.IO.Path.GetFileName(note), candidateName))
            {
                matches.Add(note);
            }
        }

        return matches;
    }

    /// <summary>Rule 1: the notes whose path segments end with (or, when anchored, equal) <paramref name="target"/>'s segments.</summary>
    private static List<string> MatchByPathSuffix(IReadOnlyList<string> notes, string target)
    {
        bool anchored = target.StartsWith('/');
        string trimmed = anchored ? target[1..] : target;
        string[] targetSegments = trimmed.Split('/');
        targetSegments[^1] = WithMarkdownExtension(targetSegments[^1]);

        List<string> matches = [];
        foreach (string note in notes)
        {
            string[] noteSegments = note.Split('/');
            if (anchored)
            {
                if (noteSegments.Length == targetSegments.Length && SegmentsEqual(noteSegments, 0, targetSegments))
                {
                    matches.Add(note);
                }
            }
            else if (noteSegments.Length >= targetSegments.Length
                && SegmentsEqual(noteSegments, noteSegments.Length - targetSegments.Length, targetSegments))
            {
                matches.Add(note);
            }
        }

        return matches;
    }

    private static bool SegmentsEqual(string[] noteSegments, int offset, string[] targetSegments)
    {
        for (int i = 0; i < targetSegments.Length; i++)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(noteSegments[offset + i], targetSegments[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A name with no Spec §6.11 known extension (<see cref="LibraryFileKinds.HasKnownExtension"/>) matches <c>&lt;name&gt;.md</c>.</summary>
    private static string WithMarkdownExtension(string name) => LibraryFileKinds.HasKnownExtension(name) ? name : name + ".md";

    private static string[] FolderSegments(string notePath)
    {
        string[] segments = notePath.Split('/');
        return segments[..^1];
    }

    private static int Distance(string[] fromFolder, string[] candidateFolder)
    {
        int common = 0;
        int max = Math.Min(fromFolder.Length, candidateFolder.Length);
        while (common < max && StringComparer.OrdinalIgnoreCase.Equals(fromFolder[common], candidateFolder[common]))
        {
            common++;
        }

        return (fromFolder.Length - common) + (candidateFolder.Length - common);
    }
}
