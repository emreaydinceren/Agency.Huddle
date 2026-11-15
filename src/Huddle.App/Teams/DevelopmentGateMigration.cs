using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The shared body of the start-up steps that add one gate to the <c>_team.md</c> of every installed development Team, once per
/// Team (#401, #538). A marker file in the data folder lists the Teams already handled, so a Human who later turns the gate off
/// is not overruled on the next start, and a Team whose file could not be read or written (unparseable, locked) is tried again
/// at the next start without repeating the Teams already done. A file of any other Team type is read and never written.
/// </summary>
/// <param name="options">The app options, for the data folder.</param>
/// <param name="catalog">Lists the Teams.</param>
/// <param name="folders">Reads and writes each Team's <c>_team.md</c>.</param>
/// <param name="logger">Receives a Team that could not be handled.</param>
/// <param name="gate">The gate to add.</param>
/// <param name="markerName">The file name, in the data folder, that records the Teams handled.</param>
internal sealed class DevelopmentGateMigration(IOptions<TeamOptions> options, ITeamCatalog catalog, ITeamFolders folders, ILogger logger, GateId gate, string markerName)
{
    /// <summary>The team type whose Teams run the gate.</summary>
    internal const string DevelopmentType = "development";

    private const string CompleteLine = "#complete";

    private readonly string gateId = ProcessDocument.ToId(gate);

    /// <summary>Runs the migration; a failure to record it is logged and never thrown.</summary>
    public void Run()
    {
        string marker = Path.Combine(options.Value.DataDir, markerName);
        try
        {
            this.Migrate(marker);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not record the {Gate} migration in {Marker}; it will run again at the next start.", this.gateId, marker);
        }
    }

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
                logger.LogWarning("The Team file of {Team} could not be read, so the {Gate} gate was not checked; it is tried again at the next start.", team, this.gateId);
                return false;
            }

            if (!string.Equals(file.TeamType.Id, DevelopmentType, StringComparison.Ordinal) || file.Process.Gates.Contains(gate))
            {
                return true;
            }

            return folders.UpdateTeamFile(team, current => current.WithGates([.. current.Process.Gates, gate]));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The Team file of {Team} could not be written, so it did not get the {Gate} gate; it is tried again at the next start.", team, this.gateId);
            return false;
        }
    }
}
