using System.Text.Json;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Holds the Sprints managers have proposed (PL-4): in memory for reading, and one JSON file per proposal under
/// <c>_proposals/</c> in the Team's folder so a restart keeps them. It never creates a Sprint (ADR-0037).
/// </summary>
internal sealed class SprintProposalStore(ITeamFolders folders)
{
    private const string ProposalsFolder = "_proposals";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Lock gate = new();
    private readonly Dictionary<string, SprintProposal> byId = new(StringComparer.Ordinal);
    private readonly HashSet<string> loadedTeams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The proposals waiting for the Human in <paramref name="team"/>, oldest file name first.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The pending proposals; empty when there are none.</returns>
    public IReadOnlyList<SprintProposal> Pending(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        lock (this.gate)
        {
            this.EnsureLoaded(team);
            return [.. this.byId.Values
                .Where(proposal => string.Equals(proposal.Team, team, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static proposal => proposal.Id, StringComparer.Ordinal)];
        }
    }

    /// <summary>Finds a loaded proposal by id.</summary>
    /// <param name="id">The proposal's id.</param>
    /// <returns>The proposal, or <see langword="null"/> when none is loaded under that id.</returns>
    public SprintProposal? Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (this.gate)
        {
            return this.byId.GetValueOrDefault(id);
        }
    }

    /// <summary>Stores a proposal in memory and on disk.</summary>
    /// <param name="proposal">The proposal to keep.</param>
    /// <returns><see langword="true"/> when it was written; <see langword="false"/> when the Team's folder could not be used, in which case nothing is kept.</returns>
    public bool Add(SprintProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        lock (this.gate)
        {
            this.EnsureLoaded(proposal.Team);
            string? directory = this.ProposalsDirectory(proposal.Team);
            if (directory is null)
            {
                return false;
            }

            try
            {
                _ = Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, proposal.Id + ".json"), JsonSerializer.Serialize(proposal, JsonOptions));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            this.byId[proposal.Id] = proposal;
        }

        this.Changed?.Invoke(proposal.Team);
        return true;
    }

    /// <summary>Raised, outside the lock, with the Team's name after a proposal was added or removed.</summary>
    public event Action<string>? Changed;

    /// <summary>Removes a proposal from memory and disk.</summary>
    /// <param name="id">The proposal's id.</param>
    /// <returns><see langword="true"/> when a proposal was removed.</returns>
    public bool Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        SprintProposal? removed;
        lock (this.gate)
        {
            if (!this.byId.Remove(id, out removed))
            {
                return false;
            }

            string? directory = this.ProposalsDirectory(removed.Team);
            if (directory is not null)
            {
                try
                {
                    File.Delete(Path.Combine(directory, id + ".json"));
                }
                catch (IOException)
                {
                    // The proposal is gone from memory; a leftover file is read back as pending after a restart, which is safe.
                }
                catch (UnauthorizedAccessException)
                {
                    // Same as above: the leftover file only makes the proposal reappear, never creates a Sprint.
                }
            }
        }

        this.Changed?.Invoke(removed.Team);
        return true;
    }

    /// <summary>Loads the Team's proposal files the first time the Team is touched.</summary>
    /// <param name="team">The Team's name.</param>
    private void EnsureLoaded(string team)
    {
        if (!this.loadedTeams.Add(team))
        {
            return;
        }

        string? directory = this.ProposalsDirectory(team);
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                SprintProposal? proposal = JsonSerializer.Deserialize<SprintProposal>(File.ReadAllText(file), JsonOptions);
                if (proposal is not null && !string.IsNullOrWhiteSpace(proposal.Id))
                {
                    this.byId[proposal.Id] = proposal;
                }
            }
            catch (JsonException)
            {
                // A hand-damaged proposal file is skipped; the Human never sees a card for it.
            }
            catch (IOException)
            {
                // An unreadable file is skipped for the same reason.
            }
        }
    }

    /// <summary>The <c>_proposals</c> folder of a Team, or <see langword="null"/> when the Team's folder cannot be resolved.</summary>
    /// <param name="team">The Team's name.</param>
    private string? ProposalsDirectory(string team)
    {
        LibraryResult<LibraryPath> result = folders.EnsureTeam(team);
        return result.Succeeded && result.Value is { } path
            ? Path.Combine(path.FullPath, ProposalsFolder)
            : null;
    }
}
