using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers mapping the "mode" <c>configOptions</c> entry onto <see cref="IAgentSession.ModeOptions"/>
/// and <see cref="IAgentSession.CurrentModeId"/>, resolving a requested
/// <see cref="AgentSessionOptions.Mode"/>, sending it with <c>session/set_config_option</c> AFTER
/// Model and Effort, and reading the response back, because an adapter may clamp what it was asked
/// for. Mirrors <see cref="DotAcpAgentHostEffortTests"/>; the new behaviour is the read-back, and
/// that a resumed session is re-applied only when it reports a different mode.
/// </summary>
public sealed class DotAcpAgentHostModeTests
{
    /// <summary>A flat "mode" configOptions entry maps onto <see cref="IAgentSession.ModeOptions"/> and the current mode.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_MapsModeConfigOptionToModeOptions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "acceptEdits", "plan"]));

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: null), cancellationToken);

        Assert.Equal(
            [
                new AgentModeOption("default", "default", null),
                new AgentModeOption("acceptEdits", "acceptEdits", null),
                new AgentModeOption("plan", "plan", null),
            ],
            session.ModeOptions);
        Assert.Equal("default", session.CurrentModeId);
    }

    /// <summary>No "mode" configOptions entry leaves <see cref="IAgentSession.ModeOptions"/> empty, and the session still starts.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_WithNoModeConfigOption_LeavesModeOptionsEmptyAndStartsAnyway()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: null), cancellationToken);

        Assert.Empty(session.ModeOptions);
        Assert.Null(session.CurrentModeId);
        Assert.Equal("sess-1", session.SessionId);
    }

    /// <summary>A grouped mode catalog still resolves and is sent via <c>session/set_config_option</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_GroupedModeOptions_StillResolvesAndSetsConfigOption()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "select",
                    ["id"] = "permission",
                    ["name"] = "Mode",
                    ["category"] = "mode",
                    ["currentValue"] = "default",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["group"] = "standard",
                            ["name"] = "Standard",
                            ["options"] = new JsonArray { new JsonObject { ["value"] = "default", ["name"] = "Manual" } },
                        },
                        new JsonObject
                        {
                            ["group"] = "planning",
                            ["name"] = "Planning",
                            ["options"] = new JsonArray { new JsonObject { ["value"] = "plan", ["name"] = "Plan" } },
                        },
                    },
                },
            },
        });

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        Assert.Contains(session.ModeOptions, option => option.Id == "plan");
        Assert.Equal(["permission:plan"], harness.SetConfigCalls());
    }

    /// <summary>A mode id absent from the catalog logs a warning and does not stop the session from starting.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModeNotInCatalog_WarnsAndStartsOnTheDefault()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "bypassPermissions"), cancellationToken);

        Assert.Equal("sess-1", session.SessionId);
        Assert.Contains(harness.Logs.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Empty(harness.SetConfigCalls());
        Assert.Equal("default", session.CurrentModeId);
    }

    /// <summary>An adapter that advertises no mode option is a warning for a requested mode, never a failure.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModeRequestedButAdapterAdvertisesNone_WarnsAndStarts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        Assert.Equal("sess-1", session.SessionId);
        Assert.Contains(harness.Logs.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Empty(harness.SetConfigCalls());
    }

    /// <summary>The adapter rejecting the mode set logs a warning, and the session remains usable afterwards.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_AdapterRejectsMode_WarnsAndTheSessionIsStillUsable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));
        harness.Launcher.Agent.OnSetConfigOption = parameters => throw new FakeRpcError(-32000, "mode rejected");

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        Assert.Contains(harness.Logs.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal("default", session.CurrentModeId);
        PromptResult result = await session.PromptAsync("hello", cancellationToken);
        Assert.Equal(StopReason.EndTurn, result.StopReason);
    }

    /// <summary>A null mode sends no <c>session/set_config_option</c> call at all.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModeNull_DoesNotCallSetConfigOption()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));

        _ = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: null), cancellationToken);

        Assert.Empty(harness.SetConfigCalls());
    }

    /// <summary>The set_config_option call for a mode writes configId (not id) and a "select" type.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_WritesConfigIdAndSelectTypeForMode()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));

        _ = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        JsonObject call = await harness.Launcher.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(2));
        JsonNode parameters = call["params"] ?? throw new InvalidOperationException("The call carried no params.");
        Assert.Equal("permission", (string?)parameters["configId"]);
        Assert.Null(parameters["id"]);
        Assert.Equal("select", (string?)parameters["type"]);
        Assert.Equal("plan", (string?)parameters["value"]);
        Assert.Equal("sess-1", (string?)parameters["sessionId"]);
    }

    /// <summary>The three set_config_option calls are sent Model, then Effort, then Mode.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_SetsTheModelThenTheEffortThenTheMode()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));
        harness.Launcher.Agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions("default", ["default", "plan"]),
        });

        _ = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan", model: "opus", effort: "high"), cancellationToken);

        Assert.Equal(["model:opus", "thinking:high", "permission:plan"], harness.SetConfigCalls());
    }

    /// <summary>The mode resolves against the POST-model-switch snapshot, not the pre-switch session/new response.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModeResolvesAgainstThePostModelSwitchOptions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "acceptEdits"]));
        harness.Launcher.Agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal)
                ? DotAcpAgentHostModeTests.ConfigOptions("default", ["default", "acceptEdits", "auto"])
                : null,
        });

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "auto", model: "opus"), cancellationToken);

        // "auto" exists only in the post-switch snapshot, so a mode call firing at all proves it.
        Assert.Equal(["model:opus", "permission:auto"], harness.SetConfigCalls());
        Assert.Contains(session.ModeOptions, option => option.Id == "auto");
    }

    /// <summary>A mode already current is not sent again.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModeAlreadyCurrent_SendsNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("plan", ["default", "plan"]));

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        Assert.Empty(harness.SetConfigCalls());
        Assert.Equal("plan", session.CurrentModeId);
    }

    /// <summary>An applied mode is read back from the response and reported as current.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModeApplied_ReportsTheEffectiveModeFromTheResponse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));
        harness.Launcher.Agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions("plan", ["default", "plan"]),
        });

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        Assert.Equal("plan", session.CurrentModeId);
        Assert.DoesNotContain(harness.Logs.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>A response carrying a different mode than requested is a clamp: warn, and report the effective mode.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_AdapterClampsTheMode_WarnsAndReportsTheEffectiveMode()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "acceptEdits", "auto"]));
        harness.Launcher.Agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions("acceptEdits", ["default", "acceptEdits", "auto"]),
        });

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "auto"), cancellationToken);

        Assert.Equal("acceptEdits", session.CurrentModeId);
        LogEntry warning = Assert.Single(harness.Logs.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal("The agent did not apply requested mode 'auto'; the session runs in 'acceptEdits'.", warning.Message);
    }

    /// <summary>A response that carries no mode option is unverifiable, not a clamp: the requested mode is reported and nothing warns.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ResponseWithoutConfigOptions_IsNotTreatedAsAClamp()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostModeTests.NewSessionResult("default", ["default", "plan"]));

        IAgentSession session = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: "plan"), cancellationToken);

        Assert.Equal("plan", session.CurrentModeId);
        Assert.DoesNotContain(harness.Logs.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>A resumed session that reports a different mode is set to the requested one.</summary>
    [Fact(Timeout = 10000)]
    public async Task ResumeSession_ReportsADifferentMode_AppliesTheRequestedMode()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnResumeSession = _ => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions("default", ["default", "acceptEdits"]),
        });
        harness.Launcher.Agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions("acceptEdits", ["default", "acceptEdits"]),
        });
        _ = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: null), cancellationToken);

        IAgentSession resumed = await harness.ResumeAsync("sess-1", DotAcpAgentHostModeTests.Options(mode: "acceptEdits"), cancellationToken);

        Assert.Equal(["permission:acceptEdits"], harness.SetConfigCalls());
        Assert.Equal("acceptEdits", resumed.CurrentModeId);
    }

    /// <summary>A resumed session that already reports the requested mode is not sent it again.</summary>
    [Fact(Timeout = 10000)]
    public async Task ResumeSession_ReportsTheRequestedMode_SendsNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Launcher.Agent.OnResumeSession = _ => Task.FromResult(new JsonObject
        {
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions("acceptEdits", ["default", "acceptEdits"]),
        });
        _ = await harness.StartAsync(DotAcpAgentHostModeTests.Options(mode: null), cancellationToken);

        IAgentSession resumed = await harness.ResumeAsync("sess-1", DotAcpAgentHostModeTests.Options(mode: "acceptEdits"), cancellationToken);

        Assert.Empty(harness.SetConfigCalls());
        Assert.Equal("acceptEdits", resumed.CurrentModeId);
    }

    /// <summary>Builds a <c>session/new</c> result carrying model, effort and mode options.</summary>
    private static JsonObject NewSessionResult(string currentMode, string[] modeValues)
    {
        return new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = DotAcpAgentHostModeTests.ConfigOptions(currentMode, modeValues),
        };
    }

    /// <summary>Builds a "model" + "thought_level" + "mode" configOptions array, fresh on every call. The mode id is deliberately "permission".</summary>
    private static JsonArray ConfigOptions(string currentMode, string[] modeValues)
    {
        JsonArray modeOptions = [];
        foreach (string value in modeValues)
        {
            modeOptions.Add(new JsonObject { ["value"] = value, ["name"] = value });
        }

        return
        [
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
                    new JsonObject { ["value"] = "high", ["name"] = "High" },
                },
            },
            new JsonObject
            {
                ["type"] = "select",
                ["id"] = "permission",
                ["name"] = "Mode",
                ["category"] = "mode",
                ["currentValue"] = currentMode,
                ["options"] = modeOptions,
            },
        ];
    }

    /// <summary>Builds session options with the given model, effort and mode requested.</summary>
    private static AgentSessionOptions Options(string? mode, string? model = null, string? effort = null)
    {
        return new AgentSessionOptions(Path.GetTempPath(), new AutoApprovePermissionHandler(), model: model, effort: effort, mode: mode);
    }

    /// <summary>One started host over the fake agent, disposed with its sessions.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly List<IAgentSession> sessions = [];
        private readonly DotAcpAgentHost host;

        /// <summary>Creates the host over a fresh fake launcher and a recording logger.</summary>
        public Harness()
        {
            this.Launcher = new FakeAgentProcessLauncher();
            this.Logs = new ListLoggerFactory();
            this.host = new DotAcpAgentHost(new AgentProcessOptions("fake", []), this.Launcher, new DotAcpHostOptions(), this.Logs);
        }

        /// <summary>The fake launcher whose <c>Agent</c> the test scripts.</summary>
        public FakeAgentProcessLauncher Launcher { get; }

        /// <summary>The recording logger factory.</summary>
        public ListLoggerFactory Logs { get; }

        /// <summary>Starts the host if needed, then a session.</summary>
        /// <param name="options">The session options.</param>
        /// <param name="cancellationToken">Cancels the start.</param>
        /// <returns>The started session.</returns>
        public async Task<IAgentSession> StartAsync(AgentSessionOptions options, CancellationToken cancellationToken)
        {
            await this.host.StartAsync(cancellationToken);
            IAgentSession session = await this.host.StartSessionAsync(options, cancellationToken);
            this.sessions.Add(session);
            return session;
        }

        /// <summary>Resumes a session by id.</summary>
        /// <param name="sessionId">The id to resume.</param>
        /// <param name="options">The session options.</param>
        /// <param name="cancellationToken">Cancels the resume.</param>
        /// <returns>The resumed session.</returns>
        public async Task<IAgentSession> ResumeAsync(string sessionId, AgentSessionOptions options, CancellationToken cancellationToken)
        {
            IAgentSession session = await this.host.ResumeSessionAsync(sessionId, options, cancellationToken);
            this.sessions.Add(session);
            return session;
        }

        /// <summary>The <c>configId:value</c> of every <c>session/set_config_option</c> received, in order.</summary>
        /// <returns>The calls, oldest first.</returns>
        public List<string> SetConfigCalls()
        {
            return this.Launcher.Agent.Received
                .Where(message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal))
                .Select(message => $"{(string?)message["params"]?["configId"]}:{(string?)message["params"]?["value"]}")
                .ToList();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            foreach (IAgentSession session in this.sessions)
            {
                await session.DisposeAsync();
            }

            await this.host.DisposeAsync();
        }
    }
}
