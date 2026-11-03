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
/// Pins that <c>IAgentSession.DeleteAsync</c> on the dotacp session never rethrows a lost connection: a delete
/// racing the agent process ending is a courtesy that failed, not an error for the caller.
/// </summary>
public sealed class DotAcpAgentSessionDeleteConnectionLostTests
{
    /// <summary>The agent process ending while <c>session/delete</c> is pending leaves <c>DeleteAsync</c> completing normally.</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_ConnectionEndsBeforeAnswer_CompletesWithoutThrowing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentInfo"] = new JsonObject { ["name"] = "fake-agent", ["version"] = "0.0.1" },
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = false,
                ["sessionCapabilities"] = new JsonObject { ["delete"] = new JsonObject() },
            },
            ["authMethods"] = new JsonArray(),
        };
        TaskCompletionSource<JsonObject> neverCompletes = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.Agent.OnSessionDelete = _ =>
        {
            launcher.Agent.CloseOutput();
            return neverCompletes.Task;
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        await host.StartAsync(cancellationToken);
        IAgentSession session = await host.StartSessionAsync(
            new AgentSessionOptions(Path.GetTempPath(), new RecordingPermissionHandler()),
            cancellationToken);
        try
        {
            Exception? thrown = await Record.ExceptionAsync(() => session.DeleteAsync(cancellationToken));

            Assert.Null(thrown);
            JsonObject deleteMessage = await launcher.Agent.WaitForAsync("session/delete", TimeSpan.FromSeconds(2));
            Assert.Equal(session.SessionId, (string?)deleteMessage["params"]?["sessionId"]);
        }
        finally
        {
            neverCompletes.TrySetResult(new JsonObject());
            await session.DisposeAsync();
            await host.DisposeAsync();
        }
    }
}
