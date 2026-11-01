using Agency.Huddle.Acp.DotAcp;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>Covers <see cref="WireOrderLedger"/>: the per-session read and dispatched counts behind the Turn-completion barrier.</summary>
public sealed class WireOrderLedgerTests
{
    /// <summary>A wait for target 0 (nothing was read) completes at once.</summary>
    [Fact(Timeout = 10000)]
    public async Task WaitUntilDispatchedAsync_TargetZero_CompletesAtOnce()
    {
        WireOrderLedger ledger = new();

        Task wait = ledger.WaitUntilDispatchedAsync("s1", 0, TestContext.Current.CancellationToken);
        await wait;

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>Two updates read and one dispatched keeps the wait open; the second dispatch completes it.</summary>
    [Fact(Timeout = 10000)]
    public async Task WaitUntilDispatchedAsync_ReadTwoDispatchOne_WaitsForSecondDispatch()
    {
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        ledger.NoteRead("s1");
        long target = ledger.ReadCount("s1");
        Task wait = ledger.WaitUntilDispatchedAsync("s1", target, TestContext.Current.CancellationToken);

        ledger.NoteDispatched("s1");
        Assert.False(wait.IsCompleted);
        ledger.NoteDispatched("s1");
        await wait;

        Assert.Equal(2, target);
        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>Dispatches of another session never complete the wait.</summary>
    [Fact(Timeout = 10000)]
    public async Task WaitUntilDispatchedAsync_OtherSessionDispatches_DoNotComplete()
    {
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        Task wait = ledger.WaitUntilDispatchedAsync("s1", ledger.ReadCount("s1"), TestContext.Current.CancellationToken);

        ledger.NoteRead("s2");
        ledger.NoteDispatched("s2");
        ledger.NoteDispatched("s2");
        Assert.False(wait.IsCompleted);
        ledger.NoteDispatched("s1");
        await wait;

        Assert.Equal(1, ledger.ReadCount("s1"));
        Assert.Equal(1, ledger.ReadCount("s2"));
    }

    /// <summary>Cancelling the token ends the wait with an <see cref="OperationCanceledException"/> (a <see cref="TaskCanceledException"/>); the caller decides what that means.</summary>
    [Fact(Timeout = 10000)]
    public async Task WaitUntilDispatchedAsync_Cancelled_ThrowsOperationCanceled()
    {
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task wait = ledger.WaitUntilDispatchedAsync("s1", 1, cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    /// <summary>A dispatch that arrives before the wait starts still counts.</summary>
    [Fact(Timeout = 10000)]
    public async Task WaitUntilDispatchedAsync_DispatchBeforeWait_StillCounts()
    {
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        ledger.NoteDispatched("s1");

        Task wait = ledger.WaitUntilDispatchedAsync("s1", ledger.ReadCount("s1"), TestContext.Current.CancellationToken);
        await wait;

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>
    /// After a timed-out barrier settles an update that was read but never dispatched, the next Turn's
    /// barrier waits only for its own update, not for the lost one as well.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task Settle_LostUpdate_NextTurnOwesOnlyItsOwn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        ledger.Settle("s1", ledger.ReadCount("s1"));

        ledger.NoteRead("s1");
        Task wait = ledger.WaitUntilDispatchedAsync("s1", ledger.ReadCount("s1"), ct);
        ledger.NoteDispatched("s1");
        await wait;

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>Forgetting a session releases a waiter on it, even when its update was never dispatched.</summary>
    [Fact(Timeout = 10000)]
    public async Task Forget_ReleasesWaiter()
    {
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        Task wait = ledger.WaitUntilDispatchedAsync("s1", 1, TestContext.Current.CancellationToken);

        ledger.Forget("s1");
        await wait;

        Assert.True(wait.IsCompletedSuccessfully);
    }

    /// <summary>Forgetting a session whose every read update was dispatched resets its counts.</summary>
    [Fact]
    public void Forget_Balanced_ResetsCounts()
    {
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        ledger.NoteDispatched("s1");

        ledger.Forget("s1");

        Assert.Equal(0, ledger.ReadCount("s1"));
    }

    /// <summary>
    /// An update read before the close but dispatched after it must not leave a dispatch with no read:
    /// a resumed session with the same id would start one dispatch ahead, and its next Turn's barrier
    /// would open before that Turn's own update was delivered.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task Forget_UpdateInFlight_ResumedSessionStillWaitsForItsOwnUpdate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WireOrderLedger ledger = new();
        ledger.NoteRead("s1");
        ledger.Forget("s1");
        ledger.NoteDispatched("s1");

        ledger.NoteRead("s1");
        Task wait = ledger.WaitUntilDispatchedAsync("s1", ledger.ReadCount("s1"), ct);
        await Task.Delay(50, ct);
        bool openedEarly = wait.IsCompleted;
        ledger.NoteDispatched("s1");
        await wait;

        Assert.False(openedEarly);
    }
}
