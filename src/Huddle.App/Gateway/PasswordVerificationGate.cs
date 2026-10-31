namespace Agency.Huddle.App.Gateway;

/// <summary>R-11: caps how many password (PBKDF2) verifications run at once, so a flood of attempts cannot starve the CPU.</summary>
internal sealed class PasswordVerificationGate : IDisposable
{
    /// <summary>The default cap.</summary>
    public const int DefaultMaxConcurrent = 4;

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim slots;
    private readonly TimeSpan wait;

    /// <summary>Creates a gate.</summary>
    /// <param name="maxConcurrent">How many verifications may run at once.</param>
    /// <param name="wait">How long an excess caller waits for a slot; <see langword="null"/> for one second.</param>
    public PasswordVerificationGate(int maxConcurrent = DefaultMaxConcurrent, TimeSpan? wait = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrent, 1);

        this.slots = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        this.wait = wait ?? DefaultWait;
    }

    /// <summary>Takes a slot, waiting briefly when none is free.</summary>
    /// <returns>A lease that frees the slot when disposed, or <see langword="null"/> when no slot became free.</returns>
    public IDisposable? TryEnter() => this.slots.Wait(this.wait) ? new Lease(this.slots) : null;

    /// <inheritdoc />
    public void Dispose() => this.slots.Dispose();

    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.released, 1) == 0)
            {
                _ = slots.Release();
            }
        }
    }
}
