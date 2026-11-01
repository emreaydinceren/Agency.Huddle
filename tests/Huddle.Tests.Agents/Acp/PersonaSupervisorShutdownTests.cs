using System.Collections.Concurrent;
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

    /// <summary>
    /// Six hosts must be stopped concurrently: each host's disposal is held open until every host has
    /// begun disposing, which a sequential loop never reaches (the first host would wait forever for the
    /// sixth). Proving it with an event instead of a stopwatch keeps it exact under CPU load, where a
    /// 200 ms delay times six stretched past the old wall-clock bound.
    /// </summary>
    [Fact]
    public async Task StopAsync_ManyHosts_StopsThemConcurrently()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        TaskCompletionSource allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int startedCount = 0;
        ConcurrentBag<FakePersonaHost> hosts = [];
        await using var started = await StartSupervisorAsync(host =>
        {
            host.OnDispose = () =>
            {
                if (Interlocked.Increment(ref startedCount) == PersonaCount)
                {
                    allStarted.SetResult();
                }
            };
            host.DisposeGate = allStarted.Task;
            hosts.Add(host);
        }, cts);

        await WaitUntilAsync(() => started.Supervisor.RunningHostCount == PersonaCount && hosts.Count == PersonaCount, ct);

        await started.Supervisor.StopAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.All(hosts, host => Assert.True(host.Disposed));
        Assert.Equal(PersonaCount, Volatile.Read(ref startedCount));
    }

    /// <summary>
    /// One host whose disposal throws must not skip the others - a skipped host is an Adapter process
    /// that outlives the app - and the stop itself still completes, because the caller can do nothing
    /// useful with the failure and the process is exiting either way.
    /// </summary>
    [Fact]
    public async Task StopAsync_OneHostThrows_StillStopsEveryOtherHost()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        ConcurrentBag<FakePersonaHost> hosts = [];
        await using var started = await StartSupervisorAsync(host =>
        {
            host.DisposeFailure = hosts.IsEmpty ? new InvalidOperationException("adapter would not stop") : null;
            hosts.Add(host);
        }, cts);

        await WaitUntilAsync(() => started.Supervisor.RunningHostCount == PersonaCount && hosts.Count == PersonaCount, ct);

        await started.Supervisor.StopAsync(ct);

        Assert.All(hosts, host => Assert.True(host.Disposed));
        Assert.Equal(0, started.Supervisor.RunningHostCount);
    }

    /// <summary>Starts a fixture, then a supervisor over <see cref="PersonaCount"/> Personas whose hosts report to <paramref name="onHostStarted"/>.</summary>
    /// <param name="onHostStarted">Called with each host as it starts.</param>
    /// <param name="budget">The test's behaviour budget, paused while the fixture starts.</param>
    private static async Task<StartedSupervisor> StartSupervisorAsync(Action<FakePersonaHost> onHostStarted, BehaviourBudget budget)
    {
        var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:Acp:Enabled"] = "true" }, budget);
        CancellationToken ct = budget.Token;
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
