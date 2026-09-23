namespace Agency.Huddle.Tests.Acp.Sessions;

using Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Pins <see cref="TurnGate"/> (RS §6.2 "Concurrency", finding P-4): it admits the lowest-numbered
/// offered ticket first, regardless of arrival order at the gate, which a plain
/// <see cref="SemaphoreSlim"/> cannot guarantee.
/// </summary>
public sealed class TurnGateTests
{
    /// <summary>Two tickets offered out of order, while a slot is occupied, admit the lower one first once it frees.</summary>
    [Fact]
    public async Task Max1_OfferedOutOfOrder_AdmitsLowestFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Offer(5);
        await gate.WaitAsync(5, ct);

        gate.Offer(3);
        gate.Offer(1);
        gate.Complete(5);

        var wait1 = gate.WaitAsync(1, ct);
        var wait3 = gate.WaitAsync(3, ct);

        await wait1.WaitAsync(TimeSpan.FromSeconds(2), ct);
        await Task.Delay(50, ct);
        Assert.False(wait3.IsCompleted);
    }

    /// <summary>A second ticket offered at max 1 waits until the first completes.</summary>
    [Fact]
    public async Task Max1_SecondWaitsUntilComplete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Offer(1);
        await gate.WaitAsync(1, ct);
        gate.Offer(2);
        var wait2 = gate.WaitAsync(2, ct);

        await Task.Delay(50, ct);
        Assert.False(wait2.IsCompleted);

        gate.Complete(1);

        await wait2.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    /// <summary>
    /// Finding P-4's interleaving: ticket 1 (Room A) is offered and admitted; ticket 3 (Room B) is
    /// offered next and waits; Room A offers its own next ticket, 2, before completing 1. Once 1
    /// completes, 2 must be admitted before 3, preserving cross-Room arrival order.
    /// </summary>
    [Fact]
    public async Task Max1_NextOfferedBeforeCompleteOfPrevious_KeepsArrivalOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Offer(1);
        await gate.WaitAsync(1, ct);
        gate.Offer(3);
        gate.Offer(2);
        gate.Complete(1);

        var wait2 = gate.WaitAsync(2, ct);
        var wait3 = gate.WaitAsync(3, ct);

        await wait2.WaitAsync(TimeSpan.FromSeconds(2), ct);
        await Task.Delay(50, ct);
        Assert.False(wait3.IsCompleted);
    }

    /// <summary>At max 2, two tickets run concurrently and a third waits.</summary>
    [Fact]
    public async Task Max2_TwoRun_ThirdWaits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(2);

        gate.Offer(1);
        gate.Offer(2);
        gate.Offer(3);

        var wait1 = gate.WaitAsync(1, ct);
        var wait2 = gate.WaitAsync(2, ct);
        var wait3 = gate.WaitAsync(3, ct);

        await wait1.WaitAsync(TimeSpan.FromSeconds(2), ct);
        await wait2.WaitAsync(TimeSpan.FromSeconds(2), ct);
        await Task.Delay(50, ct);
        Assert.False(wait3.IsCompleted);
        Assert.Equal(2, gate.Running);
    }

    /// <summary>Withdrawing a waiting (not yet admitted) ticket lets a later ticket take its place once a slot frees.</summary>
    [Fact]
    public async Task Withdraw_Waiting_LetsNextIn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Offer(1);
        await gate.WaitAsync(1, ct);
        gate.Offer(2);
        gate.Offer(3);

        gate.Withdraw(2);
        gate.Complete(1);

        var wait3 = gate.WaitAsync(3, ct);
        await wait3.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(1, gate.Running);
    }

    /// <summary>Withdrawing an already-admitted ticket frees its slot for the next waiting ticket.</summary>
    [Fact]
    public async Task Withdraw_Admitted_ReleasesSlot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Offer(1);
        await gate.WaitAsync(1, ct);
        gate.Offer(2);

        gate.Withdraw(1);

        var wait2 = gate.WaitAsync(2, ct);
        await wait2.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(1, gate.Running);
    }

    /// <summary><see cref="TurnGate.Complete"/> of a ticket that was never offered is silently ignored.</summary>
    [Fact]
    public async Task Complete_Unknown_Ignored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Complete(999);
        gate.Offer(1);
        var wait1 = gate.WaitAsync(1, ct);

        await wait1.WaitAsync(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(1, gate.Running);
    }

    /// <summary>Cancelling a pending <see cref="TurnGate.WaitAsync"/> withdraws its ticket, so a later ticket is admitted in its place.</summary>
    [Fact]
    public async Task WaitCancelled_Withdraws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnGate gate = new(1);

        gate.Offer(1);
        await gate.WaitAsync(1, ct);
        gate.Offer(2);

        using CancellationTokenSource cts = new();
        var wait2 = gate.WaitAsync(2, cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(() => wait2);

        gate.Offer(3);
        gate.Complete(1);

        var wait3 = gate.WaitAsync(3, ct);
        await wait3.WaitAsync(TimeSpan.FromSeconds(2), ct);
    }

    /// <summary><see cref="TurnGate.Running"/> counts only admitted tickets, not waiting ones.</summary>
    [Fact]
    public void Running_CountsAdmitted()
    {
        TurnGate gate = new(2);

        gate.Offer(1);
        gate.Offer(2);
        Assert.Equal(2, gate.Running);

        gate.Offer(3);
        Assert.Equal(2, gate.Running);

        gate.Complete(1);
        Assert.Equal(2, gate.Running);

        gate.Complete(2);
        Assert.Equal(1, gate.Running);

        gate.Complete(3);
        Assert.Equal(0, gate.Running);
    }
}
