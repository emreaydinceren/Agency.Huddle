using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Sessions.Fakes;

/// <summary>
/// A hand-written <see cref="ITurnScheduler"/> test double (Task 22.1.t) that admits every ticket at
/// once - like <see cref="ImmediateTurnScheduler"/> - but records every call, in call order, so a
/// test can prove <see cref="RoomSession"/> follows RS §6.1's ticket protocol exactly: one
/// <see cref="Offer"/> per ticket, a matching <see cref="Complete"/> for every admitted one, and
/// <see cref="Offer"/> for the next item before <see cref="Complete"/> for the current one.
/// </summary>
internal sealed class RecordingTurnScheduler : ITurnScheduler
{
    private readonly Lock gate = new();
    private readonly List<string> calls = [];

    /// <summary>Every call this scheduler has recorded, as <c>"MethodName(ticket)"</c>, in call order.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.calls];
            }
        }
    }

    /// <summary>How many times <see cref="Offer"/> has been called.</summary>
    public int OfferCount => this.CountOf(nameof(this.Offer));

    /// <summary>How many times <see cref="Complete"/> has been called.</summary>
    public int CompleteCount => this.CountOf(nameof(this.Complete));

    /// <inheritdoc />
    public void Offer(long ticket) => this.Record(nameof(this.Offer), ticket);

    /// <inheritdoc />
    public Task WaitAsync(long ticket, CancellationToken cancellationToken)
    {
        this.Record(nameof(this.WaitAsync), ticket);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Withdraw(long ticket) => this.Record(nameof(this.Withdraw), ticket);

    /// <inheritdoc />
    public void Complete(long ticket) => this.Record(nameof(this.Complete), ticket);

    /// <inheritdoc />
    public Task MakeRoomToOpenAsync(RoomSession requester, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requester);
        this.Record(nameof(this.MakeRoomToOpenAsync), 0);
        return Task.CompletedTask;
    }

    private int CountOf(string name)
    {
        lock (this.gate)
        {
            return this.calls.Count(call => call.StartsWith(name + "(", StringComparison.Ordinal));
        }
    }

    private void Record(string name, long ticket)
    {
        lock (this.gate)
        {
            this.calls.Add($"{name}({ticket})");
        }
    }
}
