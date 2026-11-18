using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Start-up step (#669, spec D-5): offers the Human a rewrite of an installed Team's <c>AGENTS.md</c> that still describes the Review
/// stage, as pending retro proposals from <c>Huddle</c> (the existing card with a diff and Accept, ADR-0041). It matches only the
/// exact wording in <see cref="OutdatedReviewWording"/> that occurs once in the file, offers it once per Team (the marker file
/// <c>review-tasks.proposed</c> in the Team folder, kept after a Reject), and never writes <c>AGENTS.md</c> itself. A failure for one
/// Team is logged and never stops the host.
/// </summary>
/// <param name="catalog">Lists the Teams.</param>
/// <param name="folders">Finds each Team folder.</param>
/// <param name="proposals">Holds the retro proposal sets the Human decides.</param>
/// <param name="logger">Receives a Team that could not be handled.</param>
internal sealed class ReviewStageMigration(ITeamCatalog catalog, ITeamFolders folders, RetroProposalStore proposals, ILogger<ReviewStageMigration> logger) : IHostedService
{
    /// <summary>The file, in a Team folder, that records the offer was made.</summary>
    internal const string MarkerName = "review-tasks.proposed";

    /// <summary>The proposer the retro cards show.</summary>
    internal const string Proposer = "Huddle";

    private const int ChangesPerSet = 3;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        catalog.Refresh();
        foreach (TeamSummary team in catalog.Teams)
        {
            try
            {
                this.Offer(team.Name);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "The Team {Team} could not be checked for outdated Review wording; it is tried again at the next start.", team.Name);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsOfferable(string text, RetroChange change)
    {
        string find = RetroDiff.FitLineEndings(text, change.Find);
        return RetroDiff.CountMatches(text, find) == 1
            && (OutdatedReviewWording.ContextFor(change) is not { } titleLines || titleLines.Any(titleLine => text.Contains(titleLine, StringComparison.Ordinal)));
    }

    private void Offer(string team)
    {
        if (folders.FindTeam(team).Value is not { } folder)
        {
            return;
        }

        string marker = Path.Combine(folder.FullPath, MarkerName);
        string agentsMd = Path.Combine(folder.FullPath, TeamPaths.AgentsMdFileName);
        if (File.Exists(marker) || !File.Exists(agentsMd))
        {
            return;
        }

        string text = File.ReadAllText(agentsMd);
        List<RetroChange> found = [.. OutdatedReviewWording.All.Where(change => IsOfferable(text, change))];
        bool alreadyOffered = proposals.Pending(team).Any(static set => string.Equals(set.Proposer, Proposer, StringComparison.Ordinal));
        if (found.Count > 0 && !alreadyOffered && !this.Store(team, found))
        {
            logger.LogWarning("The Team {Team} has outdated Review wording, but the proposals could not be stored; it is tried again at the next start.", team);
            return;
        }

        AtomicFile.WriteText(marker, "The Review-stage rewrite was offered to this Team once (#669); delete this file to offer it again.");
    }

    private bool Store(string team, List<RetroChange> found)
    {
        foreach (RetroChange[] chunk in found.Chunk(ChangesPerSet))
        {
            List<RetroChange> changes = [.. chunk.Select(static (change, index) => change with { Id = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) })];
            if (!proposals.Add(new RetroProposalSet(Guid.NewGuid().ToString("N"), team, Proposer, changes)))
            {
                return false;
            }
        }

        return true;
    }
}
