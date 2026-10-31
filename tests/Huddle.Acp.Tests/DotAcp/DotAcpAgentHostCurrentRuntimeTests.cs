namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

/// <summary>
/// Covers reading the agent's reported <c>currentValue</c> for the model and effort selects back
/// onto <see cref="IAgentSession.CurrentModelId"/> and <see cref="IAgentSession.CurrentEffortId"/>.
/// </summary>
public sealed class DotAcpAgentHostCurrentRuntimeTests
{
    /// <summary>With nothing requested, the session reports the model and effort the agent started on.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_NothingRequested_ReportsTheAdvertisedCurrentValues()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostCurrentRuntimeTests.NewSession("sonnet", "high"));

        await using DotAcpAgentHost host = DotAcpAgentHostCurrentRuntimeTests.CreateHost(launcher);
        await host.StartAsync(cancellationToken);
        await using IAgentSession session = await host.StartSessionAsync(DotAcpAgentHostCurrentRuntimeTests.Options(null, null), cancellationToken);

        Assert.Equal("sonnet", session.CurrentModelId);
        Assert.Equal("high", session.CurrentEffortId);
    }

    /// <summary>After a model switch the reported model and effort are the post-switch values the adapter answered with.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModelSwitched_ReportsThePostSwitchValues()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostCurrentRuntimeTests.NewSession("sonnet", "high"));
        launcher.Agent.OnSetConfigOption = parameters =>
        {
            if (string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal))
            {
                return Task.FromResult(new JsonObject { ["configOptions"] = DotAcpAgentHostCurrentRuntimeTests.ConfigOptions("opus", "medium") });
            }

            return Task.FromResult(new JsonObject());
        };

        await using DotAcpAgentHost host = DotAcpAgentHostCurrentRuntimeTests.CreateHost(launcher);
        await host.StartAsync(cancellationToken);
        await using IAgentSession session = await host.StartSessionAsync(DotAcpAgentHostCurrentRuntimeTests.Options("opus", null), cancellationToken);

        Assert.Equal("opus", session.CurrentModelId);
        Assert.Equal("medium", session.CurrentEffortId);
    }

    /// <summary>An effort the adapter clamped is read back as what it settled on, not what was requested.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_EffortApplied_ReportsTheReadBackValue()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostCurrentRuntimeTests.NewSession("sonnet", "default"));
        launcher.Agent.OnSetConfigOption = _ =>
            Task.FromResult(new JsonObject { ["configOptions"] = DotAcpAgentHostCurrentRuntimeTests.ConfigOptions("sonnet", "low") });

        await using DotAcpAgentHost host = DotAcpAgentHostCurrentRuntimeTests.CreateHost(launcher);
        await host.StartAsync(cancellationToken);
        await using IAgentSession session = await host.StartSessionAsync(DotAcpAgentHostCurrentRuntimeTests.Options(null, "high"), cancellationToken);

        Assert.Equal("low", session.CurrentEffortId);
    }

    /// <summary>The "default" effort sentinel is reported as the wire carried it; the protocol layer does not filter it.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_EffortDefaultSentinel_IsReportedUnfiltered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostCurrentRuntimeTests.NewSession("sonnet", "default"));

        await using DotAcpAgentHost host = DotAcpAgentHostCurrentRuntimeTests.CreateHost(launcher);
        await host.StartAsync(cancellationToken);
        await using IAgentSession session = await host.StartSessionAsync(DotAcpAgentHostCurrentRuntimeTests.Options(null, null), cancellationToken);

        Assert.Equal("default", session.CurrentEffortId);
    }

    /// <summary>An agent that advertises neither select leaves both current ids null.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_NoOptionsAdvertised_CurrentIdsAreNull()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        launcher.Agent.OnNewSession = _ => Task.FromResult(new JsonObject { ["sessionId"] = "sess-1" });

        await using DotAcpAgentHost host = DotAcpAgentHostCurrentRuntimeTests.CreateHost(launcher);
        await host.StartAsync(cancellationToken);
        await using IAgentSession session = await host.StartSessionAsync(DotAcpAgentHostCurrentRuntimeTests.Options(null, null), cancellationToken);

        Assert.Null(session.CurrentModelId);
        Assert.Null(session.CurrentEffortId);
    }

    /// <summary>Builds a host over the fake launcher.</summary>
    private static DotAcpAgentHost CreateHost(FakeAgentProcessLauncher launcher)
    {
        return new DotAcpAgentHost(new AgentProcessOptions("fake", []), launcher, new DotAcpHostOptions(), new ListLoggerFactory());
    }

    /// <summary>Builds session options requesting the given model and effort.</summary>
    private static AgentSessionOptions Options(string? model, string? effort)
    {
        return new AgentSessionOptions(Path.GetTempPath(), new AutoApprovePermissionHandler(), model: model, effort: effort);
    }

    /// <summary>Builds a <c>session/new</c> result whose selects report the given current values.</summary>
    private static JsonObject NewSession(string currentModel, string currentEffort)
    {
        return new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = DotAcpAgentHostCurrentRuntimeTests.ConfigOptions(currentModel, currentEffort),
        };
    }

    /// <summary>Builds model and thought_level selects reporting the given current values, fresh on every call.</summary>
    private static JsonArray ConfigOptions(string currentModel, string currentEffort)
    {
        return new JsonArray
        {
            new JsonObject
            {
                ["type"] = "select",
                ["id"] = "model",
                ["name"] = "Model",
                ["category"] = "model",
                ["currentValue"] = currentModel,
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "sonnet", ["name"] = "Sonnet" },
                    new JsonObject { ["value"] = "opus", ["name"] = "Opus" },
                },
            },
            new JsonObject
            {
                ["type"] = "select",
                ["id"] = "thinking",
                ["name"] = "Effort",
                ["category"] = "thought_level",
                ["currentValue"] = currentEffort,
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "default", ["name"] = "Default" },
                    new JsonObject { ["value"] = "low", ["name"] = "Low" },
                    new JsonObject { ["value"] = "medium", ["name"] = "Medium" },
                    new JsonObject { ["value"] = "high", ["name"] = "High" },
                },
            },
        };
    }
}
