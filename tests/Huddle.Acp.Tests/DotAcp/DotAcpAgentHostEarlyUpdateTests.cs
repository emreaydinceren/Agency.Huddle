using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// A <c>session/update</c> for a new session can reach the client before the host has registered that session: the
/// agent sends it right after (here, just before) its <c>session/new</c> answer, and the update is dispatched on the
/// reader while the host is still awaiting the answer. The real Claude adapter does this with its command list, and the
/// dropped update's Warning failed a paid live test that treats any host warning as a wrong-model fallback.
/// </summary>
public sealed class DotAcpAgentHostEarlyUpdateTests
{
    private const string SessionId = "sess-early";

    /// <summary>An update the agent sends for the new session before its <c>session/new</c> answer reaches that session, and the host logs no Warning.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSession_UpdateBeforeTheAnswer_ReachesTheSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        ListLoggerFactory loggers = new();
        DotAcpAgentHost host = new(new AgentProcessOptions("fake", []), launcher, new DotAcpHostOptions(), loggers);
        try
        {
            await host.StartAsync(cancellationToken);
            launcher.Agent.OnNewSession = async _ =>
            {
                await launcher.Agent.WriteMessageAsync(DotAcpAgentHostEarlyUpdateTests.TextChunk("early")).ConfigureAwait(false);
                return new JsonObject { ["sessionId"] = SessionId };
            };

            IAgentSession session = await host.StartSessionAsync(new AgentSessionOptions(Path.GetTempPath(), new RecordingPermissionHandler()), cancellationToken);

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            AgentEvent first = await session.Events.ReadAsync(linked.Token);
            Assert.Equal(new MessageChunk(SessionId, "early"), first);
            Assert.Empty(loggers.Logger.Entries.Where(entry => entry.Level >= LogLevel.Warning).Select(entry => entry.Message));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>Builds an <c>agent_message_chunk</c> notification for <see cref="SessionId"/>, the shape the fake's prompt context sends.</summary>
    /// <param name="text">The chunk's text.</param>
    private static JsonObject TextChunk(string text) => new()
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "session/update",
        ["params"] = new JsonObject
        {
            ["sessionId"] = SessionId,
            ["update"] = new JsonObject
            {
                ["sessionUpdate"] = "agent_message_chunk",
                ["content"] = new JsonObject { ["type"] = "text", ["text"] = text },
            },
        },
    };
}
