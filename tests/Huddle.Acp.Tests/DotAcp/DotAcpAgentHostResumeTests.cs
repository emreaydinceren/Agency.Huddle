namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Linq;
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
/// Covers <c>DotAcpAgentHost.ResumeSessionAsync</c> (RS §6.4 A-1): built like <c>StartSessionAsync</c>
/// - register the sink, then apply Model and Effort - but calling <c>session/resume</c> instead of
/// <c>session/new</c>, and keeping the id the caller asked for (<c>ResumeSessionResponse</c> carries
/// none of its own).
/// </summary>
public sealed class DotAcpAgentHostResumeTests
{
    /// <summary>A resumed session's <c>session/update</c> reaches its Events, and Model then Effort are applied via <c>session/set_config_option</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task ResumeSession_Known_RegistersSinkAndAppliesModelAndEffort()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnResumeSession = _ => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostResumeTests.CreateModelAndEffortConfigOptions(),
        });

        // Effort is resolved against the snapshot AFTER the model switch, never the pre-switch
        // response (same rule DotAcpAgentHostEffortTests pins for session/new): script the model
        // set to answer with the effort catalog still present, or the effort call never fires.
        launcher.Agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal)
                ? DotAcpAgentHostResumeTests.CreateModelAndEffortConfigOptions()
                : null,
        });
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? original = null;
        IAgentSession? resumed = null;

        try
        {
            await host.StartAsync(cancellationToken);
            original = await host.StartSessionAsync(
                DotAcpAgentHostResumeTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            resumed = await host.ResumeSessionAsync(
                "sess-1",
                DotAcpAgentHostResumeTests.CreateSessionOptions(Path.GetTempPath(), model: "opus", effort: "max"),
                cancellationToken);

            Assert.Equal("sess-1", resumed.SessionId);

            JsonObject update = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "session/update",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = "sess-1",
                    ["update"] = new JsonObject
                    {
                        ["sessionUpdate"] = "agent_message_chunk",
                        ["content"] = new JsonObject { ["type"] = "text", ["text"] = "resumed" },
                    },
                },
            };
            await launcher.Agent.WriteMessageAsync(update);

            AgentEvent receivedEvent = await resumed.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Assert.Equal(new MessageChunk("sess-1", "resumed"), receivedEvent);

            JsonObject[] setConfigCalls = launcher.Agent.Received
                .Where(message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(2, setConfigCalls.Length);
            Assert.Equal("model", (string?)setConfigCalls[0]["params"]!["configId"]);
            Assert.Equal("opus", (string?)setConfigCalls[0]["params"]!["value"]);
            Assert.Equal("thinking", (string?)setConfigCalls[1]["params"]!["configId"]);
            Assert.Equal("max", (string?)setConfigCalls[1]["params"]!["value"]);
        }
        finally
        {
            if (original is not null)
            {
                await original.DisposeAsync();
            }

            if (resumed is not null)
            {
                await resumed.DisposeAsync();
            }

            await host.DisposeAsync();
        }
    }

    /// <summary>Resuming an id the agent reports resource-not-found for throws <see cref="AgentSessionNotFoundException"/>, naming the id.</summary>
    [Fact(Timeout = 10000)]
    public async Task ResumeSession_ResourceNotFound_ThrowsAgentSessionNotFoundException()
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

            AgentSessionNotFoundException exception = await Assert.ThrowsAsync<AgentSessionNotFoundException>(
                () => host.ResumeSessionAsync(
                    "sess-unknown",
                    DotAcpAgentHostResumeTests.CreateSessionOptions(Path.GetTempPath()),
                    cancellationToken));

            Assert.Equal("sess-unknown", exception.SessionId);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>Any other <c>session/resume</c> error maps to the generic <see cref="AgentException"/>, not the not-found one.</summary>
    [Fact(Timeout = 10000)]
    public async Task ResumeSession_OtherError_ThrowsAgentException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnResumeSession = _ => throw new FakeRpcError(-32001, "internal resume failure");
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? original = null;

        try
        {
            await host.StartAsync(cancellationToken);
            original = await host.StartSessionAsync(
                DotAcpAgentHostResumeTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            AgentException exception = await Assert.ThrowsAsync<AgentException>(
                () => host.ResumeSessionAsync(
                    "sess-1",
                    DotAcpAgentHostResumeTests.CreateSessionOptions(Path.GetTempPath()),
                    cancellationToken));

            Assert.IsNotType<AgentSessionNotFoundException>(exception);
        }
        finally
        {
            if (original is not null)
            {
                await original.DisposeAsync();
            }

            await host.DisposeAsync();
        }
    }

    /// <summary>The <c>session/resume</c> request carries Cwd, McpServers and the system prompt under <c>_meta</c>, exactly as <c>session/new</c> does.</summary>
    [Fact(Timeout = 10000)]
    public async Task ResumeSession_SendsCwdMcpServersAndMeta()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        IAgentSession? original = null;
        IAgentSession? resumed = null;

        try
        {
            await host.StartAsync(cancellationToken);
            original = await host.StartSessionAsync(DotAcpAgentHostResumeTests.CreateSessionOptions(cwd), cancellationToken);

            SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");
            resumed = await host.ResumeSessionAsync(
                "sess-1",
                new AgentSessionOptions(cwd, new AutoApprovePermissionHandler(), systemPrompt),
                cancellationToken);

            JsonObject resumeCall = await launcher.Agent.WaitForAsync("session/resume", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)resumeCall["params"]!;
            Assert.Equal(cwd, (string?)parameters["cwd"]);
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            Assert.Empty(mcpServers);
            JsonObject meta = Assert.IsType<JsonObject>(parameters["_meta"]);
            JsonObject payload = Assert.IsType<JsonObject>(meta["systemPrompt"]);
            Assert.Equal("You are the COO", (string?)payload["append"]);
        }
        finally
        {
            if (original is not null)
            {
                await original.DisposeAsync();
            }

            if (resumed is not null)
            {
                await resumed.DisposeAsync();
            }

            await host.DisposeAsync();
        }
    }

    /// <summary>Builds a "model" + "thought_level" configOptions array for the resumed session's response.</summary>
    private static JsonArray CreateModelAndEffortConfigOptions()
    {
        return new JsonArray
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
                ["currentValue"] = "default",
                ["options"] = new JsonArray
                {
                    new JsonObject { ["value"] = "default", ["name"] = "Default" },
                    new JsonObject { ["value"] = "max", ["name"] = "Max" },
                },
            },
        };
    }

    /// <summary>Builds session options with no model and no effort requested.</summary>
    private static AgentSessionOptions CreateSessionOptions(string cwd)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler());
    }

    /// <summary>Builds session options with the given model and effort requested.</summary>
    private static AgentSessionOptions CreateSessionOptions(string cwd, string? model, string? effort)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler(), model: model, effort: effort);
    }
}
