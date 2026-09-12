namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Collections.Generic;
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
/// Pins how <see cref="DotAcpAgentHost.StartSessionAsync"/> maps an <see cref="AgentSessionOptions.ToolServer"/>
/// onto the wire. This is the only file allowed to know the dotacp wire shape (layering invariant, guide S5),
/// and these tests observe only the raw JSON the fake agent receives - never a dotacp protocol type.
/// </summary>
public sealed class DotAcpAgentHostToolServerTests
{
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ToolServerConfigured_SendsSingleMcpServersEntryWithHttpType()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        Uri toolServerUri = new Uri("http://127.0.0.1:5057/mcp");
        ToolServerEndpoint toolServer = new ToolServerEndpoint("team", toolServerUri);
        AgentSessionOptions options = new AgentSessionOptions(
            Path.GetTempPath(), new AutoApprovePermissionHandler(), null, toolServer);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(options, cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            JsonObject entry = Assert.IsType<JsonObject>(Assert.Single(mcpServers));

            Assert.Equal((string?)"team", (string?)entry["name"]);
            Assert.Equal(toolServerUri.ToString(), (string?)entry["url"]);

            // This is the assertion the whole test exists for: if dotacp does not put "type": "http"
            // on the wire, the adapter's own `"type" in server` check drops the server silently and the
            // feature is dead in the water. Assert the literal string, not merely "is present".
            Assert.Equal((string?)"http", (string?)entry["type"]);
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
    public async Task StartSessionAsync_ToolServerHeadersConfigured_SendsAuthorizationHeaderOnMcpServersEntry()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        Uri toolServerUri = new Uri("http://127.0.0.1:5057/mcp");
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authorization"] = "Bearer test-token-123",
        };
        ToolServerEndpoint toolServer = new ToolServerEndpoint("team", toolServerUri, headers);
        AgentSessionOptions options = new AgentSessionOptions(
            Path.GetTempPath(), new AutoApprovePermissionHandler(), null, toolServer);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(options, cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            JsonObject entry = Assert.IsType<JsonObject>(Assert.Single(mcpServers));

            JsonArray entryHeaders = Assert.IsType<JsonArray>(entry["headers"]);
            JsonObject headerEntry = Assert.IsType<JsonObject>(Assert.Single(entryHeaders));
            Assert.Equal((string?)"Authorization", (string?)headerEntry["name"]);
            Assert.StartsWith("Bearer ", (string?)headerEntry["value"], StringComparison.Ordinal);
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
    public async Task StartSessionAsync_ToolServerConfiguredWithNoHeaders_SendsEmptyHeadersArray()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        Uri toolServerUri = new Uri("http://127.0.0.1:5057/mcp");

        // No headers argument at all - the existing default must be byte-for-byte unchanged
        // even when a tool server is configured.
        ToolServerEndpoint toolServer = new ToolServerEndpoint("team", toolServerUri);
        AgentSessionOptions options = new AgentSessionOptions(
            Path.GetTempPath(), new AutoApprovePermissionHandler(), null, toolServer);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(options, cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            JsonObject entry = Assert.IsType<JsonObject>(Assert.Single(mcpServers));

            JsonArray entryHeaders = Assert.IsType<JsonArray>(entry["headers"]);
            Assert.Empty(entryHeaders);
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
    public async Task StartSessionAsync_NoToolServer_SendsEmptyMcpServersAndUnchangedRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        string cwd = Path.GetTempPath();
        AgentSessionOptions options = new AgentSessionOptions(
            cwd, new AutoApprovePermissionHandler(), null, null);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(options, cancellationToken);

            JsonObject sessionNew = await launcher.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject parameters = (JsonObject)sessionNew["params"]!;

            Assert.Equal((string?)cwd, (string?)parameters["cwd"]);
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            Assert.Empty(mcpServers);
            Assert.Null(parameters["_meta"]);
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
}
