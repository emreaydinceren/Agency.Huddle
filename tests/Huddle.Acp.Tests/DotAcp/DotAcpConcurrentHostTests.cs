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
/// Closes the gap noted in docs/acp/agent-guide.md section 8: routing by session id was only ever
/// exercised with two sessions on one host. The real shape a Blazor app produces is N concurrent
/// hosts - one adapter child process per persona - so every test here builds two fully independent
/// <see cref="DotAcpAgentHost"/> instances, each with its own <see cref="FakeAgentProcessLauncher"/>,
/// and drives them concurrently with <see cref="Task.WhenAll(Task[])"/>.
/// </summary>
public sealed class DotAcpConcurrentHostTests
{
    [Fact(Timeout = 10000)]
    public async Task TwoHosts_StartConcurrently_EachInfoPopulatedIndependently()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcherAlpha = new FakeAgentProcessLauncher();
        FakeAgentProcessLauncher launcherBravo = new FakeAgentProcessLauncher();
        launcherAlpha.Agent.OnInitialize = _ => DotAcpConcurrentHostTests.MakeInitializeResult("agent-alpha", "1.0.0-alpha");
        launcherBravo.Agent.OnInitialize = _ => DotAcpConcurrentHostTests.MakeInitializeResult("agent-bravo", "1.0.0-bravo");

        DotAcpAgentHost hostAlpha = DotAcpConcurrentHostTests.CreateHost(launcherAlpha);
        DotAcpAgentHost hostBravo = DotAcpConcurrentHostTests.CreateHost(launcherBravo);

