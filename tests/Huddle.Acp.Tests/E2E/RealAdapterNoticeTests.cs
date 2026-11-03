using System.Text;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Agency.Huddle.Acp.Tests.E2E;

/// <summary>
/// Session notices against the real <c>claude-agent-acp</c>: a session on Haiku asked for Auto mode, which
/// Haiku lacks, makes the adapter fall back to Accept edits and say so. With
/// <see cref="DotAcpHostOptions.AdvertiseSessionNotices"/> on, that advisory arrives as an
/// <see cref="AgentNotice"/> and stays out of the reply; with it off, it is written into the reply text,
/// which is the known limit this feature retires. Two short Haiku turns, so it spends real tokens.
/// </summary>
[Collection("E2E")]
public sealed class RealAdapterNoticeTests
{
    private const string FallbackTitle = "Auto mode unavailable";

    // The `haiku` alias resolves to Haiku 5.5 as of claude-agent-sdk 0.3.316, which supports Auto mode,
    // so it no longer triggers the fallback. Haiku 4.5 is the one catalog entry the SDK reports
    // without supportsAutoMode (checked with supportedModels(), 2026-11-03).
    private const string HaikuWithoutAutoMode = "claude-haiku-4-5-20251001";

    /// <summary>With notices advertised, the Auto-mode fallback reaches the session as one Warning <see cref="AgentNotice"/>, and the reply text does not carry it.</summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task AutoOnHaiku_WithNotices_ArrivesAsANotice_NotInTheReply()
    {
        Outcome outcome = await RealAdapterNoticeTests.RunAutoOnHaikuAsync(advertiseNotices: true, TestContext.Current.CancellationToken);

        AgentNotice notice = Assert.Single(outcome.Events.OfType<AgentNotice>());
        Assert.Equal(FallbackTitle, notice.Title);
        Assert.Equal(NoticeSeverity.Warning, notice.Severity);
        Assert.DoesNotContain(FallbackTitle, outcome.Reply, StringComparison.Ordinal); // contains-ok: model reply text, not markup
        Assert.Equal(StopReason.EndTurn, outcome.StopReason);
    }

    /// <summary>The control: with notices not advertised, the same fallback is written into the reply text and no <see cref="AgentNotice"/> arrives.</summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task AutoOnHaiku_WithoutNotices_IsWrittenIntoTheReply()
    {
        Outcome outcome = await RealAdapterNoticeTests.RunAutoOnHaikuAsync(advertiseNotices: false, TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Events.OfType<AgentNotice>());
        Assert.Contains("**" + FallbackTitle + ":**", outcome.Reply, StringComparison.Ordinal); // contains-ok: model reply text, not markup
    }

    /// <summary>Starts the real adapter, opens a Haiku session asking for Auto mode, sends one short prompt and returns every event, the reply text and the stop reason.</summary>
    /// <param name="advertiseNotices">Whether the host advertises <c>session.notices</c>.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<Outcome> RunAutoOnHaikuAsync(bool advertiseNotices, CancellationToken cancellationToken)
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "team-acp-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        ListLoggerFactory loggerFactory = new();
        DotAcpAgentHost host = new(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()),
            new DotAcpHostOptions(TraceWire: true, AdvertiseSessionNotices: advertiseNotices),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                new AgentSessionOptions(workingDirectory, new AutoApprovePermissionHandler(), model: HaikuWithoutAutoMode, mode: "auto"),
                cancellationToken);

            PromptResult result = await session.PromptAsync("Reply with exactly the word PONG and nothing else.", cancellationToken);

            List<AgentEvent> events = [];
            while (session.Events.TryRead(out AgentEvent? agentEvent))
            {
                events.Add(agentEvent);
            }

            StringBuilder reply = new();
            foreach (MessageChunk chunk in events.OfType<MessageChunk>())
            {
                reply.Append(chunk.Text);
            }

            return new Outcome(events, reply.ToString(), result.StopReason);
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

    /// <summary>What one run observed.</summary>
    /// <param name="Events">Every event the session published.</param>
    /// <param name="Reply">The reply text, joined from its message chunks.</param>
    /// <param name="StopReason">How the Turn ended.</param>
    private sealed record Outcome(IReadOnlyList<AgentEvent> Events, string Reply, StopReason StopReason);
}
