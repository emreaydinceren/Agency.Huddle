namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The scheduler for one shared session (Phase 0/1: <c>SessionPerRoom: false</c>). Every member is a
/// no-op that completes at once - one session needs no scheduling, since there is nothing else to
/// admit ahead of or behind it.
/// </summary>
internal sealed class ImmediateTurnScheduler : ITurnScheduler
{
    /// <inheritdoc />
    public void Offer(long ticket)
    {
        // No other Room Session ever contends for a slot in shared mode.
    }

    /// <inheritdoc />
    public Task WaitAsync(long ticket, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Withdraw(long ticket)
    {
        // Nothing was ever admitted that needs releasing.
    }

    /// <inheritdoc />
    public void Complete(long ticket)
    {
        // Nothing to free: WaitAsync never held a slot open.
    }

    /// <inheritdoc />
    public Task MakeRoomToOpenAsync(RoomSession requester, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requester);
        return Task.CompletedTask;
    }
}
