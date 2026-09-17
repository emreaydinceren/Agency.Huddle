namespace Agency.Huddle.Acp.Tests.E2E;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

/// <summary>
/// Adapters Task 12.1 (Huddle.Adapters-ProjectPlan.md, Spec §15.9 T-31): re-points a real
/// <see cref="DotAcpAgentHost"/> — the same production type <c>RealAdapterTests</c> drives against
/// the Node <c>claude-agent-acp</c> adapter — at Agency.NET's real ACP agent,
/// <c>E:\Repos\Agency\src\Acp\Agency.Acp\bin\{Debug|Release}\net10.0\Agency.Acp.exe</c>, instead of
/// any in-proc substitute.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not the unconditional D10 conformance suite Task 12.1 originally asked for.</b>
/// D10's own tests are written against <c>MockAdapterFixture</c>, which substitutes
/// <c>IAgentProcessLauncher</c> with an in-proc duplex stream pair (see that fixture's own remarks)
/// — they cannot be re-pointed at a real executable by configuration alone, only
/// <c>ProcessModeTests</c> can. This test plays that same role for <c>agency-acp</c>: it drives the
/// real, unsubstituted <see cref="AgentProcessLauncher"/> end to end, at the <c>Huddle.Acp</c> layer
/// <c>RealAdapterTests</c> already tests at, rather than through a full Persona/Room.
/// </para>
/// <para>
/// <b>Gating, copied from <c>E2E.Enabled</c>/<c>TEAM_E2E</c> in this same folder.</b> Opt-in via a
/// distinct environment variable, <c>HUDDLE_AGENCY_ACP=1</c>, so it never runs in CI or a default
/// local run and shows up as one more <em>skipped</em> test otherwise. Running it also requires:
/// </para>
/// <list type="number">
/// <item><description>
/// A built <c>Agency.Acp.exe</c>. Default lookup is the path a sibling checkout of the Agency.NET
/// repo produces by convention (<c>E:\Repos\Agency\src\Acp\Agency.Acp\bin\Release\net10.0\Agency.Acp.exe</c>
/// on this machine); override with the <c>HUDDLE_AGENCY_ACP_EXE</c> environment variable if the
/// checkout lives elsewhere or was built Debug.
/// </description></item>
/// <item><description>
/// <c>Agency.Acp.exe</c>'s own build output ships with <b>no</b> <c>appsettings.json</c> and no
/// <c>shared-appsettings.json</c> (confirmed empirically: it is the only host project under
/// Agency.NET's <c>src/</c> whose bin output carries neither). Without <c>Agent:DefaultModel</c>
/// configured, <c>session/new</c> answers a JSON-RPC error, <c>"Agent:DefaultModel is not
/// configured."</c> (code -32603) — <see cref="RealAgencyAcpTests.Initialize_ReportsAgentInfo"/>
/// below supplies the minimum via <see cref="AgentProcessOptions.EnvironmentOverrides"/> so
/// <c>session/new</c> can be reached at all; it deliberately still points
/// <c>Agent__LLmClients__0__BaseUrl</c> at an unreachable loopback port, because the handshake this
/// test proves needs no live model.
/// </description></item>
/// <item><description>
/// A real Turn (<c>session/prompt</c>) is a different, larger ask: probing this adapter directly
/// (see this repository's spike notes) showed <c>session/prompt</c> against an unreachable model
/// endpoint does not fail fast — it is still unanswered 30 seconds in, consistent with the harness's
/// own HTTP timeout and retry/backoff (Agency.Acp-Specifications.md §11.3) rather than a quick
/// connection-refused. This test therefore proves only the handshake
/// (<c>initialize</c> + <c>session/new</c>), never a Turn, so it cannot hang for that reason. A Turn
/// against a real local model is Task 12.1's unfinished remainder — see this repo's spike findings.
/// </description></item>
/// </list>
/// </remarks>
[Collection("E2E")]
public sealed class RealAgencyAcpTests
{
    /// <summary>
    /// True only when a human has opted in with <c>HUDDLE_AGENCY_ACP=1</c>. A distinct variable from
    /// <see cref="E2E.Enabled"/> (<c>TEAM_E2E</c>), because this exercises a different real process
    /// with different, heavier prerequisites (a sibling Agency.NET checkout, its own configuration).
    /// </summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("HUDDLE_AGENCY_ACP") == "1";

    /// <summary>
    /// The built <c>Agency.Acp.exe</c> to launch: <c>HUDDLE_AGENCY_ACP_EXE</c> when set, else the
    /// path this spike's sibling checkout produced.
    /// </summary>
    private static string AgencyAcpExecutablePath =>
        Environment.GetEnvironmentVariable("HUDDLE_AGENCY_ACP_EXE")
        ?? @"E:\Repos\Agency\src\Acp\Agency.Acp\bin\Release\net10.0\Agency.Acp.exe";

