using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// A test's timeout for the behaviour it checks, whose clock does not run while a <see cref="PipeHostFixture"/> starts. Pass it
/// to <see cref="PipeHostFixture.StartAsync(IReadOnlyDictionary{string, string?}?, BehaviourBudget)"/>: the fixture pauses it,
/// starts under its own <see cref="TestBudgets.Slowest"/> bound, and restarts it with the whole <see cref="Budget"/>
/// once the host is up.
/// </summary>
/// <remarks>
/// A first fixture in a cold process took 6-8 s to start in a 4-CPU Linux container running 24 test processes, so a 10 s
/// token shared by the start and the behaviour left the behaviour 2-4 s and failed at 10.0 s with the awaited event a few
/// milliseconds away. The budget is also cancelled when the test is (<c>TestContext.Current.CancellationToken</c>).
/// </remarks>
public sealed class BehaviourBudget : CancellationTokenSource
{
    private readonly CancellationTokenRegistration testCancelled;

    /// <summary>Starts the clock now; a fixture start pauses it and then restarts it in full.</summary>
    /// <param name="budget">How long the behaviour may take once the fixture is up.</param>
    public BehaviourBudget(TimeSpan budget)
        : base(budget)
    {
        this.Budget = budget;
        this.testCancelled = TestContext.Current.CancellationToken.Register(static state => (state as BehaviourBudget)?.Cancel(), this);
    }

    /// <summary>How long the behaviour may take once the fixture is up.</summary>
    public TimeSpan Budget { get; }

    /// <summary>Stops the clock while a fixture starts.</summary>
    internal void Pause() => this.CancelAfter(Timeout.InfiniteTimeSpan);

    /// <summary>Restarts the clock with the whole <see cref="Budget"/>.</summary>
    internal void Restart() => this.CancelAfter(this.Budget);

    /// <summary>Releases the link to the test's own cancellation, then the source.</summary>
    /// <param name="disposing">Whether this is a call to <see cref="CancellationTokenSource.Dispose()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.testCancelled.Dispose();
        }

        base.Dispose(disposing);
    }
}
