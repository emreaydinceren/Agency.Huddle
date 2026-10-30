namespace Agency.Huddle.App.Gateway;

/// <summary>R-6: a simple per-address rate on <c>GET /auth/challenge</c>, so one client cannot flood the nonce store.</summary>
internal sealed class ChallengeRateLimiter
{
    /// <summary>The most challenges one address may fetch in a minute.</summary>
    public const int PerMinute = 60;

    private const int MaxClients = 10_000;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, Queue<DateTimeOffset>> recent = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the limiter.</summary>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public ChallengeRateLimiter(TimeProvider? timeProvider = null) => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Counts one challenge request from <paramref name="client"/>.</summary>
    /// <param name="client">The client key (see <see cref="AuthRateLimiter.ClientKey"/>).</param>
    /// <returns>Whether the request is within the rate.</returns>
    public bool TryAcquire(string client)
    {
        ArgumentNullException.ThrowIfNull(client);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (this.recent.Count >= MaxClients)
            {
                this.Sweep(now);
            }

            if (!this.recent.TryGetValue(client, out Queue<DateTimeOffset>? times))
            {
                times = new Queue<DateTimeOffset>();
                this.recent[client] = times;
            }

            while (times.Count > 0 && now - times.Peek() >= Window)
            {
                _ = times.Dequeue();
            }

            if (times.Count >= PerMinute)
            {
                return false;
            }

            times.Enqueue(now);
            return true;
        }
    }

    /// <summary>Forgets addresses whose newest request is outside the window; if a flood of fresh addresses keeps them all, drops everything. Called under the lock.</summary>
    /// <param name="now">The current time.</param>
    private void Sweep(DateTimeOffset now)
    {
        foreach (string key in this.recent.Where(pair => pair.Value.Count == 0 || now - pair.Value.Last() >= Window).Select(pair => pair.Key).ToList())
        {
            _ = this.recent.Remove(key);
        }

        if (this.recent.Count >= MaxClients)
        {
            this.recent.Clear();
        }
    }
}
