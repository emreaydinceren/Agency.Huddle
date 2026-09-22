using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Drives one real <see cref="PersonaRunner"/> — the real <see cref="IAgentHostFactory"/>
/// (<c>DotAcpAgentHostFactory</c>), the real <c>DotAcpAgentHost</c> and ACP session, the real
/// <c>AppToolServer</c> and the real prompt composition — against a scripted <see cref="FakeAcpAgent"/>
/// reachable over an in-process duplex stream pair, with no child process ever spawned. This is the
/// Tier 3 harness Spec §15.8 ("the part that was previously missing") and Task 10.1 call for: every
/// existing test stopped either at a fake <c>IAgentHostFactory</c> or spawned a real <c>node</c>
/// adapter, and this closes that gap.
/// </summary>
/// <remarks>
/// <para>
/// The one substitution is the process launch: <see cref="ServiceCollectionExtensions.AddTeamServices"/>
/// registers <c>IAgentProcessLauncher</c> as the real <c>AgentProcessLauncher</c>, and this fixture
/// swaps in a <see cref="FakeAgentProcessLauncher"/> — an existing ACP-effort test double, already
/// proven against the real <c>DotAcpAgentHost</c> in <c>DotAcpAgentHostTests</c> — instead of adding
/// any new one. That is the whole seam: <c>DotAcpAgentHostFactory</c> already took its launcher as a
/// dependency for <c>DotAcpAgentHost</c> to accept; the only change this task needed in <c>src/</c>
/// was to stop that factory constructing its own launcher inline and instead accept one through DI,
/// exactly as CSharpPrinciples.md's "constructor injection, always" already asks for.
/// </para>
/// <para>
/// Built the same way <see cref="PipeHostFixture"/> is — in-memory configuration, a real
/// <see cref="IHost"/>, <see cref="PipeHostFixture.RemovePersonaSupervisorHostedService"/> — because a
/// hand-built <see cref="PersonaRunner"/> must be the only thing dialling the Persona's name onto the
/// pipe; a live <c>PersonaSupervisor</c> alongside it is the exact intermittent-registration race that
/// method's own remarks document. <see cref="TeamOptions.Acp"/>'s <c>Enabled</c> is <see langword="true"/>
/// here — the one fixture in this test project allowed to say so outside a scoped override, because
/// nothing in it ever reaches a real adapter process or spends a token (rules.md: "ACP is off by
/// default and spends real money").
/// </para>
/// </remarks>
public sealed class MockAdapterFixture : IAsyncDisposable
{
    private readonly IHost host;
    private readonly TempDataDir dataDir;
    private readonly PersonaRunner runner;
    private bool disposed;

    private MockAdapterFixture(IHost host, TempDataDir dataDir, PersonaRunner runner, FakeAcpAgent agent, string pipeName)
    {
        this.host = host;
        this.dataDir = dataDir;
        this.runner = runner;
        this.Agent = agent;
        this.PipeName = pipeName;
    }

    /// <summary>
    /// The scripted ACP peer the Persona's session is actually talking to. Tests read
    /// <see cref="FakeAcpAgent.Received"/> and <see cref="FakeAcpAgent.WaitForAsync"/> to assert on
    /// what the real host sent, and set its <c>On*</c> handlers before calling <see cref="StartAsync"/>
    /// to script what it answers.
    /// </summary>
    internal FakeAcpAgent Agent { get; }

    /// <summary>
    /// The one real <see cref="PersonaRunner"/> this fixture drives. Exposed so a test can subscribe
    /// to <see cref="PersonaRunner.StatusChanged"/> directly: this fixture hand-builds the runner
    /// rather than going through <c>PersonaSupervisor</c> (removed as a hosted service, see this
    /// type's own remarks), so nothing here forwards that event into <c>PersonaHealth</c> the way the
    /// real app does. A test proving rules.md's "a Stop reports no health state" needs the runner's
    /// own event, not <c>PersonaHealth</c>, because nothing in this fixture ever wires the two
    /// together.
    /// </summary>
    internal PersonaRunner Runner => this.runner;

    /// <summary>The started fixture's own composition root, for resolving anything a conformance test needs beyond <see cref="Agent"/> — <c>ChatService</c>, <c>IChatStore</c>, <c>RoomEvents</c> and so on.</summary>
    public IServiceProvider Services => this.host.Services;

    /// <summary>The named pipe this fixture's host listens on, for a test that wants its own raw pipe client alongside the Persona's.</summary>
    public string PipeName { get; }

