using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Live.MergeGate;

/// <summary>Whether the paid merge gate probe runs.</summary>
internal static class LiveMergeGate
{
    /// <summary>True when <c>TEAM_E2E_MERGEGATE=1</c>.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("TEAM_E2E_MERGEGATE") == "1";
}

/// <summary>
/// Issue #396 on the real Adapter. An Agent in the Adapter's <c>auto</c> permission mode runs a shell command in a temporary
/// git repository, with a <see cref="WorkDirPermissionHandler"/> and a fake gate behind it. With the generated hook
/// installed, the Adapter must send a permission request for <c>git merge</c> (refused or allowed by the gate) and none for
/// <c>git status</c>; without the hook, the control run shows the Adapter merging with no request at all. Paid: it never
/// runs unless <c>TEAM_E2E_MERGEGATE=1</c>, spends well under one dollar on Haiku at low Effort, and must not use a RAM disk
/// for <c>HUDDLE_TEST_TEMP</c> (the Adapter cannot resolve a working folder on <c>R:</c>).
/// </summary>
public sealed class MergeGateProbeTests
{
    private const string Skip = "Set TEAM_E2E_MERGEGATE=1 to run (paid)";

    /// <summary>LIVE-MG-1: under auto, the hook turns a refused merge into a request the gate refuses, an allowed merge into one it allows, and git status into no request; the control without the hook shows the gap.</summary>
    [Fact(Timeout = 1_200_000, SkipUnless = nameof(LiveMergeGate.Enabled), SkipType = typeof(LiveMergeGate), Skip = Skip)]
    public async Task Auto_MergeIsAskedAbout_AndStatusIsNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StringBuilder report = new();
        await using ProbeHost probe = await ProbeHost.StartAsync(ct);

        ProbeRun refused = await probe.RunAsync("refused", true, "Run exactly this command in your working folder and tell me its output: git merge --no-ff feat-refused -m merged-refused", report, ct);
        ProbeRun allowed = await probe.RunAsync("allowed", true, "Run exactly this command in your working folder and tell me its output: git merge --no-ff feat-allowed -m merged-allowed", report, ct);
        ProbeRun status = await probe.RunAsync("status", true, "Run exactly this command in your working folder and tell me its output: git status", report, ct);
        ProbeRun control = await probe.RunAsync("control-no-hook", false, "Run exactly this command in your working folder and tell me its output: git merge --no-ff feat-refused -m merged-refused", report, ct);

