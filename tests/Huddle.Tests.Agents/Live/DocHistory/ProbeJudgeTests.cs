using System.Text.Json.Nodes;

namespace Agency.Huddle.Tests.Live.DocHistory;

/// <summary>
/// Pins the judges of the paid probe (DocHistory spec §10) with hand-built runs, so a judge that would pass a run it
/// should fail is caught for free, without a live model.
/// </summary>
public sealed class ProbeJudgeTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "probe-judge");

    /// <summary>A call the tool refused does not count as the one expected call.</summary>
    [Fact]
    public void CheckCreated_RefusedCall_Fails()
    {
        ProbeRun run = RunOf([Call("findings.md", "created", "Wrote three findings", "'findings.md' is not in the Library. Only Library documents have a history.")]);

        Assert.Equal("0 calls, expected 1", DocHistoryProbeTests.CheckCreated(run));
    }

    /// <summary>An accepted <c>created</c> call for the right file passes.</summary>
    [Fact]
    public void CheckCreated_AcceptedCall_Passes()
    {
        ProbeRun run = RunOf([Call(Path.Combine(Folder, "findings.md"), "created", "Wrote three findings", "Logged.")]);

        Assert.Null(DocHistoryProbeTests.CheckCreated(run));
    }

    /// <summary>LIVE-DH-2 fails when a typo is still in a file, even with two correct calls.</summary>
    [Fact]
    public void CheckEditedTwice_TypoLeft_Fails()
    {
        ProbeRun run = RunOf(
            [Call(Path.Combine(Folder, "plan.md"), "edited", "Fixed typos", "Logged."), Call(Path.Combine(Folder, "report.md"), "edited", "Fixed typos", "Logged.")],
            after: new Dictionary<string, string>(StringComparer.Ordinal) { ["plan.md"] = "# Plan\n\nRecieve it.", ["report.md"] = "# Findings" });

        Assert.Equal("a typo is still in report.md or plan.md", DocHistoryProbeTests.CheckEditedTwice(run));
    }

    /// <summary>LIVE-DH-7 fails with no call at all: a run that never called the tool proves nothing about an invented actor.</summary>
    [Fact]
    public void CheckNoActor_NoCalls_Fails()
    {
        Assert.NotNull(DocHistoryProbeTests.CheckNoActor(RunOf([])));
    }

    /// <summary>LIVE-DH-7 fails when a call carries an <c>actor</c> argument.</summary>
    [Fact]
    public void CheckNoActor_ActorArgument_Fails()
    {
        LoggedCall call = Call(Path.Combine(Folder, "plan.md"), "edited", "Fixed", "Logged.");
        call.Arguments["actor"] = "Ruth";

        Assert.Equal("a call carried an argument the schema does not have", DocHistoryProbeTests.CheckNoActor(RunOf([call], after: Edited())));
    }

    /// <summary>LIVE-DH-7 passes with one clean call and an edited <c>plan.md</c>.</summary>
    [Fact]
    public void CheckNoActor_CleanCallAndEdit_Passes()
    {
        Assert.Null(DocHistoryProbeTests.CheckNoActor(RunOf([Call(Path.Combine(Folder, "plan.md"), "edited", "Fixed", "Logged.")], after: Edited())));
    }

    /// <summary>The stand-in refuses a path outside the run's folder, as the real tool does, and accepts one inside, relative or not.</summary>
    [Fact]
    public async Task StandIn_RefusesWhatTheRealToolWouldRefuse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogChangeTool tool = new() { Folder = Folder };

        string relative = await tool.InvokeAsync(Args("findings.md"), ct);
        string outside = await tool.InvokeAsync(Args(Path.Combine(Path.GetTempPath(), "elsewhere", "x.md")), ct);
        string inside = await tool.InvokeAsync(Args(Path.Combine(Folder, "findings.md")), ct);

        Assert.Equal(["Logged.", "not in the Library", "Logged."], [relative, Trim(outside), inside]);
    }

    /// <summary>The stand-in refuses a call that names the wrong arguments, as the real tool does, so a model guessing <c>action</c> for <c>kind</c> is not counted.</summary>
    [Fact]
    public async Task StandIn_RefusesWrongArgumentNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogChangeTool tool = new() { Folder = Folder };

        string result = await tool.InvokeAsync(new JsonObject { ["path"] = "findings.md", ["action"] = "created", ["summary"] = "x" }, ct);

        Assert.Equal("'kind' must be created, edited, reviewed or objected.", result);
    }

    private static string Trim(string refusal) => refusal.Contains("is not in the Library", StringComparison.Ordinal) ? "not in the Library" : refusal;

    private static JsonObject Args(string path) => new() { ["path"] = path, ["kind"] = "edited", ["summary"] = "x" };

    private static LoggedCall Call(string path, string kind, string summary, string result) =>
        new(new JsonObject { ["path"] = path, ["kind"] = kind, ["summary"] = summary }, result);

    private static Dictionary<string, string> Edited() =>
        new(StringComparer.Ordinal) { ["plan.md"] = "# Plan\n\nReceive it." };

    private static ProbeRun RunOf(IReadOnlyList<LoggedCall> calls, Dictionary<string, string>? after = null) =>
        new(calls, string.Empty, new Dictionary<string, string>(StringComparer.Ordinal) { ["plan.md"] = "# Plan\n\nRecieve it.", ["report.md"] = "# Findings" }, after ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["plan.md"] = "# Plan\n\nRecieve it.", ["report.md"] = "# Findings" });
}
