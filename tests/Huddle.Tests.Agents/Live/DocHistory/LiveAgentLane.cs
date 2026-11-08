using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tools;
namespace Agency.Huddle.Tests.Live.DocHistory;

/// <summary>What one live Turn cost and said.</summary>
/// <param name="Reply">The Agent's reply text.</param>
/// <param name="ContextUsed">The context tokens in use after the Turn, from the last usage event; 0 when none arrived.</param>
/// <param name="ContextSize">The context window size from that event.</param>
/// <param name="Cost">The cost the Adapter reported, or an empty string when it reported none.</param>
/// <param name="Elapsed">Wall-clock time of the Turn.</param>
/// <param name="Trace">Every event of the Turn in order: the Agent's thoughts, each tool call with its input, and the end of the Turn.</param>
internal sealed record LiveTurn(string Reply, long ContextUsed, long ContextSize, string Cost, TimeSpan Elapsed, IReadOnlyList<string> Trace);

/// <summary>
/// One real Adapter and one <see cref="AppToolServer"/> serving a single tool as <c>mcp__team__*</c>, with a fresh
/// session for each Turn (DocHistory spec §10, <i>The lane</i>). It copies the shape of the ACP effort's
/// <c>RealAdapterAppToolTests</c> and does not edit that project. Sonnet at low Effort, per manual-tests.md §0.3.
/// </summary>
internal sealed class LiveAgentLane : IAsyncDisposable
{
    private readonly AppToolServer toolServer;
    private readonly DotAcpAgentHost host;
    private readonly RecordingLoggerProvider log;
    private readonly string model;
    private readonly string? effort;

    private LiveAgentLane(AppToolServer toolServer, DotAcpAgentHost host, RecordingLoggerProvider log, string model, string? effort)
    {
        this.toolServer = toolServer;
        this.host = host;
        this.log = log;
        this.model = model;
        this.effort = effort;
    }

    /// <summary>Starts the tool server and the Adapter and finds Sonnet and its low Effort in the Adapter's catalog.</summary>
    /// <param name="tool">The tool to serve.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The lane; the test is skipped as inconclusive when the Adapter offers no Sonnet.</returns>
    public static async Task<LiveAgentLane> StartAsync(IAppTool tool, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            Assert.Skip("Inconclusive: ANTHROPIC_API_KEY is set, so the API would be billed instead of the claude login. Unset it (manual-tests.md §0.3).");
        }

