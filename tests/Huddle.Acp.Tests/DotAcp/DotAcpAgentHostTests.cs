namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

public sealed class DotAcpAgentHostTests
{
    [Fact(Timeout = 10000)]
    public async Task StartAsync_SendsInitialize_WithProtocolVersionFsCapabilitiesAndClientInfo()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            JsonObject message = Assert.Single(launcher.Agent.Received);
            Assert.Equal((string?)"initialize", (string?)message["method"]);

            JsonNode parameters = message["params"]!;
            Assert.Equal((int?)1, (int?)parameters["protocolVersion"]);
            JsonNode fs = parameters["clientCapabilities"]!["fs"]!;
            Assert.Equal((bool?)false, (bool?)fs["readTextFile"]);
            Assert.Equal((bool?)false, (bool?)fs["writeTextFile"]);
            Assert.Equal((string?)"Team.Console", (string?)parameters["clientInfo"]!["name"]);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartAsync_PopulatesInfoFromResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            new FakeAgentProcessLauncher(),
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.Equal("fake-agent", host.Info.AgentName);
            Assert.Equal("0.0.1", host.Info.AgentVersion);
            Assert.Equal(1, host.Info.ProtocolVersion);
            Assert.False(host.Info.SupportsLoadSession);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartAsync_NonEmptyAuthMethods_ExposedInInfo()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
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
            ["authMethods"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "claude-login",
                    ["name"] = "Log in with Claude Code",
                },
            },
        };

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.Single(host.Info.AuthMethods);
            Assert.Equal("claude-login", host.Info.AuthMethods[0].Id);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartAsync_PassesOptionsToLauncher()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.NotNull(launcher.LastOptions);
            Assert.Equal("fake", launcher.LastOptions.Command);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartAsync_Twice_ThrowsInvalidOperation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            new FakeAgentProcessLauncher(),
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(cancellationToken));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact]
    public void Info_BeforeStart_ThrowsInvalidOperation()
    {
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            new FakeAgentProcessLauncher(),
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = host.Info;
        });
    }

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_SendsSessionNewWithCwdAndEmptyMcpServers()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(DotAcpAgentHostTests.CreateSessionOptions(cwd), cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonNode parameters = sessionNew["params"]!;
            Assert.Equal((string?)cwd, (string?)parameters["cwd"]);
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            Assert.Empty(mcpServers);
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
    public async Task StartSessionAsync_ReturnsSessionWithAgentSessionId()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            new FakeAgentProcessLauncher(),
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            Assert.Equal("sess-1", session.SessionId);
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
    public async Task StartSessionAsync_AuthRequiredError_ThrowsAgentAuthenticationRequiredException_WithMethods()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
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
            ["authMethods"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "claude-login",
                    ["name"] = "Log in with Claude Code",
                },
            },
        };
        launcher.Agent.OnNewSession = _ => throw new FakeRpcError(-32000, "auth_required");

        try
        {
            await host.StartAsync(cancellationToken);

            AgentAuthenticationRequiredException exception = await Assert.ThrowsAsync<AgentAuthenticationRequiredException>(
                () => host.StartSessionAsync(
                    DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken));

            Assert.Single(exception.AuthMethods);
            Assert.Equal("claude-login", exception.AuthMethods[0].Id);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_OtherRpcError_ThrowsAgentException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        launcher.Agent.OnNewSession = _ => throw new FakeRpcError(-32603, "boom");

        try
        {
            await host.StartAsync(cancellationToken);

            AgentException exception = await Assert.ThrowsAsync<AgentException>(
                () => host.StartSessionAsync(
                    DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken));

            Assert.Contains("boom", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_BeforeStart_ThrowsInvalidOperation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            new FakeAgentProcessLauncher(),
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken));
    }

    [Fact(Timeout = 10000)]
    public async Task TwoSessions_UpdatesRoutedBySessionId()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        int newSessionCalls = 0;
        launcher.Agent.OnNewSession = _ =>
        {
            newSessionCalls++;
            string sessionId = newSessionCalls == 1 ? "sess-1" : "sess-2";
            JsonObject result = new JsonObject
            {
                ["sessionId"] = sessionId,
            };
            return Task.FromResult(result);
        };

        try
        {
            await host.StartAsync(cancellationToken);
            IAgentSession sessionOne = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);
            IAgentSession sessionTwo = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            JsonObject update = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "session/update",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = "sess-2",
                    ["update"] = new JsonObject
                    {
                        ["sessionUpdate"] = "agent_message_chunk",
                        ["content"] = new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = "for two",
                        },
                    },
                },
            };
            await launcher.Agent.WriteMessageAsync(update);

            AgentEvent receivedEvent = await sessionTwo.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

            Assert.Equal(new MessageChunk("sess-2", "for two"), receivedEvent);
            Assert.False(sessionOne.Events.TryRead(out _));

            await sessionOne.DisposeAsync();
            await sessionTwo.DisposeAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_DisposesConnection_AndKillsProcessIfNotExited()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        await host.StartAsync(cancellationToken);
        await host.DisposeAsync();

        Assert.True(launcher.Process.Killed);
    }

    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_DoesNotKill_WhenProcessExited()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        await host.StartAsync(cancellationToken);
        launcher.Process.ExitSource.TrySetResult(0);
        await host.DisposeAsync();

        Assert.False(launcher.Process.Killed);
    }

    [Fact(Timeout = 10000)]
    public async Task ProcessExit_FaultsOpenSessions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);
            IAgentSession session = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            launcher.Process.ExitSource.TrySetResult(0);

            Exception thrown = await Assert.ThrowsAnyAsync<Exception>(
                () => session.Events.ReadAsync(cancellationToken).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken));

            Exception actual = thrown is ChannelClosedException closed && closed.InnerException is not null
                ? closed.InnerException
                : thrown;
            Assert.IsType<AgentDisconnectedException>(actual);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_NoSystemPrompt_SendsNoMeaningfulMeta_CwdAndMcpServersUnchanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(DotAcpAgentHostTests.CreateSessionOptions(cwd), cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;

            // UNKNOWN, resolved here: System.Text.Json.Nodes represents a JSON null literal the same
            // way it represents an absent key - JsonObject's indexer returns a C# null reference for
            // both. So a single Assert.Null on the indexer result covers "key omitted" and "key present
            // with JSON null" alike, whichever Newtonsoft's NullValueHandling actually produces for a
            // null Meta dictionary. This test does not need to run to know that; it is a property of
            // System.Text.Json.Nodes, not of the not-yet-written mapping code.
            Assert.Null(parameters["_meta"]);
            Assert.Equal((string?)cwd, (string?)parameters["cwd"]);
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            Assert.Empty(mcpServers);
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
    public async Task StartSessionAsync_AppendSystemPrompt_SendsMetaSystemPromptAsObjectWithAppendKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(cwd, systemPrompt), cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;
            JsonObject meta = Assert.IsType<JsonObject>(parameters["_meta"]);

            // The object/string distinction on the wire IS the feature: Append must be an OBJECT.
            JsonObject payload = Assert.IsType<JsonObject>(meta["systemPrompt"]);
            Assert.Equal((string?)"You are the COO", (string?)payload["append"]);
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
    public async Task StartSessionAsync_ReplaceSystemPrompt_SendsMetaSystemPromptAsBareString()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO", SystemPromptMode.Replace);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(cwd, systemPrompt), cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;
            JsonObject meta = Assert.IsType<JsonObject>(parameters["_meta"]);

            JsonNode? payload = meta["systemPrompt"];

            // Replace must NOT be a JsonObject - it must be a bare JSON string.
            Assert.IsNotType<JsonObject>(payload);
            JsonValue payloadValue = Assert.IsAssignableFrom<JsonValue>(payload);
            Assert.Equal((string?)"You are the COO", (string?)payloadValue);
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
    public async Task StartSessionAsync_AppendSystemPrompt_DoesNotCarryTypeOrPresetKeys()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostTests.CreateSessionOptions(cwd, systemPrompt), cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;
            JsonObject meta = Assert.IsType<JsonObject>(parameters["_meta"]);
            JsonObject payload = Assert.IsType<JsonObject>(meta["systemPrompt"]);

            // The adapter adds "type"/"preset" itself; sending them would be a lie about what we control.
            Assert.False(payload.ContainsKey("type"));
            Assert.False(payload.ContainsKey("preset"));
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

    private static AgentSessionOptions CreateSessionOptions(string cwd)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler());
    }

    private static AgentSessionOptions CreateSessionOptions(string cwd, SystemPromptOptions systemPrompt)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler(), systemPrompt);
    }
}
