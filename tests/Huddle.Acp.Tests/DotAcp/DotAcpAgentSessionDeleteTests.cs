namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Covers <c>IAgentSession.DeleteAsync</c> on the dotacp session (Spec §7): it sends <c>session/delete</c> only
/// when the host reported <c>SupportsDeleteSession</c>, treats resource-not-found as success, still works after
/// the session is disposed, and is bounded when the peer never answers.
/// </summary>
public sealed class DotAcpAgentSessionDeleteTests
{
    private const string DeleteMethod = "session/delete";

    /// <summary>A session whose host advertised delete sends exactly one <c>session/delete</c> carrying its session id.</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_CanDelete_SendsSessionDeleteWithTheSessionId()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => DotAcpAgentSessionDeleteTests.InitializeAdvertising(deleteAdvertised: true);
        (DotAcpAgentHost host, IAgentSession session) = await DotAcpAgentSessionDeleteTests.StartAsync(launcher, cancellationToken);
        try
        {
            await session.DeleteAsync(cancellationToken);

            JsonObject deleteMessage = await launcher.Agent.WaitForAsync(DeleteMethod, TimeSpan.FromSeconds(2));
            Assert.Equal(session.SessionId, (string?)deleteMessage["params"]?["sessionId"]);
            Assert.Equal(1, DotAcpAgentSessionDeleteTests.CountDeletes(launcher));
        }
        finally
        {
            await session.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>A session whose host did not advertise delete sends nothing and completes normally.</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_CannotDelete_SendsNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => DotAcpAgentSessionDeleteTests.InitializeAdvertising(deleteAdvertised: false);
        (DotAcpAgentHost host, IAgentSession session) = await DotAcpAgentSessionDeleteTests.StartAsync(launcher, cancellationToken);
        try
        {
            await session.DeleteAsync(cancellationToken);

            Assert.Equal(0, DotAcpAgentSessionDeleteTests.CountDeletes(launcher));
        }
        finally
        {
            await session.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>An agent answering <c>ResourceNotFound</c> (-32002) means the session is already gone: <c>DeleteAsync</c> succeeds.</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_NotFound_Succeeds()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => DotAcpAgentSessionDeleteTests.InitializeAdvertising(deleteAdvertised: true);
        launcher.Agent.OnSessionDelete = _ => throw new FakeRpcError(-32002, "not found");
        ListLoggerFactory loggers = new ListLoggerFactory();
        (DotAcpAgentHost host, IAgentSession session) = await DotAcpAgentSessionDeleteTests.StartAsync(launcher, cancellationToken, loggers);
        try
        {
            await session.DeleteAsync(cancellationToken);

            Assert.Equal(1, DotAcpAgentSessionDeleteTests.CountDeletes(launcher));
            Assert.DoesNotContain(loggers.Logger.Entries, static entry => entry.Level == LogLevel.Warning);
        }
        finally
        {
            await session.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Any other RPC error is swallowed too, but it logs exactly one Warning, unlike <c>ResourceNotFound</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_OtherRpcError_IsSwallowedAndLogsOneWarning()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => DotAcpAgentSessionDeleteTests.InitializeAdvertising(deleteAdvertised: true);
        launcher.Agent.OnSessionDelete = _ => throw new FakeRpcError(-32603, "internal error");
        ListLoggerFactory loggers = new ListLoggerFactory();
        (DotAcpAgentHost host, IAgentSession session) = await DotAcpAgentSessionDeleteTests.StartAsync(launcher, cancellationToken, loggers);
        try
        {
            Exception? thrown = await Record.ExceptionAsync(() => session.DeleteAsync(cancellationToken));

            Assert.Null(thrown);
            Assert.Equal(1, DotAcpAgentSessionDeleteTests.CountDeletes(launcher));
            Assert.Single(loggers.Logger.Entries, static entry => entry.Level == LogLevel.Warning);
        }
        finally
        {
            await session.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>The fresh-start teardown closes the session first and deletes after: <c>DeleteAsync</c> after <c>DisposeAsync</c> still sends <c>session/delete</c> (no disposed guard).</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_AfterDispose_StillSendsSessionDelete()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => DotAcpAgentSessionDeleteTests.InitializeAdvertising(deleteAdvertised: true);
        (DotAcpAgentHost host, IAgentSession session) = await DotAcpAgentSessionDeleteTests.StartAsync(launcher, cancellationToken);
        try
        {
            await session.DisposeAsync();

            await session.DeleteAsync(cancellationToken);

            JsonObject deleteMessage = await launcher.Agent.WaitForAsync(DeleteMethod, TimeSpan.FromSeconds(2));
            Assert.Equal(session.SessionId, (string?)deleteMessage["params"]?["sessionId"]);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>A peer that never answers <c>session/delete</c> cannot hold <c>DeleteAsync</c> past the same 2 s bound close uses.</summary>
    [Fact(Timeout = 10000)]
    public async Task DeleteAsync_PeerNeverAnswers_IsBoundedByTwoSeconds()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnInitialize = _ => DotAcpAgentSessionDeleteTests.InitializeAdvertising(deleteAdvertised: true);
        TaskCompletionSource<JsonObject> neverCompletes = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.Agent.OnSessionDelete = _ => neverCompletes.Task;
        (DotAcpAgentHost host, IAgentSession session) = await DotAcpAgentSessionDeleteTests.StartAsync(launcher, cancellationToken);
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            await session.DeleteAsync(cancellationToken);

            stopwatch.Stop();
            Assert.Equal(1, DotAcpAgentSessionDeleteTests.CountDeletes(launcher));
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"DeleteAsync took {stopwatch.Elapsed}, expected it bounded by the 2s timeout.");
        }
        finally
        {
            neverCompletes.TrySetResult(new JsonObject());
            await session.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Starts a host over the launcher and opens one session on it.</summary>
    private static async Task<(DotAcpAgentHost Host, IAgentSession Session)> StartAsync(
        FakeAgentProcessLauncher launcher,
        CancellationToken cancellationToken,
        ListLoggerFactory? loggers = null)
    {
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            loggers ?? new ListLoggerFactory());
        await host.StartAsync(cancellationToken);
        IAgentSession session = await host.StartSessionAsync(
            new AgentSessionOptions(Path.GetTempPath(), new RecordingPermissionHandler()),
            cancellationToken);
        return (host, session);
    }

    /// <summary>Counts the <c>session/delete</c> requests the fake agent received.</summary>
    private static int CountDeletes(FakeAgentProcessLauncher launcher)
    {
        return launcher.Agent.Received.Count(
            message => string.Equals((string?)message["method"], DeleteMethod, StringComparison.Ordinal));
    }

    /// <summary>Builds an <c>initialize</c> response that advertises <c>sessionCapabilities.delete</c> or not.</summary>
    private static JsonObject InitializeAdvertising(bool deleteAdvertised)
    {
        JsonObject agentCapabilities = new JsonObject
        {
            ["loadSession"] = false,
        };
        if (deleteAdvertised)
        {
            agentCapabilities["sessionCapabilities"] = new JsonObject
            {
                ["delete"] = new JsonObject(),
            };
        }

        return new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentInfo"] = new JsonObject
            {
                ["name"] = "fake-agent",
                ["version"] = "0.0.1",
            },
            ["agentCapabilities"] = agentCapabilities,
            ["authMethods"] = new JsonArray(),
        };
    }
}
