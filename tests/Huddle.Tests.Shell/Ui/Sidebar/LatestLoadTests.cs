using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="LatestLoad"/>: only the newest load's token stays live.</summary>
public sealed class LatestLoadTests
{
    /// <summary>Starting a second load cancels the first load's token.</summary>
    [Fact]
    public void Next_SecondCall_CancelsThePreviousToken()
    {
        using LatestLoad load = new();
        CancellationToken first = load.Next();

        _ = load.Next();

        Assert.True(first.IsCancellationRequested);
    }

    /// <summary>The newest load's token is live until something supersedes it.</summary>
    [Fact]
    public void Next_NewestToken_IsNotCancelled()
    {
        using LatestLoad load = new();
        _ = load.Next();

        CancellationToken newest = load.Next();

        Assert.False(newest.IsCancellationRequested);
    }

    /// <summary>Cancel stops the current load without starting another.</summary>
    [Fact]
    public void Cancel_CurrentLoad_CancelsItsToken()
    {
        using LatestLoad load = new();
        CancellationToken current = load.Next();

        load.Cancel();

        Assert.True(current.IsCancellationRequested);
    }

    /// <summary>Cancel with no load in flight is harmless, and a later Next still yields a live token.</summary>
    [Fact]
    public void Cancel_NoLoad_LeavesNextUsable()
    {
        using LatestLoad load = new();

        load.Cancel();

        Assert.False(load.Next().IsCancellationRequested);
    }

    /// <summary>Dispose cancels the current load, and a load started afterwards (a redraw queued before Dispose) gets an already-cancelled token instead of an exception.</summary>
    [Fact]
    public void Dispose_CancelsCurrentLoad_AndNextIsAlreadyCancelled()
    {
        LatestLoad load = new();
        CancellationToken current = load.Next();

        load.Dispose();

        Assert.True(current.IsCancellationRequested);
        Assert.True(load.Next().IsCancellationRequested);
    }
}
