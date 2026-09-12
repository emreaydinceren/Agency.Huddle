namespace Agency.Huddle.Acp.Tests.Fakes;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Console.Terminal;

/// <summary>A scripted <see cref="IConsoleInput"/> that replays a queued sequence of lines and lets a test hold a read open.</summary>
internal sealed class ScriptedConsoleInput : IConsoleInput
{
    private readonly Lock gate = new Lock();

    private TaskCompletionSource? holdSource;

    private int discardCount;

    internal Queue<string?> Lines { get; } = new Queue<string?>();

    internal int DiscardCount => Volatile.Read(ref this.discardCount);

    /// <summary>Makes the next call to <see cref="ReadLineAsync"/> block until <see cref="ReleaseHold"/> is called.</summary>
    internal void HoldNextRead()
    {
        lock (this.gate)
        {
            this.holdSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Releases a read previously blocked by <see cref="HoldNextRead"/>. A no-op when nothing is held.</summary>
    internal void ReleaseHold()
    {
        TaskCompletionSource? source;
        lock (this.gate)
        {
            source = this.holdSource;
            this.holdSource = null;
        }

        source?.TrySetResult();
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task? hold;
        lock (this.gate)
        {
            hold = this.holdSource?.Task;
        }

        if (hold is not null)
        {
            await hold.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (this.gate)
        {
            return this.Lines.Count > 0 ? this.Lines.Dequeue() : null;
        }
    }

    public void DiscardPending()
    {
        Interlocked.Increment(ref this.discardCount);
    }
}