        File.WriteAllText(Path.Combine(Path.GetTempPath(), "merge-gate-probe-396.txt"), report.ToString());
        Assert.Contains(refused.Requests, static line => line.Contains("feat-refused", StringComparison.Ordinal) && line.Contains("RejectOnce", StringComparison.Ordinal)); // contains-ok: the request line carries a model-written description; the branch and the decision are the point.
        Assert.DoesNotContain("work-refused", refused.Log, StringComparison.Ordinal);
        Assert.Contains(allowed.Requests, static line => line.Contains("feat-allowed", StringComparison.Ordinal) && line.Contains("AllowOnce", StringComparison.Ordinal)); // contains-ok: the request line carries a model-written description; the branch and the decision are the point.
        Assert.Contains("work-allowed", allowed.Log, StringComparison.Ordinal); // contains-ok: git log lines carry a hash; the commit subject is the point.
        Assert.DoesNotContain(status.Requests, static line => line.Contains("git status", StringComparison.Ordinal));
        Assert.DoesNotContain(control.Requests, static line => line.Contains("feat-refused", StringComparison.Ordinal));
        Assert.Contains("work-refused", control.Log, StringComparison.Ordinal); // contains-ok: git log lines carry a hash; the commit subject is the point.
    }

    /// <summary>What one scenario saw.</summary>
    /// <param name="Requests">Every permission request the Adapter sent, with the decision.</param>
    /// <param name="Log">The repository's <c>git log --oneline</c> on main afterwards.</param>
    private sealed record ProbeRun(IReadOnlyList<string> Requests, string Log);

    /// <summary>Refuses a merge of <c>feat-refused</c> and nothing else.</summary>
    private sealed class FakeGate : IMergeGate
    {
        /// <inheritdoc />
        public string? RefusalFor(string personaName, string command) =>
            command.Contains("feat-refused", StringComparison.Ordinal) ? "A gated Task is open and has no Accept yet (probe)." : null;
    }

    /// <summary>Remembers each request and the gate's answer.</summary>
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

    /// <summary>One Adapter process, Haiku at low Effort, reused for every scenario.</summary>
    private sealed class ProbeHost : IAsyncDisposable
    {
        private readonly DotAcpAgentHost host;
        private readonly string model;
        private readonly string? effort;

        private ProbeHost(DotAcpAgentHost host, string model, string? effort)
        {
            this.host = host;
            this.model = model;
            this.effort = effort;
        }

        /// <summary>Starts the Adapter and finds Haiku in its catalog.</summary>
        /// <param name="ct">Cancels the start.</param>
        public static async Task<ProbeHost> StartAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            {
                Assert.Skip("Inconclusive: ANTHROPIC_API_KEY is set, so the API would be billed instead of the claude login.");
            }

            DotAcpAgentHost host = new(
                new AgentProcessOptions("node", [LiveDocHistory.AdapterEntry], LiveDocHistory.RepoRoot),
                new AgentProcessLauncher(NullLogger<AgentProcessLauncher>.Instance),
                new DotAcpHostOptions(TraceWire: false),
                NullLoggerFactory.Instance);
            await host.StartAsync(ct);
            string scratch = Folder("catalog");
            await using IAgentSession catalog = await host.StartSessionAsync(new AgentSessionOptions(scratch, new AutoApprovePermissionHandler()), ct);
            AgentModelOption? haiku = catalog.Models.FirstOrDefault(static option => option.Id.Contains("haiku", StringComparison.OrdinalIgnoreCase));
            if (haiku is null)
            {
                await host.DisposeAsync();
                Assert.Skip("Inconclusive: the Adapter offers no Haiku model, and a live test must never run on Opus.");
            }

            await using IAgentSession onHaiku = await host.StartSessionAsync(new AgentSessionOptions(scratch, new AutoApprovePermissionHandler(), model: haiku.Id), ct);
            string? low = onHaiku.EffortLevels.FirstOrDefault(static level => level.Id.Contains("low", StringComparison.OrdinalIgnoreCase))?.Id;
            return new ProbeHost(host, haiku.Id, low);
        }

        /// <summary>Runs one scenario in a fresh repository and session, in the Adapter's auto mode.</summary>
        /// <param name="label">The scenario's name.</param>
        /// <param name="withHook">Whether the merge gate hook is installed in the Work Dir.</param>
        /// <param name="prompt">The user's message.</param>
        /// <param name="report">Receives the evidence.</param>
        /// <param name="ct">Cancels the Turn.</param>
        public async Task<ProbeRun> RunAsync(string label, bool withHook, string prompt, StringBuilder report, CancellationToken ct)
        {
            string repo = Folder(label);
            Seed(repo);
            if (withHook)
            {
                MergeGateHook.Install(repo, Path.Combine(Path.GetTempPath(), "merge-gate-probe-hooks"), NullLogger.Instance);
            }

            List<string> requests = [];
            RecordingHandler handler = new(new WorkDirPermissionHandler(null, NullLogger<WorkDirPermissionHandler>.Instance, new FakeGate(), "Ivy"), requests);
            StringBuilder reply = new();
            await using (IAgentSession session = await this.host.StartSessionAsync(new AgentSessionOptions(repo, handler, null, null, this.model, this.effort, null, "auto"), ct))
            {
                _ = await session.PromptAsync(prompt, ct);
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
            string settings = Path.Combine(repo, ".claude", "settings.local.json");
            report.AppendLine("=== " + label + " (hook installed: " + withHook + ") ===").AppendLine("prompt: " + prompt);
            report.AppendLine("settings.local.json: " + (File.Exists(settings) ? File.ReadAllText(settings).ReplaceLineEndings(" ") : "(none)"));
            report.AppendLine("permission requests: " + requests.Count);
            requests.ForEach(line => report.AppendLine("  " + line));
            report.AppendLine("agent said: " + reply.ToString().ReplaceLineEndings(" ")).AppendLine("git log main: " + log.ReplaceLineEndings(" | ")).AppendLine();
            return new ProbeRun(requests, log);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => this.host.DisposeAsync();

        /// <summary>Creates an empty folder under the temp folder.</summary>
        /// <param name="label">A short label.</param>
        private static string Folder(string label)
        {
            string path = Path.Combine(Path.GetTempPath(), "mergegate-live-" + label + "-" + Guid.NewGuid().ToString("N"));
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
