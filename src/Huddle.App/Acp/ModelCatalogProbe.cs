using System.Collections.Concurrent;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Discovers an ACP adapter's model catalog the only way ACP allows: as a side effect of the
/// session handshake. There is no <c>models/list</c> request, so this resolves the requested
/// Adapter's profile, hands it to <see cref="IAdapterProbeRunner"/> to spawn a throwaway process and
/// do <c>initialize</c> -&gt; <c>session/new</c>, reads the resulting session's advertised catalogs,
/// and disposes — it never calls <c>PromptAsync</c>, which is exactly what makes this free: no
/// prompt turn, no tokens.
/// </summary>
/// <remarks>
/// docs/engineering/acp-session-config.md says not to cache a model list across spawns. That rule protects
/// *selection*: which model id a real session actually starts against. Selection is re-resolved
/// live, inside Huddle.Acp, on every session start — this class has no say in it. What is cached here
/// is purely presentational, for populating a &lt;select&gt; in the Teammate card, and a slightly
/// stale presentational list is far cheaper than spawning a fresh adapter process on every card
/// open.
/// </remarks>
internal sealed class ModelCatalogProbe : IModelCatalog, IDisposable
{
    // A constant, not a config key: the repo's rule is no configuration surface no feature asks
    // for, and nothing has asked to tune how long a one-off probe is allowed to hang.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    private readonly TeammatePaths teammatePaths;
    private readonly ILogger<ModelCatalogProbe> logger;
    private readonly AdapterProfileResolver resolver;
    private readonly IAdapterProbeRunner probeRunner;
    private readonly SemaphoreSlim gate = new(1, 1);

    // Keyed on the RESOLVED Adapter profile's id - never the requested id as the caller spelled it.
    // Two spellings that resolve to the same profile (no `adapter:` key at all, vs. `adapter:
    // claude` naming the default by its own id, vs. an unconfigured id that falls back to the
    // default) describe the SAME process and must share the SAME cache entry: keying on the
    // requested id instead would spawn one throwaway adapter per spelling for what is actually one
    // Adapter, contradicting the shared `gate` below and Spec §4 P7 ("probe cost follows the
    // Adapter"), and - because a failed probe is never cached - could leave two entries for one
    // Adapter permanently disagreeing about whether it is reachable. One entry per Adapter (Spec
    // §6.5). Set only once a probe actually completes, even when the agent's own list came back
    // empty - "this agent advertises no models" is itself a real answer. Never set on failure, so
    // installing the Adapter and reopening the card works immediately, with no app restart
    // required. Concurrent, not plain: the fast path below reads it without taking `gate`, and a
    // plain Dictionary write racing an unguarded read corrupts, reproducing only under two Blazor
    // circuits hitting this at once.
    private readonly ConcurrentDictionary<string, IReadOnlyList<AgentModelOption>> modelCache =
        new(StringComparer.Ordinal);

    // Keyed on "{adapterId}{model}" - a UNIT SEPARATOR, not ':' or '/', because both an
    // Adapter id and a Model id can contain ordinary punctuation, so a plain delimiter could
    // collide two different (Adapter, Model) pairs onto one cache entry (Spec §6.5). The first
    // component is the RESOLVED profile id, for the same reason modelCache above keys on it
    // rather than the requested id: two spellings of one Adapter must share one effort cache too.
    //
    // Holds the Work Modes beside the effort ladder (ADR-0033): one throwaway session answers both, so
    // one probe fills both and one cache entry serves both. The modes held here are UNFILTERED; the
    // hidden-modes policy is applied on the way out, so it is never baked into a cached answer.
    private readonly ConcurrentDictionary<string, SessionCatalog> effortCache =
        new(StringComparer.Ordinal);

    private readonly WorkModePolicy workModePolicy;

    /// <summary>Builds the probe over its resolver and its process-spawning seam.</summary>
    /// <param name="teammatePaths">Locates the Work Dir root the probe spawns into.</param>
    /// <param name="loggerFactory">Used to create this type's own logger.</param>
    /// <param name="resolver">Turns a requested Adapter id into a profile; never fails (Spec §6.2).</param>
    /// <param name="probeRunner">
    /// Spawns and negotiates one throwaway session. The production registration wires in
    /// <see cref="AdapterProcessProbeRunner"/>; a test substitutes a fake — see
    /// <see cref="IAdapterProbeRunner"/>'s own doc comment for why this seam exists.
    /// </param>
    /// <param name="workModePolicy">Decides which advertised Work Modes are offered (ADR-0033).</param>
    public ModelCatalogProbe(
        TeammatePaths teammatePaths,
        ILoggerFactory loggerFactory,
        AdapterProfileResolver resolver,
        IAdapterProbeRunner probeRunner,
        WorkModePolicy workModePolicy)
    {
        ArgumentNullException.ThrowIfNull(teammatePaths);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(probeRunner);
        ArgumentNullException.ThrowIfNull(workModePolicy);

        this.teammatePaths = teammatePaths;
        this.logger = loggerFactory.CreateLogger<ModelCatalogProbe>();
        this.resolver = resolver;
        this.probeRunner = probeRunner;
        this.workModePolicy = workModePolicy;
    }

