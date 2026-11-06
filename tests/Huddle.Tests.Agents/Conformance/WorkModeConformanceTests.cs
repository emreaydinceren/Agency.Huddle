using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Drives a real Persona host against the scripted Adapter peer and checks what reaches the wire for a
/// Persona's Work Mode (ADR-0033): the stored mode is applied after the session opens, and a hidden one
/// is never sent. No process is spawned and no tokens are spent.
/// </summary>
public sealed class WorkModeConformanceTests
{
    /// <summary>A stored Work Mode the Adapter advertises is sent with <c>session/set_config_option</c>, under the option's own id.</summary>
    [Fact]
    public async Task PersonaWithAWorkMode_TheAdvertisedModeIsSetWhenTheSessionOpens()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "acceptEdits");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, cancellationToken: ct, scriptAgent: WorkModeConformanceTests.AdvertiseModes);

        JsonObject call = await fixture.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(5));
        JsonNode parameters = call["params"] ?? throw new InvalidOperationException("The call carried no params.");
        Assert.Equal("permission", (string?)parameters["configId"]);
        Assert.Equal("acceptEdits", (string?)parameters["value"]);
        Assert.Equal("select", (string?)parameters["type"]);
    }

    /// <summary>A Persona with no Work Mode causes no <c>session/set_config_option</c> at all: the wire is what it was before modes.</summary>
    [Fact]
    public async Task PersonaWithoutAWorkMode_NothingIsSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, cancellationToken: ct, scriptAgent: WorkModeConformanceTests.AdvertiseModes);
        await fixture.SessionNewAsync(ct);
        await fixture.DisposeAsync();

        Assert.DoesNotContain(
            fixture.Agent.Received,
            message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));
    }

    /// <summary>
    /// A stored row for a hidden mode is never sent, even though the Adapter advertises it: the hidden
    /// list is enforced when the session opens, so a hand-edited database row is inert.
    /// </summary>
    [Theory]
    [InlineData("bypassPermissions")]
    [InlineData("auto")]
    [InlineData("plan")]
    public async Task PersonaWithAHiddenWorkMode_ItIsNeverSent(string hiddenMode)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: hiddenMode);

        MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, cancellationToken: ct, scriptAgent: WorkModeConformanceTests.AdvertiseModes);
        await fixture.SessionNewAsync(ct);
        await fixture.DisposeAsync();

        Assert.DoesNotContain(
            fixture.Agent.Received,
            message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));
    }

    /// <summary>
    /// With <c>Team:Acp:HiddenModes</c> lifted by an operator, the same stored mode is sent: the block is
    /// a policy, not a refusal built into the wire.
    /// </summary>
    [Fact]
    public async Task HiddenModesLifted_TheStoredModeIsSent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "auto");
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:Acp:HiddenModes:0"] = string.Empty };

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, config, cancellationToken: ct, scriptAgent: WorkModeConformanceTests.AdvertiseModes);

        JsonObject call = await fixture.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(5));
        Assert.Equal("auto", (string?)call["params"]?["value"]);
    }

    /// <summary>
    /// Issue #414: a user's own <c>permissions.defaultMode</c> puts a new session in <c>auto</c>. A Persona with no Work Mode on
    /// an isolating Adapter is moved out of it, so the session runs in the mode Huddle chose and not the one the user's settings named.
    /// </summary>
    [Fact]
    public async Task SessionOpensInUsersAuto_NoWorkMode_HuddleSetsManual()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, cancellationToken: ct, scriptAgent: static agent => WorkModeConformanceTests.AdvertiseModes(agent, "auto"));

        JsonObject call = await fixture.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(5));
        Assert.Equal("default", (string?)call["params"]?["value"]);
    }

    /// <summary>A Work Mode the Human chose still wins over the user's <c>auto</c>, and over Huddle's own choice.</summary>
    [Fact]
    public async Task SessionOpensInUsersAuto_ChosenWorkMode_IsTheOneSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "acceptEdits");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, cancellationToken: ct, scriptAgent: static agent => WorkModeConformanceTests.AdvertiseModes(agent, "auto"));

        JsonObject call = await fixture.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(5));
        Assert.Equal("acceptEdits", (string?)call["params"]?["value"]);
    }

    /// <summary>A stored row for a hidden mode is dropped, and the session still leaves the user's <c>auto</c>.</summary>
    [Fact]
    public async Task SessionOpensInUsersAuto_HiddenWorkMode_HuddleSetsManual()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", WorkMode: "bypassPermissions");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, cancellationToken: ct, scriptAgent: static agent => WorkModeConformanceTests.AdvertiseModes(agent, "auto"));

        JsonObject call = await fixture.Agent.WaitForAsync("session/set_config_option", TimeSpan.FromSeconds(5));
        Assert.Equal("default", (string?)call["params"]?["value"]);
    }

    /// <summary>
    /// An Adapter entry that does not opt in to isolation keeps the Adapter's own mode: the operator was warned at startup that
    /// the Human's settings load there, and other Adapters advertise other modes.
    /// </summary>
    [Fact]
    public async Task SessionOpensInUsersAuto_AdapterNotIsolated_NothingIsSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "plain");
        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "plain",
            ["Team:Acp:Adapters:0:Command"] = "plain-acp",
        };

        MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona, config, cancellationToken: ct, scriptAgent: static agent => WorkModeConformanceTests.AdvertiseModes(agent, "auto"));
        await fixture.SessionNewAsync(ct);
        await fixture.DisposeAsync();

        Assert.DoesNotContain(
            fixture.Agent.Received,
            message => string.Equals((string?)message["method"], "session/set_config_option", StringComparison.Ordinal));
    }

    /// <summary>Scripts the peer to advertise the five modes <c>claude-agent-acp</c> advertises, under the id "permission".</summary>
    private static void AdvertiseModes(Agency.Huddle.Acp.Tests.Fakes.FakeAcpAgent agent) => WorkModeConformanceTests.AdvertiseModes(agent, "default");

    /// <summary>Scripts the peer's modes, with a new session reporting <paramref name="initial"/> as the current one.</summary>
    /// <param name="agent">The peer.</param>
    /// <param name="initial">The mode a new session starts in, standing for the Adapter's reading of the user's settings.</param>
    private static void AdvertiseModes(Agency.Huddle.Acp.Tests.Fakes.FakeAcpAgent agent, string initial)
    {
        agent.OnNewSession = _ => Task.FromResult(new JsonObject
        {
            ["sessionId"] = "sess-1",
            ["configOptions"] = WorkModeConformanceTests.ModeOptions(initial),
        });
        agent.OnSetConfigOption = parameters => Task.FromResult(new JsonObject
        {
            ["configOptions"] = WorkModeConformanceTests.ModeOptions((string?)parameters["value"] ?? "default"),
        });
    }

    private static JsonArray ModeOptions(string current)
    {
        JsonArray options = [];
        foreach (string id in new[] { "default", "acceptEdits", "plan", "auto", "bypassPermissions" })
        {
            options.Add(new JsonObject { ["value"] = id, ["name"] = id });
        }

        return
        [
            new JsonObject
            {
                ["type"] = "select",
                ["id"] = "permission",
                ["name"] = "Mode",
                ["category"] = "mode",
                ["currentValue"] = current,
                ["options"] = options,
            },
        ];
    }
}
