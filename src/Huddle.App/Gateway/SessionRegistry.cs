using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.Gateway;

/// <summary>The in-memory sessions (Spec 6.8, 7.2). Stage B needs only issue and a validity check; Stage C extends it.</summary>
internal sealed class SessionRegistry
{
    /// <summary>The server-side lifetime of a session.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private static readonly TimeSpan SeenWriteInterval = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, DateTimeOffset> lastSeenWritten = new(StringComparer.Ordinal);
    private readonly DeviceRegistry devices;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, Session> sessions = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the registry.</summary>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="devices">The devices, to check that a session's device is still paired (Spec 8.5).</param>
    public SessionRegistry(DeviceRegistry devices, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(devices);

        this.devices = devices;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        devices.Subscribe(this.EndDeviceSessions);
    }

    /// <summary>
    /// The session for the cookie, or <see langword="null"/> when it is unknown, older than <see cref="Lifetime"/>,
    /// its device is no longer paired, or it is bound to a Tailscale login that <paramref name="currentTailscaleLogin"/> does not match (ignoring case).
    /// A device that is revoked, removed or rotated loses its sessions at once (the registry listens to <see cref="DeviceRegistry.Subscribe"/>).
    /// </summary>
    /// <param name="cookieValue">The cookie value, or <see langword="null"/>.</param>
    /// <param name="currentTailscaleLogin">The login the request's managed ingress verified now, or <see langword="null"/>.</param>
    public GatewaySession? TryGet(string? cookieValue, string? currentTailscaleLogin = null)
    {
        if (string.IsNullOrEmpty(cookieValue))
        {
            return null;
        }

        GatewaySession? session = this.Lookup(Hash(cookieValue));
        if (session is null || this.devices.Find(session.DeviceId) is not { HasToken: true })
        {
            return null;
        }

        bool bound = session.TailscaleLogin is not null && string.Equals(session.Ingress, IngressKindText.Of(IngressKind.Managed), StringComparison.Ordinal);
        if (bound && !string.Equals(session.TailscaleLogin, currentTailscaleLogin, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        this.NoteSeen(session.DeviceId);
        return session;
    }

    /// <summary>
    /// The in-memory re-check of Spec 8.5 for an open socket (C-5): expiry and whether the device's sessions were ended, no whois and no database read.
    /// </summary>
    /// <param name="sessionHash">The <see cref="GatewayClaims.Session"/> claim.</param>
    public GatewaySession? TryGetByHash(string? sessionHash)
    {
        GatewaySession? session = this.Lookup(sessionHash);
        if (session is not null)
        {
            this.NoteSeen(session.DeviceId);
        }

        return session;
    }

    /// <summary>
    /// Marks the device as seen now: in memory every time, and in <c>team.db</c> at most once a minute per device, so an active device reads
    /// "seen now" without a write per request.
    /// </summary>
    /// <param name="deviceId">The device whose session was just used.</param>
    private void NoteSeen(string deviceId)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        bool due;
        lock (this.gate)
        {
            due = !this.lastSeenWritten.TryGetValue(deviceId, out DateTimeOffset written) || now - written >= SeenWriteInterval;
            if (due)
            {
                this.lastSeenWritten[deviceId] = now;
            }
        }

        if (due)
        {
            this.devices.MarkSeen(deviceId);
        }
    }

    private GatewaySession? Lookup(string? sessionHash)
    {
        if (string.IsNullOrEmpty(sessionHash))
        {
            return null;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (!this.sessions.TryGetValue(sessionHash, out Session? session) || now - session.IssuedAt >= Lifetime)
            {
                return null;
            }

            return new GatewaySession(session.DeviceId, sessionHash, session.Scopes, session.Ingress, session.TailscaleLogin);
        }
    }

    /// <summary>Ends every session at once (strict local was switched on: nothing but the host may use Huddle).</summary>
    public void EndAll()
    {
        lock (this.gate)
        {
            this.sessions.Clear();
        }
    }

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

    private void EndDeviceSessions(string deviceId)
    {
        lock (this.gate)
        {
            foreach (string key in this.sessions.Where(pair => string.Equals(pair.Value.DeviceId, deviceId, StringComparison.Ordinal)).Select(pair => pair.Key).ToList())
            {
                _ = this.sessions.Remove(key);
            }
        }
    }

    private static string Hash(string cookie) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie)));

    private sealed record Session(string DeviceId, IReadOnlyList<string> Scopes, string Ingress, string? TailscaleLogin, DateTimeOffset IssuedAt);
}