    /// <summary>
    /// Starts a fixture: a real Huddle host with <c>Team:Acp:Enabled</c> true and its process launcher
    /// substituted, then one real <see cref="PersonaRunner"/> for <paramref name="persona"/> against it.
    /// </summary>
    /// <param name="persona">
    /// The Persona to run. Its <see cref="Persona.Adapter"/> selects which configured profile resolves
    /// (Spec §7.2) — leave it <see langword="null"/> for the single synthesised default profile this
    /// fixture's base configuration provides, or set it and add a matching
    /// <c>Team:Acp:Adapters:&lt;n&gt;:*</c> entry via <paramref name="additionalConfig"/> to exercise a
    /// second one.
    /// </param>
    /// <param name="additionalConfig">
    /// Configuration pairs applied after this fixture's own defaults, so a later value overrides an
    /// earlier one of the same key — the same merge <see cref="PipeHostFixture.StartAsync(IReadOnlyDictionary{string,string?}?,CancellationToken)"/>
    /// uses. <see langword="null"/> for none.
    /// </param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>The started fixture.</returns>
    public static async Task<MockAdapterFixture> StartAsync(
        Persona persona,
        IReadOnlyDictionary<string, string?>? additionalConfig = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persona);

        TempDataDir dataDir = new();
        string pipeName = "mock-adapter-test-" + Guid.NewGuid().ToString("N");

        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:PipeName"] = pipeName,
            ["Team:DataDir"] = dataDir.Path,
            ["Team:HumanName"] = "You",
            ["Team:DemoAgent:Enabled"] = "false",
            ["Team:Acp:Enabled"] = "true",

            // A non-empty Args wins over AdapterPath and the locator (AgentProcessOptionsFactory,
            // Spec §6.1), so the default profile resolves with no real adapter installed and never
            // touches AdapterLocator. The value itself is never read: the launcher this fixture
            // installs below launches nothing.
            ["Team:Acp:Args:0"] = "--mock-adapter-fixture",
        };

        if (additionalConfig is not null)
        {
            foreach (KeyValuePair<string, string?> pair in additionalConfig)
            {
                config[pair.Key] = pair.Value;
            }
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(config);
        builder.Services.AddTeamServices(builder.Configuration);
        PipeHostFixture.RemovePersonaSupervisorHostedService(builder.Services);

        // Replaces the real, process-spawning AgentProcessLauncher AddTeamServices just registered
        // with an existing ACP-effort test double that hands DotAcpAgentHost the other end of an
        // in-memory duplex stream instead - see this type's own remarks for why this is the only
        // substitution needed. RemoveAll first so exactly one registration survives to be resolved;
        // AddTeamServices' own AgentProcessLauncher would otherwise never be constructed anyway
        // (the last IAgentProcessLauncher registration wins), but leaving both would misstate intent.
        builder.Services.RemoveAll<IAgentProcessLauncher>();
        FakeAgentProcessLauncher launcher = new();
        builder.Services.AddSingleton<IAgentProcessLauncher>(launcher);

        IHost host = builder.Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        IOptions<TeamOptions> options = host.Services.GetRequiredService<IOptions<TeamOptions>>();
        IAgentHostFactory factory = host.Services.GetRequiredService<IAgentHostFactory>();
        IPromptSource prompts = host.Services.GetRequiredService<IPromptSource>();
        RoomFollows roomFollows = host.Services.GetRequiredService<RoomFollows>();
        ILogger<PersonaRunner> logger = host.Services.GetRequiredService<ILogger<PersonaRunner>>();

        PersonaRunner runner = new(persona, options, factory, prompts, roomFollows, logger);

        try
        {
            await runner.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await runner.DisposeAsync().ConfigureAwait(false);
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
            dataDir.Dispose();
            throw;
        }

        return new MockAdapterFixture(host, dataDir, runner, launcher.Agent, pipeName);
    }

    /// <summary>
    /// Tears the fixture down deterministically: stops the Persona's runner (which itself disposes
    /// its session, its host, and — via <c>DotAcpAgentHostFactory</c>'s <c>ToolServerOwningAgentHost</c>
    /// — its <c>AppToolServer</c>), then stops and disposes the host and its temp data directory. No
    /// step here waits on a fixed delay: every await is a real completion signal from the object being
    /// torn down.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        await this.runner.DisposeAsync().ConfigureAwait(false);
        await this.host.StopAsync().ConfigureAwait(false);
        this.host.Dispose();
        this.dataDir.Dispose();
    }
}
