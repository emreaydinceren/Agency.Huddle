using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Records every <see cref="PersonaStatus"/> a runner or host raises, safely across threads.
/// <c>StatusChanged</c> fires on the runner's own Turn and read-loop threads, so a plain
/// <see cref="List{T}"/> appended by the handler while the test enumerates it throws "Collection
/// was modified" under load. Every read here is a snapshot copy taken under the lock, and waits
/// are completed from <see cref="Record"/> itself rather than polled.
/// </summary>
internal sealed class StatusRecorder
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly Lock gate = new();
    private readonly List<PersonaStatus> statuses = [];
    private readonly List<(Func<IReadOnlyList<PersonaStatus>, bool> Condition, TaskCompletionSource Signal)> waiters = [];

    /// <summary>Appends <paramref name="status"/> and releases every waiter whose condition the new history now meets. Subscribe this to <c>StatusChanged</c>.</summary>
    /// <param name="status">The status just raised.</param>
    public void Record(PersonaStatus status)
    {
        List<TaskCompletionSource> released = [];
        lock (this.gate)
        {
            this.statuses.Add(status);
            IReadOnlyList<PersonaStatus> history = [.. this.statuses];
            for (var i = this.waiters.Count - 1; i >= 0; i--)
            {
                if (this.waiters[i].Condition(history))
                {
                    released.Add(this.waiters[i].Signal);
                    this.waiters.RemoveAt(i);
                }
            }
        }

        foreach (var signal in released)
        {
            signal.TrySetResult();
        }
    }

    /// <summary>A copy of every status recorded so far, in the order raised.</summary>
    /// <returns>The snapshot; later records never change it.</returns>
    public IReadOnlyList<PersonaStatus> Snapshot()
    {
        lock (this.gate)
        {
            return [.. this.statuses];
        }
    }

    /// <summary>
    /// Waits until the recorded history meets <paramref name="condition"/>, then returns a snapshot
    /// of it. On timeout it returns the snapshot anyway, so the caller's own assertion - not a
    /// <see cref="TimeoutException"/> - reports what was actually seen.
    /// </summary>
    /// <param name="condition">What the history must satisfy; evaluated on snapshots, under the lock.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <param name="timeout">How long to wait before giving up. Defaults to 5 seconds.</param>
    /// <returns>The history at the moment the condition was met, or at timeout.</returns>
    public async Task<IReadOnlyList<PersonaStatus>> WaitForAsync(
        Func<IReadOnlyList<PersonaStatus>, bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        TaskCompletionSource signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.gate)
        {
            if (condition([.. this.statuses]))
            {
                return [.. this.statuses];
            }

            this.waiters.Add((condition, signal));
        }

        try
        {
            await signal.Task.WaitAsync(timeout ?? StatusRecorder.DefaultTimeout, ct);
        }
        catch (TimeoutException)
        {
            lock (this.gate)
            {
                this.waiters.RemoveAll(waiter => ReferenceEquals(waiter.Signal, signal));
            }
        }

        return this.Snapshot();
    }

    /// <summary>Waits for a recorded status matching <paramref name="match"/>, then asserts one is present - the single-call form of a wait followed by <see cref="Assert.Contains{T}(IEnumerable{T}, Predicate{T})"/>.</summary>
    /// <param name="match">The status the history must contain.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <param name="timeout">How long to wait before asserting on what was seen. Defaults to 5 seconds.</param>
    public async Task AssertContainsEventuallyAsync(Predicate<PersonaStatus> match, CancellationToken ct, TimeSpan? timeout = null)
    {
        var seen = await this.WaitForAsync(history => history.Any(status => match(status)), ct, timeout);
        Assert.Contains(seen, match);
    }
}
