using System.Diagnostics;
using System.Net;
using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins the outcomes of the <c>stop</c> verb implemented by <see cref="DesktopStopClient"/>.</summary>
public sealed class DesktopStopClientTests
{
    private static readonly InstanceInfo Info = new("http://127.0.0.1:5123", "secret-stop-token", 4242);

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>Takes the instance lock the way a running instance would.</summary>
    private static InstanceLock Hold(string dataDir)
    {
        return InstanceLock.TryAcquire(dataDir)
            ?? throw new InvalidOperationException("The test could not take the instance lock.");
    }

    /// <summary>With no record the client reports that nothing is running, returns 0 and sends nothing.</summary>
    [Fact]
    public async Task RunAsync_NoInstanceFile_ReportsNotRunningAndSendsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        StringWriter output = new();

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(0, exit);
        Assert.Contains("Agency Huddle is not running.", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    /// <summary>A 202 followed by the owner releasing the lock sends one authenticated POST, prints Stopped. and returns 0.</summary>
    [Fact]
    public async Task RunAsync_AcceptedAndLockReleased_PostsWithTokenAndReportsStopped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        StringWriter output = new();
        InstanceLock held = Hold(dir.Path);
        Task release = Task.Run(
            async () =>
            {
                await Task.Delay(150, ct);
                held.Dispose();
            },
            ct);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, TimeSpan.FromSeconds(5), ct);
        await release;

        Assert.Equal(0, exit);
        Assert.Contains("Stopped.", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Info.StopToken, output.ToString(), StringComparison.Ordinal);
        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("http://127.0.0.1:5123/_desktop/stop"), request.Uri);
        Assert.Equal([Info.StopToken], request.TokenValues);
    }

    /// <summary>After a 2xx the client leaves no lock behind: the lock is free once the client returns.</summary>
    [Fact]
    public async Task RunAsync_AcceptedAndLockReleased_DoesNotLeaveTheLockHeld()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        InstanceLock held = Hold(dir.Path);
        Task release = Task.Run(
            async () =>
            {
                await Task.Delay(150, ct);
                held.Dispose();
            },
            ct);

        _ = await DesktopStopClient.RunAsync(dir.Path, handler, new StringWriter(), TimeSpan.FromSeconds(5), ct);
        await release;

        using InstanceLock? after = InstanceLock.TryAcquire(dir.Path);
        Assert.NotNull(after);
    }

    /// <summary>The URL comes from the record, so a trailing slash or path on it does not change the stop route.</summary>
    [Fact]
    public async Task RunAsync_RecordUrlHasTrailingSlash_StillPostsToStopRoute()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info with { Url = "http://127.0.0.1:5123/" });
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        using InstanceLock held = Hold(dir.Path);

        _ = await DesktopStopClient.RunAsync(dir.Path, handler, new StringWriter(), ShortTimeout, ct);

        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("http://127.0.0.1:5123/_desktop/stop"), request.Uri);
    }

    /// <summary>A 202 with the lock never released times out: exit 1 and the timeout is printed in invariant seconds.</summary>
    [Fact]
    public async Task RunAsync_AcceptedButLockNeverReleased_TimesOutWithExitOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.Contains("Did not stop within 0.3 s.", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Stopped.", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A record with nobody holding the lock is stale: it is deleted, the client returns 0 and sends no request.</summary>
    [Fact]
    public async Task RunAsync_RecordPresentAndLockFree_DeletesStaleFileAndNeverCallsHandler()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        StringWriter output = new();

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(0, exit);
        Assert.Null(InstanceFile.TryRead(dir.Path));
        Assert.False(File.Exists(Path.Combine(dir.Path, "instance.json")));
        Assert.Contains("Agency Huddle is not running.", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
        using InstanceLock? after = InstanceLock.TryAcquire(dir.Path);
        Assert.NotNull(after);
    }

    /// <summary>With the lock held and no record yet the client waits for one, gives up after the stop timeout and returns 1 without a request.</summary>
    [Fact]
    public async Task RunAsync_NoRecordAndLockHeld_WaitsThenReportsStartingAndReturnsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);
        Stopwatch clock = Stopwatch.StartNew();

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(250), $"Returned after {clock.Elapsed}.");
        Assert.Contains("Agency Huddle is starting or not answering.", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    /// <summary>A record that appears shortly after the client starts, while the lock is held, is picked up and the instance is stopped.</summary>
    [Fact]
    public async Task RunAsync_RecordAppearsWhileLockHeld_ProceedsAndStops()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Accepted);
        StringWriter output = new();
        InstanceLock held = Hold(dir.Path);
        Task owner = Task.Run(
            async () =>
            {
                await Task.Delay(100, ct);
                InstanceFile.Write(dir.Path, Info);
                await Task.Delay(300, ct);
                held.Dispose();
            },
            ct);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, TimeSpan.FromSeconds(5), ct);
        await owner;

        Assert.Equal(0, exit);
        Assert.Contains("Stopped.", output.ToString(), StringComparison.Ordinal);
        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal([Info.StopToken], request.TokenValues);
    }

    /// <summary>Connection refused while the lock is held means a live instance is starting: the record is kept and the client returns 1.</summary>
    [Fact]
    public async Task RunAsync_ConnectionRefusedAndLockHeld_KeepsFileAndReturnsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Throwing(new HttpRequestException("refused"));
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.NotNull(InstanceFile.TryRead(dir.Path));
        Assert.Contains("Agency Huddle is starting or not answering.", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A request timeout is not a stale record: the client says the instance did not answer, keeps the file and returns 1.</summary>
    [Fact]
    public async Task RunAsync_RequestTimesOut_ReportsNoAnswerAndReturnsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Throwing(new TaskCanceledException("timed out"));
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.Contains("Agency Huddle did not answer.", output.ToString(), StringComparison.Ordinal);
        Assert.NotNull(InstanceFile.TryRead(dir.Path));
    }

    /// <summary>When the caller cancels, the cancellation propagates instead of being reported as a timeout.</summary>
    [Fact]
    public async Task RunAsync_CallerCancels_PropagatesCancellation()
    {
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        using InstanceLock held = Hold(dir.Path);
        using FakeHttpHandler handler = new((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DesktopStopClient.RunAsync(dir.Path, handler, new StringWriter(), ShortTimeout, cts.Token));
    }

    /// <summary>A 401 is reported with its status and returns 1; the token is never printed.</summary>
    [Fact]
    public async Task RunAsync_Unauthorized_ReportsReasonAndReturnsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Unauthorized);
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.Contains("401", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Info.StopToken, output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A 403 is reported with its status and returns 1.</summary>
    [Fact]
    public async Task RunAsync_Forbidden_ReportsReasonAndReturnsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.Forbidden);
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.Contains("403", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Any other non-2xx status prints Unexpected status N. and returns 1.</summary>
    [Fact]
    public async Task RunAsync_ServerError_ReportsUnexpectedStatusAndReturnsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using FakeHttpHandler handler = FakeHttpHandler.Returning(HttpStatusCode.InternalServerError);
        StringWriter output = new();
        using InstanceLock held = Hold(dir.Path);

        int exit = await DesktopStopClient.RunAsync(dir.Path, handler, output, ShortTimeout, ct);

        Assert.Equal(1, exit);
        Assert.Contains("Unexpected status 500.", output.ToString(), StringComparison.Ordinal);
    }
}
