using System.Buffers.Text;
using System.Security.Cryptography;

namespace Agency.Huddle.App.Gateway;

/// <summary>The issued challenge nonces (Spec 7.2): single use, 60 seconds.</summary>
internal sealed class ChallengeNonceStore
{
    /// <summary>How long a nonce stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private const int PruneThreshold = 256;
    private const int MaxOutstanding = 10_000;

    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, DateTimeOffset> issued = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the store.</summary>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public ChallengeNonceStore(TimeProvider? timeProvider = null) => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>What <see cref="Consume"/> found.</summary>
    public enum Check
    {
        /// <summary>Issued here, unused and fresh; now spent.</summary>
        Valid,

        /// <summary>No nonce was presented.</summary>
        Required,

        /// <summary>Unknown, already used, or expired.</summary>
        Mismatch,
    }

    /// <summary>Issues a nonce: 32 random bytes as base64url, and the server time in milliseconds.</summary>
    public Challenge Issue()
    {
        string nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            this.Prune(now);
            this.issued[nonce] = now + Lifetime;
        }

        return new Challenge(nonce, now.ToUnixTimeMilliseconds());
    }

    /// <summary>Spends a nonce.</summary>
    /// <param name="nonce">The presented nonce.</param>
    public Check Consume(string? nonce)
    {
        if (string.IsNullOrEmpty(nonce))
        {
            return Check.Required;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            return this.issued.Remove(nonce, out DateTimeOffset expires) && now < expires ? Check.Valid : Check.Mismatch;
        }
    }

    /// <summary>Drops lapsed nonces, and everything when an unauthenticated flood has filled the store. Called under the lock.</summary>
    /// <param name="now">The current time.</param>
    private void Prune(DateTimeOffset now)
    {
        if (this.issued.Count < PruneThreshold)
        {
            return;
        }

        foreach (string key in this.issued.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
        {
            _ = this.issued.Remove(key);
        }

        if (this.issued.Count >= MaxOutstanding)
        {
            this.issued.Clear();
        }
    }

    /// <summary>A challenge as <c>GET /auth/challenge</c> returns it.</summary>
    /// <param name="Nonce">The nonce, base64url.</param>
    /// <param name="Ts">The server time, Unix milliseconds.</param>
    public sealed record Challenge(string Nonce, long Ts);
}
