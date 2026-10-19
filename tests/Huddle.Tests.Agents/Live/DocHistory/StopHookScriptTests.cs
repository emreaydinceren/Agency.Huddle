using System.Diagnostics;
using System.Text.Json.Nodes;
using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.Tests.Live.DocHistory;

/// <summary>
/// Runs the real Stop hook script under <c>node</c> (the same Node the Adapter needs) against hand-written transcripts, so
/// what it blocks and what it lets through is pinned without a live model. Free.
/// </summary>
public sealed class StopHookScriptTests : IDisposable
{
    private const string Tool = "mcp__team__log_change";

    private readonly string folder = LiveAgentLane.NewFolder("hookscript");

    /// <summary>Removes the temporary folder.</summary>
    public void Dispose() => LiveAgentLane.TryDelete(this.folder);

    /// <summary>A Turn that wrote a Markdown file and never logged it is sent back with the files named.</summary>
    [Fact]
    public async Task WroteMarkdown_NotLogged_Blocks()
    {
        string reason = await this.RunAsync([Prompt(), Use("Write", "/w/findings.md")]);

        Assert.Contains("You wrote or edited /w/findings.md", reason, StringComparison.Ordinal);
        Assert.Contains($"Call {Tool} once for each file", reason, StringComparison.Ordinal);
    }

    /// <summary>A Turn that wrote a Markdown file and logged it is let through.</summary>
    [Fact]
    public async Task WroteMarkdown_Logged_LetsThrough()
    {
        Assert.Equal(string.Empty, await this.RunAsync([Prompt(), Use("Edit", "/w/a.md"), Use(Tool, null)]));
    }

    /// <summary>A Turn that only opened a Markdown file is asked whether it reviewed or objected, and told it may stop if it only read.</summary>
    [Fact]
    public async Task OnlyOpenedMarkdown_AsksForReviewOrObjection()
    {
        string reason = await this.RunAsync([Prompt(), Use("Read", "/w/report.md")]);

        Assert.Contains("You opened /w/report.md", reason, StringComparison.Ordinal);
        Assert.Contains("there is nothing to log: just finish", reason, StringComparison.Ordinal);
    }

    /// <summary>A Turn that touched no Markdown file, or only a text file, costs nothing: the hook says nothing.</summary>
    [Fact]
    public async Task NoMarkdown_LetsThrough()
    {
        Assert.Equal(string.Empty, await this.RunAsync([Prompt(), Use("Write", "/w/notes.txt"), Use("Read", "/w/data.json")]));
    }

    /// <summary>The second stop in a row is never blocked, so the hook cannot loop.</summary>
    [Fact]
    public async Task StopHookActive_LetsThrough()
    {
        Assert.Equal(string.Empty, await this.RunAsync([Prompt(), Use("Write", "/w/a.md")], stopHookActive: true));
    }

    /// <summary>A log from an earlier Turn does not count for this one.</summary>
    [Fact]
    public async Task LoggedInAnEarlierTurn_StillBlocks()
    {
        string reason = await this.RunAsync([Prompt(), Use("Write", "/w/a.md"), Use(Tool, null), Prompt(), Use("Edit", "/w/a.md")]);

        Assert.Contains("You wrote or edited /w/a.md", reason, StringComparison.Ordinal);
    }

    /// <summary>A tool result is not a new prompt, so work before it still belongs to the same Turn.</summary>
    [Fact]
    public async Task ToolResultsDoNotStartANewTurn()
    {
        string reason = await this.RunAsync([Prompt(), Use("Write", "/w/a.md"), ToolResult(), Use("Read", "/w/b.md")]);

        Assert.Contains("You wrote or edited /w/a.md", reason, StringComparison.Ordinal);
    }

    private static string Prompt() => """{"type":"user","message":{"role":"user","content":"please do it"}}""";

    private static string ToolResult() =>
        """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"t1","content":"ok"}]}}""";

    private static string Use(string name, string? file)
    {
        JsonObject input = file is null ? [] : new JsonObject { ["file_path"] = file };
        JsonObject block = new() { ["type"] = "tool_use", ["name"] = name, ["input"] = input };
        JsonObject entry = new() { ["type"] = "assistant", ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = new JsonArray(block) } };
        return entry.ToJsonString();
    }

    /// <summary>Runs the script with a transcript and returns the reason it blocked with, or an empty string when it said nothing.</summary>
    /// <param name="lines">The transcript's lines.</param>
    /// <param name="stopHookActive">Whether this is the second stop in a row.</param>
    /// <returns>The block reason, or empty.</returns>
    private async Task<string> RunAsync(string[] lines, bool stopHookActive = false)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string script = Path.Combine(this.folder, DocHistoryStopHook.ScriptName);
        string transcript = Path.Combine(this.folder, "transcript.jsonl");
        await File.WriteAllTextAsync(script, DocHistoryStopHook.Script, ct);
        await File.WriteAllLinesAsync(transcript, lines, ct);
        ProcessStartInfo start = new("node", [script, Tool]) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("node did not start.");
        JsonObject input = new() { ["transcript_path"] = transcript, ["stop_hook_active"] = stopHookActive, ["hook_event_name"] = "Stop" };
        await process.StandardInput.WriteAsync(input.ToJsonString());
        process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (output.Length == 0)
        {
            return string.Empty;
        }

        JsonObject answer = (JsonObject)JsonNode.Parse(output)!;
        Assert.Equal("block", (string?)answer["decision"]);
        return (string?)answer["reason"] ?? string.Empty;
    }
}
