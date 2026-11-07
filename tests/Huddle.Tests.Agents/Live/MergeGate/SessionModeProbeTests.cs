using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Live.MergeGate;

/// <summary>Whether the paid session mode probe runs.</summary>
internal static class LiveSessionMode
{
    /// <summary>True when <c>TEAM_E2E_SESSIONMODE=1</c>.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("TEAM_E2E_SESSIONMODE") == "1";
}

/// <summary>
/// Issue #414 on the real Adapter, with the user's own <c>permissions.defaultMode: auto</c> in place. The control sends no mode, as
/// Huddle did before the fix: the session runs in <c>auto</c>, even with the isolation <c>_meta</c>, and an App Tool call never reaches
/// Huddle's handler. Then the sessions take their mode from <see cref="WorkModePolicy"/> as <c>DotAcpPersonaHost</c> does: they run in
/// the mode Huddle chose, the App Tool call comes to the handler and is approved, ordinary work is approved without a Human, the merge
/// gate still refuses and allows, and a Work Mode the Human chose is kept. Paid: it never runs unless <c>TEAM_E2E_SESSIONMODE=1</c>,
/// runs on Sonnet at low Effort, never on Opus, and must not use a RAM disk for <c>HUDDLE_TEST_TEMP</c> (the Adapter cannot
/// resolve a working folder on <c>R:</c>).
/// </summary>
public sealed class SessionModeProbeTests
{
    private const string Skip = "Set TEAM_E2E_SESSIONMODE=1 to run (paid)";

    private const string CallTool = "Call the tool mcp__team__update_task now with task DEVT-1, status Ready and assignee Ivy, then tell me what it returned.";

    private const string RunCommand = "Run exactly this command in your working folder and tell me its output: ";

