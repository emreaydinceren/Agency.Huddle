using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>One line captured from the fixture's host, in emission order.</summary>
/// <param name="Level">The severity the site logged at.</param>
/// <param name="Category">The logger's category name, typically the logging type's full name.</param>
/// <param name="Message">The rendered message, with its <c>{Placeholder}</c> arguments already substituted.</param>
public sealed record CapturedLogEntry(LogLevel Level, string Category, string Message);

/// <summary>
/// An <see cref="ILoggerProvider"/> that stores every line the fixture's host logs, so a pipe end-to-end test can
/// assert on <see cref="PipeHostFixture.LogEntries"/> instead of parsing console output.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly Lock gate = new();
    private readonly List<CapturedLogEntry> entries = [];

    /// <summary>A snapshot of every entry captured so far, in emission order.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    /// <summary>Records one entry. Called only by <see cref="CapturingLogger"/>.</summary>
    /// <param name="entry">The entry to record.</param>
    internal void Record(CapturedLogEntry entry)
    {
        lock (this.gate)
        {
            this.entries.Add(entry);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <summary>An <see cref="ILogger"/> that forwards every write to its owning <see cref="CapturingLoggerProvider"/>.</summary>
    /// <param name="categoryName">This logger's category name.</param>
    /// <param name="owner">The provider to record entries into.</param>
    private sealed class CapturingLogger(string categoryName, CapturingLoggerProvider owner) : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            owner.Record(new CapturedLogEntry(logLevel, categoryName, formatter(state, exception)));
        }
    }
}

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
    private readonly CapturingLoggerProvider loggerProvider;

    private PipeHostFixture(IHost host, string pipeName, TempDataDir dataDir, CapturingLoggerProvider loggerProvider)
    {
        this.host = host;
        this.PipeName = pipeName;
        this.dataDir = dataDir;
        this.loggerProvider = loggerProvider;
    }

    public IServiceProvider Services => this.host.Services;

    public string PipeName { get; }

    /// <summary>Every line this fixture's host has logged so far, in emission order.</summary>
    public IReadOnlyList<CapturedLogEntry> LogEntries => this.loggerProvider.Entries;

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
        RemoveBuiltinTeammateSeederHostedService(builder.Services);

        var loggerProvider = new CapturingLoggerProvider();
        builder.Logging.AddProvider(loggerProvider);

        var host = builder.Build();
        await host.StartAsync(ct);

        return new PipeHostFixture(host, pipeName, dataDir, loggerProvider);
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
    /// <remarks>
    /// <para>
    /// The Restart button changed <see cref="ServiceCollectionExtensions.AddTeamServices"/> to register
    /// <see cref="PersonaSupervisor"/> twice - once as itself (<c>AddSingleton&lt;PersonaSupervisor&gt;()</c>), so a
    /// component's Restart button can resolve the very instance the host is running, and once as
    /// <see cref="IHostedService"/> via <c>sp.GetRequiredService&lt;PersonaSupervisor&gt;()</c>, so the host still
    /// starts it. Both registrations must go, or the second one - a factory closing over the first -
    /// throws trying to resolve a <see cref="PersonaSupervisor"/> this method just removed. The factory
    /// registration carries no <see cref="ServiceDescriptor.ImplementationType"/> (it is a delegate, not
    /// a type), so it cannot be found the same way as the singleton; <see cref="ServiceDescriptor.ImplementationFactory"/>
    /// being non-null is what distinguishes it from <c>DataInitializer</c>, <c>PipeServer</c> and
    /// <c>DemoAgentHost</c> above it, which are all registered by type.
    /// </para>
    /// <para>
    /// Internal rather than private since <c>Agency.Huddle.Tests.Conformance.MockAdapterFixture</c> (D10,
    /// Task 10.1) builds its own <see cref="IHost"/> the same way this fixture does - in-memory config,
    /// <see cref="ServiceCollectionExtensions.AddTeamServices"/>, then this removal - and drives its own
    /// hand-built <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> against a real <see cref="IAgentHostFactory"/>
    /// whose process launcher it substitutes, rather than reimplementing this same removal a second time.
    /// </para>
    /// <para>
    /// <b>Matched by what the factory returns, not by counting factories (D9, corrections-B3 blocking
    /// item 2).</b> This method originally found <see cref="PersonaSupervisor"/>'s hosted-service
    /// registration by asserting there was exactly one factory-registered <see cref="IHostedService"/>
    /// at all - a delegate is the only thing that told it apart from <c>DataInitializer</c>,
    /// <c>PipeServer</c> and <c>DemoAgentHost</c>, which are all registered by type. Task 9.3 added
    /// <see cref="Agency.Huddle.App.Tasks.TaskTriggerService"/>'s hosted service, registered the same
    /// factory way, so that count is now two and the old assertion would throw on every call. A
    /// <see cref="ServiceDescriptor.ImplementationFactory"/> is a <c>Func&lt;IServiceProvider, object&gt;</c>,
    /// but <c>Func&lt;T, TResult&gt;</c>'s return-type covariance means assigning
    /// <c>sp =&gt; sp.GetRequiredService&lt;PersonaSupervisor&gt;()</c> to it does not wrap the lambda
    /// in a new delegate - the underlying method is untouched, so
    /// <c>ImplementationFactory.Method.ReturnType</c> is still <see cref="PersonaSupervisor"/>, and
    /// stays a reliable fingerprint for exactly this registration no matter how many other
    /// factory-registered hosted services join it later.
    /// </para>
    /// </remarks>
    internal static void RemovePersonaSupervisorHostedService(IServiceCollection services)
    {
        var singleton = services.FirstOrDefault(d => d.ServiceType == typeof(PersonaSupervisor))
            ?? throw new InvalidOperationException(
                $"Expected {nameof(ServiceCollectionExtensions.AddTeamServices)} to register {nameof(PersonaSupervisor)} " +
                "as a singleton, but no such registration was found. Has it been renamed or moved?");
        services.Remove(singleton);

        var hostedServiceFactory = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationFactory?.Method.ReturnType == typeof(PersonaSupervisor))
            ?? throw new InvalidOperationException(
                $"Expected {nameof(ServiceCollectionExtensions.AddTeamServices)} to register a factory-based "
                + $"{nameof(IHostedService)} whose factory returns {nameof(PersonaSupervisor)}, but none was found. "
                + "Has it been renamed or moved?");

        services.Remove(hostedServiceFactory);
    }

    /// <summary>
    /// Removes the <see cref="BuiltinTeammateSeeder"/> <see cref="IHostedService"/> that
    /// <see cref="ServiceCollectionExtensions.AddTeamServices"/> just registered, so this fixture's
    /// <see cref="IHost"/> never writes a real Chief of Staff Persona onto disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seeder always runs in the product (Spec §6.12) - that is settled, and this is not a
    /// config switch to turn it off. It IS, however, exactly the class of exclusion
    /// <see cref="RemovePersonaSupervisorHostedService"/> already sets precedent for: this fixture
    /// exists so a test can hand-craft a small, exact Persona library (<c>WritePersonaFile</c>,
    /// <c>personaStore.Add</c>) and assert on it precisely - a real Chief of Staff appearing
    /// unbidden, with its own free Name/Alias search racing whatever the test just wrote, is the
    /// same "second, independently-behaving thing polluting a controlled fixture" problem
    /// <see cref="RemovePersonaSupervisorHostedService"/>'s own remarks describe, one hosted service
    /// over. <see cref="Agency.Huddle.Tests.Acp.PersonaSupervisorTests"/> is what surfaced this: with
    /// the seeder left in, <c>Enabled_StartsOneHostPerPersonaFile</c> saw three hosts start instead
    /// of two, and several single-Persona tests saw a second, always-Offline "Chief of Staff" host
    /// racing the one Persona they actually wrote.
    /// </para>
    /// <para>
    /// Registered by type, not by factory, so the removal is a straight <c>ImplementationType</c>
    /// match rather than <see cref="RemovePersonaSupervisorHostedService"/>'s factory-return-type
    /// match.
    /// </para>
    /// </remarks>
    /// <param name="services">The fixture's service collection, already populated by <see cref="ServiceCollectionExtensions.AddTeamServices"/>.</param>
    internal static void RemoveBuiltinTeammateSeederHostedService(IServiceCollection services)
    {
        var registration = services.FirstOrDefault(
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(BuiltinTeammateSeeder))
            ?? throw new InvalidOperationException(
                $"Expected {nameof(ServiceCollectionExtensions.AddTeamServices)} to register {nameof(BuiltinTeammateSeeder)} " +
                "as an IHostedService, but no such registration was found. Has it been renamed or moved?");
        services.Remove(registration);
    }
}