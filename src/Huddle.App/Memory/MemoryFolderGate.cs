using System.Collections.Concurrent;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// One <see cref="SemaphoreSlim"/> per memory folder, keyed by full path ignoring case and created on first use
/// (Instructions spec §5.4). Every mutating command of the memory tool does its read, version check and write inside
/// it, so two copies of an Agent, or two Agents writing one Team's folder, never interleave.
/// </summary>
/// <remarks>
/// It is a semaphore and not a <see cref="System.Threading.Lock"/> because the critical section awaits. App Tools for every
/// Agent and session run inside the app process, so one singleton gate covers every writer that goes through the tool; a
/// writer outside it (the Library, an Agent's own <c>Write</c>) is caught by the version token instead.
/// </remarks>
internal sealed class MemoryFolderGate : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<SemaphoreSlim>> gates = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool disposed;

    /// <summary>Runs <paramref name="action"/> as the only holder of <paramref name="folder"/>'s gate.</summary>
    /// <typeparam name="T">The action's result.</typeparam>
    /// <param name="folder">The memory folder's path.</param>
    /// <param name="action">The work to do while holding the gate.</param>
    /// <param name="cancellationToken">Cancels the wait for the gate; the gate is released however the action ends.</param>
    /// <returns>The action's result.</returns>
    public async Task<T> RunAsync<T>(string folder, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(this.disposed, this);

        // Lazy, so the semaphore of a losing racer is never created and so never has to be disposed.
        SemaphoreSlim gate = this.gates.GetOrAdd(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
            static _ => new Lazy<SemaphoreSlim>(static () => new SemaphoreSlim(1, 1))).Value;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            ReleaseUnlessDisposed(gate);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.disposed = true;
        foreach (Lazy<SemaphoreSlim> gate in this.gates.Values)
        {
            if (gate.IsValueCreated)
            {
                gate.Value.Dispose();
            }
        }
    }

    /// <summary>Releases the gate; a gate disposed while its holder was still running has nothing left to release.</summary>
    /// <param name="gate">The gate the caller holds.</param>
    private static void ReleaseUnlessDisposed(SemaphoreSlim gate)
    {
        try
        {
            _ = gate.Release();
        }
        catch (ObjectDisposedException)
        {
            // The gate was disposed with the application while this holder was still running; there is no one left to release it for.
        }
    }
}