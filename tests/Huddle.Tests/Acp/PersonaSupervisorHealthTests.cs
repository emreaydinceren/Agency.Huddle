using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;
using static Agency.Huddle.Tests.Acp.PersonaSupervisorTestSupport;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// What <see cref="PersonaSupervisor"/> records in <see cref="PersonaHealth"/> when a host starts,
/// fails to start or is stopped. Split from the former single <c>PersonaSupervisorTests</c> class;
/// the shared builders live in <see cref="PersonaSupervisorTestSupport"/>.
/// </summary>
public sealed class PersonaSupervisorHealthTests
{
    /// <summary>
    /// Pins Spec §8.2 and Spec §4 (P4 - "degrade, never reject"): a Persona whose <see cref="Persona.Adapter"/>
    /// names an id no <see cref="AdapterProfile"/> is configured for is not rejected. <see cref="PersonaSupervisor"/>
    /// must report <see cref="PersonaState.Degraded"/> naming the unknown id, and it must still create and start a
    /// runner on the default profile - asserted here via <see cref="FakeAgentHostFactory.Calls"/>, because a test
    /// that only checked for Degraded would pass even if the Persona had failed to start at all, which is exactly
    /// the defect P4 rules out.
    /// </summary>
    [Fact]
    public async Task UnknownAdapter_IsRecordedAsDegradedAndStillStartsAHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        personaStore.Add(Identity("nova") with { Adapter = "bogus-adapter" }, "You are Nova.");

        await supervisor.StartAsync(ct);