        try
        {
            await Task.WhenAll(
                hostAlpha.StartAsync(cancellationToken),
                hostBravo.StartAsync(cancellationToken));

            Assert.Equal("agent-alpha", hostAlpha.Info.AgentName);
            Assert.Equal("1.0.0-alpha", hostAlpha.Info.AgentVersion);
            Assert.Equal("agent-bravo", hostBravo.Info.AgentName);
            Assert.Equal("1.0.0-bravo", hostBravo.Info.AgentVersion);
        }
        finally
        {
            await hostAlpha.DisposeAsync();
            await hostBravo.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task TwoHosts_ConcurrentSessionCreation_EachSessionNewCarriesItsOwnPersona()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcherAlpha = new FakeAgentProcessLauncher();
        FakeAgentProcessLauncher launcherBravo = new FakeAgentProcessLauncher();
        DotAcpAgentHost hostAlpha = DotAcpConcurrentHostTests.CreateHost(launcherAlpha);
        DotAcpAgentHost hostBravo = DotAcpConcurrentHostTests.CreateHost(launcherBravo);
        string cwdAlpha = Path.Combine(Path.GetTempPath(), "team-acp-alpha");
        string cwdBravo = Path.Combine(Path.GetTempPath(), "team-acp-bravo");
        SystemPromptOptions personaAlpha = new SystemPromptOptions("You are ALPHA, the Chief of Staff.");
        SystemPromptOptions personaBravo = new SystemPromptOptions("You are BRAVO, the COO.", SystemPromptMode.Replace);
        IAgentSession? sessionAlpha = null;
        IAgentSession? sessionBravo = null;

        try
        {
            await Task.WhenAll(
                hostAlpha.StartAsync(cancellationToken),
                hostBravo.StartAsync(cancellationToken));

            Task<IAgentSession> startAlpha = hostAlpha.StartSessionAsync(
                new AgentSessionOptions(cwdAlpha, new AutoApprovePermissionHandler(), personaAlpha), cancellationToken);
            Task<IAgentSession> startBravo = hostBravo.StartSessionAsync(
                new AgentSessionOptions(cwdBravo, new AutoApprovePermissionHandler(), personaBravo), cancellationToken);
            await Task.WhenAll(startAlpha, startBravo);
            sessionAlpha = startAlpha.Result;
            sessionBravo = startBravo.Result;

            JsonObject sessionNewAlpha = await launcherAlpha.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));
            JsonObject sessionNewBravo = await launcherBravo.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(2));

            JsonObject parametersAlpha = (JsonObject)sessionNewAlpha["params"]!;
            JsonObject parametersBravo = (JsonObject)sessionNewBravo["params"]!;

            // Wire-level assertions: host A's session/new must carry A's persona, host B's must
            // carry B's. If two Blazor instances ever crossed personas, this is the assertion that
            // would catch it - so identify every value by which host it belongs to.
            Assert.Equal((string?)cwdAlpha, (string?)parametersAlpha["cwd"]);
            Assert.Equal((string?)cwdBravo, (string?)parametersBravo["cwd"]);

            JsonObject metaAlpha = Assert.IsType<JsonObject>(parametersAlpha["_meta"]);
            JsonObject payloadAlpha = Assert.IsType<JsonObject>(metaAlpha["systemPrompt"]);
            Assert.Equal((string?)"You are ALPHA, the Chief of Staff.", (string?)payloadAlpha["append"]);

            JsonObject metaBravo = Assert.IsType<JsonObject>(parametersBravo["_meta"]);
            JsonNode? payloadBravoNode = metaBravo["systemPrompt"];
            Assert.IsNotType<JsonObject>(payloadBravoNode);
            JsonValue payloadBravoValue = Assert.IsAssignableFrom<JsonValue>(payloadBravoNode);
            Assert.Equal((string?)"You are BRAVO, the COO.", (string?)payloadBravoValue);

            // The cross-checks: A's persona text must not appear anywhere in B's session/new, and
            // vice versa.
            Assert.DoesNotContain("BRAVO", parametersAlpha.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("ALPHA", parametersBravo.ToJsonString(), StringComparison.Ordinal);
        }
        finally
        {
            if (sessionAlpha is not null)
            {
                await sessionAlpha.DisposeAsync();
            }

            if (sessionBravo is not null)
            {
                await sessionBravo.DisposeAsync();
            }

            await hostAlpha.DisposeAsync();
            await hostBravo.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task TwoHosts_ConcurrentPrompts_EachSessionOnlySeesItsOwnAgentsUpdates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcherAlpha = new FakeAgentProcessLauncher();
        FakeAgentProcessLauncher launcherBravo = new FakeAgentProcessLauncher();
        launcherAlpha.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("ALPHA-REPLY").ConfigureAwait(false);
            return "end_turn";
        };
        launcherBravo.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("BRAVO-REPLY").ConfigureAwait(false);
            return "end_turn";
        };
        DotAcpAgentHost hostAlpha = DotAcpConcurrentHostTests.CreateHost(launcherAlpha);
        DotAcpAgentHost hostBravo = DotAcpConcurrentHostTests.CreateHost(launcherBravo);
        IAgentSession? sessionAlpha = null;
        IAgentSession? sessionBravo = null;

        try
        {
            await Task.WhenAll(
                hostAlpha.StartAsync(cancellationToken),
                hostBravo.StartAsync(cancellationToken));

            sessionAlpha = await hostAlpha.StartSessionAsync(
                DotAcpConcurrentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);
            sessionBravo = await hostBravo.StartSessionAsync(
                DotAcpConcurrentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            Task<PromptResult> promptAlpha = sessionAlpha.PromptAsync("hello alpha", cancellationToken);
            Task<PromptResult> promptBravo = sessionBravo.PromptAsync("hello bravo", cancellationToken);
            await Task.WhenAll(promptAlpha, promptBravo);

            AgentEvent eventAlpha = await sessionAlpha.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            AgentEvent eventBravo = await sessionBravo.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

            Assert.Equal(new MessageChunk(sessionAlpha.SessionId, "ALPHA-REPLY"), eventAlpha);
            Assert.Equal(new MessageChunk(sessionBravo.SessionId, "BRAVO-REPLY"), eventBravo);

            // Each PromptAsync call also publishes its own TurnCompleted after the reply chunk.
            // Drain that expected event before checking for anything unexpected left behind.
            AgentEvent turnCompletedAlpha = await sessionAlpha.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            AgentEvent turnCompletedBravo = await sessionBravo.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Assert.Equal(new TurnCompleted(sessionAlpha.SessionId, StopReason.EndTurn), turnCompletedAlpha);
            Assert.Equal(new TurnCompleted(sessionBravo.SessionId, StopReason.EndTurn), turnCompletedBravo);

            // No leftover event on either side - in particular, no BRAVO event ever reached A's
            // session, and no ALPHA event ever reached B's.
            Assert.False(sessionAlpha.Events.TryRead(out AgentEvent? leftoverAlpha), $"Unexpected extra event on host A's session: {leftoverAlpha}");
            Assert.False(sessionBravo.Events.TryRead(out AgentEvent? leftoverBravo), $"Unexpected extra event on host B's session: {leftoverBravo}");
        }
        finally
        {
            if (sessionAlpha is not null)
            {
                await sessionAlpha.DisposeAsync();
            }

            if (sessionBravo is not null)
            {
                await sessionBravo.DisposeAsync();
            }

            await hostAlpha.DisposeAsync();
            await hostBravo.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task TwoHosts_SameSessionIdFromBothAgents_StillRouteIndependently()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const string collidingSessionId = "sess-shared-across-hosts";
        FakeAgentProcessLauncher launcherAlpha = new FakeAgentProcessLauncher();
        FakeAgentProcessLauncher launcherBravo = new FakeAgentProcessLauncher();
        launcherAlpha.Agent.OnNewSession = _ => Task.FromResult(new JsonObject { ["sessionId"] = collidingSessionId });
        launcherBravo.Agent.OnNewSession = _ => Task.FromResult(new JsonObject { ["sessionId"] = collidingSessionId });
        launcherAlpha.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("ALPHA-REPLY").ConfigureAwait(false);
            return "end_turn";
        };
        launcherBravo.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("BRAVO-REPLY").ConfigureAwait(false);
            return "end_turn";
        };
        DotAcpAgentHost hostAlpha = DotAcpConcurrentHostTests.CreateHost(launcherAlpha);
        DotAcpAgentHost hostBravo = DotAcpConcurrentHostTests.CreateHost(launcherBravo);
        IAgentSession? sessionAlpha = null;
        IAgentSession? sessionBravo = null;

        try
        {
            await Task.WhenAll(
                hostAlpha.StartAsync(cancellationToken),
                hostBravo.StartAsync(cancellationToken));

            Task<IAgentSession> startAlpha = hostAlpha.StartSessionAsync(
                DotAcpConcurrentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);
            Task<IAgentSession> startBravo = hostBravo.StartSessionAsync(
                DotAcpConcurrentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);
            await Task.WhenAll(startAlpha, startBravo);
            sessionAlpha = startAlpha.Result;
            sessionBravo = startBravo.Result;

            // Both fakes handed back the identical session id string. Registries are per-host
            // (DotAcpClientAdapter's sink dictionary is an instance field created fresh in
            // StartAsync), so this pins the guarantee that no shared/static registry can ever
            // silently creep in later.
            Assert.Equal(collidingSessionId, sessionAlpha.SessionId);
            Assert.Equal(collidingSessionId, sessionBravo.SessionId);

            Task<PromptResult> promptAlpha = sessionAlpha.PromptAsync("hi", cancellationToken);
            Task<PromptResult> promptBravo = sessionBravo.PromptAsync("hi", cancellationToken);
            await Task.WhenAll(promptAlpha, promptBravo);

            AgentEvent eventAlpha = await sessionAlpha.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            AgentEvent eventBravo = await sessionBravo.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

            Assert.Equal(new MessageChunk(collidingSessionId, "ALPHA-REPLY"), eventAlpha);
            Assert.Equal(new MessageChunk(collidingSessionId, "BRAVO-REPLY"), eventBravo);

            // Each PromptAsync call also publishes its own TurnCompleted after the reply chunk.
            // Drain that expected event before checking for anything unexpected left behind.
            AgentEvent turnCompletedAlpha = await sessionAlpha.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            AgentEvent turnCompletedBravo = await sessionBravo.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Assert.Equal(new TurnCompleted(collidingSessionId, StopReason.EndTurn), turnCompletedAlpha);
            Assert.Equal(new TurnCompleted(collidingSessionId, StopReason.EndTurn), turnCompletedBravo);

            Assert.False(sessionAlpha.Events.TryRead(out AgentEvent? leftoverAlpha), $"Unexpected extra event on host A's session: {leftoverAlpha}");
            Assert.False(sessionBravo.Events.TryRead(out AgentEvent? leftoverBravo), $"Unexpected extra event on host B's session: {leftoverBravo}");
        }
        finally
        {
            if (sessionAlpha is not null)
            {
                await sessionAlpha.DisposeAsync();
            }

            if (sessionBravo is not null)
            {
                await sessionBravo.DisposeAsync();
            }

            await hostAlpha.DisposeAsync();
            await hostBravo.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task DisposingOneHost_DoesNotDisturbTheOtherHost()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcherAlpha = new FakeAgentProcessLauncher();
        FakeAgentProcessLauncher launcherBravo = new FakeAgentProcessLauncher();
        launcherBravo.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("BRAVO-STILL-ALIVE").ConfigureAwait(false);
            return "end_turn";
        };
        DotAcpAgentHost hostAlpha = DotAcpConcurrentHostTests.CreateHost(launcherAlpha);
        DotAcpAgentHost hostBravo = DotAcpConcurrentHostTests.CreateHost(launcherBravo);
        IAgentSession? sessionAlpha = null;
        IAgentSession? sessionBravo = null;

        try
        {
            await Task.WhenAll(
                hostAlpha.StartAsync(cancellationToken),
                hostBravo.StartAsync(cancellationToken));

            sessionAlpha = await hostAlpha.StartSessionAsync(
                DotAcpConcurrentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);
            sessionBravo = await hostBravo.StartSessionAsync(
                DotAcpConcurrentHostTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            await hostAlpha.DisposeAsync();

            PromptResult result = await sessionBravo.PromptAsync("still there?", cancellationToken);

            AgentEvent eventBravo = await sessionBravo.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

            Assert.Equal(StopReason.EndTurn, result.StopReason);
            Assert.Equal(new MessageChunk(sessionBravo.SessionId, "BRAVO-STILL-ALIVE"), eventBravo);
        }
        finally
        {
            if (sessionAlpha is not null)
            {
                await sessionAlpha.DisposeAsync();
            }

            if (sessionBravo is not null)
            {
                await sessionBravo.DisposeAsync();
            }

            await hostAlpha.DisposeAsync();
            await hostBravo.DisposeAsync();
        }
    }

    private static DotAcpAgentHost CreateHost(FakeAgentProcessLauncher launcher)
    {
        return new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
    }

    private static AgentSessionOptions CreateSessionOptions(string cwd)
    {
        return new AgentSessionOptions(cwd, new AutoApprovePermissionHandler());
    }

    private static JsonObject MakeInitializeResult(string agentName, string agentVersion)
    {
        return new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentInfo"] = new JsonObject
            {
                ["name"] = agentName,
                ["version"] = agentVersion,
            },
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = false,
            },
            ["authMethods"] = new JsonArray(),
        };
    }
}
