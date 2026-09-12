using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Discovers an ACP adapter's model catalog the only way ACP allows: as a side effect of the
/// session handshake. There is no <c>models/list</c> request, so this spawns a throwaway adapter
/// process, does <c>initialize</c> -&gt; <c>session/new</c>, reads <see cref="IAgentSession.Models"/>
/// off the resulting session, and disposes — it never calls <c>PromptAsync</c>, which is exactly
/// what makes this free: no prompt turn, no tokens.
/// </summary>
/// <remarks>
/// docs/acp/session-config-options.md says not to cache a model list across spawns. That rule protects
/// *selection*: which model id a real session actually starts against. Selection is re-resolved
/// live, inside Team.Acp, on every session start — this class has no say in it. What is cached here
/// is purely presentational, for populating a &lt;select&gt; in the Teammate card, and a slightly
/// stale presentational list is far cheaper than spawning a fresh adapter process on every card
/// open.
/// </remarks>
internal sealed class ModelCatalogProbe : IModelCatalog, IDisposable
{
    // A constant, not a config key: the repo's rule is no configuration surface no feature asks
    // for, and nothing has asked to tune how long a one-off probe is allowed to hang.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    // The cache key for the "no model requested" case. Not a config key either: a real model id can
    // never be blank, because both AgentSessionOptions and PersonaEffortStore.Set normalise blank to
    // null at their own boundaries, so this string can never collide with a named model's id.
    private const string DefaultModelKey = "";

    private readonly TeamOptions options;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<ModelCatalogProbe> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    // Set only once a probe actually completes (adapter installed, handshake succeeded), even when
    // the agent's own list came back empty — "this agent advertises no models" is itself a real
    // answer. Never set on failure, so installing the adapter and reopening the card works
    // immediately, with no app restart required.
    private IReadOnlyList<AgentModelOption>? cached;

    // Keyed by model id (DefaultModelKey for "no model requested"). Concurrent, not plain, because
    // the fast path below reads it without taking `gate` — mirroring the single atomic reference
    // read `cached` gets above — and a plain Dictionary write racing an unguarded read corrupts,
    // reproducing only under two Blazor circuits hitting this at once.
    private readonly ConcurrentDictionary<string, IReadOnlyList<AgentEffortOption>> effortCache =
        new(StringComparer.Ordinal);

    public ModelCatalogProbe(IOptions<TeamOptions> options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.options = options.Value;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<ModelCatalogProbe>();
    }

    public void Dispose()
    {
        this.gate.Dispose();
    }

    public async ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(CancellationToken cancellationToken)
    {
        if (this.cached is { } hit)
        {
            return hit;
        }

        // Also stops two Blazor circuits (two tabs, two users each opening a card) from spawning
        // two throwaway adapter processes at once.
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.cached is { } hitAfterWait)
            {
                return hitAfterWait;
            }

            (bool succeeded, ProbeResult result) = await this.ProbeAsync(null, cancellationToken).ConfigureAwait(false);
            if (succeeded)
            {
                this.cached = result.Models;

                // This probe ran with no model requested, so its session's effort list IS the
                // default-model answer — store it under the same key GetEffortLevelsAsync would
                // have probed for itself. The payoff: opening a card and leaving the model at
                // "Use the agent's default" costs zero extra adapter spawns.
                this.effortCache[ModelCatalogProbe.DefaultModelKey] = result.EffortLevels;
            }

