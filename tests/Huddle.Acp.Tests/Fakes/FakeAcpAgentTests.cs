namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Streams;
using Xunit;

/// <summary>
/// Self-tests for <see cref="FakeAcpAgent"/>: drive it from the other end of the stream pair
/// with a plain <see cref="StreamReader"/>/<see cref="StreamWriter"/>, deliberately without any
/// protocol library, so the fake is proven before anything else trusts it.
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
}
