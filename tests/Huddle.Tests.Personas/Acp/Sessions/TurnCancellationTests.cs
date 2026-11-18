using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Sessions;

public sealed class TurnCancellationTests
{
    /// <summary>
    /// Issue 677's ordering, made deterministic: the Turn ends on its own and its teardown disposes the source, then the Stop
    /// that read the Turn a moment earlier cancels it. The cancel must do nothing rather than throw ObjectDisposedException.
    /// </summary>
    [Fact]
    public void Cancel_AfterTheTurnsTeardownDisposedIt_DoesNotThrow()
    {
        TurnCancellation cancellation = new(TestContext.Current.CancellationToken);
        CancellationToken token = cancellation.Token;

        cancellation.Dispose();
        Exception? thrown = Record.Exception(cancellation.Cancel);

        Assert.Null(thrown);
        Assert.False(token.IsCancellationRequested);
    }

    /// <summary>Cancel cancels the token and runs its callbacks before it returns, so a Stop releases the consumer at once.</summary>
    [Fact]
    public void Cancel_WhileLive_CancelsTheTokenSynchronously()
    {
        using TurnCancellation cancellation = new(TestContext.Current.CancellationToken);
        bool ran = false;
        using CancellationTokenRegistration registration = cancellation.Token.Register(() => ran = true);

        cancellation.Cancel();
        cancellation.Cancel();

        Assert.True(cancellation.Token.IsCancellationRequested);
        Assert.True(ran);
    }

    /// <summary>A Turn that ended on its own is not cancelled by its teardown, so nothing registered on it sees a late Stop.</summary>
    [Fact]
    public void Dispose_WithoutCancel_LeavesTheTokenUncancelled()
    {
        TurnCancellation cancellation = new(TestContext.Current.CancellationToken);
        CancellationToken token = cancellation.Token;

        cancellation.Dispose();
        cancellation.Dispose();

        Assert.False(token.IsCancellationRequested);
    }

    /// <summary>The run token ending (a shutdown) cancels the Turn's token too.</summary>
    [Fact]
    public void Token_WhenTheRunTokenIsCancelled_IsCancelled()
    {
        using CancellationTokenSource run = new();
        using TurnCancellation cancellation = new(run.Token);

        run.Cancel();

        Assert.True(cancellation.Token.IsCancellationRequested);
    }
}
