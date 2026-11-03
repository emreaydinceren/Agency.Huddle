using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipes;
using Agency.Huddle.App.Pipes;

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
    /// A pipe name another server already listens on - a second Huddle on the same machine - stops the loop with an error naming
    /// the pipe and <c>Team:PipeName</c>, rather than sharing the name: shared, each Agent connected to whichever program's
    /// listener answered, and the ones that reached the other program stayed Offline here with nothing logged.
    /// </summary>
    [Fact]
    public async Task PipeNameAlreadyListenedOn_StopsWithAnErrorNamingThePipe()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string pipeName = "pipe-accept-loop-tests-" + Guid.NewGuid().ToString("N");
        await using NamedPipeServerStream other = new(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        PipeAcceptLoop loop = new(pipeName, static server => server.Dispose(), NullLogger.Instance);

        // Bounded, so a loop that shares the name ends by cancellation and fails the assertion instead of waiting forever.
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(TimeSpan.FromSeconds(5));
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => loop.RunAsync(bounded.Token));

        Assert.Equal(
            $"Another program is already listening on the pipe '{pipeName}', most likely another copy of Huddle. Stop it, or give this copy its own pipe with Team:PipeName.",
            error.Message);
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
