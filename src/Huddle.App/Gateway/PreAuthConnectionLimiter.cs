namespace Agency.Huddle.App.Gateway;

/// <summary>At most 128 concurrent unauthenticated requests per address (Spec 6.5). Loopback is not exempt.</summary>
internal sealed class PreAuthConnectionLimiter
{
    /// <summary>The per-address cap.</summary>
    public const int MaxPerAddress = 128;

    private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Takes a slot for <paramref name="client"/>; <see langword="null"/> when the address is at the cap (answer 503).</summary>
    /// <param name="client">The client address text.</param>
    public IDisposable? TryAcquire(string client)
    {
        ArgumentNullException.ThrowIfNull(client);

        lock (this.gate)
        {
            this.counts.TryGetValue(client, out int current);
            if (current >= MaxPerAddress)
            {
                return null;
            }

            this.counts[client] = current + 1;
        }

        return new Lease(this, client);
    }

    private void Release(string client)
    {
        lock (this.gate)
        {
            if (!this.counts.TryGetValue(client, out int current))
            {
                return;
            }

            if (current <= 1)
            {
                this.counts.Remove(client);
            }
            else
            {
                this.counts[client] = current - 1;
            }
        }
    }

    private sealed class Lease(PreAuthConnectionLimiter owner, string client) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.released, 1) == 0)
            {
                owner.Release(client);
            }
        }
    }
}
