using System.Globalization;
using Agency.Huddle.Seeder.Scenarios;

namespace Agency.Huddle.Seeder;

/// <summary>The command line: parsing, running and reporting. Kept out of <c>Program</c> so it can be tested with fake writers and a fake clock.</summary>
internal static class SeedCli
{
    /// <summary>Parses the arguments, runs the seeder and prints the result.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">Where normal output goes.</param>
    /// <param name="error">Where errors go.</param>
    /// <param name="clock">Supplies the default run date.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The process exit code: 0 on success, 1 on a refused or failed run, 2 on bad arguments.</returns>
    internal static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, TimeProvider clock, CancellationToken ct)
    {
        (SeedRequest? request, string? message, int exitCode) = Parse(args, clock);
        if (request is null)
        {
            (exitCode == 0 ? output : error).WriteLine(message);
            return exitCode;
        }

        try
        {
            output.WriteLine($"Wiping and rebuilding {request.Root} ({request.Scenario.Name}, run date {request.Today:yyyy-MM-dd})...");
            SeedSummary summary = await SeedRunner.RunAsync(request, SeedRootGuard.DefaultProtectedFolders(), ct);
            Report(summary, output);
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>Parses the command line.</summary>
    /// <param name="args">The arguments.</param>
    /// <param name="clock">Supplies the default run date (the UTC date).</param>
    /// <returns>Either a request to run, or a message and the exit code to end with.</returns>
    internal static (SeedRequest? Request, string? Message, int ExitCode) Parse(string[] args, TimeProvider clock)
    {
        string scenarioName = ScenarioCatalog.DefaultName;
        string? root = null;
        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        Queue<string> pending = new(args);
        while (pending.TryDequeue(out string? arg))
        {
            if (arg is "-h" or "--help" or "/?")
            {
                return (null, Help(), 0);
            }

            if (!pending.TryDequeue(out string? value))
            {
                return (null, $"Missing value for {arg}.\n\n{Help()}", 2);
            }

            switch (arg)
            {
                case "--scenario":
                    scenarioName = value;
                    break;
                case "--root":
                    root = value;
                    break;
                case "--today":
                    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out today))
                    {
                        return (null, $"--today must be yyyy-MM-dd, not '{value}'.", 2);
                    }

                    break;
                default:
                    return (null, $"Unknown option {arg}.\n\n{Help()}", 2);
            }
        }

        IScenario? scenario = ScenarioCatalog.Find(scenarioName);
        if (scenario is null)
        {
            return (null, $"Unknown scenario '{scenarioName}'.\n\n{Help()}", 2);
        }

        return (new SeedRequest(scenario, root ?? DefaultRoot(scenario.Name), today), null, 0);
    }

    /// <summary>The seed root used when <c>--root</c> is not given: <c>&lt;drive&gt;:\seeds\huddle\&lt;scenario&gt;</c>.</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The path.</returns>
    internal static string DefaultRoot(string scenario)
    {
        string drive = Path.GetPathRoot(Environment.SystemDirectory) ?? Path.GetPathRoot(Path.GetTempPath()) ?? "/";
        return Path.Combine(drive, "seeds", "huddle", scenario);
    }

    private static string Help()
    {
        string scenarios = string.Join("\n", ScenarioCatalog.All.Select(s => $"  {s.Name,-14} {s.Description}"));
        return $"""
            huddle-seed: wipe a seed folder and rebuild a complete Huddle data set in it.

            Usage: huddle-seed [--scenario <name>] [--root <folder>] [--today yyyy-MM-dd]

              --scenario   Which data set to build. Default: {ScenarioCatalog.DefaultName}
              --root       The seed root. Default: {DefaultRoot(ScenarioCatalog.DefaultName)}
              --today      The date every offset is measured from. Default: today (UTC)

            The root is deleted first, including team.db. It is refused unless it is empty, absent, or
            holds the .huddle-seed marker this tool writes; drive roots, the repository and the user
            profile are always refused.

            Scenarios:
            {scenarios}
            """;
    }

    private static void Report(SeedSummary summary, TextWriter output)
    {
        output.WriteLine();
        output.WriteLine($"Seeded {summary.Plan.Teammates.Count} Teammates, {summary.Plan.Teams.Count} Teams, {summary.Plan.Tasks.Count} Tasks,");
        output.WriteLine($"{summary.Plan.Rooms.Count} Rooms ({summary.Plan.Rooms.Sum(r => r.Messages.Count)} messages), {summary.Plan.Files.Count} note and memory files.");
        output.WriteLine($"Manifest: {Path.Combine(summary.Root, "manifest.md")}");
        output.WriteLine();
        output.WriteLine("Run Huddle against it (mock adapter, no cost):");
        output.WriteLine(LaunchCommand.Build(summary.DataDir));
    }
}