    public void Dispose()
    {
        this.gate.Dispose();
    }

    public async ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(string? adapterId, CancellationToken cancellationToken)
    {
        // Resolved ONCE here, never again inside ProbeAsync (Spec §8.2: the resolver is pure, so
        // even two calls would cost nothing - but one is simpler still). The cache key is the
        // RESOLVED profile's id, not the requested adapterId, so two spellings of one Adapter
        // (absent, or naming the default by its own id, or an unknown id that falls back to it)
        // share one cache entry and one process - see modelCache's own comment for why.
        (AdapterProfile profile, string? warning) = this.resolver.Resolve(adapterId);

        if (this.modelCache.TryGetValue(profile.Id, out var hit))
        {
            return hit;
        }

        // Also stops two Blazor circuits (two tabs, two users each opening a card) from spawning
        // two throwaway adapter processes at once - SHARED across every Adapter (Spec §6.5,
        // Implementation notes): two Blazor circuits must not spawn two processes, and that is true
        // across Adapters as much as within one. A shared gate makes a slow Claude probe delay an
        // Agency probe; that is the correct trade against spawning two adapter processes at once,
        // and the 20-second timeout bounds it.
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.modelCache.TryGetValue(profile.Id, out var hitAfterWait))
            {
                return hitAfterWait;
            }

            // Logged only once we know a probe will actually run - not on every cache hit above,
            // which would otherwise repeat this warning on every card-open and every Model/Effort
            // change for a Persona on a stale Adapter id.
            if (warning is not null)
            {
                this.logger.LogWarning("Model catalog probe: {Warning}", warning);
            }

            (bool succeeded, ProbeResult result) = await this.ProbeAsync(profile, null, cancellationToken).ConfigureAwait(false);
            if (succeeded)
            {
                this.modelCache[profile.Id] = result.Models;

                // This probe ran with no model requested, so its session's effort list IS the
                // default-model answer for this Adapter - store it under the same key
                // GetEffortLevelsAsync would have probed for itself. The payoff: opening a card and
                // leaving the model at "Use the agent's default" costs zero extra adapter spawns.
                this.effortCache[ModelCatalogProbe.EffortCacheKey(profile.Id, null)] = new SessionCatalog(result.EffortLevels, result.Modes);
            }