        // Waiting on Degraded alone races StopAsync against the still-in-flight start: the
        // resolver's warning is reported before host.StartAsync is even awaited (see the comment
        // in PersonaSupervisor.StartHostIfMissingAsync), so health can already read Degraded before
        // the runner has registered with the factory. Waiting for both is what actually proves the
        // runner started, which is the point of this test.
        await WaitUntilAsync(
            () => health.Get("nova") is { State: PersonaState.Degraded } && factory.Calls.Any(call => call.Persona.Name == "nova"),
            ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Degraded, status.State);
        Assert.Contains("bogus-adapter", status.Reason, StringComparison.Ordinal);
        Assert.Single(factory.Calls, call => call.Persona.Name == "nova");
    }

    /// <summary>
    /// Pins Spec §12 F-1 and use case U14, the Skills-side twin of
    /// <see cref="UnknownAdapter_IsRecordedAsDegradedAndStillStartsAHost"/>: a Persona whose frontmatter
    /// names a Skill <see cref="Agency.Huddle.App.Skills.SkillStore"/> does not resolve is not rejected
    /// either. <see cref="PersonaSupervisor"/> must report <see cref="PersonaState.Degraded"/> with the
    /// Skill's own warning text and must still create and start a runner - asserted via
    /// <see cref="FakeAgentHostFactory.Calls"/>, for the same reason the Adapter test asserts it: a test
    /// that only checked for Degraded would pass even if the Persona had failed to start at all. As of
    /// this task, <see cref="PersonaSupervisor"/>'s constructor does not yet take a
    /// <see cref="Agency.Huddle.App.Skills.SkillStore"/> (Task 5.2.i wires it in), so nothing here yet
    /// resolves the unknown Skill name into this Degraded report - the Persona starts and stays Online,
    /// which is what fails <see cref="Assert.Equal{T}(T, T)"/> below.
    /// </summary>
    [Fact]
    public async Task Start_UnknownSkill_ReportsDegradedWithReason()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        personaStore.Add(Identity("nova") with { Skills = ["nonexistent"] }, "You are Nova.");

        await supervisor.StartAsync(ct);

        // Waiting for the factory call alone (rather than for Degraded too, as the Adapter test does)
        // is deliberate here: nothing in this constructor can report Degraded for a Skill yet, so
        // waiting on that condition as well would hang until the outer CancellationTokenSource fires
        // and fail with a cancellation, not the clean assertion failure this task calls for.
        await WaitUntilAsync(() => factory.Calls.Any(call => call.Persona.Name == "nova"), ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Degraded, status.State);
        Assert.Equal("Skill 'nonexistent' does not exist.", status.Reason);
        Assert.Single(factory.Calls, call => call.Persona.Name == "nova");
    }

    /// <summary>
    /// Pins FC §6.10: a Persona whose frontmatter declares a <c>watches</c> entry
    /// <see cref="Agency.Huddle.App.FileChanges.FileChangeTracker.CheckDeclared"/> flags is not
    /// rejected either. <see cref="PersonaSupervisor"/> must report <see cref="PersonaState.Degraded"/>
    /// with the tracker's own warning text and must still create and start a runner - the same
    /// "degrade, never reject" shape as the Adapter and Skill tests above.
    /// </summary>
    [Fact]
    public async Task Start_UnresolvableWatchesEntry_ReportsDegradedWithWarning()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, tracker);

        personaStore.Add(Identity("nova") with { Watches = ["Nope"] }, "You are Nova.");

        await supervisor.StartAsync(ct);

        await WaitUntilAsync(() => factory.Calls.Any(call => call.Persona.Name == "nova"), ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Degraded, status.State);
        var dataDirName = Path.GetFileName(options.Value.DataDir);
        Assert.Equal($"Watched folder 'Nope' is not a Teammate or a folder inside {dataDirName}.", status.Reason);
        Assert.Single(factory.Calls, call => call.Persona.Name == "nova");
    }

    /// <summary>The Adapter, Skill and Watches warnings are all independent sources of one Degraded report (FC §6.10) and join in that order: Adapter first, then Skills, then Watches.</summary>
    [Fact]
    public async Task Start_WarningsJoinAdapterSkillAndWatches()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        using var supervisor = new PersonaSupervisor(
            options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore, tracker);

        personaStore.Add(Identity("nova") with { Adapter = "bogus-adapter", Skills = ["nonexistent"], Watches = ["Nope"] }, "You are Nova.");

        await supervisor.StartAsync(ct);

        await WaitUntilAsync(
            () => health.Get("nova") is { State: PersonaState.Degraded } && factory.Calls.Any(call => call.Persona.Name == "nova"),
            ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Degraded, status.State);
        var reason = status.Reason!;
        var dataDirName = Path.GetFileName(options.Value.DataDir);
        var adapterIndex = reason.IndexOf("bogus-adapter", StringComparison.Ordinal);
        var skillIndex = reason.IndexOf("Skill 'nonexistent' does not exist.", StringComparison.Ordinal);
        var watchIndex = reason.IndexOf($"Watched folder 'Nope' is not a Teammate or a folder inside {dataDirName}.", StringComparison.Ordinal);
        Assert.True(adapterIndex >= 0, reason);
        Assert.True(skillIndex > adapterIndex, reason);
        Assert.True(watchIndex > skillIndex, reason);
    }

    /// <summary>
    /// FC §6.11: with the resolved Adapter Profile's <c>ReadsFiles</c> false and the Persona's
    /// frontmatter declaring a non-empty <c>watches</c> list, the supervisor reports the "ignored"
    /// warning instead of <see cref="Agency.Huddle.App.FileChanges.FileChangeTracker.CheckDeclared"/>'s
    /// - and does so independent of whether a <see cref="Agency.Huddle.App.FileChanges.FileChangeTracker"/>
    /// was even injected, which this test's construction call deliberately omits.
    /// </summary>
    [Fact]
    public async Task Start_ProfileReadsFilesFalse_WarnsWatchesIgnored()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:Acp:Enabled"] = "true",
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "agency-acp",
                ["Team:Acp:Adapters:0:ReadsFiles"] = "false",
            },
            ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var factory = new FakeAgentHostFactory();
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        personaStore.Add(Identity("nova") with { Adapter = "agency", Watches = ["Shared"] }, "You are Nova.");

        await supervisor.StartAsync(ct);

        await WaitUntilAsync(() => factory.Calls.Any(call => call.Persona.Name == "nova"), ct);
        await supervisor.StopAsync(ct);

        var status = health.Get("nova");
        Assert.NotNull(status);
        Assert.Equal(PersonaState.Degraded, status.State);
        Assert.Equal("Watched folders are ignored: the Adapter 'agency' cannot read files.", status.Reason);
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
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new InvalidOperationException(
            "No ACP adapter is installed for Persona 'nova'. Run tools/acp/install.ps1 "
            + "(or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.");
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new AgentAuthenticationRequiredException([new AuthMethodInfo("oauth", "OAuth", null)]);
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new AgentProcessStartException("Failed to start 'node'.", new IOException("No such file or directory"));
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new AgentException("session/new failed: the adapter rejected the request.");
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        WritePersonaFile(options, "nova");

        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var exception = new FormatException("weird format.");
        var factory = new FailingForOneAgentHostFactory("nova", exception, new FakeAgentHostFactory());
        var health = NewHealth();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

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
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        using var skillStore = NewSkillStore(options);
        using var supervisor = new PersonaSupervisor(options, personaStore, factory, resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        personaStore.Add(Identity("nova"), "You are Nova.");
        await supervisor.StartAsync(ct);
        await WaitUntilAsync(() => supervisor.RunningHostCount >= 1, ct);
        Assert.NotNull(health.Get("nova"));

        personaStore.Remove("nova");

        await WaitUntilAsync(() => health.Get("nova") is null, ct);
        await supervisor.StopAsync(ct);

        Assert.Null(health.Get("nova"));
    }
}