using System.Text.Json.Nodes;
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
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Sessions;
using Agency.Huddle.Tests.Helpers;
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
    private readonly TempDataDir? dataDir;
    private readonly PersonaRunner runner;
    private bool disposed;

    private MockAdapterFixture(IHost host, TempDataDir? dataDir, string dataDirPath, PersonaRunner runner, FakeAcpAgent agent, FakeHelperProcessRunner helperProcess, string pipeName)
    {
        this.host = host;
        this.dataDir = dataDir;
        this.DataDirPath = dataDirPath;
        this.runner = runner;
        this.Agent = agent;
        this.HelperProcess = helperProcess;
        this.PipeName = pipeName;
    }

    /// <summary>
    /// The fake the Helper reaches instead of a real adapter process (the same double <c>TeamWebApplicationFactory</c> installs). A test
    /// sets its <see cref="FakeHelperProcessRunner.Text"/> or handler, and counts <see cref="FakeHelperProcessRunner.Calls"/>.
    /// </summary>
    internal FakeHelperProcessRunner HelperProcess { get; }

    /// <summary>The data directory the host runs on. Pass it as <c>dataDirPath</c> to <see cref="StartAsync"/> to restart on the same files.</summary>
    public string DataDirPath { get; }

    /// <summary>
    /// A reply the AGENTS.md Helper accepts: a meaningful verdict with one change quoting <paramref name="quote"/>, which must be present in
    /// the file (the Helper refuses a verdict whose quote is not). Valid against <c>AgentsMdChangeVerdict</c>, whose <c>Meaningful</c> is required.
    /// </summary>
    /// <param name="quote">Text that appears in the AGENTS.md being judged.</param>
    /// <param name="summary">The change's summary.</param>
    /// <returns>The reply text.</returns>
    internal static string VerdictJson(string quote, string summary = "A rule changed.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quote);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        return new JsonObject
        {
            ["meaningful"] = true,
            ["reason"] = "A rule changed.",
            ["changes"] = new JsonArray(new JsonObject { ["kind"] = "changed", ["summary"] = summary, ["quote"] = quote }),
        }.ToJsonString();
    }

    /// <summary>Scripts the fake Helper process to answer every call with <see cref="VerdictJson"/>.</summary>
    /// <param name="quote">Text that appears in the AGENTS.md being judged.</param>
    internal void ScriptVerdict(string quote) => this.HelperProcess.Text = VerdictJson(quote);

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
    /// <param name="timeProvider">
    /// The clock the hand-built <see cref="PersonaRunner"/> uses for its Room Session pool (RS §6.2's
    /// idle sweep, among other things) — <see langword="null"/> for the real one. A conformance test
    /// that needs to drive the idle sweep deterministically passes a settable clock instead of waiting
    /// out real wall-clock minutes (D30 correction 17).
    /// </param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <param name="scriptAgent">
    /// Called with the scripted peer BEFORE the Persona's runner starts, so a test can set its
    /// <c>On*</c> handlers (the <c>session/new</c> answer, for one) in time for the session the runner
    /// opens during startup. <see langword="null"/> leaves the peer's defaults.
    /// </param>
    /// <returns>The started fixture.</returns>
    internal static Task<MockAdapterFixture> StartAsync(
        Persona persona,
        IReadOnlyDictionary<string, string?>? additionalConfig = null,
        TimeProvider? timeProvider = null,
        Action<FakeAcpAgent>? scriptAgent = null,
        CancellationToken cancellationToken = default)
        => StartCoreAsync(persona, additionalConfig, timeProvider, scriptAgent, fullPipeline: false, dataDirPath: null, cancellationToken);

    /// <summary>
    /// Starts a fixture whose runner gets the whole Turn pipeline the supervisor builds from the container (AGENTS.md hook first),
    /// optionally on an existing data directory to restart on the same files. Everything else is as for the other overload.
    /// </summary>
    /// <param name="persona">The Persona to run.</param>
    /// <param name="dataDirPath">An existing data directory to run on, which this fixture then does not delete; <see langword="null"/> for a fresh one it owns.</param>
    /// <param name="additionalConfig">Configuration applied after the defaults, so it can override <c>Team:AgentsMd:NoticeWaitSeconds</c> and the Helper's <c>WaitSeconds</c>.</param>
    /// <param name="timeProvider">The runner's clock; <see langword="null"/> for the real one.</param>
    /// <param name="scriptAgent">Called with the scripted peer before the runner starts.</param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>The started fixture.</returns>
    internal static Task<MockAdapterFixture> StartFullPipelineAsync(
        Persona persona,
        string? dataDirPath = null,
        IReadOnlyDictionary<string, string?>? additionalConfig = null,
        TimeProvider? timeProvider = null,
        Action<FakeAcpAgent>? scriptAgent = null,
        CancellationToken cancellationToken = default)
        => StartCoreAsync(persona, additionalConfig, timeProvider, scriptAgent, fullPipeline: true, dataDirPath, cancellationToken);

    private static async Task<MockAdapterFixture> StartCoreAsync(
        Persona persona,
        IReadOnlyDictionary<string, string?>? additionalConfig,
        TimeProvider? timeProvider,
        Action<FakeAcpAgent>? scriptAgent,
        bool fullPipeline,
        string? dataDirPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);

        TempDataDir? dataDir = dataDirPath is null ? new() : null;
        string dataPath = dataDirPath ?? dataDir?.Path ?? throw new InvalidOperationException("No data directory.");
        string pipeName = "mock-adapter-test-" + Guid.NewGuid().ToString("N");

        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:PipeName"] = pipeName,
            ["Team:DataDir"] = dataPath,
            ["Team:HumanName"] = "You",
            ["Team:DemoAgent:Enabled"] = "false",
            ["Team:Acp:Enabled"] = "true",

            // A non-empty Args wins over AdapterPath and the locator (AgentProcessOptionsFactory,
            // Spec §6.1), so the default profile resolves with no real adapter installed and never
            // touches AdapterLocator. The value itself is never read: the launcher this fixture
            // installs below launches nothing.
            ["Team:Acp:Args:0"] = "--mock-adapter-fixture",

            // A Turn never waits for a summary by default, and the Helper judges after a second, not the
            // default two: a real clock pays both in wall time per test. Either can be overridden.
            ["Team:AgentsMd:NoticeWaitSeconds"] = "0",
            ["Team:Helpers:AgentsMdChange:WaitSeconds"] = "1",
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
        scriptAgent?.Invoke(launcher.Agent);
        builder.Services.AddSingleton<IAgentProcessLauncher>(launcher);

        // The Helper must never reach a real adapter process: the fake answers instead.
        builder.Services.RemoveAll<IHelperProcessRunner>();
        FakeHelperProcessRunner helperProcess = new();
        builder.Services.AddSingleton<IHelperProcessRunner>(helperProcess);

        IHost host = builder.Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        IOptions<TeamOptions> options = host.Services.GetRequiredService<IOptions<TeamOptions>>();
        IAgentHostFactory factory = host.Services.GetRequiredService<IAgentHostFactory>();
        IPromptSource prompts = host.Services.GetRequiredService<IPromptSource>();
        RoomFollows roomFollows = host.Services.GetRequiredService<RoomFollows>();
        ILogger<PersonaRunner> logger = host.Services.GetRequiredService<ILogger<PersonaRunner>>();
        (TurnHookPipeline hooks, MemoryPaths? memoryPaths) = TurnHookTestFactory.ForPersona(host.Services, persona, fullPipeline);
        RoomSessionStore roomSessions = host.Services.GetRequiredService<RoomSessionStore>();
        OwnPosts ownPosts = host.Services.GetRequiredService<OwnPosts>();

        // Optional, exactly as PersonaSupervisor takes it: the runner it starts is handed whatever the
        // container registered, so a container with no bridge answers every agent form as cancelled.
        IElicitationBridge? elicitationBridge = host.Services.GetService<IElicitationBridge>();

        PersonaRunner runner = new(persona, options, factory, prompts, roomFollows, logger, timeProvider: timeProvider, roomSessions: roomSessions, ownPosts: ownPosts, elicitationBridge: elicitationBridge, hooks: hooks, memoryPaths: memoryPaths);

        try
        {
            await runner.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await runner.DisposeAsync().ConfigureAwait(false);
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
            dataDir?.Dispose();
            throw;
        }

        return new MockAdapterFixture(host, dataDir, dataPath, runner, launcher.Agent, helperProcess, pipeName);
    }

    /// <summary>Waits up to five seconds for the <c>session/new</c> request the real host sends the scripted peer.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The <c>session/new</c> request.</returns>
    internal async Task<JsonObject> SessionNewAsync(CancellationToken cancellationToken)
    {
        JsonObject request = await this.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        cancellationToken.ThrowIfCancellationRequested();
        return request;
    }

    /// <summary>Waits for <c>session/new</c> and returns the system prompt text the host appended in its <c>_meta</c> payload.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The appended system prompt.</returns>
    internal async Task<string> AppendedSystemPromptAsync(CancellationToken cancellationToken)
    {
        JsonObject request = await this.SessionNewAsync(cancellationToken);
        JsonObject? systemPrompt = SessionNewMeta(request)?["systemPrompt"] as JsonObject;
        string? appended = (string?)systemPrompt?["append"];

        Assert.NotNull(appended);
        return appended;
    }

    /// <summary>Walks a <c>session/new</c> request down to <c>params._meta</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The <c>_meta</c> object, or <see langword="null"/> when the request has none.</returns>
    internal static JsonObject? SessionNewMeta(JsonObject request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (request["params"] as JsonObject)?["_meta"] as JsonObject;
    }

    /// <summary>
    /// Tears the fixture down deterministically: stops the Persona's runner (which itself disposes
    /// its session, its host, and — via <c>DotAcpAgentHostFactory</c>'s <c>DotAcpPersonaHost</c>
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
        this.dataDir?.Dispose();
    }
}
