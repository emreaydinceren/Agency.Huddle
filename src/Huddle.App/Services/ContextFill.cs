namespace Agency.Huddle.App.Services;

/// <summary>
/// How full each Agent's context window is, per Room. Fed by <see cref="Agency.Huddle.App.Acp.Sessions.RoomSession"/>
/// from every usage update; read by the Members panel's context meter.
/// </summary>
/// <remarks>
/// <c>Used</c> is a gauge, not a counter: it falls after a compaction. The store therefore keeps only
/// the latest reading per (Agent, Room) and never adds values together. A <see langword="null"/> room
/// id is the Agent's shared entry, the one reading of a shared-mode session that serves every Room.
/// </remarks>
internal sealed class ContextFill
{
    private readonly Dictionary<string, Dictionary<string, ContextFillReading>> byAgent = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Raised after a call actually changes a stored reading, outside the lock.</summary>
    public event Action? Changed;

    /// <summary>Stores the latest reading for <paramref name="agentId"/> in <paramref name="roomId"/>; an identical repeat is a no-op.</summary>
    /// <param name="agentId">The Agent whose context was measured.</param>
    /// <param name="roomId">The Room the session belongs to, or <see langword="null"/> for a shared-mode session.</param>
    /// <param name="used">How full the context window is now.</param>
    /// <param name="size">The size of the context window; zero when the Adapter did not say.</param>
    public void Report(string agentId, string? roomId, long used, long size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        if (roomId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(used);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        bool changed;
        lock (this.gate)
        {
            if (!this.byAgent.TryGetValue(agentId, out Dictionary<string, ContextFillReading>? rooms))
            {
                rooms = new Dictionary<string, ContextFillReading>(StringComparer.Ordinal);
                this.byAgent[agentId] = rooms;
            }

            string key = roomId ?? string.Empty;
            changed = !rooms.TryGetValue(key, out ContextFillReading? existing) || existing.Used != used || existing.Size != size;
            if (changed)
            {
                rooms[key] = new ContextFillReading(used, size, Shared: false);
            }
        }

        if (changed)
        {
            this.Changed?.Invoke();
        }
    }

    /// <summary>Drops the reading for one (Agent, Room) pair, or the Agent's shared reading when <paramref name="roomId"/> is <see langword="null"/>.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room, or <see langword="null"/> for the shared entry.</param>
    public void Forget(string agentId, string? roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        if (roomId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        }

        bool changed = false;
        lock (this.gate)
        {
            if (this.byAgent.TryGetValue(agentId, out Dictionary<string, ContextFillReading>? rooms))
            {
                changed = rooms.Remove(roomId ?? string.Empty);
                if (rooms.Count == 0)
                {
                    this.byAgent.Remove(agentId);
                }
            }
        }

        if (changed)
        {
            this.Changed?.Invoke();
        }
    }

    /// <summary>
    /// Drops every reading for <paramref name="agentId"/>. <see cref="Agency.Huddle.App.Acp.PersonaRunner"/>
    /// calls it at Welcome and when the runner stops, so a restart or a stopped process never leaves a frozen number.
    /// </summary>
    /// <param name="agentId">The Agent whose readings should all be dropped.</param>
    public void ClearAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        bool changed;
        lock (this.gate)
        {
            changed = this.byAgent.Remove(agentId);
        }

        if (changed)
        {
            this.Changed?.Invoke();
        }
    }

    /// <summary>
    /// The reading to show for <paramref name="agentId"/> in <paramref name="roomId"/>: the Room's own entry,
    /// else the Agent's shared entry marked <see cref="ContextFillReading.Shared"/>, else <see langword="null"/>.
    /// </summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room being shown.</param>
    public ContextFillReading? Get(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            if (!this.byAgent.TryGetValue(agentId, out Dictionary<string, ContextFillReading>? rooms))
            {
                return null;
            }

            if (rooms.TryGetValue(roomId, out ContextFillReading? own))
            {
                return own;
            }

            return rooms.TryGetValue(string.Empty, out ContextFillReading? shared) ? shared with { Shared = true } : null;
        }
    }
}
