using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipes;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// <see cref="PipeAcceptLoop"/>'s one guarantee: a listening instance exists at every moment, so a
/// client that dials in while a connection is being handed off is accepted rather than lost.
/// </summary>
public sealed class PipeAcceptLoopTests
{
    /// <summary>How long the second client's connect may wait for a listening instance.</summary>
    private const int ConnectTimeoutMilliseconds = 5_000;

    /// <summary>
    /// A client that connects while the first connection's handler still holds the loop, and whose
    /// predecessor is disposed before that handler returns, is still accepted and served. This is the
    /// interleaving that reset a fresh Persona runner's pipe on Linux, made synchronous: with the next
    /// instance created only after the handler, the disposed instance was the listener's last owner, so
    /// the queued client was reset (Linux) or found no instance to connect to (Windows).
    /// </summary>
    [Fact]
    public async Task ClientConnectingWhileThePreviousConnectionEnds_IsAcceptedAndServed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string pipeName = "pipe-accept-loop-tests-" + Guid.NewGuid().ToString("N");
        TaskCompletionSource<NamedPipeClientStream> secondDialled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int connections = 0;

        void OnConnected(NamedPipeServerStream server)
        {
            if (Interlocked.Increment(ref connections) > 1)
            {
                _ = EchoOneByteAsync(server, ct);
                return;
            }

            NamedPipeClientStream second = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                second.Connect(ConnectTimeoutMilliseconds);
            }
            catch (TimeoutException exception)
            {
                second.Dispose();
                secondDialled.TrySetException(exception);
                server.Dispose();
                return;
            }

