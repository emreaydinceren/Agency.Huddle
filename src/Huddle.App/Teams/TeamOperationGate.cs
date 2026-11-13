namespace Agency.Huddle.App.Teams;

/// <summary>
/// The one lock that serialises Team operations that move a Team's folder: archive, unarchive, recovery and rename
/// (corrections-batch2 tr5 item 1). A singleton shared by <c>ArchiveService</c> and <see cref="TeamRenameService"/>.
/// </summary>
internal sealed class TeamOperationGate : IDisposable
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    /// <summary>Waits for the gate.</summary>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>A scope; disposing it releases the gate (once).</returns>
    public async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        await this.semaphore.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(this.semaphore);
    }

    /// <inheritdoc />
    public void Dispose() => this.semaphore.Dispose();

    /// <summary>Releases the gate when disposed, once.</summary>
    /// <param name="semaphore">The gate's semaphore.</param>
    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int released;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.released, 1) == 0)
            {
                _ = semaphore.Release();
            }
        }
    }
}
