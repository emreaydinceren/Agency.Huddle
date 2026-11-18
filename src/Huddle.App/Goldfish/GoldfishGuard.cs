using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Goldfish;

/// <summary>Tells a tool whether its caller is the goldfish: the Agent's one busy Room (<see cref="TurnActivity.SoleRoom"/>) holds an open possession.</summary>
internal sealed class GoldfishGuard(IPossessionLookup lookup, TurnActivity activity, IPromptSource prompts)
{
    /// <summary>The possession the caller's current Turn belongs to.</summary>
    /// <param name="agentId">The calling Agent.</param>
    /// <returns>The possession id, or <see langword="null"/> when the caller is not a goldfish.</returns>
    public string? PossessionOf(string agentId) =>
        activity.SoleRoom(agentId) is { } room ? lookup.PossessionOf(agentId, room) : null;

    /// <summary>The final refusal text for a goldfish caller.</summary>
    /// <param name="agentId">The calling Agent.</param>
    /// <returns>The refusal, or <see langword="null"/> when the caller is not a goldfish.</returns>
    public string? Refusal(string agentId) =>
        this.PossessionOf(agentId) is not null ? prompts.Render("tool.goldfish.refused", new Dictionary<string, string>(StringComparer.Ordinal)) : null;
}
