using System.Text.Json;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>One change a retro proposes: an exact-text replacement in one allowed file (RT-5).</summary>
/// <param name="Id">The change's id within its set: <c>1</c>, <c>2</c> or <c>3</c>.</param>
/// <param name="Title">A short name for the change; it becomes the register row's Finding.</param>
/// <param name="TargetFile">The file to edit: <c>Process.md</c>, <c>_team.md</c> or a Team member's <c>Name.md</c>.</param>
/// <param name="Find">The text to replace; it must occur exactly once in the file when the change is accepted.</param>
/// <param name="Replace">The replacement; empty removes the found text.</param>
internal sealed record RetroChange(string Id, string Title, string TargetFile, string Find, string Replace);

/// <summary>Up to three changes one retro agent proposed to the Human, held under <c>Teams/&lt;Team&gt;/_proposals/retro/</c>.</summary>
/// <param name="Id">The set's id, a Guid in <c>"N"</c> format.</param>
/// <param name="Team">The Team the retro was for.</param>
/// <param name="Proposer">The Name of the Teammate that proposed the changes.</param>
/// <param name="Changes">The changes still waiting for the Human.</param>
internal sealed record RetroProposalSet(string Id, string Team, string Proposer, IReadOnlyList<RetroChange> Changes);

/// <summary>What accepting or rejecting one retro change did.</summary>
internal enum RetroChangeOutcomeKind
{
    /// <summary>The edit was made and the register row recorded.</summary>
    Applied,

    /// <summary>Nothing was changed because the text did not match exactly once, or the file could not be written; the change stays pending.</summary>
    Refused,

    /// <summary>The Human rejected the change; nothing was written.</summary>
    Rejected,

    /// <summary>The change was already taken by another click, or never existed in this room.</summary>
    Gone,
}

/// <summary>The result of accepting or rejecting one retro change.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Text">The text posted in the room, or the reason for a refusal; empty for <see cref="RetroChangeOutcomeKind.Gone"/>.</param>
internal sealed record RetroChangeOutcome(RetroChangeOutcomeKind Kind, string Text);

