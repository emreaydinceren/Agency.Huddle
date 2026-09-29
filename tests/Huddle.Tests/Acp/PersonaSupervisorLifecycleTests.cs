using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;
using static Agency.Huddle.Tests.Acp.PersonaSupervisorTestSupport;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// <see cref="PersonaSupervisor"/>'s host lifecycle: which hosts it starts, and when a changed or
/// unchanged Persona does or does not restart one. Split from the former single
/// <c>PersonaSupervisorTests</c> class so the three behaviours run as parallel collections; the
/// shared builders live in <see cref="PersonaSupervisorTestSupport"/>.
/// </summary>
public sealed class PersonaSupervisorLifecycleTests
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
        WritePersonaFile(options, "nova");

        using var personaStore = new PersonaStore(
            new TeammatePaths(options),
            new Agency.Huddle.App.Data.PersonaModelStore(options),
            new Agency.Huddle.App.Data.PersonaEffortStore(options),
            NullLogger<PersonaStore>.Instance);
        var factory = new FakeAgentHostFactory();
        var resolver = new AdapterProfileResolver(new AdapterCatalog(options));
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "nova");
        WritePersonaFile(options, "zeta");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
    /// is exactly the value <see cref="DotAcpAgentHostFactory.StartAsync"/> reads
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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var directory = fixture.Services.GetRequiredService<Agency.Huddle.App.Data.ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();
        var factory = new FakeAgentHostFactory();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "bad");
        WritePersonaFile(options, "good");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var inner = new FakeAgentHostFactory();
        var factory = new FailingForOneAgentHostFactory("bad", new InvalidOperationException("Simulated failure starting Persona 'bad'."), inner);
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "zeta");
        var factory = new FakeAgentHostFactory();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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

    /// <summary>
    /// Regression guard for Spec §14 D-15: <c>skills</c> lives on <see cref="PersonaEntry"/> and
    /// <see cref="PersonaIdentity"/>, never on <see cref="Persona"/> itself. Every debounced watcher
    /// event - even one about a wholly unrelated, malformed file - rebuilds the whole
    /// <see cref="PersonaIndex"/> from scratch, so "zeta"'s <see cref="PersonaEntry.Skills"/> is a
    /// brand-new list on every refresh. If <see cref="Persona"/> ever grew a list-typed Skills
    /// member, <see cref="PersonaSupervisor"/>'s record-equality <c>NeedsRestart</c> check would
    /// compare that freshly built list against the one captured at start by reference - never
    /// equal - and restart every Teammate holding any Skill on every single refresh, related or
    /// not. Deliberately drives the refresh through the real <see cref="FileSystemWatcher"/> and its
    /// debounce (rather than <see cref="PersonaStore.Add"/>, as the other tests in this class use),
    /// since that is the actual trigger path this guard is about. The touched file is malformed on
    /// purpose - missing <c>Name</c> - so it is rejected rather than started, isolating the
    /// assertion to "zeta" alone: nothing else ever calls into <see cref="FakeAgentHostFactory"/>.
    /// </summary>
    [Fact]
    public async Task OnPersonasChanged_UnrelatedFileEvent_DoesNotRestartPersonaWithSkills()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var paths = new TeammatePaths(options);
        TestPersonaFiles.Write(paths, "zeta", SkillsPersonaText("zeta"));
        var factory = new FakeAgentHostFactory();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => factory.Calls.Count >= 1, ct);

        var personasChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        personaStore.PersonasChanged += () => personasChanged.TrySetResult();

        // A different Persona file - missing the required "Name" field, so it is rejected rather
        // than started. Only the debounced watcher event matters here, not a second host.
        TestPersonaFiles.Write(paths, "malformed", "---\nTitle: Malformed\nAlias: mal\n---\nbody");

        using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = waitCts.Token.Register(() => personasChanged.TrySetCanceled());
        await personasChanged.Task;

        // Bounded grace period past the watcher's own debounce settling, so OnPersonasChanged's
        // per-name loop (including "zeta") has had time to run before this asserts.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        await supervisor.StopAsync(ct);

        Assert.Single(factory.Calls);
    }

    /// <summary>
    /// Pins Spec §1.3 (O-2) and Spec §7.3 ("Why this gets restart-on-change for free"): two <see cref="Persona"/>
    /// records differing only in <see cref="Persona.Adapter"/> are unequal. <see cref="PersonaSupervisor"/>'s
    /// restart check - <c>private static bool NeedsRestart(Persona persona, Persona? started) =&gt; started is
    /// null || persona != started</c> - is exactly this record comparison, and it is <c>private</c> with no test
    /// seam of its own, so there is nothing else to call here: this inequality IS the restart guarantee Spec §7.3
    /// describes, proven directly rather than through the supervisor's internals.
    /// </summary>
    [Fact]
    public void Persona_DifferingOnlyInAdapter_AreUnequal()
    {
        Persona started = new("nova", "You are Nova.", Model: null, Effort: null, Adapter: "claude");

        Persona changed = started with { Adapter = "agency" };

        Assert.NotEqual(started, changed);
    }
}