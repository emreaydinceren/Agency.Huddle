using System.Collections.Concurrent;
using System.Diagnostics;
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
/// How <see cref="PersonaSupervisor.StopAsync"/> tears down every running Persona host: together, so a
/// graceful stop costs about one Adapter grace period rather than one per Persona, and without one
/// host's failure ever leaving another's Adapter process running.
/// </summary>
public sealed class PersonaSupervisorShutdownTests
{
    private const int PersonaCount = 6;

    private static readonly TimeSpan HostStopTime = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Six hosts that each take 200 ms to stop must be stopped concurrently: the sequential loop took
    /// about 1200 ms, the concurrent one about 200 ms, and the bound leaves wide room for a loaded CI
    /// machine without ever admitting the sequential time.
    /// </summary>
    [Fact]
    public async Task StopAsync_ManyHosts_StopsThemConcurrently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        ConcurrentBag<FakePersonaHost> hosts = [];
        await using var started = await StartSupervisorAsync(host =>
        {
            host.DisposeDelay = HostStopTime;
            hosts.Add(host);
        }, ct);

        await WaitUntilAsync(() => started.Supervisor.RunningHostCount == PersonaCount && hosts.Count == PersonaCount, ct);

        var stopwatch = Stopwatch.StartNew();
        await started.Supervisor.StopAsync(ct);
        stopwatch.Stop();

        Assert.All(hosts, host => Assert.True(host.Disposed));
        Assert.True(
            stopwatch.Elapsed < HostStopTime * (PersonaCount / 2),
            $"Stopping {PersonaCount} hosts of {HostStopTime.TotalMilliseconds} ms each took {stopwatch.Elapsed.TotalMilliseconds} ms; they were stopped one after another.");
    }

    /// <summary>
    /// One host whose disposal throws must not skip the others - a skipped host is an Adapter process
    /// that outlives the app - and the stop itself still completes, because the caller can do nothing
    /// useful with the failure and the process is exiting either way.
    /// </summary>
    [Fact]
    public async Task StopAsync_OneHostThrows_StillStopsEveryOtherHost()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        ConcurrentBag<FakePersonaHost> hosts = [];
        await using var started = await StartSupervisorAsync(host =>
        {
            host.DisposeFailure = hosts.IsEmpty ? new InvalidOperationException("adapter would not stop") : null;
            hosts.Add(host);
        }, ct);

        await WaitUntilAsync(() => started.Supervisor.RunningHostCount == PersonaCount && hosts.Count == PersonaCount, ct);

        await started.Supervisor.StopAsync(ct);

        Assert.All(hosts, host => Assert.True(host.Disposed));
        Assert.Equal(0, started.Supervisor.RunningHostCount);
    }

    private static async Task<StartedSupervisor> StartSupervisorAsync(Action<FakePersonaHost> onHostStarted, CancellationToken ct)
    {
        var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var personaStore = fixture.Services.GetRequiredService<PersonaStore>();
        var resolver = fixture.Services.GetRequiredService<AdapterProfileResolver>();
        var skillStore = NewSkillStore(options);
        FakeAgentHostFactory factory = new() { OnHostStarted = onHostStarted };
        PersonaSupervisor supervisor = new(
            options, personaStore, factory, resolver, NewHealth(), new FakePromptSource(), new RoomFollows(),
            NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);

        for (var index = 0; index < PersonaCount; index++)
        {
            personaStore.Add(Identity($"persona{index}"), "You are a persona.");
        }

        await supervisor.StartAsync(ct);
        return new StartedSupervisor(fixture, skillStore, supervisor);
    }

    /// <summary>Owns what <see cref="StartSupervisorAsync"/> builds, so a test disposes all of it with one <see langword="await using"/>.</summary>
    private sealed class StartedSupervisor(PipeHostFixture fixture, Agency.Huddle.App.Skills.SkillStore skillStore, PersonaSupervisor supervisor) : IAsyncDisposable
    {
        /// <summary>The supervisor under test, already started.</summary>
        public PersonaSupervisor Supervisor { get; } = supervisor;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            this.Supervisor.Dispose();
            skillStore.Dispose();
            await fixture.DisposeAsync();
        }
    }
}