/// <summary>Computes the unified diff the proposal card shows. The agent never supplies a diff; Huddle derives it from the current file.</summary>
internal static class RetroDiff
{
    /// <summary>Counts the places where <paramref name="find"/> starts in <paramref name="text"/>, overlapping starts included.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="find">The text to look for; empty counts as no match.</param>
    /// <returns>The number of starts.</returns>
    public static int CountMatches(string text, string find)
    {
        if (find.Length == 0)
        {
            return 0;
        }

        int count = 0;
        int index = text.IndexOf(find, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(find, index + 1, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Gives <paramref name="value"/> the line endings of <paramref name="text"/>: CRLF when the file uses them, else LF.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="value">The agent's find or replace text.</param>
    /// <returns>The text with matching line endings.</returns>
    public static string FitLineEndings(string text, string value)
    {
        string lf = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.Contains("\r\n", StringComparison.Ordinal) ? lf.Replace("\n", "\r\n", StringComparison.Ordinal) : lf;
    }

    /// <summary>Writes the diff of replacing the one occurrence of <paramref name="find"/>: a header, one line of context either side, then the old lines with <c>-</c> and the new lines with <c>+</c>.</summary>
    /// <param name="file">The file name for the header.</param>
    /// <param name="text">The current file text.</param>
    /// <param name="find">The text to replace; it must occur in <paramref name="text"/>.</param>
    /// <param name="replace">The replacement.</param>
    /// <returns>The diff, lines joined with a line feed and no trailing line feed.</returns>
    public static string Unified(string file, string text, string find, string replace)
    {
        int start = text.IndexOf(find, StringComparison.Ordinal);
        int end = start + find.Length;
        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        int lineEnd = text.IndexOf('\n', end);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        string oldBlock = text[lineStart..lineEnd];
        string newBlock = string.Concat(text.AsSpan(lineStart, start - lineStart), replace, text.AsSpan(end, lineEnd - end));
        List<string> lines = ["--- " + file, "+++ " + file, "@@"];
        if (lineStart > 0)
        {
            int previousEnd = lineStart - 1;
            int previousStart = previousEnd == 0 ? 0 : text.LastIndexOf('\n', previousEnd - 1) + 1;
            lines.Add(" " + text[previousStart..previousEnd].TrimEnd('\r'));
        }

        foreach (string line in oldBlock.Split('\n'))
        {
            lines.Add("-" + line.TrimEnd('\r'));
        }

        if (newBlock.Length > 0)
        {
            foreach (string line in newBlock.Split('\n'))
            {
                lines.Add("+" + line.TrimEnd('\r'));
            }
        }

        if (lineEnd + 1 < text.Length)
        {
            int nextStart = lineEnd + 1;
            int nextEnd = text.IndexOf('\n', nextStart);
            if (nextEnd < 0)
            {
                nextEnd = text.Length;
            }

            lines.Add(" " + text[nextStart..nextEnd].TrimEnd('\r'));
        }

        return string.Join('\n', lines);
    }
}

/// <summary>
/// Holds retro proposal sets under <c>Teams/&lt;Team&gt;/_proposals/retro/&lt;id&gt;.json</c>, so a restart keeps them. The folder is
/// separate from the Sprint proposals' <c>_proposals</c> files, which are read as Sprint proposals. A Team's files are read the first
/// time the Team is asked about.
/// </summary>
/// <param name="folders">Finds the Team folder.</param>
internal sealed class RetroProposalStore(ITeamFolders folders)
{
    private const string ProposalsFolder = "_proposals";

    private const string RetroFolder = "retro";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Lock gate = new();
    private readonly Dictionary<string, RetroProposalSet> byId = new(StringComparer.Ordinal);
    private readonly HashSet<string> loadedTeams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised with the Team name after a set was added, changed or removed.</summary>
    public event Action<string>? Changed;

    /// <summary>The sets waiting for the Human in one Team, ordered by id.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The sets.</returns>
    public IReadOnlyList<RetroProposalSet> Pending(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        lock (this.gate)
        {
            this.EnsureLoaded(team);
            return [.. this.byId.Values
                .Where(set => string.Equals(set.Team, team, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static set => set.Id, StringComparer.Ordinal)];
        }
    }

    /// <summary>Finds a set by id.</summary>
    /// <param name="id">The set's id.</param>
    /// <returns>The set, or <see langword="null"/> when it is gone.</returns>
    public RetroProposalSet? Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (this.gate)
        {
            return this.byId.GetValueOrDefault(id);
        }
    }

    /// <summary>Stores a set on disk and in memory.</summary>
    /// <param name="set">The set.</param>
    /// <returns><see langword="false"/> when the Team has no folder or the file could not be written.</returns>
    public bool Add(RetroProposalSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        lock (this.gate)
        {
            this.EnsureLoaded(set.Team);
            if (!this.Write(set))
            {
                return false;
            }

            this.byId[set.Id] = set;
        }

        this.Changed?.Invoke(set.Team);
        return true;
    }

    /// <summary>Removes one change from a set, and the set itself when it was the last.</summary>
    /// <param name="setId">The set's id.</param>
    /// <param name="changeId">The change's id.</param>
    /// <returns><see langword="true"/> when the change was there and is now gone.</returns>
    public bool RemoveChange(string setId, string changeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeId);
        string team;
        lock (this.gate)
        {
            if (!this.byId.TryGetValue(setId, out RetroProposalSet? set)
                || !set.Changes.Any(change => string.Equals(change.Id, changeId, StringComparison.Ordinal)))
            {
                return false;
            }

            team = set.Team;
            List<RetroChange> left = [.. set.Changes.Where(change => !string.Equals(change.Id, changeId, StringComparison.Ordinal))];
            if (left.Count == 0)
            {
                _ = this.byId.Remove(setId);
                this.Delete(set);
            }
            else
            {
                RetroProposalSet smaller = set with { Changes = left };
                this.byId[setId] = smaller;
                _ = this.Write(smaller);
            }
        }

        this.Changed?.Invoke(team);
        return true;
    }

    /// <summary>Removes a whole set.</summary>
    /// <param name="setId">The set's id.</param>
    /// <returns><see langword="true"/> when the set was there.</returns>
    public bool Remove(string setId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        RetroProposalSet? removed;
        lock (this.gate)
        {
            if (!this.byId.Remove(setId, out removed))
            {
                return false;
            }

            this.Delete(removed);
        }

        this.Changed?.Invoke(removed.Team);
        return true;
    }

    private string? Directory(string team)
    {
        LibraryResult<LibraryPath> folder = folders.FindTeam(team);
        return folder.Value is { } path ? Path.Combine(path.FullPath, ProposalsFolder, RetroFolder) : null;
    }

    private bool Write(RetroProposalSet set)
    {
        string? directory = this.Directory(set.Team);
        if (directory is null)
        {
            return false;
        }

        try
        {
            _ = System.IO.Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, set.Id + ".json"), JsonSerializer.Serialize(set, JsonOptions));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Delete(RetroProposalSet set)
    {
        string? directory = this.Directory(set.Team);
        if (directory is null)
        {
            return;
        }

        try
        {
            File.Delete(Path.Combine(directory, set.Id + ".json"));
        }
        catch (IOException)
        {
            // The set is gone from memory; a leftover file only makes it reappear after a restart, and nothing is applied until Accept.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: the leftover file never edits anything on its own.
        }
    }

    private void EnsureLoaded(string team)
    {
        if (!this.loadedTeams.Add(team))
        {
            return;
        }

        string? directory = this.Directory(team);
        if (directory is null || !System.IO.Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in System.IO.Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                RetroProposalSet? set = JsonSerializer.Deserialize<RetroProposalSet>(File.ReadAllText(file), JsonOptions);
                if (set is not null && !string.IsNullOrWhiteSpace(set.Id))
                {
                    this.byId[set.Id] = set;
                }
            }
            catch (JsonException)
            {
                // A hand-damaged file is skipped; the Human never sees a card for it.
            }
            catch (IOException)
            {
                // An unreadable file is skipped for the same reason.
            }
        }
    }
}
