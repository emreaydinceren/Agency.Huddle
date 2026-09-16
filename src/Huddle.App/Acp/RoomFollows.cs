namespace Agency.Huddle.App.Acp;

/// <summary>
/// Tracks which Agents are following which Rooms - roadmap item 8's mechanism for an Agent to
/// receive every Message in a Room without being Mentioned in each one. Keyed by Room id, because
/// the hot read is "who is following this Room": <see cref="RoomReplyResolver.Resolve"/> and the
/// Room view both need that set on every delivery, while a follow or an unfollow happens only when a
/// (later) tool call runs.
/// </summary>
/// <remarks>
/// <para>
/// A singleton, not a field on <c>PersonaRunner</c>. ADR-0005 specified a per-Persona
/// <see cref="HashSet{T}"/> shared through <c>IAgentHostFactory.CreateAsync</c>, but that call
/// returns only an <c>(IAgentHost, IAgentSession)</c> pair - the tools built inside it are invisible
/// to the caller - so sharing a set that way would mean widening <see cref="IAgentHostFactory"/>,
/// whose own doc comment calls it "purely a test seam" for exercising the pipe path against a fake
/// agent for zero tokens. A singleton sidesteps that entirely: a future follow/unfollow tool takes
/// this from DI exactly as <c>PostMessageTool</c> takes <c>ChatService</c> today, changing no seam,
/// and it gives one source of truth that the Room view can also read directly.
/// </para>
/// <para>
/// This state never crosses the wire, which is the property ADR-0005 and <c>traps.md</c> both rely
/// on for roadmap item 8 costing no <c>ProtocolVersion</c> bump: <see cref="ReplyGate.Decide"/>'s
/// <c>following</c> parameter is computed in-process, from this table, and never appears on an
/// Envelope.
/// </para>
/// <para>
/// Self-heal on restart is explicit here, not incidental. A per-runner field would have gotten a
/// forgotten <c>unfollow_room</c> cleared for free, on object lifetime, the moment the runner was
/// recreated. A singleton outlives any one runner, so <c>PersonaRunner</c> calls
/// <see cref="ClearAgent"/> itself, right after its handshake yields an Agent id and before its read
/// loop starts. Doing it explicitly rather than inheriting it from lifetime is acceptable - it is
/// testable on its own, and it also covers a case object-lifetime self-heal never could: an Agent
/// that reconnects on the same pipe without its <c>PersonaRunner</c> ever being recreated.
/// </para>
/// </remarks>
internal sealed class RoomFollows
{
    private readonly Dictionary<string, HashSet<string>> followersByRoom = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>
    /// Makes <paramref name="agentId"/> follow <paramref name="roomId"/>: every future Message
    /// delivered to it in that Room passes <see cref="ReplyGate.Decide"/>'s <c>following</c> argument
    /// as <see langword="true"/>, regardless of Mention.
    /// </summary>
    /// <param name="agentId">The following Agent's id.</param>
    /// <param name="roomId">The Room to follow.</param>
    /// <returns>
    /// <see langword="true"/> if this Agent was not already following the Room; <see langword="false"/>
    /// if it already was, so a caller can say "already following" rather than claiming a fresh action
    /// that did not happen.
    /// </returns>
    internal bool Follow(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            if (!this.followersByRoom.TryGetValue(roomId, out var followers))
            {
                followers = new HashSet<string>(StringComparer.Ordinal);
                this.followersByRoom[roomId] = followers;
            }

            return followers.Add(agentId);
        }
    }

    /// <summary>
    /// Stops <paramref name="agentId"/> following <paramref name="roomId"/>. Removes the Room's own
    /// entry once its follower set empties, so a Room nobody follows any more does not sit in the
    /// table forever.
    /// </summary>
    /// <param name="agentId">The Agent to stop following.</param>
    /// <param name="roomId">The Room to unfollow.</param>
    /// <returns><see langword="true"/> if the Agent had been following the Room; otherwise <see langword="false"/>.</returns>
    internal bool Unfollow(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            if (!this.followersByRoom.TryGetValue(roomId, out var followers))
            {
                return false;
            }

            var removed = followers.Remove(agentId);
            if (followers.Count == 0)
            {
                this.followersByRoom.Remove(roomId);
            }

            return removed;
        }
    }

    /// <summary>Whether <paramref name="agentId"/> currently follows <paramref name="roomId"/>.</summary>
    /// <param name="agentId">The Agent to check.</param>
    /// <param name="roomId">The Room to check.</param>
    /// <returns><see langword="true"/> if the Agent is currently following the Room.</returns>
    internal bool IsFollowing(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            return this.followersByRoom.TryGetValue(roomId, out var followers) && followers.Contains(agentId);
        }
    }

    /// <summary>
    /// Every Agent id currently following <paramref name="roomId"/>, as a snapshot copy - never the
    /// live set - so a caller can read it outside <see cref="gate"/> while a tool call mutates the
    /// real set on another thread. Empty, never <see langword="null"/>, when the Room has no
    /// followers.
    /// </summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>A snapshot of the Agent ids following <paramref name="roomId"/> right now.</returns>
    internal IReadOnlySet<string> FollowersOf(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            return this.followersByRoom.TryGetValue(roomId, out var followers)
                ? new HashSet<string>(followers, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Drops every follow <paramref name="agentId"/> holds, across every Room. <c>PersonaRunner</c>
    /// calls this right after its handshake yields an Agent id and before its read loop starts, so a
    /// forgotten <c>unfollow_room</c> self-heals on restart or reconnect rather than leaving a stale
    /// follow behind forever. Harmless to call for an Agent with no recorded follows.
    /// </summary>
    /// <param name="agentId">The Agent whose follows should all be dropped.</param>
    internal void ClearAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            List<string> emptied = [];
            foreach (var (roomId, followers) in this.followersByRoom)
            {
                if (followers.Remove(agentId) && followers.Count == 0)
                {
                    emptied.Add(roomId);
                }
            }

            foreach (var roomId in emptied)
            {
                this.followersByRoom.Remove(roomId);
            }
        }
    }
}
