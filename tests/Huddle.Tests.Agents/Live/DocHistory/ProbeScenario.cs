using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Live.DocHistory;

/// <summary>What one probe run left behind: the calls it made, what the Agent said and the folder before and after.</summary>
/// <param name="Calls">The <c>log_change</c> calls of this run only.</param>
/// <param name="Reply">The Agent's reply text.</param>
/// <param name="Before">The working folder's files as seeded, by relative path.</param>
/// <param name="After">The working folder's files after the Turn, by relative path.</param>
internal sealed record ProbeRun(IReadOnlyList<LoggedCall> Calls, string Reply, IReadOnlyDictionary<string, string> Before, IReadOnlyDictionary<string, string> After)
{
    /// <summary>The calls the real tool would have accepted: a rooted path inside the working folder.</summary>
    public IReadOnlyList<LoggedCall> Accepted => [.. this.Calls.Where(static call => string.Equals(call.Result, "Logged.", StringComparison.Ordinal))];

    /// <summary>The file names of every accepted call's <c>path</c>, in order.</summary>
    public IReadOnlyList<string> CalledFiles => [.. this.Accepted.Select(static call => Path.GetFileName((string?)call.Arguments["path"] ?? string.Empty))];

    /// <summary>The <c>kind</c> of every accepted call, lower-cased, in order.</summary>
    public IReadOnlyList<string> CalledKinds => [.. this.Accepted.Select(static call => ((string?)call.Arguments["kind"] ?? string.Empty).ToLowerInvariant())];

    /// <summary>Whether <paramref name="file"/> is exactly as it was seeded.</summary>
    /// <param name="file">A seeded file's relative path.</param>
    /// <returns><see langword="true"/> when the file still holds its seeded bytes.</returns>
    public bool Unchanged(string file) =>
        this.Before.TryGetValue(file, out string? before) && this.After.TryGetValue(file, out string? after) && string.Equals(before, after, StringComparison.Ordinal);
}

/// <summary>One probe scenario of DocHistory spec §10 (DH-T0): a prompt, a seeded folder, what a passing run looks like and how often it must.</summary>
/// <param name="Id">The scenario's id, for example <c>LIVE-DH-1</c>.</param>
/// <param name="Prompt">The user's message to the Agent.</param>
/// <param name="Seed">Writes the scenario's files into an empty working folder.</param>
/// <param name="Failure">The reason a run failed, or <see langword="null"/> when it passed.</param>
/// <param name="MinPasses">How many of the runs must pass.</param>
/// <param name="GuardsOtherLogging">Whether a run also fails when the Agent logged some other way (spec §10, <i>A run that logs some other way fails</i>).</param>
/// <param name="Reviewer">Whether the Agent has a role that covers verify, so its Stop hook also reminds it after a Turn that only read a file (#354).</param>
internal sealed record ProbeScenario(string Id, string Prompt, Action<string> Seed, Func<ProbeRun, string?> Failure, int MinPasses, bool GuardsOtherLogging, bool Reviewer = false)
{
    private static readonly Regex ClaimsToHaveLogged = new(@"\b(logged|recorded)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Four findings, each cited, over a sources list that supports each one; F2 is contradicted by F1.</summary>
    internal const string Report = """
        # Findings

        - F1: Revenue grew 12% in 2025 [1].
        - F2: Revenue shrank in 2025 [2].
        - F3: Headcount rose from 40 to 52 [3].
        - F4: Churn stayed under 5% [4].

        ## Sources

        1. Annual report 2025, p.4: revenue up 12%.
        2. Analyst preliminary note, Feb 2026, p.1: revenue down 3% in 2025.
        3. People report 2025, p.2: headcount 40 to 52.
        4. Customer report 2025, p.7: churn 4.6%.
        """;

    /// <summary>The same report with two typos for LIVE-DH-2 to fix.</summary>
    internal const string ReportWithTypos = """
        # Findings

        - F1: Revenue grew 12% in 2025 [1].
        - F2: Revenue shrank in 2025 [2].
        - F3: Headcount rose from 40 to 52, which we did not expecct [3].
        - F4: Churn stayed under 5%, teh best result in years [4].

        ## Sources

        1. Annual report 2025, p.4: revenue up 12%.
        2. Analyst preliminary note, Feb 2026, p.1: revenue down 3% in 2025.
        3. People report 2025, p.2: headcount 40 to 52.
        4. Customer report 2025, p.7: churn 4.6%.
        """;

    /// <summary>A plan with two typos.</summary>
    internal const string PlanWithTypos = """
        # Plan

        1. Recieve the sources from the analysts.
        2. Summarise teh risks for the board.
        """;

    /// <summary>The seeded files of the common case: the report, the plan and a plain-text note.</summary>
    /// <param name="folder">The empty working folder.</param>
    internal static void SeedCommon(string folder)
    {
        File.WriteAllText(Path.Combine(folder, "report.md"), Report);
        File.WriteAllText(Path.Combine(folder, "plan.md"), PlanWithTypos);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "Remember to book the room.");
    }

    /// <summary>Reads every file under <paramref name="folder"/>, by path relative to it.</summary>
    /// <param name="folder">The working folder.</param>
    /// <returns>Each file's text.</returns>
    internal static Dictionary<string, string> Snapshot(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(folder, file).Replace('\\', '/'), File.ReadAllText, StringComparer.Ordinal);

    /// <summary>Applies the scenario's check, and the rule against logging some other way when the scenario has it.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>The reason it failed, or <see langword="null"/> when it passed.</returns>
    internal string? Judge(ProbeRun run)
    {
        if (this.GuardsOtherLogging)
        {
            string? other = OtherLogging(run);
            if (other is not null)
            {
                return other;
            }
        }

        return this.Failure(run);
    }

    /// <summary>A new file that looks like a change log, or a reply that claims logging no call backs up.</summary>
    /// <param name="run">The run to inspect.</param>
    /// <returns>The reason, or <see langword="null"/> when the Agent did not log some other way.</returns>
    private static string? OtherLogging(ProbeRun run)
    {
        string? logFile = run.After.Keys
            .Where(file => !run.Before.ContainsKey(file))
            .FirstOrDefault(static file =>
            {
                string name = Path.GetFileName(file);
                return name.StartsWith("CHANGELOG", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("HISTORY", StringComparison.OrdinalIgnoreCase)
                    || (name.Contains("log", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            });
        if (logFile is not null)
        {
            return $"logged some other way: new file '{logFile}'";
        }

        return run.Calls.Count == 0 && ClaimsToHaveLogged.IsMatch(run.Reply)
            ? "the reply says it logged or recorded the change, but no call was made"
            : null;
    }

    /// <summary>Whether a call's arguments name only the three arguments the schema has.</summary>
    /// <param name="arguments">A call's arguments.</param>
    /// <returns><see langword="true"/> when no other property was sent.</returns>
    internal static bool OnlySchemaArguments(JsonObject arguments) =>
        arguments.All(static property => property.Key is "path" or "kind" or "summary");
}
