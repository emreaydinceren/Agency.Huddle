using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises <see cref="PersonaSupervisor"/> directly (not through <see cref="Agency.Huddle.App.ServiceCollectionExtensions"/>)
/// against a <see cref="FakeAgentHostFactory"/>-backed <see cref="PersonaRunner"/> connected over a real pipe (via
/// <see cref="PipeHostFixture"/>), so no test launches <c>node</c> or spends a token.
/// </summary>
public sealed class PersonaSupervisorTests
{
    [Fact]
    public async Task Disabled_StartsNoHosts()
    {
        // Money-safety test: starting an Agent Host spends real money on the user's Claude
        // subscription (agent-guide.md §3.1). With Team:Acp:Enabled left at its default (false,
        // which is what PipeHostFixture's base config sets), the supervisor must start nothing
        // even though a Persona exists on disk.
        var ct = TestContext.Current.CancellationToken;

        using var dataDir = new TempDataDir();
        var options = dataDir.Options();
        WritePersonaFile(options.Value, "nova");

        using var personaStore = new PersonaStore(
            options,
            new Agency.Huddle.App.Data.PersonaModelStore(options),
            new Agency.Huddle.App.Data.PersonaEffortStore(options),
            NullLogger<PersonaStore>.Instance);
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await supervisor.StartAsync(startCts.Token);
        await supervisor.StopAsync(ct);

        Assert.Empty(factory.Calls);
        Assert.Equal(0, supervisor.RunningHostCount);
    }

