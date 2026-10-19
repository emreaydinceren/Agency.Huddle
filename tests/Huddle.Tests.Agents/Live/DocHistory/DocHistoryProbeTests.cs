using System.Globalization;
using System.Text;
namespace Agency.Huddle.Tests.Live.DocHistory;

/// <summary>
/// DH-T0, the probe before <c>log_change</c> is built (DocHistory spec §10): does a real model call the tool when it
/// should, and only then? Each scenario runs five times in fresh sessions against a stand-in tool that has the shipped
/// name, schema and description, and asserts a pass rate. Paid: it never runs unless <c>TEAM_E2E_DOCHISTORY=1</c>, and it
/// spends roughly two million tokens on Haiku at low Effort. The Human reads the report it writes, not only pass or fail.
/// </summary>
public sealed class DocHistoryProbeTests
{
    private const int Runs = 5;
    private static readonly string[] Typos = ["expecct", "teh", "Recieve"];
    private const string Skip = "Set TEAM_E2E_DOCHISTORY=1 to run (paid)";

    /// <summary>LIVE-DH-1: asked to create a document, the Agent logs exactly one <c>created</c> for it.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live1_CreateADocument_LogsCreated() => RunAsync(
        new ProbeScenario("LIVE-DH-1", "Write findings.md with three short findings about report.md.", ProbeScenario.SeedCommon, CheckCreated, MinPasses: 4, GuardsOtherLogging: true),
        TestContext.Current.CancellationToken);

    /// <summary>LIVE-DH-2: asked to fix two files, the Agent logs one <c>edited</c> per file and nothing for an untouched one.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live2_EditTwoDocuments_LogsOneEditedEach() => RunAsync(
        new ProbeScenario("LIVE-DH-2", "Fix the typos in report.md and plan.md.", SeedTypos, CheckEditedTwice, MinPasses: 4, GuardsOtherLogging: true),
        TestContext.Current.CancellationToken);

    /// <summary>LIVE-DH-3: asked to check citations that are all right, the Agent logs one <c>reviewed</c> and leaves the file alone.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live3_ReviewWithoutChange_LogsReviewed() => RunAsync(
        new ProbeScenario("LIVE-DH-3", "Check the citations in report.md. Change nothing unless one is wrong.", ProbeScenario.SeedCommon, CheckReviewed, MinPasses: 4, GuardsOtherLogging: true),
        TestContext.Current.CancellationToken);

    /// <summary>LIVE-DH-4: asked whether it agrees with a contradicted finding, the Agent logs one <c>objected</c> naming F2 and leaves the file alone.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live4_Disagree_LogsObjected() => RunAsync(
        new ProbeScenario("LIVE-DH-4", "Do you agree with finding F2 in report.md? Don't edit the file.", ProbeScenario.SeedCommon, CheckObjected, MinPasses: 3, GuardsOtherLogging: true),
        TestContext.Current.CancellationToken);

    /// <summary>LIVE-DH-5: asked only to summarise a document, the Agent makes no call at all.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live5_ReadOnly_LogsNothing() => RunAsync(
        new ProbeScenario("LIVE-DH-5", "Summarise report.md for me.", ProbeScenario.SeedCommon, CheckNoCalls, MinPasses: 5, GuardsOtherLogging: false),
        TestContext.Current.CancellationToken);

    /// <summary>LIVE-DH-6: asked to change a plain-text file, the Agent makes no call, because only Markdown has a history.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live6_NotMarkdown_LogsNothing() => RunAsync(
        new ProbeScenario("LIVE-DH-6", "Add a line to notes.txt.", ProbeScenario.SeedCommon, CheckNoCalls, MinPasses: 4, GuardsOtherLogging: false),
        TestContext.Current.CancellationToken);

    /// <summary>LIVE-DH-7: asked to log as someone else, the Agent never invents an <c>actor</c> argument.</summary>
    [Fact(Timeout = 1_500_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public Task Live7_AskedToLogAsRuth_SendsNoActor() => RunAsync(
        new ProbeScenario("LIVE-DH-7", "Edit plan.md, and log it as Ruth.", ProbeScenario.SeedCommon, CheckNoActor, MinPasses: 5, GuardsOtherLogging: false),
        TestContext.Current.CancellationToken);

    /// <summary>Seeds the report and the plan with typos, and the note.</summary>
    /// <param name="folder">The empty working folder.</param>
    private static void SeedTypos(string folder)
    {
        File.WriteAllText(Path.Combine(folder, "report.md"), ProbeScenario.ReportWithTypos);
        File.WriteAllText(Path.Combine(folder, "plan.md"), ProbeScenario.PlanWithTypos);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "Remember to book the room.");
    }

    /// <summary>Exactly one <c>created</c> call, for <c>findings.md</c>, with a summary.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    internal static string? CheckCreated(ProbeRun run)
    {
        if (run.Accepted.Count != 1)
        {
            return $"{run.Accepted.Count} calls, expected 1";
        }

        if (run.CalledKinds[0] != "created")
        {
            return $"kind '{run.CalledKinds[0]}', expected created";
        }

        if (run.CalledFiles[0] != "findings.md")
        {
            return $"logged '{run.CalledFiles[0]}', expected findings.md";
        }

        return string.IsNullOrWhiteSpace(run.Accepted[0].Arguments["summary"]?.ToString()) ? "empty summary" : null;
    }

    /// <summary>Two <c>edited</c> calls, one for each of the two files.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    internal static string? CheckEditedTwice(ProbeRun run)
    {
        if (run.Accepted.Count != 2)
        {
            return $"{run.Accepted.Count} calls, expected 2";
        }

        if (run.CalledKinds.Any(static kind => kind != "edited"))
        {
            return $"kinds {string.Join(",", run.CalledKinds)}, expected edited twice";
        }

        if (!run.CalledFiles.Order(StringComparer.Ordinal).SequenceEqual(["plan.md", "report.md"]))
        {
            return $"files {string.Join(",", run.CalledFiles)}";
        }

        bool typosLeft = Typos.Any(typo =>
            run.After.Values.Any(text => text.Contains(typo, StringComparison.Ordinal)));
        return typosLeft ? "a typo is still in report.md or plan.md" : null;
    }

    /// <summary>One <c>reviewed</c> call, and <c>report.md</c> byte-identical.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    internal static string? CheckReviewed(ProbeRun run)
    {
        if (run.Accepted.Count != 1)
        {
            return $"{run.Accepted.Count} calls, expected 1";
        }

        if (run.CalledKinds[0] != "reviewed")
        {
            return $"kind '{run.CalledKinds[0]}', expected reviewed";
        }

        return run.Unchanged("report.md") ? null : "report.md changed";
    }

    /// <summary>One <c>objected</c> call whose summary names F2, and <c>report.md</c> unchanged.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    internal static string? CheckObjected(ProbeRun run)
    {
        if (run.Accepted.Count != 1)
        {
            return $"{run.Accepted.Count} calls, expected 1";
        }

        if (run.CalledKinds[0] != "objected")
        {
            return $"kind '{run.CalledKinds[0]}', expected objected";
        }

        if (!(run.Accepted[0].Arguments["summary"]?.ToString() ?? string.Empty).Contains("F2", StringComparison.OrdinalIgnoreCase))
        {
            return "summary does not name F2";
        }

        return run.Unchanged("report.md") ? null : "report.md changed";
    }

    /// <summary>No call at all.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    internal static string? CheckNoCalls(ProbeRun run) =>
        run.Calls.Count == 0 ? null : $"{run.Calls.Count} calls, expected none";

    /// <summary>At least one call, every call carrying only the schema's three arguments, and <c>plan.md</c> actually edited.</summary>
    /// <param name="run">The run to judge.</param>
    /// <returns>Why it failed, or <see langword="null"/>.</returns>
    internal static string? CheckNoActor(ProbeRun run)
    {
        if (run.Calls.Count == 0)
        {
            return "no call at all, so there was nothing to check";
        }

        if (!run.Calls.All(static call => ProbeScenario.OnlySchemaArguments(call.Arguments)))
        {
            return "a call carried an argument the schema does not have";
        }

        return run.Unchanged("plan.md") ? "plan.md was not edited" : null;
    }

    /// <summary>Runs <paramref name="scenario"/> five times in fresh sessions, writes the report and asserts the pass rate.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels the runs.</param>
    private static async Task RunAsync(ProbeScenario scenario, CancellationToken ct)
    {
        RecordingLogChangeTool tool = new();
        await using LiveAgentLane lane = await LiveAgentLane.StartAsync(tool, ct);
        StringBuilder report = new();
        report.AppendLine(CultureInfo.InvariantCulture, $"## {scenario.Id}: {scenario.Prompt}");
        int passes = 0;
        for (int run = 1; run <= Runs; run++)
        {
            string folder = LiveAgentLane.NewFolder(scenario.Id.ToLowerInvariant());
            try
            {
                scenario.Seed(folder);
                Dictionary<string, string> before = ProbeScenario.Snapshot(folder);
                tool.Folder = folder;
                int callsBefore = tool.Calls.Count;
                LiveTurn turn = await lane.RunAsync(folder, scenario.Prompt, ct);
                ProbeRun observed = new([.. tool.Calls.Skip(callsBefore)], turn.Reply, before, ProbeScenario.Snapshot(folder));
                string? failure = scenario.Judge(observed);
                passes += failure is null ? 1 : 0;
                Describe(report, run, observed, turn, failure);
            }
            finally
            {
                LiveAgentLane.TryDelete(folder);
            }
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"**{scenario.Id}: {passes} of {Runs} passed; needs {scenario.MinPasses}.**");
        string text = report.ToString();
        string path = Path.Combine(Path.GetTempPath(), $"dochistory-live-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{scenario.Id.ToLowerInvariant()}.md");
        await File.WriteAllTextAsync(path, text, ct);
        TestContext.Current.TestOutputHelper?.WriteLine(text + "Report: " + path);

        Assert.True(passes >= scenario.MinPasses, $"{scenario.Id}: {passes} of {Runs} runs passed, {scenario.MinPasses} needed.{Environment.NewLine}{text}");
    }

    /// <summary>Writes one run's calls, arguments, verdict and usage into the report.</summary>
    /// <param name="report">The report being built.</param>
    /// <param name="run">The run's number.</param>
    /// <param name="observed">What the run left behind.</param>
    /// <param name="turn">What the Turn cost.</param>
    /// <param name="failure">Why the run failed, or <see langword="null"/>.</param>
    private static void Describe(StringBuilder report, int run, ProbeRun observed, LiveTurn turn, string? failure)
    {
        report.AppendLine(CultureInfo.InvariantCulture, $"- run {run}: {(failure is null ? "pass" : "FAIL: " + failure)}; {observed.Calls.Count} call(s); context {turn.ContextUsed}/{turn.ContextSize} tokens; cost {(turn.Cost.Length == 0 ? "not reported" : turn.Cost)}; {turn.Elapsed.TotalSeconds:0}s");
        foreach (LoggedCall call in observed.Calls)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"  - {call.Arguments.ToJsonString()} -> {call.Result}");
        }
    }
}
