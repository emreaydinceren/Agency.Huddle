using System.Text.Json.Nodes;
using System.Threading.Channels;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers session notices end to end through a real <see cref="DotAcpAgentHost"/> and the fake agent:
/// what <c>initialize</c> advertises when <see cref="DotAcpHostOptions.AdvertiseSessionNotices"/> is set,
/// and that a <c>notice</c> session update, which stable dotacp cannot deserialise, reaches the session
/// as an <see cref="AgentNotice"/> without holding the Turn's completion.
/// </summary>
public sealed class DotAcpAgentHostNoticeWireTests
{
    private const string SessionId = "sess-1";

    /// <summary>With the option on, <c>initialize</c> carries <c>clientCapabilities.session</c> equal to exactly <c>{"notices":{}}</c>, and nothing else is added.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_WithOption_AdvertisesNoticesOnly()
    {
        JsonObject capabilities = await DotAcpAgentHostNoticeWireTests.StartAndReadCapabilitiesAsync(
            new DotAcpHostOptions(AdvertiseSessionNotices: true),
            TestContext.Current.CancellationToken);

        JsonNode expected = JsonNode.Parse("""{"fs":{"readTextFile":false,"writeTextFile":false},"terminal":false,"session":{"notices":{}}}""")
            ?? throw new InvalidOperationException("The expected literal did not parse.");
        Assert.True(JsonNode.DeepEquals(expected, capabilities), capabilities.ToJsonString());
    }

    /// <summary>With both options on, the capabilities carry elicitation and notices side by side: the two subclasses compose.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_WithBothOptions_AdvertisesElicitationAndNotices()
    {
        JsonObject capabilities = await DotAcpAgentHostNoticeWireTests.StartAndReadCapabilitiesAsync(
            new DotAcpHostOptions(AdvertiseElicitationForm: true, AdvertiseSessionNotices: true),
            TestContext.Current.CancellationToken);

        JsonNode expected = JsonNode.Parse("""{"fs":{"readTextFile":false,"writeTextFile":false},"terminal":false,"elicitation":{"form":{}},"session":{"notices":{}}}""")
            ?? throw new InvalidOperationException("The expected literal did not parse.");
        Assert.True(JsonNode.DeepEquals(expected, capabilities), capabilities.ToJsonString());
    }

    /// <summary>By default the host advertises no <c>session</c> capability at all, so the agent keeps writing notices into the reply.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_Default_AdvertisesNoSessionCapability()
    {
        JsonObject capabilities = await DotAcpAgentHostNoticeWireTests.StartAndReadCapabilitiesAsync(
            new DotAcpHostOptions(),
            TestContext.Current.CancellationToken);

        Assert.False(capabilities.ContainsKey("session"));
    }

    /// <summary>The option is off unless asked for: no arguments, the probe host's construction and the Console's construction all leave it false.</summary>
    [Fact]
    public void HostOptions_ProbeAndConsoleConstructions_LeaveNoticesOff()
    {
        DotAcpHostOptions noArguments = new();
        DotAcpHostOptions probe = new("Team.App", TraceWire: true);
        DotAcpHostOptions console = new(ClientVersion: "1.2.3", TraceWire: false);

        Assert.False(noArguments.AdvertiseSessionNotices);
        Assert.False(probe.AdvertiseSessionNotices);
        Assert.False(console.AdvertiseSessionNotices);
    }

    /// <summary>
    /// With the option on, a <c>notice</c> sent between two text chunks reaches the session as one
    /// <see cref="AgentNotice"/>, the reply text is only the two chunks, and the Turn completes last:
    /// the rewritten line is counted and dispatched, so the wire-order barrier is released.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task Prompt_WithOption_NoticeArrivesAsAgentNoticeAndTurnCompletes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = new(new AgentProcessOptions("fake", []), launcher, new DotAcpHostOptions(AdvertiseSessionNotices: true), new ListLoggerFactory());
        try
        {
            await host.StartAsync(cancellationToken);
            IAgentSession session = await host.StartSessionAsync(new AgentSessionOptions(Path.GetTempPath(), new RecordingPermissionHandler()), cancellationToken);
            launcher.Agent.OnPrompt = async context =>
            {
                await context.SendTextChunkAsync("a").ConfigureAwait(false);
                await context.SendUpdateAsync(new JsonObject
                {
                    ["sessionUpdate"] = "notice",
                    ["severity"] = "warning",
                    ["title"] = "Auto mode unavailable",
                    ["description"] = "The selected model does not support Auto mode; using Accept edits instead.",
                }).ConfigureAwait(false);
                await context.SendTextChunkAsync("b").ConfigureAwait(false);
                return "end_turn";
            };

            Task<PromptResult> prompt = session.PromptAsync("hello", cancellationToken);
            List<AgentEvent> events = await DotAcpAgentHostNoticeWireTests.ReadUntilTurnCompletedAsync(session.Events, cancellationToken);
            PromptResult result = await prompt;

            Assert.Equal(StopReason.EndTurn, result.StopReason);
            Assert.Equal(new TurnCompleted(SessionId, StopReason.EndTurn), events[^1]);
            Assert.Equal(
                new AgentNotice(SessionId, NoticeSeverity.Warning, "Auto mode unavailable", "The selected model does not support Auto mode; using Accept edits instead."),
                Assert.Single(events.OfType<AgentNotice>()));
            Assert.Equal(["a", "b"], events.OfType<MessageChunk>().Select(chunk => chunk.Text));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>Starts a host with <paramref name="options"/> against a default fake agent and returns the <c>clientCapabilities</c> object the agent received in <c>initialize</c>.</summary>
    /// <param name="options">The host options under test.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<JsonObject> StartAndReadCapabilitiesAsync(DotAcpHostOptions options, CancellationToken cancellationToken)
    {
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = new(new AgentProcessOptions("fake", []), launcher, options, new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            JsonObject initialize = launcher.Agent.Received.Single(message => string.Equals((string?)message["method"], "initialize", StringComparison.Ordinal));
            JsonObject? capabilities = (initialize["params"] as JsonObject)?["clientCapabilities"] as JsonObject;
            Assert.NotNull(capabilities);
            return capabilities;
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>Reads the session's events up to and including its <see cref="TurnCompleted"/>, within five seconds.</summary>
    /// <param name="reader">The session's event channel.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<List<AgentEvent>> ReadUntilTurnCompletedAsync(ChannelReader<AgentEvent> reader, CancellationToken cancellationToken)
    {
        List<AgentEvent> events = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        while (true)
        {
            AgentEvent agentEvent = await reader.ReadAsync(linked.Token);
            events.Add(agentEvent);
            if (agentEvent is TurnCompleted)
            {
                return events;
            }
        }
    }
}
