using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins the second-launch behaviour of <see cref="DesktopHandOff"/>.</summary>
public sealed class DesktopHandOffTests
{
    private static readonly InstanceInfo Info = new("http://127.0.0.1:5123", "token", 4242);

    /// <summary>A record that is already published is handed off at once: the browser opens its URL and the result is true.</summary>
    [Fact]
    public async Task OpenExistingAsync_RecordPresent_OpensUrlOnceAndReturnsTrue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        RecordingBrowserOpener opener = new();

        bool result = await DesktopHandOff.OpenExistingAsync(
            dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);

        Assert.True(result);
        Assert.Equal([Info.Url], opener.Opened);
    }

    /// <summary>With no record the call gives up after the timeout, returns false and never opens a browser.</summary>
    [Fact]
    public async Task OpenExistingAsync_NoRecord_ReturnsFalseAndDoesNotOpen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RecordingBrowserOpener opener = new();

        bool result = await DesktopHandOff.OpenExistingAsync(
            dir.Path, opener, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(20), ct);

        Assert.False(result);
        Assert.Empty(opener.Opened);
    }

    /// <summary>A record that appears while the call is polling is picked up and opened.</summary>
    [Fact]
    public async Task OpenExistingAsync_RecordAppearsLater_OpensUrlAndReturnsTrue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RecordingBrowserOpener opener = new();
        Task writer = Task.Run(
            async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                InstanceFile.Write(dir.Path, Info);
            },
            ct);

        bool result = await DesktopHandOff.OpenExistingAsync(
            dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);
        await writer;

        Assert.True(result);
        Assert.Equal([Info.Url], opener.Opened);
    }

    /// <summary>A token that is already cancelled stops the call even when a record exists, and nothing is opened.</summary>
    [Fact]
    public async Task OpenExistingAsync_CancelledToken_ThrowsOperationCanceled()
    {
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        RecordingBrowserOpener opener = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DesktopHandOff.OpenExistingAsync(
                dir.Path, opener, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), cts.Token));

        Assert.Empty(opener.Opened);
    }
}
