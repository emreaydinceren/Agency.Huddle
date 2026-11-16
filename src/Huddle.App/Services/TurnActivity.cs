using Agency.Huddle.App.Tasks;
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
/// <see cref="End"/>. It also keeps the Task each Turn was woken for (<see cref="RecordWake"/>, <see cref="TaskInHand"/>).
/// </remarks>
/// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
/// <param name="timeProvider">Stamps pending wakes so a stale one expires; defaults to <see cref="TimeProvider.System"/>.</param>
internal sealed class TurnActivity(ILogger<TurnActivity>? logger = null, TimeProvider? timeProvider = null)
{
    /// <summary>
    /// How long a pending wake note stays usable. A wake starts its Turn within moments, so a Turn that begins much later in the
    /// same Room (the Agent was offline, or the wake was folded into a Turn already running) may be unrelated to it. Missing the
    /// note only falls back to the Room's Project.
    /// </summary>
    private static readonly TimeSpan PendingWakeLifetime = TimeSpan.FromMinutes(5);

    private readonly ILogger<TurnActivity> logger = logger ?? NullLogger<TurnActivity>.Instance;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, Dictionary<string, int>> countsByAgent = new(StringComparer.Ordinal);
    private readonly Dictionary<(string AgentId, string RoomId), Dictionary<TaskId, DateTimeOffset>> pendingWakes = [];
    private readonly Dictionary<(string AgentId, string RoomId), TaskId> tasksInHand = [];
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
            if (changed)
            {
                this.TakePendingWake((agentId, roomId));
            }
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

                    _ = this.tasksInHand.Remove((agentId, roomId));
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
            foreach ((string AgentId, string RoomId) key in this.tasksInHand.Keys.Where(key => string.Equals(key.AgentId, agentId, StringComparison.Ordinal)).ToList())
            {
                _ = this.tasksInHand.Remove(key);
            }

            foreach ((string AgentId, string RoomId) key in this.pendingWakes.Keys.Where(key => string.Equals(key.AgentId, agentId, StringComparison.Ordinal)).ToList())
            {
                _ = this.pendingWakes.Remove(key);
            }
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>
    /// Notes that <paramref name="taskId"/>'s wake is being posted to <paramref name="agentId"/> in <paramref name="roomId"/>. It is
    /// pending until the pair's next Turn begins, when it becomes that Turn's <see cref="TaskInHand"/>. Call it before the post,
    /// because a post can start the Turn before it returns.
    /// </summary>
    /// <param name="agentId">The Agent being woken.</param>
    /// <param name="roomId">The Room the wake is posted in.</param>
    /// <param name="taskId">The Task the wake is for.</param>
    public void RecordWake(string agentId, string roomId, TaskId taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            if (!this.pendingWakes.TryGetValue((agentId, roomId), out Dictionary<TaskId, DateTimeOffset>? pending))
            {
                pending = [];
                this.pendingWakes[(agentId, roomId)] = pending;
            }

            pending[taskId] = this.timeProvider.GetUtcNow();
        }
    }

    /// <summary>Withdraws a <see cref="RecordWake"/> whose post did not go through.</summary>
    /// <param name="agentId">The Agent that was to be woken.</param>
    /// <param name="roomId">The Room the wake was to be posted in.</param>
    /// <param name="taskId">The Task the wake was for.</param>
    public void ForgetWake(string agentId, string roomId, TaskId taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            if (this.pendingWakes.TryGetValue((agentId, roomId), out Dictionary<TaskId, DateTimeOffset>? pending) && pending.Remove(taskId) && pending.Count == 0)
            {
                _ = this.pendingWakes.Remove((agentId, roomId));
            }
        }
    }

    /// <summary>
    /// The Task the running Turn of <paramref name="agentId"/> in <paramref name="roomId"/> was woken for, or <see langword="null"/>
    /// when the Turn did not start from a Task wake, or when two different Tasks' wakes were waiting and it cannot be tied to one.
    /// </summary>
    /// <param name="agentId">The Agent to check.</param>
    /// <param name="roomId">The Room to check.</param>
    /// <returns>The Task in hand, or <see langword="null"/>.</returns>
    public TaskId? TaskInHand(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            return this.tasksInHand.TryGetValue((agentId, roomId), out TaskId taskId) ? taskId : null;
        }
    }

    /// <summary>Turns the pair's pending wake into its Task in hand, when exactly one Task is pending. Call under <see cref="gate"/>.</summary>
    /// <param name="pair">The Agent and Room whose Turn is beginning.</param>
    private void TakePendingWake((string AgentId, string RoomId) pair)
    {
        if (!this.pendingWakes.Remove(pair, out Dictionary<TaskId, DateTimeOffset>? pending))
        {
            return;
        }

        DateTimeOffset oldestAllowed = this.timeProvider.GetUtcNow() - PendingWakeLifetime;
        List<TaskId> live = pending.Where(note => note.Value >= oldestAllowed).Select(note => note.Key).ToList();
        if (live.Count == 1)
        {
            this.tasksInHand[pair] = live[0];
        }
    }
}
