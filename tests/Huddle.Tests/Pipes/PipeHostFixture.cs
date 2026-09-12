using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO.Pipes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// Hosts a real <see cref="Agency.Huddle.App"/> composition root (via <see cref="ServiceCollectionExtensions.AddTeamServices"/>)
/// bound to a unique named pipe and an isolated temp data directory, so pipe end-to-end tests can connect real
/// <see cref="NamedPipeClientStream"/> clients against it.
/// </summary>
/// <remarks>
/// This fixture never lets the composition root's own <see cref="PersonaSupervisor"/> hosted service run — see
/// <see cref="RemovePersonaSupervisorHostedService"/> for why that hosted service is pulled back out right after
/// <see cref="ServiceCollectionExtensions.AddTeamServices"/> registers it.
/// </remarks>
public sealed class PipeHostFixture : IAsyncDisposable
{
    private readonly TempDataDir dataDir;
    private readonly IHost host;

    private PipeHostFixture(IHost host, string pipeName, TempDataDir dataDir)
    {
        this.host = host;
        this.PipeName = pipeName;
        this.dataDir = dataDir;
    }

    public IServiceProvider Services => this.host.Services;

    public string PipeName { get; }

    public static Task<PipeHostFixture> StartAsync(CancellationToken ct = default) => StartAsync(null, ct);

    public static async Task<PipeHostFixture> StartAsync(
        IReadOnlyDictionary<string, string?>? additionalConfig, CancellationToken ct = default)
    {
        var dataDir = new TempDataDir();
        var pipeName = "team-test-" + Guid.NewGuid().ToString("N");

        var config = new Dictionary<string, string?>
        {
            ["Team:PipeName"] = pipeName,
            ["Team:DataDir"] = dataDir.Path,
            ["Team:HumanName"] = "You",
            ["Team:DemoAgent:Enabled"] = "false",
            ["Team:Acp:Enabled"] = "false",
        };

        if (additionalConfig is not null)
        {
            foreach (var pair in additionalConfig)
            {
                config[pair.Key] = pair.Value;
            }
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(config);
        builder.Services.AddTeamServices(builder.Configuration);
        RemovePersonaSupervisorHostedService(builder.Services);

        var host = builder.Build();
        await host.StartAsync(ct);

        return new PipeHostFixture(host, pipeName, dataDir);
    }

    public async Task<JsonLineStream> ConnectClientAsync(CancellationToken ct = default)
    {
        var client = new NamedPipeClientStream(".", this.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);
        return new JsonLineStream(client);
    }

    public async ValueTask DisposeAsync()
    {
        await this.host.StopAsync();
        this.host.Dispose();
        this.dataDir.Dispose();
    }

    /// <summary>
    /// Removes the <see cref="PersonaSupervisor"/> <see cref="IHostedService"/> that
    /// <see cref="ServiceCollectionExtensions.AddTeamServices"/> just registered, so this fixture's <see cref="IHost"/>
    /// never starts one of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Agency.Huddle.Tests.Acp.PersonaSupervisorTests"/> sets <c>Team:Acp:Enabled=true</c> on this fixture and then
    /// builds its own <see cref="PersonaSupervisor"/> by hand, wired to a <see cref="Agency.Huddle.Tests.Acp.Fakes.FakeAgentHostFactory"/>,
    /// so it never has to launch a real "node" adapter to prove the supervisor's behaviour. Before this method existed,
    /// the fixture's composition root ran a second, DI-owned <see cref="PersonaSupervisor"/> alongside that hand-built
    /// one — both watching the same Persona directory. Each one started its own <see cref="PersonaRunner"/> per Persona,
    /// and both runners dialed the same named pipe and sent their own <c>Hello</c> for the same Persona name. Whichever
    /// registered second won: <see cref="Agency.Huddle.App.Pipes.AgentGateway.Register"/> treats a same-id registration as a
    /// reconnect and closes the earlier connection as stale (correctly — that is by design for a real reconnect). For
    /// the runner that lost, that meant its own connection got closed out from under it while it was still parked on
    /// <see cref="Agency.Huddle.App.Acp.PersonaRunner.StartAsync"/>'s read waiting for its <c>Welcome</c>, so it threw
    /// "Expected a Welcome envelope for Persona '...' but received end of stream." Which runner lost the race was
    /// timing-dependent, so the tests passed alone and failed intermittently in the full suite — <see
    /// cref="Agency.Huddle.Tests.Acp.PersonaSupervisorTests.PersonaRemoved_StopsItsHost"/> and
    /// <see cref="Agency.Huddle.Tests.Acp.PersonaSupervisorTests.Shutdown_DisposesEveryHost"/> were the ones caught doing it.
    /// It looked like a supervisor bug; it was two supervisors.
    /// </para>
    /// <para>
    /// Removing the registration also keeps <c>docs/agencyteam/rules.md</c> row 26 honest ("ACP is off by default
    /// and spends real money... both test fixtures pin it false"): with the hosted service gone, no test running
    /// against this fixture can end up resolving the real <see cref="Agency.Huddle.App.Acp.DotAcpAgentHostFactory"/> and
    /// spawning a real <c>node</c> adapter process, no matter what it sets <c>Team:Acp:Enabled</c> to — a test that
    /// wants a supervisor now has to build one itself, against a fake factory, exactly as every existing test already
    /// does.
    /// </para>
    /// </remarks>
    /// <param name="services">The fixture's service collection, already populated by <see cref="ServiceCollectionExtensions.AddTeamServices"/>.</param>
    private static void RemovePersonaSupervisorHostedService(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(PersonaSupervisor))
            ?? throw new InvalidOperationException(
                $"Expected {nameof(ServiceCollectionExtensions.AddTeamServices)} to register {nameof(PersonaSupervisor)} " +
                "as an IHostedService, but no such registration was found. Has it been renamed or moved?");

        services.Remove(descriptor);
    }
}