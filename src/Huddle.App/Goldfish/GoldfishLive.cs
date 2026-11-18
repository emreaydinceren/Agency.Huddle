using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Goldfish;

/// <summary>
/// The read model the UI draws goldfish mode from: the live possessions (Starting, Working, Waiting) with their Agents' names,
/// refreshed whenever <see cref="GoldfishStore.Changed"/> fires. Decisions still read <see cref="GoldfishStore"/>; this only
/// feeds components, which re-render on <see cref="Changed"/>.
/// </summary>
internal sealed class GoldfishLive : IDisposable
{
    private readonly GoldfishStore store;
    private readonly ITeamDirectory team;
    private readonly ILogger<GoldfishLive> logger;
    private readonly Lock gate = new();
    private IReadOnlyList<LivePossession> snapshot = [];
    private long issued;
    private long applied;
    private bool disposed;

    /// <summary>Subscribes to the store and starts one initial refresh.</summary>
    /// <param name="store">The goldfish ledger.</param>
    /// <param name="team">Resolves an Agent's display name.</param>
    /// <param name="logger">Reports a refresh that failed.</param>
    public GoldfishLive(GoldfishStore store, ITeamDirectory team, ILogger<GoldfishLive> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(logger);

        this.store = store;
        this.team = team;
        this.logger = logger;
        store.Changed += this.OnStoreChanged;
        _ = this.RefreshAsync();
    }

    /// <summary>Raised, outside any lock, after each refresh that replaced the snapshot.</summary>
    public event Action? Changed;

    /// <summary>The live possessions, oldest first.</summary>
    public IReadOnlyList<LivePossession> Snapshot
    {
        get
        {
            lock (this.gate)
            {
                return this.snapshot;
            }
        }
    }

    /// <summary>The live possession in a Room.</summary>
    /// <param name="roomId">The Room.</param>
    /// <returns>The possession, or <see langword="null"/> when none is live there.</returns>
    public LivePossession? InRoom(string roomId) =>
        this.Snapshot.FirstOrDefault(item => string.Equals(item.Row.RoomId, roomId, StringComparison.Ordinal));

    /// <summary>The live possession of an Agent, by display name.</summary>
    /// <param name="agentName">The Agent's display name.</param>
    /// <returns>The possession, or <see langword="null"/> when the Agent is not in goldfish mode.</returns>
    public LivePossession? ForAgent(string agentName) =>
        this.Snapshot.FirstOrDefault(item => string.Equals(item.AgentName, agentName, StringComparison.Ordinal));

    /// <summary>The name with the goldfish stamp when that Agent is in goldfish mode in that Room.</summary>
    /// <param name="agentName">The Agent's display name.</param>
    /// <param name="roomId">The Room the name is shown for.</param>
    /// <returns>The stamped name, or the name unchanged.</returns>
    public string StampedIfPossessed(string agentName, string roomId) =>
        this.ForAgent(agentName) is { } live && string.Equals(live.Row.RoomId, roomId, StringComparison.Ordinal)
            ? GoldfishStamp.Apply(agentName)
            : agentName;

    /// <summary>Reads a possession from the store, live or ended.</summary>
    /// <param name="id">The possession id.</param>
    /// <returns>The row, or <see langword="null"/> when it is unknown or pruned.</returns>
    public Possession? Get(string id) => this.store.Get(id);

    /// <summary>Re-reads the live possessions and swaps the snapshot. A failure is logged and never thrown.</summary>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns>A task that completes when the snapshot is current.</returns>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        long ticket;
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            ticket = ++this.issued;
        }

        try
        {
            List<LivePossession> live = [];
            foreach (Possession row in this.store.Open().Where(static row => row.State is PossessionState.Starting or PossessionState.Working or PossessionState.Waiting))
            {
                User? agent = await this.team.GetUserAsync(row.AgentId, ct).ConfigureAwait(false);
                live.Add(new LivePossession(row, agent?.Name ?? row.AgentId, row.State == PossessionState.Waiting));
            }

            if (!this.Swap(ticket, live))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "The goldfish live state could not be refreshed; the last snapshot stays.");
            return;
        }

        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>Stops listening to the store.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            this.disposed = true;
        }

        this.store.Changed -= this.OnStoreChanged;
    }

    private bool Swap(long ticket, List<LivePossession> live)
    {
        lock (this.gate)
        {
            if (this.disposed || ticket < this.applied)
            {
                return false;
            }

            this.applied = ticket;
            this.snapshot = live;
            return true;
        }
    }

    private void OnStoreChanged() => _ = this.RefreshAsync();
}
