using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Which Agent has a Turn running in which Room. Fed by <see cref="Agency.Huddle.App.Acp.Sessions.RoomSession"/>;
/// read by the Tasks UI to show the "AI reacting" badge (Spec §10.8).
/// </summary>
/// <remarks>
/// Counts <see cref="Begin"/> calls per (Agent, Room) pair rather than recording a single mark, so a
/// late <see cref="End"/> arriving from an already-superseded Turn (D8 correction 7 - a runner's
/// startup self-heal races an old session's own cleanup) cannot clear a Turn a newer <see cref="Begin"/>
/// is still holding: the pair stays busy until every outstanding <see cref="Begin"/> has a matching
/// <see cref="End"/>.
/// </remarks>
/// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
internal sealed class TurnActivity(ILogger<TurnActivity>? logger = null)
{
    private readonly ILogger<TurnActivity> logger = logger ?? NullLogger<TurnActivity>.Instance;
    private readonly Dictionary<string, Dictionary<string, int>> countsByAgent = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Raised after a <see cref="Begin"/> or <see cref="End"/> call actually changes busy state, outside the lock.</summary>
    public event Action? Changed;

    /// <summary>Marks a Turn as running for <paramref name="agentId"/> in <paramref name="roomId"/>.</summary>
    /// <param name="agentId">The Agent whose Turn is starting.</param>
    /// <param name="roomId">The Room the Turn belongs to.</param>
    public void Begin(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        bool changed;
        lock (this.gate)
        {
            if (!this.countsByAgent.TryGetValue(agentId, out var rooms))
            {
                rooms = new Dictionary<string, int>(StringComparer.Ordinal);
                this.countsByAgent[agentId] = rooms;
            }

            rooms.TryGetValue(roomId, out var count);
            rooms[roomId] = count + 1;
            changed = count == 0;
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>
    /// Clears one outstanding <see cref="Begin"/> mark for <paramref name="agentId"/> and
    /// <paramref name="roomId"/>. A pair with more than one outstanding <see cref="Begin"/> stays
    /// busy - see the class remarks - and an <see cref="End"/> with nothing outstanding is a silent
    /// no-op.
    /// </summary>
    /// <param name="agentId">The Agent whose Turn ended.</param>
    /// <param name="roomId">The Room the Turn belonged to.</param>
    public void End(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        var changed = false;
        lock (this.gate)
        {
            if (this.countsByAgent.TryGetValue(agentId, out var rooms) && rooms.TryGetValue(roomId, out var count) && count > 0)
            {
                if (count == 1)
                {
                    rooms.Remove(roomId);
                    if (rooms.Count == 0)
                    {
                        this.countsByAgent.Remove(agentId);
                    }

                    changed = true;
                }
                else
                {
                    rooms[roomId] = count - 1;
                }
            }
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>Whether <paramref name="agentId"/> has a Turn running in any Room.</summary>
    /// <param name="agentId">The Agent to check.</param>
    public bool IsBusy(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            return this.countsByAgent.TryGetValue(agentId, out var rooms) && rooms.Count > 0;
        }
    }

    /// <summary>Whether <paramref name="agentId"/> has a Turn running in <paramref name="roomId"/>.</summary>
    /// <param name="agentId">The Agent to check.</param>
    /// <param name="roomId">The Room to check.</param>
    public bool IsBusyIn(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            return this.countsByAgent.TryGetValue(agentId, out var rooms) && rooms.TryGetValue(roomId, out var count) && count > 0;
        }
    }

    /// <summary>
    /// The Room of <paramref name="agentId"/>'s running Turn when exactly one Room has one, else <see langword="null"/>. At the
    /// default of one Turn at a time per Agent this is the Room a tool call made during that Turn came from; with Turns in two
    /// Rooms at once a call cannot be tied to either, so there is no answer.
    /// </summary>
    /// <param name="agentId">The Agent to check.</param>
    /// <returns>The single busy Room's id, or <see langword="null"/>.</returns>
    public string? SoleRoom(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            return this.countsByAgent.TryGetValue(agentId, out var rooms) && rooms.Count == 1 ? rooms.Keys.First() : null;
        }
    }

    /// <summary>
    /// Drops every outstanding <see cref="Begin"/> mark for <paramref name="agentId"/>, across every
    /// Room. <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> calls this at Welcome, beside
    /// <c>OwnPosts.ClearAgent</c>, so a restart or reconnect self-heals rather than leaving a stale
    /// entry marked busy forever.
    /// </summary>
    /// <param name="agentId">The Agent whose entries should all be dropped.</param>
    public void ClearAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        bool changed;
        lock (this.gate)
        {
            changed = this.countsByAgent.Remove(agentId);
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }
}