            return result.Models;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? adapterId, string? model, CancellationToken cancellationToken)
    {
        SessionCatalog catalog = await this.GetSessionCatalogAsync(adapterId, model, cancellationToken).ConfigureAwait(false);
        return catalog.EffortLevels;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AgentModeOption>> GetWorkModesAsync(string? adapterId, string? model, CancellationToken cancellationToken)
    {
        SessionCatalog catalog = await this.GetSessionCatalogAsync(adapterId, model, cancellationToken).ConfigureAwait(false);

        // Filtered here, on the way out, so the cache holds what the Adapter advertised.
        return this.workModePolicy.Filter(catalog.Modes);
    }

    /// <summary>
    /// The throwaway session's effort ladder and Work Modes for one (Adapter, Model), served from the
    /// shared cache entry or probed once. Both public methods above go through here, which is what makes
    /// asking for both cost one spawn: a second, modes-only probe would double the Adapter cost the card
    /// pays on every open.
    /// </summary>
    /// <param name="adapterId">The requested Adapter id, or <see langword="null"/> for the default.</param>
    /// <param name="model">The model id to probe against, or <see langword="null"/> for the Adapter's own default.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    private async ValueTask<SessionCatalog> GetSessionCatalogAsync(string? adapterId, string? model, CancellationToken cancellationToken)
    {
        // See GetAsync's own comment: resolved once, and keyed on the RESOLVED profile id.
        (AdapterProfile profile, string? warning) = this.resolver.Resolve(adapterId);
        var key = ModelCatalogProbe.EffortCacheKey(profile.Id, model);
        if (this.effortCache.TryGetValue(key, out var hit))
        {
            return hit;
        }

        // Same gate the model probe uses: a second, effort-only gate would let a model probe and an
        // effort probe race each other into spawning two adapter processes at once.
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.effortCache.TryGetValue(key, out var hitAfterWait))
            {
                return hitAfterWait;
            }

            if (warning is not null)
            {
                this.logger.LogWarning("Model catalog probe: {Warning}", warning);
            }

            (bool succeeded, ProbeResult result) = await this.ProbeAsync(profile, model, cancellationToken).ConfigureAwait(false);
            SessionCatalog answered = new(result.EffortLevels, result.Modes);
            if (succeeded)
            {
                this.effortCache[key] = answered;
            }

            return answered;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>
    /// Drops the adapter's own <c>"default"</c> sentinel entry from an advertised effort catalog.
    /// Filtered HERE, in the app layer, and never in <c>Huddle.Acp</c>: <see
    /// cref="IAgentSession.EffortLevels"/> must stay a faithful report of what the agent actually
    /// advertised, because a protocol reader that silently drops an advertised option is exactly the
    /// class of failure <c>docs/engineering/traps.md</c> exists for. The reason the filter belongs
    /// somewhere is that the Teammate card's blank "Use the agent's default" option already IS this
    /// choice, and already stores <c>null</c> for it — offering the sentinel too would be two
    /// options with one meaning, and would let the literal string <c>"default"</c> reach
    /// <c>persona_efforts</c>, where it would be sent on the wire as a real, named selection: a
    /// different behaviour wearing the same label.
    /// </summary>
    /// <remarks>
    /// Pure and <c>internal</c> by design: no I/O, no state, so it is directly unit-testable without
    /// tripping <c>docs/engineering/rules.md</c> row 35 ("No test may reach the real
    /// <see cref="ModelCatalogProbe"/>"). <c>src/Huddle.App/Huddle.App.csproj</c> already grants
    /// <c>InternalsVisibleTo("Huddle.Tests")</c>, which is what makes that visible to the test project.
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

    /// <summary>
    /// The effort cache key for one (Adapter, Model) pair - see the field's own comment for why a
    /// unit separator, and for why the first component is the RESOLVED profile id.
    /// </summary>
    /// <param name="profileId">The RESOLVED Adapter profile's id - never the id as the caller requested it.</param>
    /// <param name="model">The model id, or <see langword="null"/> for the Adapter's own default.</param>
    private static string EffortCacheKey(string profileId, string? model) => string.Concat(profileId, "\u001F", model);

    /// <summary>The catalogs one throwaway session can answer, read off the same session.</summary>
    private sealed record ProbeResult(
        IReadOnlyList<AgentModelOption> Models,
        IReadOnlyList<AgentEffortOption> EffortLevels,
        IReadOnlyList<AgentModeOption> Modes)
    {
        internal static ProbeResult Empty { get; } = new ProbeResult([], [], []);
    }

    /// <summary>The effort ladder and the Work Modes of one (Adapter, Model), cached as a pair because one probe answers both.</summary>
    private sealed record SessionCatalog(
        IReadOnlyList<AgentEffortOption> EffortLevels,
        IReadOnlyList<AgentModeOption> Modes);

    private async Task<(bool Succeeded, ProbeResult Result)> ProbeAsync(AdapterProfile profile, string? model, CancellationToken cancellationToken)
    {
        // The Teammates ROOT, Directory.CreateDirectory'd — not a Persona's own folder, since this
        // probe is not a Persona and has no name to scope a subdirectory to. Never the repo root or
        // AppContext.BaseDirectory either: the adapter auto-loads CLAUDE.md and
        // .claude/settings.json from its cwd, so either of those would silently hand the repo's own
        // instructions to a process that is only being asked what models it offers.
        var probeCwd = this.teammatePaths.DefinitionsRoot;
        Directory.CreateDirectory(probeCwd);

        // profile is already resolved by the caller (GetAsync/GetEffortLevelsAsync) - resolving
        // again here would be a second call for the one request Spec §8.2 already budgets for one.
        var processOptions = AgentProcessOptionsFactory.TryCreate(profile, probeCwd, AppContext.BaseDirectory);
        if (processOptions is null)
        {
            // Graceful degradation, exactly like AgentProcessOptionsFactory itself: no adapter
            // installed is not an error, just an empty catalog.
            this.logger.LogInformation("No ACP adapter is installed; the model catalog is empty.");
            return (false, ProbeResult.Empty);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ModelCatalogProbe.ProbeTimeout);

        try
        {
            AdapterProbeOutcome outcome = await this.probeRunner.RunAsync(processOptions, probeCwd, model, timeoutCts.Token).ConfigureAwait(false);
            return (true, new ProbeResult(outcome.Models, ModelCatalogProbe.WithoutAdapterDefault(outcome.EffortLevels), outcome.Modes));
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
    }
}