            server.Dispose();
            secondDialled.TrySetResult(second);
        }

        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        PipeAcceptLoop loop = new(pipeName, OnConnected, NullLogger.Instance);
        Task loopTask = loop.RunAsync(stop.Token);

        await using NamedPipeClientStream first = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await first.ConnectAsync(ct);
        await using NamedPipeClientStream second = await secondDialled.Task.WaitAsync(ct);

        byte[] sent = [42];
        await second.WriteAsync(sent, ct);
        byte[] echoed = new byte[1];
        int read = await second.ReadAsync(echoed, ct);

        await stop.CancelAsync();
        await loopTask;

        Assert.Equal(1, read);
        Assert.Equal(42, echoed[0]);
    }

    /// <summary>
    /// A client that connects to the next instance and closes before the loop has issued its accept - Windows lets a client connect
    /// as soon as an instance exists - is logged as a client that left, not as a failed accept, the loop goes straight back to
    /// listening, and the next client is served. The accept then fails with ERROR_NO_DATA ("The pipe is being closed."), which the loop
    /// once logged as "Failed to accept" and paused 250 ms for. The first handler holds the loop while the second client connects
    /// and closes, so the loop meets the closed instance deterministically.
    /// </summary>
    [Fact]
    public async Task ClientClosingBeforeItIsAccepted_IsLoggedAsLeftAndTheNextClientIsServed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows lets a client connect to an instance before the server accepts it; elsewhere the second client is simply accepted.");

        CancellationToken ct = TestContext.Current.CancellationToken;
        string pipeName = "pipe-accept-loop-tests-" + Guid.NewGuid().ToString("N");
        RecordingLogger logger = new();
        TaskCompletionSource leaverGone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int connections = 0;

        void OnConnected(NamedPipeServerStream server)
        {
            if (Interlocked.Increment(ref connections) > 1)
            {
                _ = EchoOneByteAsync(server, ct);
                return;
            }

            using (NamedPipeClientStream leaver = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                leaver.Connect(ConnectTimeoutMilliseconds);
            }

            server.Dispose();
            leaverGone.TrySetResult();
        }

        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        PipeAcceptLoop loop = new(pipeName, OnConnected, logger);
        Task loopTask = loop.RunAsync(stop.Token);

        await using NamedPipeClientStream first = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await first.ConnectAsync(ct);

        // The third client must not dial until the leaver has taken the one waiting instance.
        await leaverGone.Task.WaitAsync(ct);
        await using NamedPipeClientStream third = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await third.ConnectAsync(ConnectTimeoutMilliseconds, ct);

        byte[] sent = [7];
        await third.WriteAsync(sent, ct);
        byte[] echoed = new byte[1];
        int read = await third.ReadAsync(echoed, ct);

        await stop.CancelAsync();
        await loopTask;

        Assert.Equal(1, read);
        Assert.Equal(7, echoed[0]);
        IReadOnlyList<LogEntry> warnings = logger.AtExactly(LogLevel.Warning);
        LogEntry warning = Assert.Single(warnings);
        Assert.Equal($"A client connected to pipe {pipeName} and closed its end before it was accepted.", warning.Message);
    }

    /// <summary>
    /// A pipe name held only briefly - the previous host is still going away on a quick restart - is claimed once the holder lets go
    /// within the wait, instead of stopping the host on the first refusal.
    /// </summary>
    [Fact]
    public async Task PipeNameReleasedDuringTheWait_IsClaimed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string pipeName = "pipe-accept-loop-tests-" + Guid.NewGuid().ToString("N");
        TrackedFakeTimeProvider clock = new();
        TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NamedPipeServerStream other = new(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        PipeAcceptLoop loop = new(
            pipeName,
            server =>
            {
                server.Dispose();
                connected.TrySetResult();
            },
            NullLogger.Instance,
            clock);
        Task loopTask = loop.RunAsync(stop.Token);

        await DriveClockAsync(clock, loopTask, until: static () => false, ct, drive: 1);
        Assert.False(loopTask.IsCompleted, "The loop gave up on the first refusal.");
        await other.DisposeAsync();
        await using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        Task dial = client.ConnectAsync(ct);
        await DriveClockAsync(clock, loopTask, until: () => connected.Task.IsCompleted, ct);
        await connected.Task.WaitAsync(ct);
        await dial.WaitAsync(ct);

        Assert.False(loopTask.IsCompleted);
        await stop.CancelAsync();
        await loopTask.WaitAsync(ct);
    }

    /// <summary>
    /// A pipe name another server holds for the whole wait - a second Huddle on the same machine - stops the loop, only after the wait has
    /// run out, with an error naming the pipe and <c>Team:PipeName</c>, rather than sharing the name: shared, each Agent connected to
    /// whichever program's listener answered, and the ones that reached the other program stayed Offline here with nothing logged.
    /// </summary>
    [Fact]
    public async Task PipeNameHeldThroughoutTheWait_GivesTheMessageAfterTheBound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string pipeName = "pipe-accept-loop-tests-" + Guid.NewGuid().ToString("N");
        TrackedFakeTimeProvider clock = new();
        await using NamedPipeServerStream other = new(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        PipeAcceptLoop loop = new(pipeName, static server => server.Dispose(), NullLogger.Instance, clock);
        DateTimeOffset start = clock.GetUtcNow();
        Task loopTask = loop.RunAsync(ct);

        await DriveClockAsync(clock, loopTask, until: static () => false, ct);
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => loopTask);

        Assert.True(clock.GetUtcNow() - start >= TimeSpan.FromSeconds(1), "The loop gave up before the clock moved at all.");
        Assert.Equal(
            $"Another program is already listening on the pipe '{pipeName}', most likely another copy of Huddle. Stop it, or give this copy its own pipe with Team:PipeName.",
            error.Message);
    }

    /// <summary>Stopping the host while the loop waits for a held pipe name ends the wait quietly, without the already-listening error.</summary>
    [Fact]
    public async Task StopDuringTheWait_EndsTheLoopWithoutAnError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string pipeName = "pipe-accept-loop-tests-" + Guid.NewGuid().ToString("N");
        TrackedFakeTimeProvider clock = new();
        await using NamedPipeServerStream other = new(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        PipeAcceptLoop loop = new(pipeName, static server => server.Dispose(), NullLogger.Instance, clock);
        Task loopTask = loop.RunAsync(stop.Token);

        await DriveClockAsync(clock, loopTask, until: static () => false, ct, drive: 1);
        Assert.False(loopTask.IsCompleted, "The loop gave up on the first refusal.");
        await stop.CancelAsync();
        await loopTask.WaitAsync(ct);

        Assert.True(loopTask.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Moves <paramref name="clock"/> a second at a time, each time the loop has armed its wait, until <paramref name="until"/> holds,
    /// the loop ends, or <paramref name="drive"/> steps have been taken.
    /// </summary>
    /// <param name="clock">The loop's clock.</param>
    /// <param name="loopTask">The running loop.</param>
    /// <param name="until">True when no more time is needed.</param>
    /// <param name="ct">Bounds the whole drive.</param>
    /// <param name="drive">The most steps to take.</param>
    private static async Task DriveClockAsync(TrackedFakeTimeProvider clock, Task loopTask, Func<bool> until, CancellationToken ct, int drive = 100)
    {
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            for (int step = 0; step < drive && !loopTask.IsCompleted && !until(); step++)
            {
                Task advanced = clock.AdvanceWhenArmedAsync(1, TimeSpan.FromSeconds(1), bounded.Token);
                _ = await Task.WhenAny(advanced, loopTask);
            }
        }
        finally
        {
            await bounded.CancelAsync();
        }
    }

    /// <summary>Reads one byte from <paramref name="server"/>, writes it back, then disposes the instance.</summary>
    /// <param name="server">The connected instance; owned by this method.</param>
    /// <param name="ct">Cancels the read and the write.</param>
    private static async Task EchoOneByteAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        await using (server)
        {
            byte[] buffer = new byte[1];
            if (await server.ReadAsync(buffer, ct) == 1)
            {
                await server.WriteAsync(buffer, ct);
                await server.FlushAsync(ct);
            }
        }
    }
}
