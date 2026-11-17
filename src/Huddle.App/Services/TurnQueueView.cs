using Agency.Huddle.App.Data;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Services;

/// <summary>Reads the waiting rows of a Room for the notice above the composer.</summary>
/// <param name="store">The <c>turn_queue</c> rows.</param>
/// <param name="chat">Tells whether the Room's Budget is spent.</param>
/// <param name="team">Names the Agents.</param>
/// <param name="options">Supplies <c>Team:TurnQueue:Enabled</c>.</param>
internal sealed class TurnQueueView(TurnQueueStore store, ChatService chat, ITeamDirectory team, IOptions<TeamOptions> options) : ITurnQueueView
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<WaitingCount>> WaitingInAsync(string roomId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        if (!options.Value.TurnQueue.Enabled)
        {
            return [];
        }

        // Only waiting rows count: the running one is the Agent's current Turn, which its Draft or typing row already shows.
        List<IGrouping<string, TurnQueueRow>> byAgent =
        [
            .. store.OpenIn(roomId)
                .Where(static row => row.Status == TurnQueueStatus.Waiting)
                .GroupBy(static row => row.AgentId, StringComparer.Ordinal),
        ];
        if (byAgent.Count == 0)
        {
            return [];
        }

        bool held = chat.GetBudget(roomId).Exhausted;
        List<WaitingCount> counts = [];
        foreach (IGrouping<string, TurnQueueRow> group in byAgent)
        {
            User? agent = await team.GetUserAsync(group.Key, ct).ConfigureAwait(false);
            counts.Add(new WaitingCount(group.Key, agent?.Name ?? group.Key, group.Count(), held));
        }

        return counts;
    }
}
