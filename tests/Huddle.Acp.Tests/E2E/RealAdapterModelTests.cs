using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Agency.Huddle.Acp.Tests.E2E;

/// <summary>
/// The model that answered, against the real <c>claude-agent-acp</c>: a session on the <c>haiku</c> alias
/// publishes <see cref="UsageUpdated"/> events whose <see cref="UsageUpdated.Model"/> is the resolved model id,
/// read from <c>_meta["_claude/model"]</c>. One short Haiku turn, so it spends real tokens.
/// </summary>
[Collection("E2E")]
public sealed class RealAdapterModelTests
{
    /// <summary>After one short turn on the <c>haiku</c> alias, some <see cref="UsageUpdated"/> names a model starting with <c>claude-haiku</c>.</summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task Haiku_Usage_NamesTheResolvedModel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = Path.Combine(Path.GetTempPath(), "team-acp-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        ListLoggerFactory loggerFactory = new();
        DotAcpAgentHost host = new(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()),
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                new AgentSessionOptions(workingDirectory, new AutoApprovePermissionHandler(), model: "haiku"),
                cancellationToken);

            _ = await session.PromptAsync("Reply with exactly the word PONG and nothing else.", cancellationToken);

            List<string?> models = [];
            while (session.Events.TryRead(out AgentEvent? agentEvent))
            {
                if (agentEvent is UsageUpdated usage)
                {
                    models.Add(usage.Model);
                }
            }

            TestContext.Current.TestOutputHelper?.WriteLine("Observed models: " + string.Join(", ", models.Select(model => model ?? "<null>")));
            Assert.Contains(models, model => model is not null && model.StartsWith("claude-haiku", StringComparison.Ordinal)); // contains-ok: model id, not markup
        }
        finally
        {
            // The wire trace goes to the test output so a failure shows what the adapter actually sent.
            foreach (LogEntry entry in loggerFactory.Logger.Entries)
            {
                TestContext.Current.TestOutputHelper?.WriteLine($"[{entry.Level}] {entry.Message}");
            }

            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await host.DisposeAsync();
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (IOException)
            {
                // The killed node process can hold its working directory a moment longer; a leftover temp folder is harmless.
            }
        }
    }
}