        RecordingLoggerProvider log = new(LogLevel.Warning);
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.AddProvider(log));
        AppToolServer toolServer = new("team", [tool], NullLoggerFactory.Instance);
        DotAcpAgentHost host = new(
            new AgentProcessOptions("node", [LiveDocHistory.AdapterEntry], LiveDocHistory.RepoRoot),
            new AgentProcessLauncher(NullLogger<AgentProcessLauncher>.Instance),
            new DotAcpHostOptions(TraceWire: false),
            loggers);
        try
        {
            await toolServer.StartAsync(cancellationToken);
            await host.StartAsync(cancellationToken);
            string scratch = NewFolder("lane-catalog");
            try
            {
                await using IAgentSession catalog = await host.StartSessionAsync(new AgentSessionOptions(scratch, new AutoApprovePermissionHandler()), cancellationToken);
                AgentModelOption? sonnet = catalog.Models.FirstOrDefault(static option =>
                    option.Id.Contains("sonnet", StringComparison.OrdinalIgnoreCase) || option.Name.Contains("sonnet", StringComparison.OrdinalIgnoreCase));
                if (sonnet is null)
                {
                    Assert.Skip("Inconclusive: the Adapter offers no Sonnet model, and a live test must never run on Opus (manual-tests.md §0.3).");
                }

                // The effort levels belong to the model, so they are read from a session already on Sonnet. No Turn runs.
                await using IAgentSession onSonnet = await host.StartSessionAsync(new AgentSessionOptions(scratch, new AutoApprovePermissionHandler(), model: sonnet.Id), cancellationToken);
                string? low = onSonnet.EffortLevels.FirstOrDefault(static level =>
                    level.Id.Contains("low", StringComparison.OrdinalIgnoreCase) || level.Name.Contains("low", StringComparison.OrdinalIgnoreCase))?.Id;
                AssertNoWarnings(log, "choosing Sonnet");
                return new LiveAgentLane(toolServer, host, log, sonnet.Id, low);
            }
            finally
            {
                TryDelete(scratch);
            }
        }
        catch
        {
            await host.DisposeAsync();
            await toolServer.DisposeAsync();
            throw;
        }
    }

    /// <summary>Runs one Turn in a fresh session whose working folder is <paramref name="workDirectory"/>.</summary>
    /// <param name="workDirectory">An existing absolute folder.</param>
    /// <param name="prompt">The user's message.</param>
    /// <param name="remindReads">Whether the Agent plays a reviewer, whose Stop hook also reminds it after a Turn that only read a file, as for a role that covers verify.</param>
    /// <param name="cancellationToken">Cancels the Turn.</param>
    /// <returns>The reply and the usage the Adapter reported.</returns>
    public async Task<LiveTurn> RunAsync(string workDirectory, string prompt, bool remindReads, CancellationToken cancellationToken)
    {
        string names = string.Join(", ", this.toolServer.ToolNames.Select(static name => $"mcp__team__{name}"));
        // The same paragraph the app adds to a real Persona's prompt when it offers the tool (DocHistory spec section 10).
        string guidance = this.toolServer.ToolNames.Contains("log_change", StringComparer.Ordinal)
            ? " " + PromptRenderer.Render(PromptCatalog.Get("systemPrompt.docHistory").Default, new Dictionary<string, string> { ["{{logChangeTool}}"] = "mcp__team__log_change" })
            : string.Empty;
        SystemPromptOptions system = new(
            $"The Team application exposes these tools, which run inside the application process: {names}.{guidance}",
            SystemPromptMode.Append);
        // What the app does when it starts a Persona that is offered the tool: put the Stop hook in the Work Dir first.
        if (this.toolServer.ToolNames.Contains("log_change", StringComparer.Ordinal))
        {
            DocHistoryStopHook.Install(workDirectory, Path.Combine(TestPaths.GetAdapterTempPath(), "dochistory-live-hooks"), "mcp__team__log_change", NullLogger.Instance, remindReads);
        }

        Stopwatch clock = Stopwatch.StartNew();
        await using IAgentSession session = await this.host.StartSessionAsync(
            new AgentSessionOptions(workDirectory, new AutoApprovePermissionHandler(), system, this.toolServer.Endpoint, this.model, this.effort),
            cancellationToken);

        // The host logs a warning and carries on with the Adapter's default model when Sonnet cannot be set. That
        // default may be Opus, which a live test must never spend (manual-tests.md §0.3), so stop before the Turn.
        AssertNoWarnings(this.log, "starting the session");
        _ = await session.PromptAsync(prompt, cancellationToken);

        StringBuilder reply = new();
        long used = 0;
        long size = 0;
        string cost = string.Empty;
        List<string> trace = [];
        while (session.Events.TryRead(out AgentEvent? agentEvent))
        {
            switch (agentEvent)
            {
                case ThoughtChunk thought:
                    trace.Add("thought: " + Clip(thought.Text));
                    break;
                case ToolCallStarted call:
                    trace.Add($"tool {call.Kind} '{call.Title}' {Clip(call.RawInputJson ?? string.Empty)}");
                    break;
                case TurnCompleted done:
                    trace.Add($"turn completed: {done.StopReason}");
                    break;
                case MessageChunk chunk:
                    reply.Append(chunk.Text);
                    break;
                case UsageUpdated usage:
                    used = usage.Used;
                    size = usage.Size;
                    cost = usage.Cost is { } amount ? $"{amount.Amount} {amount.Currency}" : string.Empty;
                    break;
            }
        }

        return new LiveTurn(reply.ToString(), used, size, cost, clock.Elapsed, trace);
    }

    /// <summary>Shortens a trace entry to one readable line.</summary>
    /// <param name="text">The entry's text.</param>
    /// <returns>At most 240 characters on one line.</returns>
    private static string Clip(string text)
    {
        string line = text.ReplaceLineEndings(" ");
        return line.Length > 240 ? line[..240] + "…" : line;
    }

    /// <summary>Fails when the host warned, which is how it reports falling back from the model or Effort the lane asked for.</summary>
    /// <param name="log">The host's warnings so far.</param>
    /// <param name="stage">What the lane was doing, for the message.</param>
    private static void AssertNoWarnings(RecordingLoggerProvider log, string stage) =>
        Assert.True(log.CountAtLeast(LogLevel.Warning) == 0, $"The Adapter host warned while {stage}, so the lane may be on the wrong model: {string.Join(" | ", log.Messages)}");

    /// <summary>Creates an empty folder for one run under <see cref="TestPaths.GetAdapterTempPath"/>, where the Adapter can work.</summary>
    /// <param name="label">A short label, for finding the folder.</param>
    /// <returns>The folder's full path.</returns>
    public static string NewFolder(string label)
    {
        string path = Path.Combine(TestPaths.GetAdapterTempPath(), "dochistory-live-" + label + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Deletes a run's folder, tolerating a handle the killed Adapter still holds for a moment.</summary>
    /// <param name="path">The folder to delete.</param>
    public static void TryDelete(string path)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The OS can keep the Adapter's working-directory handle open briefly after it is killed.
                Thread.Sleep(200);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.host.DisposeAsync();
        await this.toolServer.DisposeAsync();
    }
}
