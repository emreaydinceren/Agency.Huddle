using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.Gateway;

/// <summary>The in-memory sessions (Spec 6.8, 7.2). Stage B needs only issue and a validity check; Stage C extends it.</summary>
internal sealed class SessionRegistry
{
    /// <summary>The server-side lifetime of a session.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, Session> sessions = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the registry.</summary>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public SessionRegistry(TimeProvider? timeProvider = null) => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Issues a session and returns its cookie value (32 random bytes as base64url); only its hash is kept.</summary>
    /// <param name="deviceId">The device.</param>
    /// <param name="scopes">The scopes.</param>
    /// <param name="ingress">The ingress kind, as text.</param>
    /// <param name="tailscaleLogin">The Tailscale login the session is bound to, if any.</param>
    public string Issue(string deviceId, IReadOnlyList<string> scopes, string ingress, string? tailscaleLogin)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(ingress);

        string cookie = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            foreach (string key in this.sessions.Where(pair => now - pair.Value.IssuedAt >= Lifetime).Select(pair => pair.Key).ToList())
            {
                _ = this.sessions.Remove(key);
            }

            this.sessions[Hash(cookie)] = new Session(deviceId, scopes, ingress, tailscaleLogin, now);
        }

        return cookie;
    }

    /// <summary>Whether the cookie value names a live session.</summary>
    /// <param name="cookieValue">The cookie value, or <see langword="null"/>.</param>
    public bool IsValid(string? cookieValue)
    {
        if (string.IsNullOrEmpty(cookieValue))
        {
            return false;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            return this.sessions.TryGetValue(Hash(cookieValue), out Session? session) && now - session.IssuedAt < Lifetime;
        }
    }

    private static string Hash(string cookie) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie)));

    private sealed record Session(string DeviceId, IReadOnlyList<string> Scopes, string Ingress, string? TailscaleLogin, DateTimeOffset IssuedAt);
}
