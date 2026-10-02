using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins the second-launch behaviour of <see cref="DesktopHandOff"/>.</summary>
public sealed class DesktopHandOffTests
{
    private static readonly InstanceInfo Info = new("http://127.0.0.1:5123", "token", 4242);

    /// <summary>A token that is already cancelled stops the call even when the lock is free, and nothing is opened or acquired.</summary>
    [Fact]
    public async Task AcquireOrHandOffAsync_CancelledToken_ThrowsOperationCanceledAndAcquiresNothing()
    {
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        RecordingBrowserOpener opener = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DesktopHandOff.AcquireOrHandOffAsync(
                dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), cts.Token));

        Assert.Empty(opener.Opened);
        using InstanceLock? after = InstanceLock.TryAcquire(dir.Path);
        Assert.NotNull(after);
    }

    /// <summary>When nobody owns the lock the caller becomes the owner: the lock is returned and nothing is opened.</summary>
    [Fact]
    public async Task AcquireOrHandOffAsync_LockFree_ReturnsTheLockAndDoesNotOpen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RecordingBrowserOpener opener = new();

        HandOffResult result = await DesktopHandOff.AcquireOrHandOffAsync(
            dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);
        using InstanceLock? acquired = result.Lock;

        Assert.NotNull(acquired);
        Assert.False(result.Opened);
        Assert.Empty(opener.Opened);
    }

    /// <summary>When another owner holds the lock and has published a record, its URL is opened once and no lock is returned.</summary>
    [Fact]
    public async Task AcquireOrHandOffAsync_LockHeldAndRecordPresent_OpensUrlOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using InstanceLock? owner = InstanceLock.TryAcquire(dir.Path);
        RecordingBrowserOpener opener = new();

        HandOffResult result = await DesktopHandOff.AcquireOrHandOffAsync(
            dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);

        Assert.NotNull(owner);
        Assert.Null(result.Lock);
        Assert.True(result.Opened);
        Assert.Equal([Info.Url], opener.Opened);
    }

    /// <summary>A record that appears while the lock is held by someone else is picked up and opened.</summary>
    [Fact]
    public async Task AcquireOrHandOffAsync_LockHeldAndRecordAppearsLater_OpensUrl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using InstanceLock? owner = InstanceLock.TryAcquire(dir.Path);
        RecordingBrowserOpener opener = new();
        Task writer = Task.Run(
            async () =>
            {
                await Task.Delay(100, ct);
                InstanceFile.Write(dir.Path, Info);
            },
            ct);

        HandOffResult result = await DesktopHandOff.AcquireOrHandOffAsync(
            dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);
        await writer;

        Assert.NotNull(owner);
        Assert.Null(result.Lock);
        Assert.True(result.Opened);
        Assert.Equal([Info.Url], opener.Opened);
    }

    /// <summary>A lock that stays held with no record ever published times out with neither a lock nor an opened browser.</summary>
    [Fact]
    public async Task AcquireOrHandOffAsync_LockHeldAndNoRecord_TimesOutWithNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using InstanceLock? owner = InstanceLock.TryAcquire(dir.Path);
        RecordingBrowserOpener opener = new();

        HandOffResult result = await DesktopHandOff.AcquireOrHandOffAsync(
            dir.Path, opener, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(20), ct);

        Assert.NotNull(owner);
        Assert.Null(result.Lock);
        Assert.False(result.Opened);
        Assert.Empty(opener.Opened);
    }

    /// <summary>A relaunch during shutdown: the old owner releases the lock while no record exists, and the caller takes over.</summary>
    [Fact]
    public async Task AcquireOrHandOffAsync_LockReleasedWhileWaiting_ReturnsTheLock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceLock? owner = InstanceLock.TryAcquire(dir.Path);
        RecordingBrowserOpener opener = new();
        Task releaser = Task.Run(
            async () =>
            {
                await Task.Delay(100, ct);
                owner?.Dispose();
            },
            ct);

        HandOffResult result = await DesktopHandOff.AcquireOrHandOffAsync(
            dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);
        await releaser;
        using InstanceLock? acquired = result.Lock;

        Assert.NotNull(owner);
        Assert.NotNull(acquired);
        Assert.False(result.Opened);
        Assert.Empty(opener.Opened);
    }
}
