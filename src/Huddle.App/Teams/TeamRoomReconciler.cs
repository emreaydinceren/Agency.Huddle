using Microsoft.Data.Sqlite;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Keeps one Team Room per Team (Team chats spec §6.4, ADR-0052): creates it when a Team first has a
/// connected member, invites later members into it, leaves an Archived one alone, and archives every
/// Room scoped to a Team that has left the catalog. Runs once at startup and again on
/// <see cref="ITeamCatalog.Changed"/> and <see cref="RoomEvents.RoomsChanged"/> (a member's User row
/// appearing). Runs are serialised, and no failure escapes an event handler.
/// </summary>
internal sealed partial class TeamRoomReconciler(
    ITeamDirectory directory,
    ChatService chat,
    ITeamCatalog catalog,
    RoomEvents events,
    ILogger<TeamRoomReconciler> logger,
    IFeedPublisher? feed = null) : IHostedService, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly StopSignal stopping = new();
    private readonly Lock sync = new();
    private readonly HashSet<string> lastSeenTeams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> archivedByReconciler = new(StringComparer.Ordinal);
    private bool seeded;
    private bool disposed;
    private bool running;
    private bool dirty;
    private Task lastRun = Task.CompletedTask;

    /// <summary>The loop task currently running, or the last one that ran; completed when none has started. Tests await it.</summary>
    internal Task LastRun
    {
        get
        {
            lock (this.sync)
            {
                return this.lastRun;
            }
        }
    }

    /// <summary>Subscribes to both change signals and starts the first reconcile in the background.</summary>
    /// <param name="cancellationToken">Unused; the first run is bound to the reconciler's own stop token.</param>
    /// <returns>A completed task; the first run does not delay the host.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        catalog.Changed += this.OnSignal;
        events.RoomsChanged += this.OnSignal;
        this.OnSignal();
        return Task.CompletedTask;
    }

    /// <summary>Unsubscribes and cancels any run in progress.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A task that completes once the run in progress, if any, has ended.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task loop;
        lock (this.sync)
        {
            if (this.disposed)
            {
                return;
            }

            loop = this.lastRun;
        }

        catalog.Changed -= this.OnSignal;
        events.RoomsChanged -= this.OnSignal;
        // Under WebApplicationFactory the factory's stop and the host's Dispose run on two threads, so Dispose can land
        // after the check above (CI run 1260); StopSignal makes this Stop safe either way.
        this.stopping.Stop();
        await loop.ConfigureAwait(false);
    }

    /// <summary>
    /// Unsubscribes and cancels the run in progress. The run gate and the stop token source are released by
    /// whoever finishes last: here when no run is active, otherwise the loop when its run ends. Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        lock (this.sync)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            catalog.Changed -= this.OnSignal;
            events.RoomsChanged -= this.OnSignal;
            this.stopping.Stop();
            if (!this.running)
            {
                this.ReleaseResources();
            }
        }
    }

    /// <summary>Disposes the gate and the stop signal. Called once, with <see cref="sync"/> held, when no run is active.</summary>
    private void ReleaseResources()
    {
        this.gate.Dispose();
        this.stopping.Dispose();
    }

    /// <summary>
    /// Brings the Team Rooms in line with the catalog. Does nothing while the catalog is empty, so a
    /// catalog that has not loaded yet cannot archive everything.
    /// </summary>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>A task that completes when the run has finished.</returns>
    internal async Task ReconcileAsync(CancellationToken ct = default)
    {
        await this.gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IReadOnlyList<TeamSummary> teams = catalog.Teams;
            if (teams.Count == 0)
            {
                return;
            }

            if (!this.seeded)
            {
                foreach (Room room in await directory.GetRoomsAsync(ct).ConfigureAwait(false))
                {
                    if (room.Team is not null)
                    {
                        _ = this.lastSeenTeams.Add(room.Team);
                    }
                }

                this.seeded = true;
            }

            HashSet<string> catalogNow = new(teams.Select(static t => t.Name), StringComparer.OrdinalIgnoreCase);
            await this.UnarchiveReappearedTeamsAsync(catalogNow, ct).ConfigureAwait(false);

            foreach (TeamSummary team in teams)
            {
                await this.EnsureTeamRoomAsync(team, ct).ConfigureAwait(false);
            }

            await this.ArchiveVanishedTeamsAsync(catalogNow, ct).ConfigureAwait(false);
            this.lastSeenTeams.Clear();
            this.lastSeenTeams.UnionWith(catalogNow);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Creates the Team Room or invites the members it lacks; an Archived Team Room is left alone.</summary>
    private async Task EnsureTeamRoomAsync(TeamSummary team, CancellationToken ct)
    {
        List<User> members = [];
        foreach (string name in team.Members)
        {
            if (await directory.FindUserByNameAsync(name, ct).ConfigureAwait(false) is { Kind: UserKind.Agent } user)
            {
                members.Add(user);
            }
        }

        if (members.Count == 0)
        {
            return;
        }

        Room? room = await directory.FindTeamRoomAsync(team.Name, ct).ConfigureAwait(false);
        if (room is null)
        {
            room = await this.TryCreateAsync(team, members, ct).ConfigureAwait(false);
            if (room is null)
            {
                return;
            }
        }

        if (room.Archived)
        {
            return;
        }

        IReadOnlyList<User> present = await directory.GetRoomMembersAsync(room.Id, ct).ConfigureAwait(false);
        foreach (User member in members)
        {
            if (!present.Any(p => string.Equals(p.Id, member.Id, StringComparison.Ordinal)))
            {
                await chat.InviteAsync(room.Id, member.Name, FeedActor.Huddle, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates the Team Room. When the unique index rejects it because another writer got there first,
    /// returns that writer's Room instead.
    /// </summary>
    private async Task<Room?> TryCreateAsync(TeamSummary team, List<User> members, CancellationToken ct)
    {
        string[] ids = [KnownIds.Human, .. members.Select(static m => m.Id)];
        try
        {
            Room created = await directory.CreateRoomAsync(team.Name, ids, team.Name, true, ct).ConfigureAwait(false);
            events.PublishRoomsChanged();
            await this.PostCreatedAsync(created, members, ct).ConfigureAwait(false);
            return created;
        }
        catch (SqliteException)
        {
            return await directory.FindTeamRoomAsync(team.Name, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Tells the Feed Huddle opened a Team Room. The Feed never decides, and a failed lookup is dropped.</summary>
    /// <param name="room">The Team Room that was just created.</param>
    /// <param name="members">The Agents it holds besides the Human.</param>
    /// <param name="ct">Cancels the Human lookup.</param>
    private async Task PostCreatedAsync(Room room, List<User> members, CancellationToken ct)
    {
        if (feed is null)
        {
            return;
        }

        try
        {
            User? human = await directory.GetUserAsync(KnownIds.Human, ct).ConfigureAwait(false);
            List<string> names = [human?.Name ?? "You", .. members.Select(static m => m.Name)];
            feed.Post(new FeedMessage(FeedSourceType.RoomCreated, room.Id, FeedActor.Huddle, false, new RoomCreatedPayload(room.Id, room.Name, room.Team, names)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The Feed could not record the Team Room {RoomId}.", room.Id);
        }
    }

    /// <summary>
    /// Archives every non-Archived Room scoped to a Team that was in the catalog on the previous run and is
    /// not now, remembering them so the Team's return can unarchive exactly those. Only the present-to-absent
    /// transition archives, so a Room the Human unarchived while its Team is gone stays unarchived.
    /// </summary>
    private async Task ArchiveVanishedTeamsAsync(HashSet<string> catalogNow, CancellationToken ct)
    {
        HashSet<string> vanished = new(this.lastSeenTeams, StringComparer.OrdinalIgnoreCase);
        vanished.ExceptWith(catalogNow);
        if (vanished.Count == 0)
        {
            return;
        }

        foreach (Room room in await directory.GetRoomsAsync(ct).ConfigureAwait(false))
        {
            if (room.Team is not null && !room.Archived && vanished.Contains(room.Team))
            {
                await chat.SetRoomArchivedAsync(room.Id, true, FeedActor.Huddle, ct).ConfigureAwait(false);
                this.archivedByReconciler[room.Id] = room.Team;
            }
        }
    }

    /// <summary>
    /// Unarchives the Rooms this reconciler archived for a Team that is back in the catalog after being
    /// absent from the previous run. A Room the Human has already unarchived is only forgotten.
    /// </summary>
    private async Task UnarchiveReappearedTeamsAsync(HashSet<string> catalogNow, CancellationToken ct)
    {
        foreach ((string roomId, string team) in this.archivedByReconciler.ToList())
        {
            if (!catalogNow.Contains(team) || this.lastSeenTeams.Contains(team))
            {
                continue;
            }

            Room? room = await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false);
            if (room is { Archived: true })
            {
                await chat.SetRoomArchivedAsync(roomId, false, FeedActor.Huddle, ct).ConfigureAwait(false);
            }

            _ = this.archivedByReconciler.Remove(roomId);
        }
    }

    /// <summary>
    /// Records a change signal and makes sure one loop is running. A signal during a run only marks it dirty,
    /// so a burst costs at most one follow-up run; the handler itself never throws.
    /// </summary>
    private void OnSignal()
    {
        lock (this.sync)
        {
            if (this.disposed)
            {
                return;
            }

            this.dirty = true;
            if (!this.running)
            {
                this.running = true;
                this.lastRun = Task.Run(this.RunLoopAsync, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Runs reconciles back to back while signals keep arriving, then ends. Releases the gate and the token
    /// source when the reconciler was disposed during the loop.
    /// </summary>
    private async Task RunLoopAsync()
    {
        CancellationToken ct = this.stopping.Token;
        while (true)
        {
            lock (this.sync)
            {
                if (!this.dirty || ct.IsCancellationRequested)
                {
                    this.dirty = false;
                    this.running = false;
                    if (this.disposed)
                    {
                        this.ReleaseResources();
                    }

                    return;
                }

                this.dirty = false;
            }

            await this.RunSafelyAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>Runs <see cref="ReconcileAsync"/> and logs, rather than propagates, any failure.</summary>
    private async Task RunSafelyAsync(CancellationToken ct)
    {
        try
        {
            await this.ReconcileAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping: the run is abandoned and the next start reconciles again.
        }
        catch (Exception ex)
        {
            // A supervisor-style catch: a failed reconcile must not take down a change-event publisher,
            // and the next signal or restart retries from the stored state.
            LogReconcileFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Team Room reconcile failed; it will retry on the next change.")]
    private static partial void LogReconcileFailed(ILogger logger, Exception exception);
}