    [Fact]
    public async Task Enabled_StartsOneHostPerPersonaFile()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");
        WritePersonaFile(options.Value, "zeta");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Equal(2, factory.Calls.Count);
        Assert.Contains(factory.Calls, call => call.Persona.Name == "nova");
        Assert.Contains(factory.Calls, call => call.Persona.Name == "zeta");
    }

    [Fact]
    public async Task PersonaAddedAtRuntime_StartsAHostImmediately()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        Assert.Equal(0, supervisor.RunningHostCount);

        personaStore.Add(Identity("nova"), "You are Nova.");

        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);
        await supervisor.StopAsync(ct);

        Assert.Single(factory.Calls);
        Assert.Equal("nova", factory.Calls[0].Persona.Name);
    }

    [Fact]
    public async Task Enabled_PassesThePersonaModelToTheFactory()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.", "claude-opus-4");

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        await supervisor.StopAsync(ct);

        Assert.Equal("claude-opus-4", factory.Calls[0].Persona.Model);
    }

    /// <summary>
    /// Mirrors <see cref="Enabled_PassesThePersonaModelToTheFactory"/>. <see cref="IAgentHostFactory"/>
    /// is the boundary <see cref="DotAcpAgentHostFactory"/> sits behind - the real implementation is
    /// untestable here without launching a real "node" adapter process (rules.md row 26), so this is
    /// the closest a unit test gets to proving an Effort makes it to the factory: it shows the whole
    /// Persona, Effort included, reaches whatever <see cref="IAgentHostFactory"/> is configured, which
    /// is exactly the value <see cref="DotAcpAgentHostFactory.CreateAsync"/> reads
    /// <c>persona.Effort</c> off of to build its <c>AgentSessionOptions</c>.
    /// </summary>
    [Fact]
    public async Task Enabled_PassesThePersonaEffortToTheFactory()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.", "claude-opus-4", "high");

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);
        await supervisor.StopAsync(ct);

        Assert.Equal("high", factory.Calls[0].Persona.Effort);
    }

    [Fact]
    public async Task Shutdown_DisposesEveryHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        var novaId = await WaitForAgentOnlineAsync(directory, gateway, "nova", ct);

        await supervisor.StopAsync(ct);

        Assert.Equal(0, supervisor.RunningHostCount);
        await WaitUntilAsync(() => !gateway.IsOnline(novaId), ct);
    }

    [Fact]
    public async Task OnePersonaFailingToStart_DoesNotStopTheOthers()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "bad");
        WritePersonaFile(options.Value, "good");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var inner = new FakeAgentHostFactory();
        var factory = new FailingForOneAgentHostFactory("bad", new InvalidOperationException("Simulated failure starting Persona 'bad'."), inner);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        // Bounded grace period: give a wrongly-crashed supervisor every chance to also start
        // (or fail to start) "good" before asserting the final state.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        var runningBeforeShutdown = supervisor.RunningHostCount;
        await supervisor.StopAsync(ct);

        Assert.Equal(1, runningBeforeShutdown);
        Assert.Single(inner.Calls);
        Assert.Equal("good", inner.Calls[0].Persona.Name);
    }

    [Fact]
    public async Task PersonaRemoved_StopsItsHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        // Two personas, so removing one has something to leave alone: "zeta" proves the
        // supervisor stops only the removed persona's host, not every host it manages.
        personaStore.Add(Identity("nova"), "You are Nova.");
        personaStore.Add(Identity("zeta"), "You are Zeta.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 2, ct);
        var novaId = await WaitForAgentOnlineAsync(directory, gateway, "nova", ct);
        var zetaId = await WaitForAgentOnlineAsync(directory, gateway, "zeta", ct);

        personaStore.Remove("nova");

        await WaitUntilAsync(() => supervisor.RunningHostCount == 1, ct);
        await WaitUntilAsync(() => !gateway.IsOnline(novaId), ct);

        // "nova"'s host was stopped and taken offline, while "zeta"'s host, never removed, is
        // still up: the supervisor reacted to the specific persona removed, not to every host.
        Assert.Equal(1, supervisor.RunningHostCount);
        Assert.False(gateway.IsOnline(novaId));
        Assert.True(gateway.IsOnline(zetaId));
        Assert.Equal(2, factory.Calls.Count);
        Assert.Contains(factory.Calls, call => call.Persona.Name == "nova");
        Assert.Contains(factory.Calls, call => call.Persona.Name == "zeta");

        await supervisor.StopAsync(ct);

        Assert.Equal(0, supervisor.RunningHostCount);
    }

    [Fact]
    public async Task PersonaTextChanged_RestartsItsHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        personaStore.Update("nova", PersonaText("nova", "You are a changed Nova."), model: null, effort: null);

        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Equal(2, factory.Calls.Count);
        Assert.All(factory.Calls, call => Assert.Equal("nova", call.Persona.Name));
        Assert.Equal(PersonaText("nova", "You are a changed Nova."), factory.Calls[^1].Persona.Text);
    }

    [Fact]
    public async Task PersonaModelChanged_RestartsItsHost()
    {
        // This is the test that proves a database-only change (the text never changes) still
        // reaches the runner: PersonaStore.Get joins the model in, and PersonaSupervisor compares
        // the whole Persona, not just its text.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.", "a");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        personaStore.Update("nova", PersonaText("nova", "You are Nova."), "b", effort: null);

        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Equal(2, factory.Calls.Count);
        Assert.Equal("b", factory.Calls[^1].Persona.Model);
        Assert.Equal(PersonaText("nova", "You are Nova."), factory.Calls[^1].Persona.Text);
    }

    [Fact]
    public async Task PersonaUnchanged_DoesNotRestartItsHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        WritePersonaFile(options.Value, "zeta");
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        // Adding an unrelated Persona raises PersonasChanged without touching "zeta"'s file, which
        // is exactly the shape of event a debounced watcher can deliver after a no-op save.
        personaStore.Add(Identity("other"), "You are Other.");
        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);

        // Bounded grace period: prove no further, unwanted restart of "zeta" happens.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        await supervisor.StopAsync(ct);

        Assert.Single(factory.Calls, call => call.Persona.Name == "zeta");
    }

    [Fact]
    public async Task PersonaModelUnchanged_DoesNotRestartItsHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("zeta"), "You are a persona.", "a");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        // Same text AND same model: PersonasChanged still fires (Update always raises it), but
        // there is nothing for the supervisor to act on. Resubmitting the text Add itself just
        // composed - rather than a separately re-typed PersonaText string - is what makes this a
        // genuine no-op: Add's own frontmatter (lowercase keys, quoted scalars) does not match
        // PersonaText's capitalised, unquoted style, so re-typing it here would be a real text
        // change and a real restart, defeating the point of this test.
        var unchangedText = personaStore.Get("zeta")!.Text;
        personaStore.Update("zeta", unchangedText, "a", effort: null);

        // Adding an unrelated Persona proves PersonasChanged was actually observed after the
        // no-op Update above, without which the absence of a second "zeta" call would be
        // meaningless.
        personaStore.Add(Identity("other"), "You are Other.");
        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);

        // Bounded grace period: prove no further, unwanted restart of "zeta" happens.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        await supervisor.StopAsync(ct);

        Assert.Single(factory.Calls, call => call.Persona.Name == "zeta");
    }

    [Fact]
    public async Task PersonaEffortChanged_RestartsItsHost()
    {
        // Mirrors PersonaModelChanged_RestartsItsHost: text and Model are identical, only Effort
        // changes, and that alone must still be enough for NeedsRestart's record comparison to fire.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.", "a", "low");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        personaStore.Update("nova", PersonaText("nova", "You are Nova."), "a", "high");

        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);
        await supervisor.StopAsync(ct);

        Assert.Equal(2, factory.Calls.Count);
        Assert.Equal("high", factory.Calls[^1].Persona.Effort);
        Assert.Equal("a", factory.Calls[^1].Persona.Model);
        Assert.Equal(PersonaText("nova", "You are Nova."), factory.Calls[^1].Persona.Text);
    }

    [Fact]
    public async Task PersonaEffortUnchanged_DoesNotRestartItsHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("zeta"), "You are a persona.", "a", "high");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        // Same text, Model AND Effort: PersonasChanged still fires (Update always raises it), but
        // there is nothing for the supervisor to act on. See PersonaModelUnchanged_DoesNotRestartItsHost
        // for why the text resubmitted here has to be whatever Add itself just composed.
        var unchangedText = personaStore.Get("zeta")!.Text;
        personaStore.Update("zeta", unchangedText, "a", "high");

        // Adding an unrelated Persona proves PersonasChanged was actually observed after the
        // no-op Update above, without which the absence of a second "zeta" call would be
        // meaningless.
        personaStore.Add(Identity("other"), "You are Other.");
        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);

        // Bounded grace period: prove no further, unwanted restart of "zeta" happens.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        await supervisor.StopAsync(ct);

        Assert.Single(factory.Calls, call => call.Persona.Name == "zeta");
    }

    /// <summary>An adapter-not-installed <see cref="InvalidOperationException"/> - the same shape <c>DotAcpAgentHostFactory</c> throws - is recorded as Offline, carrying the exception's own actionable message.</summary>
    [Fact]
    public async Task MissingAdapter_IsRecordedAsOfflineWithItsReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new InvalidOperationException(
            "No ACP adapter is installed for Persona 'nova'. Run tools/acp/install.ps1 "
            + "(or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.");
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Offline, status.State);
        Assert.Contains("tools/acp/install.ps1", status.Reason, StringComparison.Ordinal);
    }

    /// <summary>An <see cref="AgentAuthenticationRequiredException"/> is recorded as Offline, and when it carries auth methods their names are the actionable text in the reason.</summary>
    [Fact]
    public async Task AuthenticationRequired_IsRecordedAsOfflineAndNamesTheAuthMethods()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new AgentAuthenticationRequiredException([new AuthMethodInfo("oauth", "OAuth", null)]);
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Offline, status.State);
        Assert.Contains("OAuth", status.Reason, StringComparison.Ordinal);
    }

    /// <summary>An <see cref="AgentProcessStartException"/> is recorded as Offline, carrying the exception's own message verbatim - it already names the path and the OS error.</summary>
    [Fact]
    public async Task AdapterProcessFailingToStart_IsRecordedAsOfflineWithItsReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new AgentProcessStartException("Failed to start 'node'.", new IOException("No such file or directory"));
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Offline, status.State);
        Assert.Equal("Failed to start 'node'.", status.Reason);
    }

    /// <summary>A bare <see cref="AgentException"/> - the shape a failed <c>session/new</c> call throws - is recorded as Offline, carrying its own message verbatim.</summary>
    [Fact]
    public async Task SessionNewFailing_IsRecordedAsOfflineWithItsReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new AgentException("session/new failed: the adapter rejected the request.");
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Offline, status.State);
        Assert.Equal("session/new failed: the adapter rejected the request.", status.Reason);
    }

    /// <summary>An exception type nothing here anticipated still produces a reason - never a blank badge - naming the exception's own type and message.</summary>
    [Fact]
    public async Task AnUnanticipatedException_StillProducesAReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new FormatException("weird format.");
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Offline, status.State);
        Assert.Equal("FormatException: weird format.", status.Reason);
    }

    /// <summary>Removing a Persona - which stops and disposes its host - also drops its now-stale health entry, rather than leaving an Online badge for an Agent that no longer exists.</summary>
    [Fact]
    public async Task StoppingAHost_RemovesItsHealthEntry()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);
        Assert.NotNull(health.Get("nova"));

        personaStore.Remove("nova");

        await WaitUntilAsync(() => health.Get("nova") is null, ct);
        await supervisor.StopAsync(ct);

        Assert.Null(health.Get("nova"));
    }

    /// <summary>
    /// T7.2's whole reason for existing: a Persona whose adapter failed to start is not stuck until
    /// the entire app restarts. <see cref="PersonaSupervisor.RestartAsync"/> on a Persona with no
    /// currently running host must start it, not no-op.
    /// </summary>
    [Fact]
    public async Task RestartAsync_StartsAPersonaThatFailedToStart()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        WritePersonaFile(options.Value, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var inner = new FakeAgentHostFactory();
        var factory = new FailOnceThenSucceedAgentHostFactory(
            "nova", new InvalidOperationException("Simulated failure starting Persona 'nova'."), inner);
        var health = NewHealth();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, health, new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => health.Get("nova") is { State: PersonaState.Offline }, ct);
        Assert.Equal(0, supervisor.RunningHostCount);

        await supervisor.RestartAsync("nova", ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        Assert.Equal(1, supervisor.RunningHostCount);
        Assert.Equal(PersonaState.Online, health.Get("nova")?.State);

        await supervisor.StopAsync(ct);
    }

    /// <summary>A Restart of an already-running Persona stops its old host and starts a new one - a second factory call for the same Persona, never a second, concurrent host.</summary>
    [Fact]
    public async Task RestartAsync_ReplacesARunningHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        await supervisor.RestartAsync("nova", ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 2, ct);

        Assert.Equal(1, supervisor.RunningHostCount);
        await supervisor.StopAsync(ct);

        Assert.Equal(2, factory.Calls.Count);
        Assert.All(factory.Calls, call => Assert.Equal("nova", call.Persona.Name));
    }

    /// <summary>
    /// Two concurrent Restarts of the same Persona is exactly the race the shared <c>restarting</c>
    /// set (also used by the automatic file-change restart) exists to stop - a Restart button is the
    /// new way to provoke it. The second call must return harmlessly rather than throw or start a
    /// second host racing the first.
    /// </summary>
    [Fact]
    public async Task RestartAsync_IsSafeWhenCalledTwiceConcurrently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, NewHealth(), new FakeHookSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        var first = supervisor.RestartAsync("nova", ct);
        var second = supervisor.RestartAsync("nova", ct);
        await Task.WhenAll(first, second);

        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);

        // Bounded grace period: give a wrongly-racing supervisor every chance to start a second host.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        Assert.Equal(1, supervisor.RunningHostCount);
        await supervisor.StopAsync(ct);
    }

    /// <summary>Builds a fresh <see cref="PersonaHealth"/> against the real clock - nothing in this file asserts against <see cref="PersonaStatus.Since"/> precisely enough to need a controllable one.</summary>
    private static PersonaHealth NewHealth() => new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);

    private static void WritePersonaFile(TeamOptions options, string name, string body = "You are a persona.")
    {
        var teamsDir = Path.Combine(options.DataDir, options.Acp.TeamsDir);
        Directory.CreateDirectory(teamsDir);
        File.WriteAllText(Path.Combine(teamsDir, $"{name}.md"), PersonaText(name, body));
    }

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/> - identity is front-matter driven from this phase on, so every seeded Persona needs one to be discoverable at all.</summary>
    private static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";

    /// <summary>A valid <see cref="PersonaIdentity"/> for <paramref name="name"/>, with Title and Alias both <paramref name="name"/> too and no Teams - the structured input <see cref="PersonaStore.Add"/> now takes.</summary>
    private static PersonaIdentity Identity(string name) => new(name, name, name, []);

    private static async Task<string> WaitForAgentOnlineAsync(
        Agency.Huddle.App.Data.ITeamDirectory directory, Agency.Huddle.App.Pipes.IAgentGateway gateway, string agentName, CancellationToken ct)
    {
        while (true)
        {
            var user = await directory.FindUserByNameAsync(agentName, ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                return user.Id;
            }

            await Task.Delay(50, ct);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            await Task.Delay(50, ct);
        }
    }

    /// <summary>A test double for <see cref="IAgentHostFactory"/> that throws a caller-supplied exception for one named Persona and otherwise delegates to <paramref name="inner"/>.</summary>
    private sealed class FailingForOneAgentHostFactory(string failingPersonaName, Exception exception, FakeAgentHostFactory inner) : IAgentHostFactory
    {
        public Task<(IAgentHost Host, IAgentSession Session)> CreateAsync(Persona persona, string agentId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(persona);

            if (string.Equals(persona.Name, failingPersonaName, StringComparison.Ordinal))
            {
                throw exception;
            }

            return inner.CreateAsync(persona, agentId, cancellationToken);
        }
    }

    /// <summary>
    /// A test double for <see cref="IAgentHostFactory"/> that throws a caller-supplied exception the
    /// FIRST time it is asked to create the named Persona's host, and delegates to
    /// <paramref name="inner"/> every time after that - the shape a Restart actually fixes (the
    /// adapter got installed, authentication completed) rather than one that keeps failing forever.
    /// </summary>
    private sealed class FailOnceThenSucceedAgentHostFactory(string failingPersonaName, Exception exception, FakeAgentHostFactory inner) : IAgentHostFactory
    {
        private bool hasFailedOnce;

        public Task<(IAgentHost Host, IAgentSession Session)> CreateAsync(Persona persona, string agentId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(persona);

            if (!this.hasFailedOnce && string.Equals(persona.Name, failingPersonaName, StringComparison.Ordinal))
            {
                this.hasFailedOnce = true;
                throw exception;
            }

            return inner.CreateAsync(persona, agentId, cancellationToken);
        }
    }
}