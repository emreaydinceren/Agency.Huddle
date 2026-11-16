using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The models an ACP adapter advertises, discovered by a throwaway session handshake rather than a
/// dedicated ACP call — ACP has no <c>models/list</c>. Internal, like <see cref="IAgentHostFactory"/>:
/// this is a test seam, not a public extension point, and <c>Huddle.App</c>'s
/// <c>InternalsVisibleTo("Huddle.Tests")</c> already covers it.
/// </summary>
internal interface IModelCatalog
{
    /// <summary>
    /// Reads the model catalog for one Adapter, discovered as a side effect of a throwaway session
    /// handshake against that Adapter's resolved profile. Cached per Adapter, so a Persona on one
    /// Adapter is never offered another Adapter's models (Spec §1.3, O-3).
    /// </summary>
    /// <param name="adapterId">
    /// The Persona's configured Adapter id, or <see langword="null"/> to probe the installation's
    /// default Adapter. Resolution never fails — an id naming no configured Adapter probes the
    /// default instead, exactly like <see cref="AdapterProfileResolver.Resolve(string?)"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(string? adapterId, CancellationToken cancellationToken);

    /// <summary>
    /// The effort (thinking/reasoning) levels an ACP adapter advertises for one model, discovered
    /// the same way as <see cref="GetAsync"/>: as a side effect of a throwaway session handshake,
    /// this time started with that model selected. The list is for exactly ONE model on exactly ONE
    /// Adapter — effort is downstream of model choice, not a sibling catalog beside it — and the
    /// adapter's own <c>"default"</c> sentinel entry is already removed before this returns: the
    /// Agent card's blank "Agent default" option already IS that choice, and it stores
    /// <c>null</c>, so there is nothing left for the sentinel string to mean here.
    /// </summary>
    /// <remarks>
    /// An empty list is a REAL answer — "this model offers no effort choice" — exactly like
    /// <see cref="IAgentSession.EffortLevels"/> documents. A FAILED probe also returns an empty
    /// list, but unlike a successful empty answer, a failure is never cached: installing or
    /// authenticating the adapter and reopening the card must work with no app restart, the same
    /// guarantee <see cref="GetAsync"/> already makes for the model catalog.
    /// </remarks>
    /// <param name="adapterId">
    /// The Persona's configured Adapter id, or <see langword="null"/> for the installation's default
    /// Adapter — the same resolution <see cref="GetAsync"/> performs, and its own cache dimension:
    /// the same model id can mean something different, or nothing, on two different Adapters.
    /// </param>
    /// <param name="model">
    /// The model id to probe against, or <c>null</c> for the adapter's own default model. This is
    /// its own cache entry, separate from every named model: which model the adapter happens to
    /// pick for itself when none is requested is a different question from what any specific named
    /// model supports.
    /// </param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? adapterId, string? model, CancellationToken cancellationToken);

    /// <summary>
    /// The Work Modes (ADR-0033) an ACP adapter advertises, discovered by the same throwaway session as
    /// <see cref="GetEffortLevelsAsync"/> and sharing its cache entry, so asking for both of one
    /// (Adapter, Model) costs one probe. Modes belong to the Adapter, not to a model, but the entry is
    /// keyed the same way so the two answers always come from one session.
    /// </summary>
    /// <remarks>
    /// Unlike the effort ladder, the adapter's <c>"default"</c> id is KEPT: for a mode it is Manual, a
    /// real mode, not a sentinel. Modes the operator hides (<c>Team:Acp:HiddenModes</c>) are removed
    /// before this returns. An empty list is a REAL answer (the Adapter offers no mode choice), and a
    /// FAILED probe also returns an empty list but is never cached.
    /// </remarks>
    /// <param name="adapterId">The Persona's configured Adapter id, or <see langword="null"/> for the default Adapter.</param>
    /// <param name="model">The model id to probe against, or <see langword="null"/> for the adapter's own default.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    ValueTask<IReadOnlyList<AgentModeOption>> GetWorkModesAsync(string? adapterId, string? model, CancellationToken cancellationToken);

