namespace Agency.Huddle.App.Gateway;

/// <summary>OpenClaw's <c>auth-rate-limit.ts</c> (Spec 6.5): a sliding window of failures per key, then a lockout.</summary>
internal sealed class AuthRateLimiter
{
    /// <summary>The error code a 429 carries.</summary>
    public const string ErrorCode = "AUTH_RATE_LIMITED";

    /// <summary>The most entries kept.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>The key prefix for browser-origin failures.</summary>
    public const string BrowserOriginPrefix = "browser-origin:";

    private const double LoopbackDelayStartMs = 250;
    private const double LoopbackDelayCapMs = 5000;

    private readonly AuthRateLimitOptions options;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates a limiter.</summary>
    /// <param name="options">Attempts, window, lockout and the loopback exemption.</param>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public AuthRateLimiter(AuthRateLimitOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The number of tracked keys.</summary>
    public int Count
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.Count;
            }
        }
    }

    /// <summary>Builds <c>{scope}:{ip}</c>.</summary>
    /// <param name="scope">One of <see cref="AuthRateLimitScopes"/>.</param>
    /// <param name="client">The client address text.</param>
    public static string Key(string scope, string client) => $"{scope}:{client}";

    /// <summary>Builds <c>browser-origin:{origin}</c>.</summary>
    /// <param name="origin">The Origin header value.</param>
    public static string BrowserOriginKey(string origin) => BrowserOriginPrefix + origin;

    /// <summary>Whether <paramref name="key"/> may attempt now.</summary>
    /// <param name="key">A key from <see cref="Key"/> or <see cref="BrowserOriginKey"/>.</param>
    /// <param name="loopback">Whether the client is loopback.</param>
    public AuthRateLimitDecision Check(string key, bool loopback)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (this.IsExempt(key, loopback))
        {
            return new AuthRateLimitDecision(true, 0);
        }

        lock (this.gate)
        {
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            return this.entries.TryGetValue(key, out Entry? entry) && entry.LockedUntil > now
                ? new AuthRateLimitDecision(false, RemainingMs(entry, now))
                : new AuthRateLimitDecision(true, 0);
        }
    }

    /// <summary>Records one failed attempt.</summary>
    /// <param name="key">A key from <see cref="Key"/> or <see cref="BrowserOriginKey"/>.</param>
    /// <param name="loopback">Whether the client is loopback.</param>
    public AuthRateLimitFailure RecordFailure(string key, bool loopback)
    {
        ArgumentNullException.ThrowIfNull(key);

        bool exempt = this.IsExempt(key, loopback);
        lock (this.gate)
        {
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            Entry entry = this.GetOrAdd(key, now);

            if (entry.LockedUntil > now)
            {
                return exempt
                    ? new AuthRateLimitFailure(false, 0, TimeSpan.Zero)
                    : new AuthRateLimitFailure(true, RemainingMs(entry, now), TimeSpan.Zero);
            }

            entry.LockedUntil = DateTimeOffset.MinValue;
            TimeSpan window = TimeSpan.FromSeconds(this.options.WindowSeconds);
            entry.Failures.RemoveAll(failure => now - failure >= window);
            entry.Failures.Add(now);

            if (exempt)
            {
                return new AuthRateLimitFailure(false, 0, LoopbackDelay(entry.Failures.Count));
            }

            if (entry.Failures.Count < this.options.MaxAttempts)
            {
                return new AuthRateLimitFailure(false, 0, TimeSpan.Zero);
            }

            entry.Failures.Clear();
            entry.LockedUntil = now + TimeSpan.FromSeconds(this.options.LockoutSeconds);
            return new AuthRateLimitFailure(true, RemainingMs(entry, now), TimeSpan.Zero);
        }
    }

    /// <summary>Forgets <paramref name="key"/> after a success.</summary>
    /// <param name="key">The key.</param>
    public void Reset(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (this.gate)
        {
            this.entries.Remove(key);
        }
    }

    /// <summary>Removes entries whose window and lockout have passed.</summary>
    public void Prune()
    {
        lock (this.gate)
        {
            this.PruneLocked(this.timeProvider.GetUtcNow());
        }
    }

    private static long RemainingMs(Entry entry, DateTimeOffset now) => (long)(entry.LockedUntil - now).TotalMilliseconds;

    private static TimeSpan LoopbackDelay(int failures)
    {
        double milliseconds = LoopbackDelayStartMs * Math.Pow(2, Math.Min(failures - 1, 16));
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, LoopbackDelayCapMs));
    }

    private bool IsExempt(string key, bool loopback) =>
        loopback && this.options.ExemptLoopback && !key.StartsWith(BrowserOriginPrefix, StringComparison.Ordinal);

    private Entry GetOrAdd(string key, DateTimeOffset now)
    {
        if (this.entries.TryGetValue(key, out Entry? existing))
        {
            return existing;
        }

        if (this.entries.Count >= MaxEntries)
        {
            this.PruneLocked(now);
        }

        if (this.entries.Count >= MaxEntries)
        {
            this.EvictOldest();
        }

        Entry created = new();
        this.entries[key] = created;
        return created;
    }

    private void PruneLocked(DateTimeOffset now)
    {
        TimeSpan window = TimeSpan.FromSeconds(this.options.WindowSeconds);
        List<string> expired = [];
        foreach ((string key, Entry entry) in this.entries)
        {
            bool lockedOut = entry.LockedUntil > now;
            bool recent = entry.Failures.Exists(failure => now - failure < window);
            if (!lockedOut && !recent)
            {
                expired.Add(key);
            }
        }

        foreach (string key in expired)
        {
            this.entries.Remove(key);
        }
    }

    private void EvictOldest()
    {
        string? oldestKey = null;
        DateTimeOffset oldest = DateTimeOffset.MaxValue;
        foreach ((string key, Entry entry) in this.entries)
        {
            DateTimeOffset last = entry.Failures.Count > 0 ? entry.Failures[^1] : entry.LockedUntil;
            if (last < oldest)
            {
                oldest = last;
                oldestKey = key;
            }
        }

        if (oldestKey is not null)
        {
            this.entries.Remove(oldestKey);
        }
    }

    private sealed class Entry
    {
        public List<DateTimeOffset> Failures { get; } = [];

        public DateTimeOffset LockedUntil { get; set; } = DateTimeOffset.MinValue;
    }
}