    /// <summary>
    /// The minimum configuration <c>Agency.Acp.exe</c> needs for <c>session/new</c> to reach the
    /// fail-soft path in its own <c>SessionFactory.CreateAsync</c> instead of throwing
    /// <c>"Agent:DefaultModel is not configured."</c>. The model id and base URL are both
    /// deliberately fake — <c>session/new</c> never calls the model (Agency.Acp-Specifications.md
    /// §8.1 steps 2 and 7 are both fail-soft), only <c>session/prompt</c> would, and this test never
    /// sends one.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> MinimalAgentConfig = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Agent__DefaultModel"] = "huddle-spike-placeholder-model",
        ["Agent__DefaultClientName"] = "HuddleSpike",
        ["Agent__LLmClients__0__Name"] = "HuddleSpike",
        ["Agent__LLmClients__0__ClientType"] = "OpenAI",
        ["Agent__LLmClients__0__BaseUrl"] = "http://127.0.0.1:1/v1",
        ["Agent__LLmClients__0__ApiKey"] = "unused",
    };

    /// <summary>
    /// <c>initialize</c> against the real <c>agency-acp</c> process reports a non-empty agent name
    /// and protocol version 1 — the same assertion shape as
    /// <c>RealAdapterTests.RealAdapter_Initialize_ReportsAgentInfo</c>, pointed at a different peer.
    /// </summary>
    [Fact(Timeout = 60000, SkipUnless = nameof(Enabled), SkipType = typeof(RealAgencyAcpTests), Skip = "Set HUDDLE_AGENCY_ACP=1 to run")]
    public async Task Initialize_ReportsAgentInfo()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AssertExecutableExists();

        ListLoggerFactory loggerFactory = new();
        AgentProcessLauncher launcher = new(loggerFactory.CreateLogger<AgentProcessLauncher>());
        DotAcpAgentHost host = new(
            new AgentProcessOptions(AgencyAcpExecutablePath, [], null, MinimalAgentConfig),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.Equal("agency-acp", host.Info.AgentName);
            Assert.Equal(1, host.Info.ProtocolVersion);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// <c>session/new</c>, driven through the real, production <see cref="DotAcpAgentHost.StartSessionAsync"/>
    /// — the same code path <c>PersonaRunner</c> uses, carrying a real loopback-shaped MCP server
    /// entry and a <see cref="SystemPromptOptions"/> in <c>_meta.systemPrompt</c> exactly as
    /// <c>DotAcpAgentHost</c> builds it for a real Persona — completes and returns a session,
    /// against the real <c>agency-acp</c> process rather than any mock.
    /// </summary>
    /// <remarks>
    /// This test does not assert that the Persona's system prompt took effect — proving that needs a
    /// live model to prompt against, which this test deliberately does not have. What it does prove
    /// is that <c>agency-acp</c> accepts the request shape at all (no <c>-32602</c>) — this spike's
    /// direct wire probing separately showed <c>agency-acp</c>'s <c>session/new</c> handler reads only
    /// <c>_meta.model</c>, never <c>_meta.systemPrompt</c>, so this call succeeds by silently ignoring
    /// the Persona's identity rather than by honouring it. That is the divergence Task 12.1 exists to
    /// surface (Spec §12 E-18) — see this repo's spike findings for the full account.
    /// </remarks>
    [Fact(Timeout = 60000, SkipUnless = nameof(Enabled), SkipType = typeof(RealAgencyAcpTests), Skip = "Set HUDDLE_AGENCY_ACP=1 to run")]
    public async Task StartSession_WithLoopbackMcpServerAndSystemPrompt_ReturnsSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        AssertExecutableExists();

        ListLoggerFactory loggerFactory = new();
        AgentProcessLauncher launcher = new(loggerFactory.CreateLogger<AgentProcessLauncher>());
        DotAcpAgentHost host = new(
            new AgentProcessOptions(AgencyAcpExecutablePath, [], null, MinimalAgentConfig),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);

            ToolServerEndpoint toolServer = new(
                "team",
                new Uri("http://127.0.0.1:54321/mcp"),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer huddle-spike-placeholder-token" });
            SystemPromptOptions systemPrompt = new("You are Nova.", SystemPromptMode.Replace);

            session = await host.StartSessionAsync(
                new AgentSessionOptions(Path.GetTempPath(), new AutoApprovePermissionHandler(), systemPrompt, toolServer),
                cancellationToken);

            Assert.False(string.IsNullOrEmpty(session.SessionId));
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await host.DisposeAsync();
        }
    }

    private static void AssertExecutableExists()
    {
        if (!File.Exists(AgencyAcpExecutablePath))
        {
            Assert.Fail(
                $"Could not find Agency.Acp.exe at '{AgencyAcpExecutablePath}'. Build the Agency.NET "
                + "solution (Release) first, or point HUDDLE_AGENCY_ACP_EXE at your own build.");
        }
    }
}
