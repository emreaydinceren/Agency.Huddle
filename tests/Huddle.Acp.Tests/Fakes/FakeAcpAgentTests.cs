namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nerdbank.Streams;
using StreamJsonRpc;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

/// <summary>
/// Self-tests for <see cref="FakeAcpAgent"/>: drive it from the other end of the stream pair
/// with a plain <see cref="StreamReader"/>/<see cref="StreamWriter"/>, deliberately without any
/// protocol library, so the fake is proven before anything else trusts it. The RS §6.4 A-5
/// conformance tests below (distinct session ids, advertised resume/close capabilities,
/// <c>session/resume</c> behaviour) instead drive it through a real <c>dotacp.client.Connection</c>
/// - the same wire library <see cref="DotAcpAgentHost"/> uses - because they exercise the real
/// wire shapes those capabilities are read from.
/// </summary>
public sealed class FakeAcpAgentTests
{
    [Fact(Timeout = 10000)]
    public async Task RespondsToInitializeWithSameId()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new Harness();
        _ = harness.Agent.RunAsync(cancellationToken);

        await harness.SendAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"initialize\",\"params\":{}}",
            cancellationToken);

        JsonObject response = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal(7, (int?)response["id"]);
        Assert.Equal("fake-agent", (string?)response["result"]?["agentInfo"]?["name"]);
    }

    [Fact(Timeout = 10000)]
    public async Task RespondsToPromptWithScriptedChunksThenResult()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new Harness();
        harness.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("first").ConfigureAwait(false);
            await context.SendTextChunkAsync("second").ConfigureAwait(false);
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);

        await harness.SendAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"session/prompt\",\"params\":{\"sessionId\":\"sess-1\",\"prompt\":[]}}",
            cancellationToken);

        JsonObject firstUpdate = await harness.ReceiveAsync(cancellationToken);
        JsonObject secondUpdate = await harness.ReceiveAsync(cancellationToken);
        JsonObject result = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal("session/update", (string?)firstUpdate["method"]);
        Assert.Equal("first", (string?)firstUpdate["params"]?["update"]?["content"]?["text"]);
        Assert.Equal("session/update", (string?)secondUpdate["method"]);
        Assert.Equal("second", (string?)secondUpdate["params"]?["update"]?["content"]?["text"]);
        Assert.Equal(1, (int?)result["id"]);
        Assert.Equal("end_turn", (string?)result["result"]?["stopReason"]);
    }

    [Fact(Timeout = 10000)]
    public async Task AgentInitiatedRequest_ResolvesWhenClientResponds()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new Harness();
        string? observedOptionId = null;
        harness.Agent.OnPrompt = async context =>
        {
            JsonObject toolCall = new JsonObject
            {
                ["toolCallId"] = "call-1",
                ["title"] = "Write hello.txt",
                ["kind"] = "edit",
                ["status"] = "pending",
            };
            JsonObject[] options = new JsonObject[]
            {
                new JsonObject { ["optionId"] = "allow", ["name"] = "Allow once", ["kind"] = "allow_once" },
            };

            JsonObject outcome = await context.RequestPermissionAsync(toolCall, options).ConfigureAwait(false);
            observedOptionId = (string?)outcome["optionId"];
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);

        await harness.SendAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"session/prompt\",\"params\":{\"sessionId\":\"sess-1\",\"prompt\":[]}}",
            cancellationToken);

        JsonObject permissionRequest = await harness.ReceiveAsync(cancellationToken);
        Assert.Equal("session/request_permission", (string?)permissionRequest["method"]);
        int requestId = (int)permissionRequest["id"]!;
        Assert.Equal(1000, requestId);

        await harness.SendAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":" + requestId.ToString(CultureInfo.InvariantCulture) + ",\"result\":{\"outcome\":{\"outcome\":\"selected\",\"optionId\":\"allow\"}}}",
            cancellationToken);

        JsonObject promptResult = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal("end_turn", (string?)promptResult["result"]?["stopReason"]);
        Assert.Equal("allow", observedOptionId);
    }

    [Fact(Timeout = 10000)]
    public async Task RejectsLineWithoutJsonRpcField()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new Harness();

        await harness.SendAsync("{\"id\":1,\"method\":\"initialize\",\"params\":{}}", cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Agent.RunAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
    }

    [Fact(Timeout = 10000)]
    public async Task NewSession_FakeRpcError_ProducesErrorResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new Harness();
        harness.Agent.OnNewSession = _ => throw new FakeRpcError(-32000, "auth_required");
        _ = harness.Agent.RunAsync(cancellationToken);

        await harness.SendAsync(
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"session/new\",\"params\":{}}",
            cancellationToken);

        JsonObject response = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal(-32000, (int?)response["error"]?["code"]);
        Assert.Equal("auth_required", (string?)response["error"]?["message"]);
    }

    /// <summary>Each <c>session/new</c> gets a distinct id, so every existing "sess-1" assertion elsewhere still holds for the first call.</summary>
    [Fact(Timeout = 10000)]
    public async Task NewSession_Twice_DistinctIds()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ConnectedFake connected = FakeAcpAgentTests.Connect();
        await connected.InitializeAsync(cancellationToken);

        dotacp.protocol.NewSessionResponse first = await connected.NewSessionAsync(cancellationToken);
        dotacp.protocol.NewSessionResponse second = await connected.NewSessionAsync(cancellationToken);

        Assert.Equal("sess-1", (string)first.SessionId);
        Assert.Equal("sess-2", (string)second.SessionId);
    }

    /// <summary>The default <c>initialize</c> response advertises both <c>resume</c> and <c>close</c> session capabilities.</summary>
    [Fact(Timeout = 10000)]
    public async Task Initialize_AdvertisesResumeAndClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ConnectedFake connected = FakeAcpAgentTests.Connect();

        dotacp.protocol.InitializeResponse response = await connected.InitializeAsync(cancellationToken);

        Assert.NotNull(response.AgentCapabilities?.SessionCapabilities?.Resume);
        Assert.NotNull(response.AgentCapabilities?.SessionCapabilities?.Close);
    }

    /// <summary>Resuming an id the fake minted for a <c>session/new</c> succeeds.</summary>
    [Fact(Timeout = 10000)]
    public async Task Resume_KnownId_Succeeds()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ConnectedFake connected = FakeAcpAgentTests.Connect();
        await connected.InitializeAsync(cancellationToken);
        dotacp.protocol.NewSessionResponse created = await connected.NewSessionAsync(cancellationToken);

        dotacp.protocol.ResumeSessionResponse resumed = await connected.Connection.ResumeSessionAsync(
            new dotacp.protocol.ResumeSessionRequest { SessionId = created.SessionId, Cwd = Path.GetTempPath() },
            cancellationToken);

        Assert.NotNull(resumed);
    }

    /// <summary>Resuming an id the fake never minted fails with the resource-not-found error code (RS §6.4 A-2).</summary>
    [Fact(Timeout = 10000)]
    public async Task Resume_UnknownId_ResourceNotFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ConnectedFake connected = FakeAcpAgentTests.Connect();
        await connected.InitializeAsync(cancellationToken);

        RemoteInvocationException exception = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
            connected.Connection.ResumeSessionAsync(
                new dotacp.protocol.ResumeSessionRequest { SessionId = "sess-unknown", Cwd = Path.GetTempPath() },
                cancellationToken));

        Assert.Equal((int)dotacp.protocol.ErrorCode.ResourceNotFound, exception.ErrorCode);
    }

    /// <summary>A closed session can still be resumed: Claude Code keeps closed conversations on disk (RS §6.2 "Closing").</summary>
    [Fact(Timeout = 10000)]
    public async Task Close_ThenResume_Succeeds()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ConnectedFake connected = FakeAcpAgentTests.Connect();
        await connected.InitializeAsync(cancellationToken);
        dotacp.protocol.NewSessionResponse created = await connected.NewSessionAsync(cancellationToken);

        await connected.Connection.CloseAsync(new dotacp.protocol.CloseSessionRequest { SessionId = created.SessionId }, cancellationToken);
        dotacp.protocol.ResumeSessionResponse resumed = await connected.Connection.ResumeSessionAsync(
            new dotacp.protocol.ResumeSessionRequest { SessionId = created.SessionId, Cwd = Path.GetTempPath() },
            cancellationToken);

        Assert.NotNull(resumed);
    }

    private static ConnectedFake Connect()
    {
        (Stream clientEnd, Stream agentEnd) = FullDuplexStream.CreatePair();
        FakeAcpAgent agent = new FakeAcpAgent(agentEnd);
        _ = agent.RunAsync(CancellationToken.None);

        DotAcpClientAdapter clientAdapter = new DotAcpClientAdapter(new ListLoggerFactory().CreateLogger<DotAcpClientAdapter>());
        dotacp.client.Connection connection = dotacp.client.Connection.RunClient(clientAdapter, clientEnd, clientEnd, null)
            ?? throw new InvalidOperationException("Failed to create connection.");

        return new ConnectedFake(agent, connection);
    }

    /// <summary>Wires a stream pair, exposing the fake's end and a plain reader/writer over the test's end.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly Stream testStream;

        private readonly StreamReader reader;

        internal Harness()
        {
            (Stream testEnd, Stream agentEnd) = FullDuplexStream.CreatePair();
            this.testStream = testEnd;
            this.reader = new StreamReader(testEnd, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            this.Agent = new FakeAcpAgent(agentEnd);
        }

        internal FakeAcpAgent Agent { get; }

        internal async Task SendAsync(string json, CancellationToken cancellationToken)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json + "\n");
            await this.testStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await this.testStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        internal async Task<JsonObject> ReceiveAsync(CancellationToken cancellationToken)
        {
            string? line = await this.reader.ReadLineAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            Assert.NotNull(line);
            return (JsonObject)JsonNode.Parse(line!)!;
        }

        public async ValueTask DisposeAsync()
        {
            this.reader.Dispose();
            this.testStream.Dispose();
            await this.Agent.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>A live <see cref="FakeAcpAgent"/> paired with a real <c>dotacp.client.Connection</c> talking to it, for the RS §6.4 A-5 conformance tests.</summary>
    private sealed class ConnectedFake(FakeAcpAgent agent, dotacp.client.Connection connection) : IAsyncDisposable
    {
        internal dotacp.client.Connection Connection { get; } = connection;

        internal Task<dotacp.protocol.InitializeResponse> InitializeAsync(CancellationToken cancellationToken)
        {
            return this.Connection.InitializeAsync(
                new dotacp.protocol.InitializeRequest
                {
                    ProtocolVersion = dotacp.protocol.ProtocolMeta.Version,
                    ClientCapabilities = new dotacp.protocol.ClientCapabilities
                    {
                        Fs = new dotacp.protocol.FileSystemCapabilities { ReadTextFile = false, WriteTextFile = false },
                        Terminal = false,
                    },
                    ClientInfo = new dotacp.protocol.Implementation { Name = "Huddle.Acp.Tests", Version = "0.1.0" },
                },
                cancellationToken);
        }

        internal Task<dotacp.protocol.NewSessionResponse> NewSessionAsync(CancellationToken cancellationToken)
        {
            return this.Connection.NewSessionAsync(
                new dotacp.protocol.NewSessionRequest { Cwd = Path.GetTempPath(), McpServers = [] },
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            this.Connection.Dispose();
            await agent.DisposeAsync().ConfigureAwait(false);
        }
    }
}
