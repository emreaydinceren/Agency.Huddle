namespace Agency.Huddle.Acp.Tests.DotAcp;

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
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

    /// <summary>An agent advertising <c>promptCapabilities</c> {image:true, embeddedContext:false} is reported as exactly that.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_AgentAdvertisesImageOnly_PromptCapabilitiesImageOnly()
    {
        AgentPromptCapabilities? capabilities = await DotAcpAgentHostCapabilityTests.StartAndReadPromptCapabilitiesAsync(
            new JsonObject { ["image"] = true, ["embeddedContext"] = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(new AgentPromptCapabilities(Image: true, EmbeddedContext: false), capabilities);
    }

    /// <summary>An agent that says nothing about <c>promptCapabilities</c> takes no Prompt blocks: ACP reads an omitted capability as unsupported.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_NoPromptCapabilities_None()
    {
        AgentPromptCapabilities? capabilities = await DotAcpAgentHostCapabilityTests.StartAndReadPromptCapabilitiesAsync(
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(AgentPromptCapabilities.None, capabilities);
    }

    /// <summary>An agent that advertises <c>image:false</c> is reported as not taking images, the same as an omitted field.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_ImageFalse_NeitherCapability()
    {
        AgentPromptCapabilities? capabilities = await DotAcpAgentHostCapabilityTests.StartAndReadPromptCapabilitiesAsync(
            new JsonObject { ["image"] = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(new AgentPromptCapabilities(Image: false, EmbeddedContext: false), capabilities);
    }

    /// <summary>An agent advertising only <c>embeddedContext</c> is reported as taking embedded text and no images.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_AgentAdvertisesEmbeddedContextOnly_EmbeddedContextOnly()
    {
        AgentPromptCapabilities? capabilities = await DotAcpAgentHostCapabilityTests.StartAndReadPromptCapabilitiesAsync(
            new JsonObject { ["embeddedContext"] = true },
            TestContext.Current.CancellationToken);

        Assert.Equal(new AgentPromptCapabilities(Image: false, EmbeddedContext: true), capabilities);
    }

    /// <summary>Starts a host against a fake agent whose <c>initialize</c> carries the given <c>promptCapabilities</c> (or none) and returns what the host reports.</summary>
    /// <param name="promptCapabilities">The object to advertise, or null to omit the field.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<AgentPromptCapabilities?> StartAndReadPromptCapabilitiesAsync(JsonObject? promptCapabilities, CancellationToken cancellationToken)
    {
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ =>
        {
            JsonObject capabilities = new JsonObject { ["loadSession"] = false };
            if (promptCapabilities is not null)
            {
                capabilities["promptCapabilities"] = promptCapabilities.DeepClone();
            }

            return new JsonObject
            {
                ["protocolVersion"] = 1,
                ["agentInfo"] = new JsonObject { ["name"] = "fake-agent", ["version"] = "0.0.1" },
                ["agentCapabilities"] = capabilities,
                ["authMethods"] = new JsonArray(),
            };
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            return host.Info.PromptCapabilities;
        }
        finally
        {
            await host.DisposeAsync();
        }
    }
}
