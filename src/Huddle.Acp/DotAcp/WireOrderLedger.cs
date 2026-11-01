namespace Agency.Huddle.Acp.DotAcp;

/// <summary>
/// Counts, per session, the <c>session/update</c> notifications read off an agent's wire and the ones
/// dispatched to the client adapter, so a Turn can wait until everything the agent wrote before its
/// <c>session/prompt</c> response has actually been delivered.
/// </summary>
/// <remarks>
/// <para>
/// The stream that reads the agent's output calls <see cref="NoteRead"/> for each update line, in wire
/// order, before StreamJsonRpc can see any later line. <see cref="DotAcpClientAdapter"/> calls
/// <see cref="NoteDispatched"/> once it has handed that update to the session's sink. After a
/// response arrives, <see cref="ReadCount"/> is therefore at least the number of updates that preceded
/// it, and <see cref="WaitUntilDispatchedAsync"/> on that number returns only when all of them have
/// been dispatched. Updates read after the response may raise the count further; waiting for those
/// too is harmless.
/// </para>
/// <para>
/// One instance serves one host; its sessions are independent of each other. All state is guarded by
/// one <see cref="Lock"/>, held only for counter updates and waiter bookkeeping, never while a waiter
/// continues (waiters complete with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>).
/// </para>
/// </remarks>
internal sealed class WireOrderLedger
{
    private readonly Lock gate = new();

    private readonly Dictionary<string, SessionCounts> sessions = new(StringComparer.Ordinal);

    /// <summary>Records that one <c>session/update</c> for <paramref name="sessionId"/> was read off the wire.</summary>
    /// <param name="sessionId">The session the update belongs to.</param>
    public void NoteRead(string sessionId)
    {
        lock (this.gate)
        {
            this.CountsFor(sessionId).Read++;
        }
    }

    /// <summary>Records that one <c>session/update</c> for <paramref name="sessionId"/> has been dispatched, and wakes every waiter it satisfies.</summary>
    /// <param name="sessionId">The session the update belongs to.</param>
    public void NoteDispatched(string sessionId)
    {
        lock (this.gate)
        {
            SessionCounts counts = this.CountsFor(sessionId);
            counts.Dispatched++;
            WakeSatisfied(counts);
        }
    }

    /// <summary>Completes and removes every waiter whose target the dispatched count has reached. The caller holds the lock.</summary>
    /// <param name="counts">The session's counts.</param>
    private static void WakeSatisfied(SessionCounts counts)
    {
        for (int index = counts.Waiters.Count - 1; index >= 0; index--)
        {
            Waiter waiter = counts.Waiters[index];
            if (waiter.Target <= counts.Dispatched)
            {
                counts.Waiters.RemoveAt(index);
                _ = waiter.Completion.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Treats every update up to <paramref name="target"/> as dispatched, and wakes the waiters that
    /// satisfies. A Turn calls this when its barrier timed out, so an update that was read but never
    /// dispatched, for example one StreamJsonRpc could not deserialize, is not still owed by every
    /// later Turn of the session.
    /// </summary>
    /// <param name="sessionId">The session whose barrier timed out.</param>
    /// <param name="target">The dispatched count that Turn waited for.</param>
    public void Settle(string sessionId, long target)
    {
        lock (this.gate)
        {
            SessionCounts counts = this.CountsFor(sessionId);
            counts.Dispatched = Math.Max(counts.Dispatched, target);
            WakeSatisfied(counts);
        }
    }

    /// <summary>Gets how many updates for <paramref name="sessionId"/> have been read off the wire so far.</summary>
    /// <param name="sessionId">The session to count.</param>
    /// <returns>The read count; 0 for a session never seen.</returns>
    public long ReadCount(string sessionId)
    {
        lock (this.gate)
        {
            return this.sessions.TryGetValue(sessionId, out SessionCounts? counts) ? counts.Read : 0;
        }
    }

    /// <summary>
    /// Completes when at least <paramref name="target"/> updates for <paramref name="sessionId"/> have
    /// been dispatched. A dispatch that happened before this call counts.
    /// </summary>
    /// <param name="sessionId">The session to wait on.</param>
    /// <param name="target">The dispatched count to reach; 0 or less completes at once.</param>
    /// <param name="cancellationToken">Ends the wait with an <see cref="OperationCanceledException"/>.</param>
    /// <returns>A task that completes when the target is reached, or the session is forgotten.</returns>
    public async Task WaitUntilDispatchedAsync(string sessionId, long target, CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Waiter waiter = new(target, completion);
        lock (this.gate)
        {
            SessionCounts counts = this.CountsFor(sessionId);
            if (counts.Dispatched >= target)
            {
                return;
            }

            counts.Waiters.Add(waiter);
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => this.Abandon(sessionId, waiter, cancellationToken));

        await completion.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Releases anything still waiting on a closed session, and drops its counts when every update read
    /// has also been dispatched.
    /// </summary>
    /// <remarks>
    /// Counts that do not balance are kept. An update read before the close but dispatched after it
    /// would otherwise recreate the entry as one dispatch with no read. A later resume of the same
    /// session id would then start one dispatch ahead, and its barrier could release a Turn before the
    /// Turn's last chunk was delivered.
    /// </remarks>
    /// <param name="sessionId">The session that closed.</param>
    public void Forget(string sessionId)
    {
        lock (this.gate)
        {
            if (!this.sessions.TryGetValue(sessionId, out SessionCounts? counts))
            {
                return;
            }

            foreach (Waiter waiter in counts.Waiters)
            {
                _ = waiter.Completion.TrySetResult();
            }

            counts.Waiters.Clear();
            if (counts.Read == counts.Dispatched)
            {
                _ = this.sessions.Remove(sessionId);
            }
        }
    }

    /// <summary>Removes a cancelled waiter and cancels its task.</summary>
    /// <param name="sessionId">The session the waiter was on.</param>
    /// <param name="waiter">The waiter to cancel.</param>
    /// <param name="cancellationToken">The token that was cancelled.</param>
    private void Abandon(string sessionId, Waiter waiter, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (this.sessions.TryGetValue(sessionId, out SessionCounts? counts))
            {
                _ = counts.Waiters.Remove(waiter);
            }
        }

        _ = waiter.Completion.TrySetCanceled(cancellationToken);
    }

    /// <summary>Gets or creates the counts of a session. The caller holds the lock.</summary>
    /// <param name="sessionId">The session.</param>
    private SessionCounts CountsFor(string sessionId)
    {
        if (!this.sessions.TryGetValue(sessionId, out SessionCounts? counts))
        {
            counts = new SessionCounts();
            this.sessions[sessionId] = counts;
        }

        return counts;
    }

    private sealed class SessionCounts
    {
        internal long Read { get; set; }

        internal long Dispatched { get; set; }

        internal List<Waiter> Waiters { get; } = [];
    }

    private sealed record Waiter(long Target, TaskCompletionSource Completion);
}
