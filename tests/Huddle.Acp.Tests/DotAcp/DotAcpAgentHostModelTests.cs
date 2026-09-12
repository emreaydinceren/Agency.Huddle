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
/// Covers reading the "model" <c>configOptions</c> entry off <c>session/new</c> and, when
/// <see cref="AgentSessionOptions.Model"/> is set, resolving it and calling
/// <c>session/set_config_option</c> - the protocol-correct route, since the vendored adapter
/// offers no <c>session/set_model</c>.
/// </summary>
public sealed class DotAcpAgentHostModelTests
{
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_MapsConfigOptionsToModels()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModelTests.CreateFlatNewSessionResult());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostModelTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            Assert.Equal(
                new[]
                {
                    new AgentModelOption("sonnet", "Sonnet", "Balanced model"),
                    new AgentModelOption("opus", "Opus", null),
                },
                session.Models);
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

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_WithNoModelConfigOption_LeavesModelsEmpty()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostModelTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            // Empty, not null: the type system already rules null out. This is the "the agent
            // never told us" case, and must never be read as "the agent has no models".
            Assert.Empty(session.Models);
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

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModelInCatalog_SetsConfigOptionWithConfigIdAndSelectType()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModelTests.CreateFlatNewSessionResult());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostModelTests.CreateSessionOptions(Path.GetTempPath(), "opus"), cancellationToken);

            JsonObject setConfigOption = await launcher.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(2));
            JsonNode parameters = setConfigOption["params"]!;

            // configId, not id - the property this adapter's docs mis-name, and the wire spelling
            // ACP actually requires.
            Assert.Equal("model", (string?)parameters["configId"]);
            Assert.Null(parameters["id"]);
            Assert.Equal("select", (string?)parameters["type"]);
            Assert.Equal("opus", (string?)parameters["value"]);
            Assert.Equal("sess-1", (string?)parameters["sessionId"]);
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

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_GroupedModelOptions_ModelInCatalog_StillResolvesAndSetsConfigOption()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModelTests.CreateGroupedNewSessionResult());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostModelTests.CreateSessionOptions(Path.GetTempPath(), "opus-3"), cancellationToken);

            Assert.Contains(session.Models, model => model.Id == "opus-3");

            JsonObject setConfigOption = await launcher.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(2));
            JsonNode parameters = setConfigOption["params"]!;
            Assert.Equal("model", (string?)parameters["configId"]);
            Assert.Equal("opus-3", (string?)parameters["value"]);
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

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModelNotInCatalog_LeavesSessionOnAgentDefaultAndDoesNotThrow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModelTests.CreateFlatNewSessionResult());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);

            // A stale stored model must not brick a session: this must complete normally.
            session = await host.StartSessionAsync(
                DotAcpAgentHostModelTests.CreateSessionOptions(Path.GetTempPath(), "gpt-nonexistent"), cancellationToken);

            Assert.Equal("sess-1", session.SessionId);
            Assert.DoesNotContain(
                launcher.Agent.Received,
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));
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

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModelNull_DoesNotCallSetConfigOption()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModelTests.CreateFlatNewSessionResult());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostModelTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            Assert.DoesNotContain(
                launcher.Agent.Received,
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));
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

    private static JsonObject CreateFlatNewSessionResult()
    {
        return new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "select",
                    ["id"] = "model",
                    ["name"] = "Model",
                    ["description"] = "AI model to use",
                    ["category"] = "model",
                    ["currentValue"] = "sonnet",
                    ["options"] = new JsonArray
                    {
                        new JsonObject { ["value"] = "sonnet", ["name"] = "Sonnet", ["description"] = "Balanced model" },
                        new JsonObject { ["value"] = "opus", ["name"] = "Opus" },
                    },
                },
            },
        };
    }

    private static JsonObject CreateGroupedNewSessionResult()
    {
        return new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "select",
                    ["id"] = "model",
                    ["name"] = "Model",
                    ["category"] = "model",
                    ["currentValue"] = "sonnet",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["group"] = "recommended",
                            ["name"] = "Recommended",
                            ["options"] = new JsonArray
                            {
                                new JsonObject { ["value"] = "sonnet", ["name"] = "Sonnet" },
                            },
                        },
                        new JsonObject
                        {
                            ["group"] = "legacy",
                            ["name"] = "Legacy",
                            ["options"] = new JsonArray
                            {
                                new JsonObject { ["value"] = "opus-3", ["name"] = "Opus 3" },
                            },
                        },
                    },
                },
            },
        };
    }

    private static AgentSessionOptions CreateSessionOptions(string cwd)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler());
    }

    private static AgentSessionOptions CreateSessionOptions(string cwd, string model)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler(), model: model);
    }
}
