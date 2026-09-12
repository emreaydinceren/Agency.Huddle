namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

/// <summary>
/// Covers mapping the "thought_level" <c>configOptions</c> entry onto
/// <see cref="IAgentSession.EffortLevels"/>, resolving a requested
/// <see cref="AgentSessionOptions.Effort"/> and sending it via
/// <c>session/set_config_option</c>, and the post-model-switch restructure that makes both of
/// those read the snapshot true AFTER the model has been applied rather than the original
/// <c>session/new</c> response.
/// </summary>
public sealed class DotAcpAgentHostEffortTests
{
    /// <summary>A flat "thought_level" configOptions entry maps onto <see cref="IAgentSession.EffortLevels"/>.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_MapsThoughtLevelConfigOptionToEffortLevels()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostEffortTests.CreateFlatNewSessionResultWithEffort());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            Assert.Equal(
                new[]
                {
                    new AgentEffortOption("default", "Default", null),
                    new AgentEffortOption("low", "Low", null),
                    new AgentEffortOption("high", "High", null),
                },
                session.EffortLevels);
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

    /// <summary>No "thought_level" configOptions entry leaves <see cref="IAgentSession.EffortLevels"/> empty, and the session still starts.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_WithNoThoughtLevelConfigOption_LeavesEffortLevelsEmptyAndStartsAnyway()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            // Empty, not null - the model reports no effort support, or the agent never advertised
            // the option at all; either way the session must still start.
            Assert.Empty(session.EffortLevels);
            Assert.Equal("sess-1", session.SessionId);
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

    /// <summary>A grouped effort catalog still resolves and is sent via <c>session/set_config_option</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_GroupedEffortOptions_StillResolvesAndSetsConfigOption()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostEffortTests.CreateGroupedNewSessionResultWithEffort());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: null, effort: "max"), cancellationToken);

            Assert.Contains(session.EffortLevels, level => level.Id == "max");

            JsonObject setConfigOption = await launcher.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(2));
            JsonNode parameters = setConfigOption["params"]!;
            Assert.Equal("thinking", (string?)parameters["configId"]);
            Assert.Equal("max", (string?)parameters["value"]);
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

    /// <summary>An effort id absent from the catalog logs a warning and does not stop the session from starting.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_EffortNotInCatalog_WarnsAndStartsOnTheDefault()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostEffortTests.CreateFlatNewSessionResultWithEffort());
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: null, effort: "ultra-nonexistent"), cancellationToken);

            Assert.Equal("sess-1", session.SessionId);
            Assert.Contains(loggerFactory.Logger.Entries, entry => entry.Level == LogLevel.Warning);
            Assert.DoesNotContain(
                launcher.Agent.Received,
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal)
                    && string.Equals((string?)message["params"]?["configId"], "thinking", StringComparison.Ordinal));
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

    /// <summary>The adapter rejecting the effort set logs a warning, and the session remains usable afterwards.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_AdapterRejectsEffort_WarnsAndTheSessionIsStillUsable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostEffortTests.CreateFlatNewSessionResultWithEffort());
        launcher.Agent.OnSetConfigOption = parameters =>
        {
            if (string.Equals((string?)parameters["configId"], "thinking", StringComparison.Ordinal))
            {
                throw new FakeRpcError(-32000, "effort rejected");
            }

            return Task.FromResult(new JsonObject());
        };
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: null, effort: "low"), cancellationToken);

            Assert.Contains(loggerFactory.Logger.Entries, entry => entry.Level == LogLevel.Warning);

            // The rejection must not have broken the session: it can still take a prompt.
            PromptResult result = await session.PromptAsync("hello", cancellationToken);
            Assert.Equal(StopReason.EndTurn, result.StopReason);
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

    /// <summary>A null effort sends no <c>session/set_config_option</c> call at all.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_EffortNull_DoesNotCallSetConfigOptionForEffort()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostEffortTests.CreateFlatNewSessionResultWithEffort());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath()), cancellationToken);

            Assert.DoesNotContain(
                launcher.Agent.Received,
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));
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

    /// <summary>The set_config_option call for effort writes configId (not id) and a "select" type.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_WritesConfigIdAndSelectTypeForEffort()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(DotAcpAgentHostEffortTests.CreateFlatNewSessionResultWithEffort());
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: null, effort: "low"), cancellationToken);

            JsonObject setConfigOption = await launcher.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(2));
            JsonNode parameters = setConfigOption["params"]!;

            // configId, not id - mirrors the model wire-shape test: this is the property the wire
            // actually requires, even though the adapter's own docs mis-name it.
            Assert.Equal("thinking", (string?)parameters["configId"]);
            Assert.Null(parameters["id"]);
            Assert.Equal("select", (string?)parameters["type"]);
            Assert.Equal("low", (string?)parameters["value"]);
            Assert.Equal("sess-1", (string?)parameters["sessionId"]);
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

    /// <summary>Builds a <c>session/new</c> result advertising a flat "thought_level" select with a "thinking" id.</summary>
    private static JsonObject CreateFlatNewSessionResultWithEffort()
    {
        return new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "select",
                    ["id"] = "thinking",
                    ["name"] = "Effort",
                    ["description"] = "Available effort levels for this model",
                    ["category"] = "thought_level",
                    ["currentValue"] = "default",
                    ["options"] = new JsonArray
                    {
                        new JsonObject { ["value"] = "default", ["name"] = "Default" },
                        new JsonObject { ["value"] = "low", ["name"] = "Low" },
                        new JsonObject { ["value"] = "high", ["name"] = "High" },
                    },
                },
            },
        };
    }

    /// <summary>Effort must resolve against the POST-model-switch snapshot, not the pre-switch session/new response.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_EffortResolvesAgainstThePostModelSwitchOptions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "high"]),
        });
        launcher.Agent.OnSetConfigOption = parameters =>
        {
            if (string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal))
            {
                return Task.FromResult(new JsonObject
                {
                    ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "max"]),
                });
            }

            return Task.FromResult(new JsonObject());
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: "opus", effort: "max"), cancellationToken);

            // "max" only exists in the POST-switch snapshot ("high" is what session/new advertised
            // pre-switch); a second set_config_option firing at all, let alone with value "max",
            // proves effort resolved against the right snapshot.
            List<JsonObject> setConfigOptionCalls = launcher.Agent.Received.FindAll(
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));

            Assert.Equal(2, setConfigOptionCalls.Count);
            JsonNode effortParameters = setConfigOptionCalls[1]["params"]!;
            Assert.Equal("thinking", (string?)effortParameters["configId"]);
            Assert.Equal("max", (string?)effortParameters["value"]);
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

    /// <summary><see cref="IAgentSession.EffortLevels"/> reflects the POST-model-switch catalog, not the pre-switch one.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_EffortLevelsComeFromThePostModelSwitchSnapshot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "high"]),
        });
        launcher.Agent.OnSetConfigOption = parameters =>
        {
            if (string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal))
            {
                return Task.FromResult(new JsonObject
                {
                    ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "max"]),
                });
            }

            return Task.FromResult(new JsonObject());
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: "opus", effort: null), cancellationToken);

            Assert.Contains(session.EffortLevels, level => level.Id == "max");
            Assert.DoesNotContain(session.EffortLevels, level => level.Id == "high");
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

    /// <summary>When the model set is rejected, effort must fall back to resolving against the session/new snapshot.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_ModelSetRejected_EffortResolvesAgainstTheSessionNewSnapshot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "high"]),
        });
        launcher.Agent.OnSetConfigOption = parameters =>
        {
            if (string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal))
            {
                throw new FakeRpcError(-32000, "model rejected");
            }

            return Task.FromResult(new JsonObject());
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: "opus", effort: "high"), cancellationToken);

            // The model set was rejected, so ApplyModelAsync must fall back to the session/new
            // snapshot - which advertised "high" - and effort resolution must still succeed there.
            List<JsonObject> effortCalls = launcher.Agent.Received.FindAll(
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal)
                    && string.Equals((string?)message["params"]?["configId"], "thinking", StringComparison.Ordinal));

            Assert.Single(effortCalls);
            Assert.Equal("high", (string?)effortCalls[0]["params"]!["value"]);
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

    /// <summary>The model's set_config_option call must be sent strictly before the effort's.</summary>
    [Fact(Timeout = 10000)]
    public async Task StartSessionAsync_SetsTheModelBeforeTheEffort()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
        launcher.Agent.OnNewSession = _ => Task.FromResult(new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "high"]),
        });
        launcher.Agent.OnSetConfigOption = parameters =>
        {
            if (string.Equals((string?)parameters["configId"], "model", StringComparison.Ordinal))
            {
                return Task.FromResult(new JsonObject
                {
                    ["configOptions"] = DotAcpAgentHostEffortTests.CreateModelAndEffortConfigOptions(["default", "low", "high"]),
                });
            }

            return Task.FromResult(new JsonObject());
        };
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(),
            new ListLoggerFactory());
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                DotAcpAgentHostEffortTests.CreateSessionOptions(Path.GetTempPath(), model: "opus", effort: "low"), cancellationToken);

            List<JsonObject> received = launcher.Agent.Received;
            int modelIndex = received.FindIndex(
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal)
                    && string.Equals((string?)message["params"]?["configId"], "model", StringComparison.Ordinal));
            int effortIndex = received.FindIndex(
                message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal)
                    && string.Equals((string?)message["params"]?["configId"], "thinking", StringComparison.Ordinal));

            Assert.True(modelIndex >= 0);
            Assert.True(effortIndex >= 0);
            Assert.True(modelIndex < effortIndex);
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

    /// <summary>Builds a "model" + "thought_level" configOptions array, with the given effort values, fresh on every call.</summary>
    private static JsonArray CreateModelAndEffortConfigOptions(IEnumerable<string> effortValues)
    {
        JsonArray effortOptions = new JsonArray();
        foreach (string value in effortValues)
        {
            effortOptions.Add(new JsonObject { ["value"] = value, ["name"] = value });
        }

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
                ["options"] = effortOptions,
            },
        };
    }

    /// <summary>Builds a <c>session/new</c> result advertising a grouped "thought_level" select with a "thinking" id.</summary>
    private static JsonObject CreateGroupedNewSessionResultWithEffort()
    {
        return new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "select",
                    ["id"] = "thinking",
                    ["name"] = "Effort",
                    ["category"] = "thought_level",
                    ["currentValue"] = "default",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["group"] = "standard",
                            ["name"] = "Standard",
                            ["options"] = new JsonArray
                            {
                                new JsonObject { ["value"] = "default", ["name"] = "Default" },
                            },
                        },
                        new JsonObject
                        {
                            ["group"] = "extended",
                            ["name"] = "Extended",
                            ["options"] = new JsonArray
                            {
                                new JsonObject { ["value"] = "low", ["name"] = "Low" },
                                new JsonObject { ["value"] = "max", ["name"] = "Max" },
                            },
                        },
                    },
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
