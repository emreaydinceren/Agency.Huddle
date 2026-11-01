using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Holds retro proposal sets under <c>Teams/&lt;Team&gt;/_proposals/retro/&lt;id&gt;.json</c>, so a restart keeps them. The folder is
/// separate from the Sprint proposals' <c>_proposals</c> files, which are read as Sprint proposals. A Team's files are read the first
/// time the Team is asked about.
/// </summary>
/// <param name="folders">Finds the Team folder.</param>
/// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
internal sealed class RetroProposalStore(ITeamFolders folders, ILogger<RetroProposalStore>? logger = null)
{
    private readonly ILogger<RetroProposalStore> logger = logger ?? NullLogger<RetroProposalStore>.Instance;

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

        EventRaise.Isolated(this.Changed, set.Team, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
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

        EventRaise.Isolated(this.Changed, team, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
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

        EventRaise.Isolated(this.Changed, removed.Team, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
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
            AtomicFile.WriteText(Path.Combine(directory, set.Id + ".json"), JsonSerializer.Serialize(set, JsonOptions));
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
                if (set is not null
                    && !string.IsNullOrWhiteSpace(set.Id)
                    && string.Equals(set.Id, Path.GetFileNameWithoutExtension(file), StringComparison.Ordinal)
                    && string.Equals(set.Team, team, StringComparison.OrdinalIgnoreCase))
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
            catch (UnauthorizedAccessException)
            {
                // A file this process may not read is skipped for the same reason.
            }
        }
    }
}