    /// <summary>
    /// The display names of an Agent's Model and Effort, read from what earlier probes already cached.
    /// It never probes and never waits: a surface that must not block (the avatar profile card) calls it
    /// and falls back to the raw ids and its own default wording for whatever comes back null.
    /// </summary>
    /// <remarks>
    /// A chosen id maps to its display name in the cached model list (or, for effort, the cached effort
    /// list of that (Adapter, Model)). A <see langword="null"/> id means "none chosen", and then the name of
    /// the Adapter's own default is returned, learned from the throwaway session's reported current values.
    /// A chosen Claude alias id (<c>sonnet</c>, <c>opus</c>, <c>haiku</c>) reads by its alias name while nothing is cached for that Adapter.
    /// The Adapter's <c>"default"</c> effort sentinel names no level, so it counts as unknown. The default
    /// Effort is only known for the Adapter's default model, so it is unknown when a model was chosen.
    /// </remarks>
    /// <param name="adapterId">The Persona's configured Adapter id, or <see langword="null"/> for the default Adapter.</param>
    /// <param name="model">The chosen model id, or <see langword="null"/> when none was chosen.</param>
    /// <param name="effort">The chosen effort id, or <see langword="null"/> when none was chosen.</param>
    /// <returns>The names that could be resolved from the cache; <see langword="null"/> members are unknown.</returns>
    RuntimeNames PeekRuntime(string? adapterId, string? model, string? effort);

    /// <summary>
    /// The id of the model the Adapter's own throwaway session reported running, read from what an earlier
    /// <see cref="GetAsync"/> cached. It never probes and never waits: Build a team calls it right after
    /// <see cref="GetAsync"/> to store the model a new draft would otherwise only name.
    /// </summary>
    /// <param name="adapterId">The Persona's configured Adapter id, or <see langword="null"/> for the default Adapter.</param>
    /// <returns>The cached default model id, or <see langword="null"/> when nothing is cached for the Adapter.</returns>
    string? PeekDefaultModel(string? adapterId);

    /// <summary>
    /// Whether the Adapter reported a person signed in, read from what the latest probe of it observed. It never
    /// probes and never waits: a surface that must not block calls it and shows nothing for
    /// <see cref="AgentSignInStatus.Unknown"/>.
    /// </summary>
    /// <remarks>
    /// Every probe records what it saw: <see cref="GetAsync"/>, the effort and Work Mode reads and
    /// <see cref="CheckSignInAsync"/> alike. A record is believed for a few minutes only, after which the answer is
    /// Unknown again. An Adapter that does not report its sign-in is always Unknown, and so is one whose latest probe
    /// failed for any reason other than needing authentication.
    /// </remarks>
    /// <param name="adapterId">The Persona's configured Adapter id, or <see langword="null"/> for the default Adapter.</param>
    /// <returns>The fresh recorded status, or <see cref="AgentSignInStatus.Unknown"/> when nothing fresh is recorded.</returns>
    AgentSignInStatus PeekSignIn(string? adapterId);

    /// <summary>
    /// Whether the Adapter reports a person signed in, probing for it when nothing fresh is recorded or when asked to.
    /// </summary>
    /// <remarks>
    /// A probe is one throwaway session with no model requested, run under the same gate as every other probe, and it
    /// updates the caches exactly as <see cref="GetAsync"/> does on a success. So a later <see cref="GetAsync"/> costs no
    /// spawn, and a refresh after the person signed in replaces what was cached. A probe that finds nobody signed in
    /// caches no catalog at all: authenticating and reopening works with no restart.
    /// </remarks>
    /// <param name="adapterId">The Persona's configured Adapter id, or <see langword="null"/> for the default Adapter.</param>
    /// <param name="refresh">
    /// <see langword="true"/> to probe even when a fresh status is recorded (a "Check again" after signing in);
    /// <see langword="false"/> to answer a fresh record without spawning anything.
    /// </param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The status the probe or the fresh record gave; <see cref="AgentSignInStatus.Unknown"/> when it cannot be determined.</returns>
    ValueTask<AgentSignInStatus> CheckSignInAsync(string? adapterId, bool refresh, CancellationToken cancellationToken);
}
