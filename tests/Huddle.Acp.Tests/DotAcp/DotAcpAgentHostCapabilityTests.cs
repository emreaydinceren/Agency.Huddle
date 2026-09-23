namespace Agency.Huddle.Acp.Tests.DotAcp;

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

/// <summary>
/// Covers <c>AgentHostInfo.SupportsResumeSession</c> (RS §6.4 A-3): read off
/// <c>agentCapabilities.sessionCapabilities.resume</c> on the <c>initialize</c> response.
/// </summary>
public sealed class DotAcpAgentHostCapabilityTests
{
    /// <summary>An agent advertising <c>sessionCapabilities.resume</c> makes the host report <c>SupportsResumeSession</c> true.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_AgentAdvertisesResume_SupportsResumeSessionTrue()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentInfo"] = new JsonObject
            {
                ["name"] = "fake-agent",
                ["version"] = "0.0.1",
            },
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = false,
                ["sessionCapabilities"] = new JsonObject
                {
                    ["resume"] = new JsonObject(),
                },
            },
            ["authMethods"] = new JsonArray(),
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.True(host.Info.SupportsResumeSession);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>An agent with no <c>sessionCapabilities</c> at all leaves <c>SupportsResumeSession</c> false, the safe default. Scripted explicitly: the fake's own default now advertises resume (RS §6.4 A-5), so this pins the absent case rather than relying on that default.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_NoSessionCapabilities_SupportsResumeSessionFalse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentInfo"] = new JsonObject
            {
                ["name"] = "fake-agent",
                ["version"] = "0.0.1",
            },
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = false,
            },
            ["authMethods"] = new JsonArray(),
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.False(host.Info.SupportsResumeSession);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }
}
