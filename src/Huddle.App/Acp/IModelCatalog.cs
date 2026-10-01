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
    /// Teammate card's blank "Use the agent's default" option already IS that choice, and it stores
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
}
