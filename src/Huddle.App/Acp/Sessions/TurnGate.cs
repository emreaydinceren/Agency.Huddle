namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// A ticketed admission gate for Turns across every Room Session a Persona owns (RS §6.2
/// "Concurrency", finding <b>P-4</b>). Admits the lowest-numbered offered ticket first, whenever a
/// slot is free, so cross-Room arrival order is preserved exactly.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not a <see cref="SemaphoreSlim"/> (finding P-4):</b> a semaphore admits whichever waiter
/// happens to be parked on it when a slot frees, which is release order, not ticket order. With
/// items A1, A2, B1 arriving in that order and <c>MaxConcurrentTurns</c> 1: A1 runs; B's consumer is
/// already blocked waiting for the semaphore by the time A1 releases it, so B1 is handed the slot
/// before A2 ever gets a chance to queue behind it — even though A2 arrived first. A semaphore has no
/// concept of "the next ticket", only "the next thread that happened to call Wait". This gate fixes
/// that by tracking tickets explicitly in a <see cref="SortedDictionary{TKey,TValue}"/> and always
/// admitting the numerically lowest one waiting, regardless of arrival order at the gate itself.
/// </para>
/// <para>
/// Every member takes one <see cref="Lock"/>. <see cref="Offer"/> registers a ticket and immediately
/// pumps; <see cref="Complete"/> and <see cref="Withdraw"/> of an admitted ticket free its slot and
/// pump again — the RS §6.1 ticket protocol relies on a Room Session offering its next ticket
/// <em>before</em> completing its current one, so that pump, not the caller, decides who runs next.
/// </para>
/// </remarks>
/// <param name="maxConcurrent">The most tickets this gate admits at once.</param>
internal sealed class TurnGate(int maxConcurrent)
{
    private readonly Lock gate = new();
    private readonly SortedDictionary<long, TaskCompletionSource> waiting = [];
    private readonly HashSet<long> admitted = [];

    /// <summary>How many tickets are currently admitted.</summary>
    internal int Running
    {
        get
        {
            lock (this.gate)
            {
                return this.admitted.Count;
            }
        }
    }

    /// <summary>Marks <paramref name="ticket"/> as waiting to run, admitting it at once if a slot is free.</summary>
    /// <param name="ticket">The ticket to offer. Offered exactly once, per RS §6.1 rule 1.</param>
    internal void Offer(long ticket)
    {
        lock (this.gate)
        {
            if (this.waiting.ContainsKey(ticket) || this.admitted.Contains(ticket))
            {
                // Already offered or already admitted: RS §6.1 rule 1 promises this never happens,
                // but a defensive no-op is cheaper than a corrupted gate if it ever does.
                return;
            }

            this.waiting[ticket] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.Pump();
        }
    }

    /// <summary>Waits until <paramref name="ticket"/> is admitted.</summary>
    /// <param name="ticket">The offered ticket to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait; on cancellation, <paramref name="ticket"/> is withdrawn.</param>
    internal Task WaitAsync(long ticket, CancellationToken cancellationToken)
    {
        Task? pending;
        lock (this.gate)
        {
            pending = this.waiting.TryGetValue(ticket, out var tcs) ? tcs.Task : null;
        }

        // Already admitted (or, defensively, unknown): nothing left to wait for.
        return pending is null ? Task.CompletedTask : this.WaitOrWithdrawAsync(pending, ticket, cancellationToken);
    }

    /// <summary>Withdraws <paramref name="ticket"/>: it will not run. Frees its slot if it was already admitted.</summary>
    /// <param name="ticket">The ticket to withdraw.</param>
    internal void Withdraw(long ticket)
    {
        lock (this.gate)
        {
            if (this.waiting.Remove(ticket, out var tcs))
            {
                tcs.TrySetCanceled();
                return;
            }

            if (this.admitted.Remove(ticket))
            {
                this.Pump();
            }

            // Neither waiting nor admitted: already completed or withdrawn. No-op.
        }
    }

    /// <summary>Marks an admitted <paramref name="ticket"/>'s Turn as ended, freeing its slot.</summary>
    /// <param name="ticket">The ticket whose Turn ended.</param>
    internal void Complete(long ticket)
    {
        lock (this.gate)
        {
            if (this.admitted.Remove(ticket))
            {
                this.Pump();
            }

            // Unknown ticket: already completed, or never admitted. No-op.
        }
    }

    /// <summary>Admits the lowest-numbered waiting tickets while a slot remains free. Called under <see cref="gate"/>.</summary>
    private void Pump()
    {
        while (this.admitted.Count < maxConcurrent && this.waiting.Count > 0)
        {
            var lowest = this.waiting.Keys.First();
            var tcs = this.waiting[lowest];
            this.waiting.Remove(lowest);
            this.admitted.Add(lowest);
            tcs.TrySetResult();
        }
    }

    private async Task WaitOrWithdrawAsync(Task pending, long ticket, CancellationToken cancellationToken)
    {
        try
        {
            await pending.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.Withdraw(ticket);
            throw;
        }
    }
}