    /// <summary>LIVE-SM-1: under the user's auto, Huddle's chosen mode holds on open and on resume, an App Tool call is Huddle's to answer, ordinary work is approved, the merge gate still decides, and a chosen Work Mode is kept; the control shows the gap.</summary>
    [Fact(Timeout = 1_200_000, SkipUnless = nameof(LiveSessionMode.Enabled), SkipType = typeof(LiveSessionMode), Skip = Skip)]
    public async Task UsersAuto_SessionRunsInTheModeHuddleChose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string userSettings = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json"));
        if (!JsonNode.Parse(userSettings)?["permissions"]?["defaultMode"]?.GetValue<string>().Equals("auto", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            Assert.Skip("Inconclusive: this machine's ~/.claude/settings.json does not set defaultMode to auto, so there is nothing to override.");
        }

        StringBuilder report = new();
        WorkModePolicy policy = new(Options.Create(new TeamOptions()));
        string? huddleChoice = policy.EffectiveMode("Ivy", null, true, NullLogger.Instance);
        string? humanChoice = policy.EffectiveMode("Ivy", "acceptEdits", true, NullLogger.Instance);
        await using ProbeHost probe = await ProbeHost.StartAsync(report, ct);

        ProbeRun control = await probe.RunAsync("control-no-mode-sent", probe.Sonnet, null, CallTool, report, ct);
        ProbeRun tool = await probe.RunAsync("chosen-app-tool", probe.Sonnet, huddleChoice, CallTool, report, ct);
        ProbeRun refused = await probe.RunAsync("chosen-merge-refused", probe.Sonnet, huddleChoice, RunCommand + "git merge --no-ff feat-refused -m merged-refused", report, ct);
        ProbeRun allowed = await probe.RunAsync("chosen-merge-allowed", probe.Sonnet, huddleChoice, RunCommand + "git merge --no-ff feat-allowed -m merged-allowed", report, ct);
        ProbeRun work = await probe.RunAsync("chosen-ordinary-work", probe.Sonnet, huddleChoice, RunCommand + "git branch made-by-agent", report, ct);
        ProbeRun human = await probe.RunAsync("human-work-mode", probe.Sonnet, humanChoice, RunCommand + "git merge --no-ff feat-refused -m merged-refused", report, ct);
        string? resumedInherit = await probe.ResumeAsync(control, null, ct);
        string? resumedChosen = await probe.ResumeAsync(control, huddleChoice, ct);
        report.AppendLine("=== resume of the control's session ===").AppendLine("no mode sent: " + resumedInherit).AppendLine("Huddle's choice sent: " + resumedChosen);

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "session-mode-probe-414.txt"), report.ToString());

        Assert.Equal("auto", control.Mode);
        Assert.DoesNotContain(control.Requests, static line => line.Contains("update_task", StringComparison.Ordinal));
        Assert.Equal("default", huddleChoice);
        Assert.Equal("default", tool.Mode);
        Assert.Contains(tool.Requests, static line => line.Contains("update_task", StringComparison.Ordinal) && line.Contains("AllowOnce", StringComparison.Ordinal)); // contains-ok: the request line carries a model-written description; the tool and the decision are the point.
        Assert.True(tool.ToolRuns > 0, "The approved App Tool call must have run.");
        Assert.Equal("default", refused.Mode);
        Assert.Contains(refused.Requests, static line => line.Contains("feat-refused", StringComparison.Ordinal) && line.Contains("RejectOnce", StringComparison.Ordinal)); // contains-ok: as above.
        Assert.DoesNotContain("work-refused", refused.Log, StringComparison.Ordinal);
        Assert.Contains(allowed.Requests, static line => line.Contains("feat-allowed", StringComparison.Ordinal) && line.Contains("AllowOnce", StringComparison.Ordinal)); // contains-ok: as above.
        Assert.Contains("work-allowed", allowed.Log, StringComparison.Ordinal); // contains-ok: git log lines carry a hash; the commit subject is the point.
        Assert.Contains(work.Requests, static line => line.Contains("made-by-agent", StringComparison.Ordinal) && line.Contains("AllowOnce", StringComparison.Ordinal)); // contains-ok: as above.
        Assert.Contains("made-by-agent", work.Branches, StringComparison.Ordinal); // contains-ok: git branch prints a marker and spaces; the name is the point.
        Assert.Equal("acceptEdits", human.Mode);
        Assert.Contains(human.Requests, static line => line.Contains("feat-refused", StringComparison.Ordinal) && line.Contains("RejectOnce", StringComparison.Ordinal)); // contains-ok: as above.
        Assert.DoesNotContain("work-refused", human.Log, StringComparison.Ordinal);
        Assert.Equal("default", resumedChosen);
    }

    /// <summary>What one scenario saw.</summary>
    /// <param name="SessionId">The Adapter's session id.</param>
    /// <param name="Model">The model it ran on.</param>
    /// <param name="Mode">The mode the session reported after its options were applied.</param>
    /// <param name="Requests">Every permission request the Adapter sent, with the decision.</param>
    /// <param name="ToolRuns">How many times the App Tool's body ran.</param>
    /// <param name="Log">The repository's <c>git log --oneline</c> on main afterwards.</param>
    /// <param name="Branches">The repository's <c>git branch</c> afterwards.</param>
    private sealed record ProbeRun(string SessionId, string Model, string? Mode, IReadOnlyList<string> Requests, int ToolRuns, string Log, string Branches);

    /// <summary>Stands for Huddle's <c>update_task</c>: same name, same outward shape, and a count of its runs.</summary>
    private sealed class FakeUpdateTaskTool : IAppTool
    {
        private int runs;

        /// <summary>How many times the tool has run.</summary>
        public int Runs => Volatile.Read(ref this.runs);

        /// <inheritdoc />
        public string Name => "update_task";

        /// <inheritdoc />
        public string Description => "Updates a Task on the Team's board: its status and assignee.";

        /// <inheritdoc />
        public JsonObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["task"] = new JsonObject { ["type"] = "string" },
                ["status"] = new JsonObject { ["type"] = "string" },
                ["assignee"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray("task"),
        };

        /// <inheritdoc />
        public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.runs);
            return Task.FromResult("Task updated.");
        }
    }

    /// <summary>Refuses a merge of <c>feat-refused</c> and nothing else.</summary>
    private sealed class FakeGate : IMergeGate
    {
        /// <inheritdoc />
        public string? RefusalFor(string personaName, string command) =>
            command.Contains("feat-refused", StringComparison.Ordinal) ? "A gated Task is open and has no Accept yet (probe)." : null;
    }

    /// <summary>Remembers each request and the handler's answer.</summary>
    /// <param name="inner">The handler under test.</param>
    /// <param name="requests">Where the lines go.</param>
    private sealed class RecordingHandler(IPermissionHandler inner, List<string> requests) : IPermissionHandler
    {
        /// <inheritdoc />
        public async Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
        {
            PermissionDecision decision = await inner.DecideAsync(context, cancellationToken);
            string kind = decision is SelectedDecision selected
                ? context.Options.FirstOrDefault(option => option.OptionId == selected.OptionId)?.Kind.ToString() ?? "?"
                : decision.ToString() ?? "?";
            requests.Add($"request '{context.ToolCall.Title}' {(context.ToolCall.RawInputJson ?? string.Empty).ReplaceLineEndings(" ")} => {kind}");
            return decision;
        }
    }

    /// <summary>One Adapter process and one tool server, reused for every scenario.</summary>
    private sealed class ProbeHost : IAsyncDisposable
    {
        private static readonly AdapterProfile Isolating = new("claude", "Claude", null, "node", null, null, true, IsolateUserSettings: true);

        private readonly DotAcpAgentHost host;
        private readonly AppToolServer toolServer;
        private readonly FakeUpdateTaskTool tool;
        private readonly Dictionary<string, string?> lowEffort;

        private ProbeHost(DotAcpAgentHost host, AppToolServer toolServer, FakeUpdateTaskTool tool, string sonnet, Dictionary<string, string?> lowEffort)
        {
            this.host = host;
            this.toolServer = toolServer;
            this.tool = tool;
            this.Sonnet = sonnet;
            this.lowEffort = lowEffort;
        }

        /// <summary>The Sonnet model id, a model the Adapter's <c>auto</c> runs on.</summary>
        public string Sonnet { get; }

        /// <summary>Starts the tool server and the Adapter, and finds Sonnet.</summary>
        /// <param name="report">Receives what a session given no mode starts in.</param>
        /// <param name="ct">Cancels the start.</param>
        public static async Task<ProbeHost> StartAsync(StringBuilder report, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            {
                Assert.Skip("Inconclusive: ANTHROPIC_API_KEY is set, so the API would be billed instead of the claude login.");
            }

            FakeUpdateTaskTool tool = new();
            AppToolServer toolServer = new("team", [tool], NullLoggerFactory.Instance);
            await toolServer.StartAsync(ct);
            DotAcpAgentHost host = NewHost();
            await host.StartAsync(ct);
            string scratch = Folder("catalog");
            await using IAgentSession catalog = await host.StartSessionAsync(new AgentSessionOptions(scratch, new AutoApprovePermissionHandler(), meta: DotAcpAgentHostFactory.BuildIsolationMeta(Isolating, connectors: false)), ct);
            report.AppendLine("no mode sent, Adapter default model: " + catalog.CurrentModeId);
            string? sonnet = catalog.Models.FirstOrDefault(static option => option.Id.Contains("sonnet", StringComparison.OrdinalIgnoreCase))?.Id;
            if (sonnet is null)
            {
                await host.DisposeAsync();
                await toolServer.DisposeAsync();
                Assert.Skip("Inconclusive: the Adapter does not offer Sonnet, and a live test must never run on Opus.");
            }

            Dictionary<string, string?> lowEffort = new(StringComparer.Ordinal);
            foreach (string model in new[] { sonnet })
            {
                await using IAgentSession onModel = await host.StartSessionAsync(new AgentSessionOptions(scratch, new AutoApprovePermissionHandler(), model: model, meta: DotAcpAgentHostFactory.BuildIsolationMeta(Isolating, connectors: false)), ct);
                lowEffort[model] = onModel.EffortLevels.FirstOrDefault(static level => level.Id.Contains("low", StringComparison.OrdinalIgnoreCase))?.Id;
                report.AppendLine("no mode sent, " + model + ": " + onModel.CurrentModeId);
            }

            report.AppendLine();
            return new ProbeHost(host, toolServer, tool, sonnet, lowEffort);
        }

        /// <summary>Runs one scenario in a fresh repository and session, with the merge gate hook installed as Huddle installs it.</summary>
        /// <param name="label">The scenario's name.</param>
        /// <param name="model">The model id.</param>
        /// <param name="mode">The mode to send, or <see langword="null"/> to send none.</param>
        /// <param name="prompt">The user's message.</param>
        /// <param name="report">Receives the evidence.</param>
        /// <param name="ct">Cancels the Turn.</param>
        public async Task<ProbeRun> RunAsync(string label, string model, string? mode, string prompt, StringBuilder report, CancellationToken ct)
        {
            string repo = Folder(label);
            Seed(repo);
            MergeGateHook.Install(repo, Path.Combine(Path.GetTempPath(), "session-mode-probe-hooks"), NullLogger.Instance);

            int toolRunsBefore = this.tool.Runs;
            List<string> requests = [];
            StringBuilder reply = new();
            string sessionId;
            string? current;
            string? stop;
            await using (IAgentSession session = await this.host.StartSessionAsync(this.Options(repo, model, mode, requests), ct))
            {
                sessionId = session.SessionId;
                current = session.CurrentModeId;
                stop = (await session.PromptAsync(prompt, ct)).ToString();
                while (session.Events.TryRead(out AgentEvent? agentEvent))
                {
                    if (agentEvent is MessageChunk chunk)
                    {
                        reply.Append(chunk.Text);
                    }
                    else if (agentEvent is ToolCallStarted call)
                    {
                        reply.Append("\n[tool ").Append(call.Title).Append(' ').Append(call.RawInputJson).Append("]\n");
                    }
                }
            }

            string log = Git(repo, "log --oneline main");
            string branches = Git(repo, "branch");
            int toolRuns = this.tool.Runs - toolRunsBefore;
            report.AppendLine("=== " + label + " ===").AppendLine("model: " + model + ", mode sent: " + (mode ?? "(none)") + ", session reports: " + current + ", stop: " + stop).AppendLine("prompt: " + prompt);
            report.AppendLine("permission requests: " + requests.Count);
            requests.ForEach(line => report.AppendLine("  " + line));
            report.AppendLine("update_task ran: " + toolRuns).AppendLine("agent said: " + reply.ToString().ReplaceLineEndings(" ")).AppendLine("git log main: " + log.ReplaceLineEndings(" | ")).AppendLine();
            return new ProbeRun(sessionId, model, current, requests, toolRuns, log, branches);
        }

        /// <summary>Resumes a scenario's session on a second Adapter process, as an App restart does, sends nothing, and reports its mode.</summary>
        /// <param name="run">The scenario.</param>
        /// <param name="mode">The mode to send, or <see langword="null"/> to send none.</param>
        /// <param name="ct">Cancels the resume.</param>
        public async Task<string?> ResumeAsync(ProbeRun run, string? mode, CancellationToken ct)
        {
            await using DotAcpAgentHost second = NewHost();
            await second.StartAsync(ct);
            string repo = Folder("resume");
            await using IAgentSession session = await second.ResumeSessionAsync(run.SessionId, this.Options(repo, run.Model, mode, []), ct);
            return session.CurrentModeId;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await this.host.DisposeAsync();
            await this.toolServer.DisposeAsync();
        }

        /// <summary>Builds one session's options the way <c>DotAcpPersonaHost</c> does: isolation, the real handler, the App Tool.</summary>
        private AgentSessionOptions Options(string repo, string model, string? mode, List<string> requests)
        {
            RecordingHandler handler = new(new WorkDirPermissionHandler(NullLogger<WorkDirPermissionHandler>.Instance, [new MergeGateGuard(new FakeGate())], "Ivy"), requests);
            SystemPromptOptions prompt = new("The Team application exposes this tool, which runs inside the application: mcp__team__update_task. Use it when asked.", SystemPromptMode.Append);
            return new AgentSessionOptions(repo, handler, prompt, this.toolServer.Endpoint, model, this.lowEffort[model], DotAcpAgentHostFactory.BuildIsolationMeta(Isolating, connectors: false), mode);
        }

        /// <summary>A new, unstarted host on the real Adapter.</summary>
        private static DotAcpAgentHost NewHost() => new(
            new AgentProcessOptions("node", [LiveDocHistory.AdapterEntry], LiveDocHistory.RepoRoot),
            new AgentProcessLauncher(NullLogger<AgentProcessLauncher>.Instance),
            new DotAcpHostOptions(TraceWire: false),
            NullLoggerFactory.Instance);

        /// <summary>Creates an empty folder under the temp folder.</summary>
        /// <param name="label">A short label.</param>
        private static string Folder(string label)
        {
            string path = Path.Combine(Path.GetTempPath(), "sessionmode-live-" + label + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Makes a repository on main with two branches, each with one commit main lacks.</summary>
        /// <param name="repo">The empty folder.</param>
        private static void Seed(string repo)
        {
            Git(repo, "init -q -b main");
            Git(repo, "config user.email probe@example.com");
            Git(repo, "config user.name Probe");
            File.WriteAllText(Path.Combine(repo, "base.txt"), "base");
            Git(repo, "add base.txt");
            Git(repo, "commit -q -m base");
            foreach (string name in new[] { "refused", "allowed" })
            {
                Git(repo, $"checkout -q -b feat-{name}");
                File.WriteAllText(Path.Combine(repo, name + ".txt"), name);
                Git(repo, $"add {name}.txt");
                Git(repo, $"commit -q -m work-{name}");
                Git(repo, "checkout -q main");
            }
        }

        /// <summary>Runs git in a folder and returns its standard output.</summary>
        /// <param name="repo">The working folder.</param>
        /// <param name="arguments">The arguments.</param>
        private static string Git(string repo, string arguments)
        {
            ProcessStartInfo info = new("git", arguments) { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true };
            using Process process = Process.Start(info) ?? throw new InvalidOperationException("git did not start.");
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
    }
}
