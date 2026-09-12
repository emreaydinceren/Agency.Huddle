using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The models an ACP adapter advertises, discovered by a throwaway session handshake rather than a
/// dedicated ACP call — ACP has no <c>models/list</c>. Internal, like <see cref="IAgentHostFactory"/>:
/// this is a test seam, not a public extension point, and <c>Team.App</c>'s
/// <c>InternalsVisibleTo("Team.Tests")</c> already covers it.
/// </summary>
internal interface IModelCatalog
{
    ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The effort (thinking/reasoning) levels an ACP adapter advertises for one model, discovered
    /// the same way as <see cref="GetAsync"/>: as a side effect of a throwaway session handshake,
    /// this time started with that model selected. The list is for exactly ONE model — effort is
    /// downstream of model choice, not a sibling catalog beside it — and the adapter's own
    /// <c>"default"</c> sentinel entry is already removed before this returns: the Teammate card's
    /// blank "Use the agent's default" option already IS that choice, and it stores <c>null</c>, so
    /// there is nothing left for the sentinel string to mean here.
    /// </summary>
    /// <remarks>
    /// An empty list is a REAL answer — "this model offers no effort choice" — exactly like
    /// <see cref="IAgentSession.EffortLevels"/> documents. A FAILED probe also returns an empty
    /// list, but unlike a successful empty answer, a failure is never cached: installing or
    /// authenticating the adapter and reopening the card must work with no app restart, the same
    /// guarantee <see cref="GetAsync"/> already makes for the model catalog.
    /// </remarks>
    /// <param name="model">
    /// The model id to probe against, or <c>null</c> for the adapter's own default model. This is
    /// its own cache entry, separate from every named model: which model the adapter happens to
    /// pick for itself when none is requested is a different question from what any specific named
    /// model supports.
    /// </param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? model, CancellationToken cancellationToken);
}