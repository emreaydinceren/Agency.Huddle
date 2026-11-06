using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Start-up step (#401): adds <c>accept-before-merge</c> to the <c>_team.md</c> of every installed development Team, once per
/// Team. A state file in the data folder lists the Teams already handled, so a Human who later turns the gate off is not
/// overruled on the next start, and a Team whose file could not be read or written (unparseable, locked) is tried again at the
/// next start without repeating the Teams already done. A file of any other Team type is read and never written. A failure is
/// logged and never stops the host. New Teams get the gate from their process template instead.
/// </summary>
/// <param name="options">The app options, for the data folder.</param>
/// <param name="catalog">Lists the Teams.</param>
/// <param name="folders">Reads and writes each Team's <c>_team.md</c>.</param>
/// <param name="logger">Receives a Team that could not be handled.</param>
internal sealed class AcceptBeforeMergeMigration(IOptions<TeamOptions> options, ITeamCatalog catalog, ITeamFolders folders, ILogger<AcceptBeforeMergeMigration> logger) : IHostedService
{
    /// <summary>The team type whose Teams merge code.</summary>
    internal const string DevelopmentType = "development";

    private const string MarkerName = "accept-before-merge.migrated";

    private const string CompleteLine = "#complete";

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        string marker = Path.Combine(options.Value.DataDir, MarkerName);
        try
        {
            this.Migrate(marker);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not record the accept-before-merge migration in {Marker}; it will run again at the next start.", marker);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Migrate(string marker)
    {
        HashSet<string> handled = File.Exists(marker)
            ? [.. File.ReadAllLines(marker).Where(static line => line.Length > 0)]
            : [];
        if (handled.Contains(CompleteLine))
        {
            return;
        }

        catalog.Refresh();
        bool all = true;
        foreach (TeamSummary summary in catalog.Teams)
        {
            if (handled.Contains(summary.Name))
            {
                continue;
            }

            if (this.Handle(summary.Name))
            {
                _ = handled.Add(summary.Name);
            }
            else
            {
                all = false;
            }
        }

        if (all)
        {
            _ = handled.Add(CompleteLine);
        }

        Directory.CreateDirectory(options.Value.DataDir);
        AtomicFile.WriteBytes(marker, System.Text.Encoding.UTF8.GetBytes(string.Join('\n', handled.Order(StringComparer.Ordinal))));
    }

    /// <summary>Adds the gate to one Team when it is a development Team without it.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns><see langword="true"/> when the Team is settled; <see langword="false"/> when its file could not be read or written.</returns>
    private bool Handle(string team)
    {
        try
        {
            if (folders.ReadTeamFile(team) is not { } file)
            {
                logger.LogWarning("The Team file of {Team} could not be read, so the accept-before-merge gate was not checked; it is tried again at the next start.", team);
                return false;
            }

            if (!string.Equals(file.TeamType.Id, DevelopmentType, StringComparison.Ordinal) || file.Process.Gates.Contains(GateId.AcceptBeforeMerge))
            {
                return true;
            }

            return folders.UpdateTeamFile(team, static current => current.WithGates([.. current.Process.Gates, GateId.AcceptBeforeMerge]));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The Team file of {Team} could not be written, so it did not get the accept-before-merge gate; it is tried again at the next start.", team);
            return false;
        }
    }
}
