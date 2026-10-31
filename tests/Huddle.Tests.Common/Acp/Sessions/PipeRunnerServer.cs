using System.IO.Pipes;
using System.Threading.Channels;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// A hand-scripted hub end of the runner pipe: it gives a test full control over the Welcome a real
/// <see cref="PersonaRunner"/> receives, without a real <c>ChatService</c>. After the handshake a
/// background pump drains the pipe for the rest of the server's life, because every write the runner
/// makes needs a reader on the other end or it blocks forever. The pump answers each
/// <see cref="ReadTranscript"/> itself and records, rather than swallows, any fault: waits fail fast
/// once the pipe closes, and <see cref="DisposeAsync"/> throws when the pump faulted (OP-7).
/// </summary>
internal sealed class PipeRunnerServer : IAsyncDisposable
{
    private readonly NamedPipeServerStream pipe;
    private readonly Channel<ProtocolMessage> inbox = Channel.CreateUnbounded<ProtocolMessage>();
    private readonly bool recordReceived;
    private readonly Lock gate = new();
    private readonly List<ProtocolMessage> received = [];

    private JsonLineStream? stream;
    private Task? pumpTask;
    private Exception? pumpFault;
    private bool disposed;

    /// <summary>Creates the server-side pipe instance, unconnected, under a unique per-instance name.</summary>
    /// <param name="namePrefix">Starts the pipe's name, so a stray pipe can be traced to its test class.</param>
    /// <param name="recordReceived">Whether <see cref="Received"/> keeps every envelope the runner sends; <see langword="false"/> keeps nothing.</param>
    public PipeRunnerServer(string namePrefix, bool recordReceived = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

        this.recordReceived = recordReceived;
        this.PipeName = namePrefix + "-" + Guid.NewGuid().ToString("N");
        this.pipe = new NamedPipeServerStream(
            this.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    }

    /// <summary>The named pipe this server listens on; hand it to the runner's <c>TeamOptions.PipeName</c>.</summary>
    public string PipeName { get; }

    /// <summary>When true, every <see cref="ReadTranscript"/> is answered with a refusal instead of an empty tail.</summary>
    public bool RefuseTranscriptReads { get; init; }

    /// <summary>A snapshot of every envelope the runner sent so far, in order; empty when the server was built with <c>recordReceived: false</c>.</summary>
    public IReadOnlyList<ProtocolMessage> Received
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.received];
            }
        }
    }

    /// <summary>Starts <paramref name="runner"/>, accepts its connection, answers the handshake with <paramref name="rooms"/>, then starts the always-on pump.</summary>
    /// <param name="runner">The runner to start against this server.</param>
    /// <param name="rooms">The Welcome's Room list.</param>
    /// <param name="ct">Cancels the accept, the handshake, and the runner's own start.</param>
    public async Task HandshakeAsync(PersonaRunner runner, IReadOnlyList<RoomInfo> rooms, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(rooms);

        Task startTask = runner.StartAsync(ct);
        await this.pipe.WaitForConnectionAsync(ct);

        JsonLineStream lineStream = new(this.pipe);
        this.stream = lineStream;

        Hello hello = Assert.IsType<Hello>(await lineStream.ReadAsync(ct));
        await lineStream.WriteAsync(new Welcome("agent-1", hello.Name, rooms), ct);

        await startTask;

        // From here until disposal every runner write has a reader: a test that stops waiting on the
        // pipe while it polls for something else must not stall an unrelated Turn on an unread pipe.
        this.pumpTask = this.PumpAsync(lineStream);
    }

    /// <summary>Writes one envelope to the connected runner.</summary>
    /// <param name="message">The envelope to send.</param>
    /// <param name="ct">Cancels the write.</param>
    public Task SendAsync(ProtocolMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (this.stream is not { } lineStream)
        {
            throw new InvalidOperationException("HandshakeAsync must complete before SendAsync is called.");
        }

        return lineStream.WriteAsync(message, ct);
    }

    /// <summary>
    /// Waits for the next envelope of type <typeparamref name="T"/> that satisfies <paramref name="match"/>,
    /// discarding everything else. Fails with <see cref="InvalidOperationException"/> as soon as the pipe is
    /// closed and nothing queued matches, carrying the pump's fault as the inner exception when there was one.
    /// </summary>
    /// <typeparam name="T">The envelope type to wait for.</typeparam>
    /// <param name="match">Which envelope of type <typeparamref name="T"/> to stop on; <see langword="null"/> accepts the first.</param>
    /// <param name="ct">Bounds the wait.</param>
    /// <returns>The matching envelope.</returns>
    public async Task<T> ReceiveUntilAsync<T>(Func<T, bool>? match, CancellationToken ct)
        where T : ProtocolMessage
    {
        while (await this.inbox.Reader.WaitToReadAsync(ct))
        {
            while (this.inbox.Reader.TryRead(out ProtocolMessage? message))
            {
                if (message is T typed && (match is null || match(typed)))
                {
                    return typed;
                }
            }
        }

        throw new InvalidOperationException(
            "The runner pipe closed before a matching " + typeof(T).Name + " arrived.", this.CurrentFault());
    }

    /// <summary>Closes the pipe, waits for the pump to end, and throws if the pump recorded a fault.</summary>
    /// <returns>A task that completes when the server is torn down.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (this.stream is not null)
        {
            await this.stream.DisposeAsync();
        }
        else
        {
            await this.pipe.DisposeAsync();
        }

        if (this.pumpTask is not null)
        {
            await this.pumpTask;
        }

        if (this.CurrentFault() is { } fault)
        {
            throw new InvalidOperationException("The runner pipe pump faulted.", fault);
        }
    }

    /// <summary>Reads the pump fault under the lock, or <see langword="null"/> when there is none.</summary>
    private Exception? CurrentFault()
    {
        lock (this.gate)
        {
            return this.pumpFault;
        }
    }

    /// <summary>True for the exceptions a closing pipe raises: an expected end of the pump, not a fault.</summary>
    /// <param name="exception">The exception the pump caught.</param>
    private static bool IsClosure(Exception exception) =>
        exception is IOException or ObjectDisposedException or OperationCanceledException;

    /// <summary>Drains <paramref name="lineStream"/> into the inbox until the stream ends, answering every <see cref="ReadTranscript"/> itself; records any non-closure fault.</summary>
    /// <param name="lineStream">The connected stream to pump.</param>
    private async Task PumpAsync(JsonLineStream lineStream)
    {
        try
        {
            while (true)
            {
                ProtocolMessage? message = await lineStream.ReadAsync(CancellationToken.None);
                if (message is null)
                {
                    break;
                }

                if (this.recordReceived)
                {
                    lock (this.gate)
                    {
                        this.received.Add(message);
                    }
                }

                if (message is ReadTranscript read)
                {
                    ProtocolMessage answer = this.RefuseTranscriptReads
                        ? new ProtocolError("transcriptRefused", "refused for this test", read.RequestId)
                        : new TranscriptTail(read.RequestId, read.RoomId, [], Omitted: 0);
                    await lineStream.WriteAsync(answer, CancellationToken.None);
                    continue;
                }

                _ = this.inbox.Writer.TryWrite(message);
            }
        }
#pragma warning disable CA1031 // The pump records every non-closure fault for DisposeAsync to throw; only a closing pipe is expected.
        catch (Exception exception)
        {
            if (!IsClosure(exception))
            {
                lock (this.gate)
                {
                    this.pumpFault = exception;
                }
            }

            // Otherwise the pipe closed (this server disposing, or the runner tearing down); nothing more to pump.
        }
#pragma warning restore CA1031
        finally
        {
            _ = this.inbox.Writer.TryComplete();
        }
    }
}
