using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Live.DocHistory;

namespace Agency.Huddle.Tests.Live.MergeGate;

/// <summary>
/// Runs the real merge gate hook script under <c>node</c> (the same Node the Adapter needs) with hand-written hook input, so
/// what it asks about and what it leaves alone is pinned without a live model (issue #396). Free.
/// </summary>
public sealed class MergeGateHookScriptTests : IDisposable
{
    private readonly string folder = LiveAgentLane.NewFolder("mergehook");

    /// <summary>Removes the temporary folder.</summary>
    public void Dispose() => LiveAgentLane.TryDelete(this.folder);

    /// <summary>A merge or a push, in any of the spellings the permission handler recognises, is answered with <c>ask</c> and a reason.</summary>
    [Theory]
    [InlineData("git merge feat/quote-calculator")]
    [InlineData("git checkout main && git merge --no-ff feat/quote-calculator")]
    [InlineData("git -C repo push origin main")]
    [InlineData("git -c user.name=x merge topic")]
    [InlineData("\"C:\\Program Files\\Git\\bin\\git.exe\" merge topic")]
    [InlineData("cd work; git push")]
    [InlineData("GIT_TRACE=1 git merge topic")]
    [InlineData("sh -c \"git merge topic\"")]
    [InlineData("git MERGE topic")]
    [InlineData("true || git push origin main")]
    public async Task MergeOrPush_AnswersAsk(string command)
    {
        JsonObject? answer = await this.RunAsync(Input("Bash", command));

        JsonNode? output = answer?["hookSpecificOutput"];
        Assert.Equal("PreToolUse", (string?)output?["hookEventName"]);
        Assert.Equal("ask", (string?)output?["permissionDecision"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)output?["permissionDecisionReason"]));
    }

    /// <summary>Every other command gets no output at all, so nothing about it changes and no request is added.</summary>
    [Theory]
    [InlineData("git status")]
    [InlineData("git log --oneline -5")]
    [InlineData("git commit -m \"wip\"")]
    [InlineData("git checkout main")]
    [InlineData("ls -la")]
    [InlineData("echo merge and push")]
    [InlineData("npm run push")]
    [InlineData("dotnet build Huddle.slnx")]
    public async Task OtherCommand_SaysNothing(string command)
    {
        Assert.Null(await this.RunAsync(Input("Bash", command)));
    }

    /// <summary>Input with no command, input that is not JSON and empty input all say nothing and never fail: a broken check must not change any command.</summary>
    [Theory]
    [InlineData("""{"tool_name":"Read","tool_input":{"file_path":"/w/a.md"}}""")]
    [InlineData("""{"tool_name":"Bash"}""")]
    [InlineData("""{"tool_name":"Bash","tool_input":{"command":42}}""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task UnreadableInput_SaysNothing(string raw)
    {
        Assert.Null(await this.RunAsync(raw));
    }

    /// <summary>
    /// The script must not under-ask: every command line the permission handler would send to the gate is one the script
    /// asks about. A refuse-all gate makes the handler's own detection visible, and the script is compared with it.
    /// </summary>
    [Theory]
    [InlineData("git merge topic")]
    [InlineData("git checkout main && git merge --no-ff feat/quote-calculator")]
    [InlineData("git -C repo push origin main")]
    [InlineData("git --git-dir=x -c a=b merge topic")]
    [InlineData("\"git\" push")]
    [InlineData("git.exe merge a")]
    [InlineData("/usr/bin/git push origin")]
    [InlineData("echo hi\ngit merge a")]
    [InlineData("(git push)")]
    [InlineData("GIT_EDITOR=true git merge feat")]
    [InlineData("bash -c \"git merge feat\"")]
    [InlineData("sh -c 'git push origin HEAD'")]
    [InlineData("pwsh -Command \"git merge feat\"")]
    [InlineData("git -C \"a(b)\" merge feat")]
    [InlineData("git merge -")]
    [InlineData("git pull origin feat")]
    [InlineData("git push origin 2>&1")]
    [InlineData("git pull origin main")]
    [InlineData("git pull")]
    [InlineData("git status")]
    [InlineData("git commit -m merge")]
    [InlineData("ls")]
    public async Task EveryCommandTheHandlerGates_IsOneTheScriptAsksAbout(string command)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WorkDirPermissionHandler handler = new(null, NullLogger<WorkDirPermissionHandler>.Instance, new RefuseAllGate(), "Ivy");
        ToolCallInfo call = new("c1", "Terminal", ToolKind.Execute, ToolCallStatus.Pending, new JsonObject { ["command"] = command }.ToJsonString());
        PermissionRequestContext context = new("s1", call, [new PermissionOptionInfo("allow", "Allow", PermissionOptionKind.AllowOnce), new PermissionOptionInfo("reject", "Reject", PermissionOptionKind.RejectOnce)]);

        PermissionDecision decision = await handler.DecideAsync(context, ct);
        JsonObject? answer = await this.RunAsync(Input("Bash", command));

        bool handlerGates = decision is SelectedDecision { OptionId: "reject" };
        Assert.True(answer is not null || !handlerGates, $"The handler gates '{command}' but the script says nothing.");
    }

    private static string Input(string tool, string command) =>
        new JsonObject
        {
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = tool,
            ["tool_input"] = new JsonObject { ["command"] = command },
        }.ToJsonString();

    /// <summary>Runs the script with the given standard input.</summary>
    /// <param name="raw">The hook input.</param>
    /// <returns>The JSON the script printed, or <see langword="null"/> when it printed nothing. Fails when node exits non-zero.</returns>
    private async Task<JsonObject?> RunAsync(string raw)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string script = Path.Combine(this.folder, MergeGateHook.ScriptName);
        await File.WriteAllTextAsync(script, MergeGateHook.Script, ct);
        ProcessStartInfo start = new("node", [script]) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("node did not start.");
        await process.StandardInput.WriteAsync(raw);
        process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.Equal(0, process.ExitCode);
        return output.Length == 0 ? null : (JsonObject?)JsonNode.Parse(output);
    }

    /// <summary>A gate that refuses every command it is asked about.</summary>
    private sealed class RefuseAllGate : IMergeGate
    {
        /// <inheritdoc />
        public string? RefusalFor(string personaName, string command) => "refused";
    }
}