            return result.Models;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? model, CancellationToken cancellationToken)
    {
        var key = model ?? ModelCatalogProbe.DefaultModelKey;
        if (this.effortCache.TryGetValue(key, out var hit))
        {
            return hit;
        }

        // Same gate the model probe uses: a second, effort-only gate would let a model probe and an
        // effort probe race each other into spawning two node processes at once.
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.effortCache.TryGetValue(key, out var hitAfterWait))
            {
                return hitAfterWait;
            }

            (bool succeeded, ProbeResult result) = await this.ProbeAsync(model, cancellationToken).ConfigureAwait(false);
            if (succeeded)
            {
                this.effortCache[key] = result.EffortLevels;
            }

            return result.EffortLevels;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>
    /// Drops the adapter's own <c>"default"</c> sentinel entry from an advertised effort catalog.
    /// Filtered HERE, in the app layer, and never in <c>Team.Acp</c>: <see
    /// cref="IAgentSession.EffortLevels"/> must stay a faithful report of what the agent actually
    /// advertised, because a protocol reader that silently drops an advertised option is exactly the
    /// class of failure <c>docs/agencyteam/traps.md</c> exists for. The reason the filter belongs
    /// somewhere is that the Teammate card's blank "Use the agent's default" option already IS this
    /// choice, and already stores <c>null</c> for it — offering the sentinel too would be two
    /// options with one meaning, and would let the literal string <c>"default"</c> reach
    /// <c>persona_efforts</c>, where it would be sent on the wire as a real, named selection: a
    /// different behaviour wearing the same label.
    /// </summary>
    /// <remarks>
    /// Pure and <c>internal</c> by design: no I/O, no state, so it is directly unit-testable without
    /// tripping <c>docs/agencyteam/rules.md</c> row 35 ("No test may reach the real
    /// <see cref="ModelCatalogProbe"/>"). <c>src/Team.App/Team.App.csproj</c> already grants
    /// <c>InternalsVisibleTo("Team.Tests")</c>, which is what makes that visible to the test project.
    /// Matches by <see cref="AgentEffortOption.Id"/>, not by position: the sentinel is documented as
    /// always first, but a positional drop would silently eat a real level if that ever changed,
    /// whereas an id match at worst misses a differently-named sentinel on some other adapter.
    /// </remarks>
    /// <param name="levels">The effort catalog as the agent advertised it, in wire order.</param>
    /// <returns>The same list with any entry whose id is <c>"default"</c> removed.</returns>
    internal static IReadOnlyList<AgentEffortOption> WithoutAdapterDefault(IReadOnlyList<AgentEffortOption> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        return levels.Count == 0
            ? levels
            : levels.Where(level => !string.Equals(level.Id, "default", StringComparison.Ordinal)).ToList();
    }

    /// <summary>The two catalogs one throwaway session can answer, read off the same session.</summary>
    private sealed record ProbeResult(
        IReadOnlyList<AgentModelOption> Models,
        IReadOnlyList<AgentEffortOption> EffortLevels)
    {
        internal static ProbeResult Empty { get; } = new ProbeResult([], []);
    }

    private async Task<(bool Succeeded, ProbeResult Result)> ProbeAsync(string? model, CancellationToken cancellationToken)
    {
        // The Work Dir ROOT, Directory.CreateDirectory'd — not a Persona's work dir, since this
        // probe is not a Persona and has no name to scope a subdirectory to. Never the repo root or
        // AppContext.BaseDirectory either: the adapter auto-loads CLAUDE.md and
        // .claude/settings.json from its cwd, so either of those would silently hand the repo's own
        // instructions to a process that is only being asked what models it offers.
        var probeCwd = Path.Combine(this.options.DataDir, this.options.Acp.WorkDir);
        Directory.CreateDirectory(probeCwd);

        var processOptions = AgentProcessOptionsFactory.TryCreate(this.options.Acp, probeCwd, AppContext.BaseDirectory);
        if (processOptions is null)
        {
            // Graceful degradation, exactly like AgentProcessOptionsFactory itself: no adapter
            // installed is not an error, just an empty catalog.
            this.logger.LogInformation("No ACP adapter is installed; the model catalog is empty.");
            return (false, ProbeResult.Empty);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ModelCatalogProbe.ProbeTimeout);

        DotAcpAgentHost? host = null;
        IAgentSession? session = null;
        try
        {
            var launcher = new AgentProcessLauncher(this.loggerFactory.CreateLogger<AgentProcessLauncher>());
            var hostOptions = new DotAcpHostOptions("Team.App", TraceWire: this.options.Acp.TraceWire);
            host = new DotAcpAgentHost(processOptions, launcher, hostOptions, this.loggerFactory);
            await host.StartAsync(timeoutCts.Token).ConfigureAwait(false);

            session = await host.StartSessionAsync(
                new AgentSessionOptions(probeCwd, new AutoApprovePermissionHandler(), model: model),
                timeoutCts.Token).ConfigureAwait(false);

            return (true, new ProbeResult(session.Models, ModelCatalogProbe.WithoutAdapterDefault(session.EffortLevels)));
        }
        catch (AgentAuthenticationRequiredException ex)
        {
            this.logger.LogWarning(ex, "Model catalog probe skipped: the adapter needs authentication.");
            return (false, ProbeResult.Empty);
        }
        catch (AgentException ex)
        {
            // Covers AgentProcessStartException too (process-launch failures): both derive from
            // AgentException, and neither is worth its own catch when the outcome is identical.
            this.logger.LogWarning(ex, "Model catalog probe failed to start or talk to the adapter.");
            return (false, ProbeResult.Empty);
        }
        catch (OperationCanceledException ex)
        {
            this.logger.LogWarning(ex, "Model catalog probe timed out after {Timeout}.", ModelCatalogProbe.ProbeTimeout);
            return (false, ProbeResult.Empty);
        }
        catch (IOException ex)
        {
            this.logger.LogWarning(ex, "Model catalog probe failed talking to the adapter process.");
            return (false, ProbeResult.Empty);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}