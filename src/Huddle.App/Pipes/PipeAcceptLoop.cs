using System.IO.Pipes;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Accepts clients on one named pipe for as long as it runs, and hands each connected instance to a
/// handler. Its one guarantee is that a listening instance exists at every moment: the next one is
/// created after a client connects and <em>before</em> the connected one is handed off.
/// </summary>
/// <remarks>
/// <para>
/// The ordering is load-bearing on Linux, not a tidiness. There a named pipe is a Unix domain socket,
/// and every <see cref="NamedPipeServerStream"/> of one name in a process shares a single listening
/// socket, closed and unlinked when the last instance of that name is disposed. A client's connect
/// completes in the kernel's accept backlog with no server call at all, so a client can be "connected"
/// to a listener whose only owner is the instance just handed off. If that connection then runs its
/// whole life synchronously - a handler's awaits all complete at once when the peer has already sent
/// everything and closed - its dispose closes the listener, and the queued client is reset: a broken
/// pipe on its first write or a connection reset on its first read. On Windows the same gap only
/// makes the client's connect wait, so it never showed there.
/// </para>
/// <para>
/// The handler is called synchronously on the loop, so it may start I/O at once; the loop has already
/// put the next listening instance in place.
/// </para>
/// </remarks>
/// <param name="pipeName">The pipe to listen on.</param>
/// <param name="onConnected">Takes ownership of each connected instance, including its disposal.</param>
/// <param name="logger">Receives a failed accept.</param>
internal sealed class PipeAcceptLoop(string pipeName, Action<NamedPipeServerStream> onConnected, ILogger logger)
{
    /// <summary>
    /// In and out buffer size for every pipe instance, in bytes. Sized to hold many whole protocol
    /// envelopes rather than to any measured figure - the requirement is only that a normal write never
    /// has to wait for the peer to read, and an envelope is a JSON line. Matches the 64 KB the
    /// <c>FileSystemWatcher</c> in <c>PersonaStore</c> uses, for the same class of reason.
    /// </summary>
    private const int PipeBufferBytes = 64 * 1024;

    /// <summary>The pause after a failed accept before listening again.</summary>
    private static readonly TimeSpan AcceptRetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Accepts clients until <paramref name="stoppingToken"/> is cancelled, then disposes the listening instance.</summary>
    /// <param name="stoppingToken">Ends the loop.</param>
    /// <returns>A task that completes when the loop has stopped.</returns>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        NamedPipeServerStream listening = this.CreateInstance();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool connected = await this.WaitForClientAsync(listening, stoppingToken).ConfigureAwait(false);

                // Replace the listening instance first, whichever way the wait ended, so the listener
                // it shares with every other instance of this name always has a live owner.
                NamedPipeServerStream finished = listening;
                listening = this.CreateInstance();

                if (connected)
                {
                    onConnected(finished);
                }
                else
                {
                    await finished.DisposeAsync().ConfigureAwait(false);
                    await Task.Delay(AcceptRetryDelay, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            await listening.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Waits for one client on <paramref name="listening"/>.</summary>
    /// <param name="listening">The instance to wait on.</param>
    /// <param name="stoppingToken">
    /// Cancels the wait. The instance is never disposed to end a wait: on Linux a dispose that races
    /// <see cref="NamedPipeServerStream.WaitForConnectionAsync(CancellationToken)"/> clears the state the
    /// wait is about to read and fails it with a <see cref="NullReferenceException"/>. The token alone
    /// ends the wait on both platforms; <see cref="RunAsync"/> disposes the instance after it has.
    /// </param>
    /// <returns><see langword="true"/> when a client connected; <see langword="false"/> when the accept failed and was logged.</returns>
    private async Task<bool> WaitForClientAsync(NamedPipeServerStream listening, CancellationToken stoppingToken)
    {
        try
        {
            await listening.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Failed to accept a connection on pipe {PipeName}.", pipeName);
            return false;
        }
    }

    /// <summary>Creates one unconnected server instance of the pipe.</summary>
    /// <returns>The new instance, owned by the caller.</returns>
    private NamedPipeServerStream CreateInstance() =>
        // The buffer sizes are load-bearing, and their absence deadlocked an Agent permanently.
        // Left unspecified they default to 0, which on Windows means every write blocks until the
        // peer reads it - a lock-step handshake. AgentConnection answers a refused post (a spent
        // Room Budget, most often) by writing a ProtocolError from INSIDE its read loop, so a
        // client that had sent its post and gone straight on to its next envelope - which is
        // well-behaved, and what DemoAgentHost and PersonaRunner both do - met a server blocked
        // writing while it was blocked writing. Neither side read again.
        //
        // Worse than a stalled turn: JsonLineStream serialises writes behind one semaphore, so
        // the stuck read-loop write also held the lock that SendAsync needs, and that Agent stopped
        // receiving messages in every Room while its pipe stayed open and its tile stayed green.
        // A buffer lets an envelope-sized write complete into the OS and release the lock.
        new(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: PipeBufferBytes,
            outBufferSize: PipeBufferBytes);
}
