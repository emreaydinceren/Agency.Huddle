namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The seam a <see cref="RoomSession"/> calls into around every Turn (RS §6.1's ticket protocol),
/// filled by <c>RoomSessionPool</c>'s <c>TurnGate</c> from D23 on. Every ticket is a
/// <c>QueuedWork.Sequence</c> value.
/// </summary>
internal interface ITurnScheduler
{
    /// <summary>Marks <paramref name="ticket"/> as its Room Session's current head — offered exactly once, per RS §6.1 rule 1.</summary>
    /// <param name="ticket">The ticket now at the head of its queue.</param>
    void Offer(long ticket);

    /// <summary>Waits until <paramref name="ticket"/> is admitted to run its Turn.</summary>
    /// <param name="ticket">The offered ticket to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    Task WaitAsync(long ticket, CancellationToken cancellationToken);

    /// <summary>Withdraws <paramref name="ticket"/>: it will not run. Frees its slot if it was already admitted.</summary>
    /// <param name="ticket">The ticket to withdraw.</param>
    void Withdraw(long ticket);

    /// <summary>Marks an admitted <paramref name="ticket"/>'s Turn as ended, freeing its slot.</summary>
    /// <param name="ticket">The ticket whose Turn ended.</param>
    void Complete(long ticket);

    /// <summary>Makes room, if needed, for <paramref name="requester"/> to open inside its admitted slot (finding P-5).</summary>
    /// <param name="requester">The Room Session that is about to open.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    Task MakeRoomToOpenAsync(RoomSession requester, CancellationToken cancellationToken);
}
