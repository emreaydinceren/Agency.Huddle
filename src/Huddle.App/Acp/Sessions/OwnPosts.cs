using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Remembers an Agent's own <c>post_message</c> calls into Rooms other than the one its current Turn
/// is running in, so its next Turn in each of those Rooms can be told what it said there (RS §6.7,
/// finding P-7). An Agent never receives its own Message (<c>AgentGateway.DeliverAsync</c>), and under
/// Room Sessions a Room Session sees only its own Room's Transcript, so a post made from a Turn in
/// Room A into Room B would otherwise never reach B's own session at all.
/// </summary>
/// <remarks>
/// <para>
/// A singleton, exactly like <see cref="Agency.Huddle.App.Acp.RoomFollows"/>:
/// <see cref="Agency.Huddle.App.Acp.Tools.PostMessageTool"/> takes it from DI the same way it already
/// takes <c>ChatService</c>, and it outlives any one <see cref="Agency.Huddle.App.Acp.PersonaRunner"/>
/// or <see cref="RoomSession"/>, so a forgotten entry self-heals on restart through
/// <see cref="ClearAgent"/> rather than relying on object lifetime.
/// </para>
/// <para>
/// A post into a Room whose own Room Session is currently <see langword="Busy"/> is not recorded at
/// all: that session made the post, so it already knows about it (RS §6.7). <see cref="BeginTurn"/>
/// and <see cref="EndTurn"/> mark that window; a <see langword="null"/> Room id means a shared session
/// (Phase 0/1), which is Busy for every Room the Agent is in at once, per <c>BeginTurn(agent, null)</c>
/// refusing every <see cref="Record"/> call while it holds the Turn.
/// </para>
/// </remarks>
internal sealed class OwnPosts(IOptions<TeamOptions> options)
{
    private readonly Dictionary<string, Dictionary<string, List<string>>> postsByAgent = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> busyRoomsByAgent = new(StringComparer.Ordinal);
    private readonly HashSet<string> sharedBusyAgents = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>
    /// Records <paramref name="text"/> as posted by <paramref name="agentId"/> into
    /// <paramref name="roomId"/>, unless that Room (or, in shared mode, every Room) is currently
    /// marked <see langword="Busy"/> for this Agent by <see cref="BeginTurn"/>: that session made the
    /// post itself and needs no reminder of it. Capped at <see cref="AcpOptions.CatchUpMessages"/> per
    /// Room; the oldest line is dropped once the cap is reached.
    /// </summary>
    /// <param name="agentId">The Agent that posted.</param>
    /// <param name="roomId">The Room it posted into.</param>
    /// <param name="text">The posted text.</param>
    internal void Record(string agentId, string roomId, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(text);

        lock (this.gate)
        {
            if (this.IsBusyLocked(agentId, roomId))
            {
                return;
            }

            if (!this.postsByAgent.TryGetValue(agentId, out var byRoom))
            {
                byRoom = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                this.postsByAgent[agentId] = byRoom;
            }

            if (!byRoom.TryGetValue(roomId, out var lines))
            {
                lines = [];
                byRoom[roomId] = lines;
            }

            lines.Add(text);

            var cap = options.Value.Acp.CatchUpMessages;
            while (lines.Count > cap && cap >= 0)
            {
                lines.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Returns and clears every line recorded for <paramref name="agentId"/> in
    /// <paramref name="roomId"/>, oldest first.
    /// </summary>
    /// <param name="agentId">The Agent whose own posts are being drained.</param>
    /// <param name="roomId">The Room to drain.</param>
    /// <returns>The recorded lines, oldest first, or an empty list when nothing was recorded.</returns>
    internal IReadOnlyList<string> Take(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            if (!this.postsByAgent.TryGetValue(agentId, out var byRoom) || !byRoom.TryGetValue(roomId, out var lines))
            {
                return [];
            }

            byRoom.Remove(roomId);
            if (byRoom.Count == 0)
            {
                this.postsByAgent.Remove(agentId);
            }

            return lines;
        }
    }

    /// <summary>
    /// Marks <paramref name="roomId"/> (or, when <see langword="null"/>, every Room) <see langword="Busy"/>
    /// for <paramref name="agentId"/>: a <see cref="Record"/> call for that Agent and Room while this
    /// holds is dropped, because the Turn now starting is the one making the post.
    /// </summary>
    /// <param name="agentId">The Agent whose Turn is starting.</param>
    /// <param name="roomId">The Room the Turn belongs to, or <see langword="null"/> for a shared session.</param>
    internal void BeginTurn(string agentId, string? roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            if (roomId is null)
            {
                this.sharedBusyAgents.Add(agentId);
                return;
            }

            if (!this.busyRoomsByAgent.TryGetValue(agentId, out var rooms))
            {
                rooms = new HashSet<string>(StringComparer.Ordinal);
                this.busyRoomsByAgent[agentId] = rooms;
            }

            rooms.Add(roomId);
        }
    }

    /// <summary>Clears the <see langword="Busy"/> mark <see cref="BeginTurn"/> set for this Agent and Room.</summary>
    /// <param name="agentId">The Agent whose Turn ended.</param>
    /// <param name="roomId">The Room the Turn belonged to, or <see langword="null"/> for a shared session.</param>
    internal void EndTurn(string agentId, string? roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            if (roomId is null)
            {
                this.sharedBusyAgents.Remove(agentId);
                return;
            }

            if (this.busyRoomsByAgent.TryGetValue(agentId, out var rooms) && rooms.Remove(roomId) && rooms.Count == 0)
            {
                this.busyRoomsByAgent.Remove(agentId);
            }
        }
    }

    /// <summary>
    /// Drops every recorded post and Busy mark for <paramref name="agentId"/>, across every Room.
    /// <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> calls this at Welcome, beside
    /// <c>RoomFollows.ClearAgent</c>, so a restart or reconnect self-heals rather than leaving a
    /// stale entry behind forever.
    /// </summary>
    /// <param name="agentId">The Agent whose entries should all be dropped.</param>
    internal void ClearAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            this.postsByAgent.Remove(agentId);
            this.busyRoomsByAgent.Remove(agentId);
            this.sharedBusyAgents.Remove(agentId);
        }
    }

    /// <summary>Whether <paramref name="roomId"/> is currently marked <see langword="Busy"/> for <paramref name="agentId"/>, under <see cref="gate"/>.</summary>
    private bool IsBusyLocked(string agentId, string roomId) =>
        this.sharedBusyAgents.Contains(agentId)
        || (this.busyRoomsByAgent.TryGetValue(agentId, out var rooms) && rooms.Contains(roomId));
}
