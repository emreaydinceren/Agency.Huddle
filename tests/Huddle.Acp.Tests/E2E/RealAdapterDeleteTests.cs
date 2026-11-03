using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Agency.Huddle.Acp.Tests.E2E;

/// <summary>
/// Session deletion against the real <c>claude-agent-acp</c>: after the production order (one turn, close the
/// session, <see cref="IAgentSession.DeleteAsync"/>), resuming the old id is refused as not found. One short
/// Haiku turn, so it spends real tokens.
/// </summary>
[Collection("E2E")]
public sealed class RealAdapterDeleteTests
{
    // Haiku 4.5 is the cheapest catalog entry, the same one RealAdapterNoticeTests uses.
    private const string CheapModel = "claude-haiku-4-5-20251001";

    /// <summary>A session that was closed and then deleted cannot be resumed by its id: the host raises <see cref="AgentSessionNotFoundException"/>.</summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task DeletedSession_CannotBeResumed()
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

        try
        {
            await host.StartAsync(cancellationToken);
            Assert.True(host.Info.SupportsDeleteSession);

            AgentSessionOptions options = new(workingDirectory, new AutoApprovePermissionHandler(), model: CheapModel);
            IAgentSession session = await host.StartSessionAsync(options, cancellationToken);
            string sessionId = session.SessionId;
            PromptResult result = await session.PromptAsync("Reply with exactly the word PONG and nothing else.", cancellationToken);
            Assert.Equal(StopReason.EndTurn, result.StopReason);

            // Production order: close the session, then delete it.
            await session.DisposeAsync();
            await session.DeleteAsync(cancellationToken);

            AgentSessionNotFoundException thrown = await Assert.ThrowsAsync<AgentSessionNotFoundException>(
                () => host.ResumeSessionAsync(sessionId, options, cancellationToken));
            Assert.Equal(sessionId, thrown.SessionId);
        }
        finally
        {
            // The wire trace goes to the test output so a failure shows what the adapter actually sent.
            foreach (LogEntry entry in loggerFactory.Logger.Entries)
            {
                TestContext.Current.TestOutputHelper?.WriteLine($"[{entry.Level}] {entry.Message}");
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
