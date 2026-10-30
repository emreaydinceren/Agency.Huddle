using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.Gateway;

/// <summary>Bootstrap tokens (Spec 6.7, 7.2): hash to kind, bound device and expiry; in memory.</summary>
internal sealed class BootstrapTokenStore
{
    /// <summary>How long a bootstrap token lives.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private static readonly Redemption Refused = new(false, Kind.Admin, []);

    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, Entry> tokens = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the store.</summary>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public BootstrapTokenStore(TimeProvider? timeProvider = null) => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>What a bootstrap token is for.</summary>
    public enum Kind
    {
        /// <summary>The desktop launcher, <c>dashboard</c> or the dev console: pairs the redeeming device as admin.</summary>
        Admin,

        /// <summary>A setup link (QR): limited at most, never auto-approved.</summary>
        SetupLink,
    }

    /// <summary>Issues a token: 32 random bytes as base64url. Only its hash is kept.</summary>
    /// <param name="kind">What it is for.</param>
    public string Issue(Kind kind)
    {
        string token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            this.Prune(now);
            this.tokens[Hash(token)] = new Entry(kind, now + Lifetime);
        }

        return token;
    }

    /// <summary>
    /// Redeems a token for a device. The first device to redeem it becomes its bound device and may present it again until it expires;
    /// any other device is refused.
    /// </summary>
    /// <param name="token">The presented token.</param>
    /// <param name="deviceId">The redeeming device.</param>
    public Redemption Redeem(string token, string deviceId)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(deviceId);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (token.Length == 0 || !this.tokens.TryGetValue(Hash(token), out Entry? entry) || entry.Burned || now >= entry.Expires)
            {
                return Refused;
            }

            entry.BoundDevice ??= deviceId;
            if (!string.Equals(entry.BoundDevice, deviceId, StringComparison.Ordinal))
            {
                return Refused;
            }

            return new Redemption(true, entry.TokenKind, entry.TokenKind == Kind.Admin ? GatewayScopes.AdminSet : GatewayScopes.Limited);
        }
    }

    /// <summary>Makes a token unusable by anyone (the <c>/auth/bootstrap/burn</c> endpoint, C-9).</summary>
    /// <param name="token">The token.</param>
    public void Burn(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        lock (this.gate)
        {
            if (this.tokens.TryGetValue(Hash(token), out Entry? entry))
            {
                entry.Burned = true;
            }
        }
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Drops lapsed tokens. Called under the lock.</summary>
    /// <param name="now">The current time.</param>
    private void Prune(DateTimeOffset now)
    {
        foreach (string key in this.tokens.Where(pair => pair.Value.Expires <= now).Select(pair => pair.Key).ToList())
        {
            _ = this.tokens.Remove(key);
        }
    }

    /// <summary>The result of <see cref="Redeem"/>.</summary>
    /// <param name="Valid">Whether the token was accepted.</param>
    /// <param name="TokenKind">What it is for; meaningful when <paramref name="Valid"/>.</param>
    /// <param name="Scopes">Admin tokens carry every scope, setup links the limited ones; empty when not valid.</param>
    public sealed record Redemption(bool Valid, Kind TokenKind, IReadOnlyList<string> Scopes);

    private sealed class Entry(Kind kind, DateTimeOffset expires)
    {
        public Kind TokenKind { get; } = kind;

        public DateTimeOffset Expires { get; } = expires;

        public string? BoundDevice { get; set; }

        public bool Burned { get; set; }
    }
}
