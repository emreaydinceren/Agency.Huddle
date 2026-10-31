namespace Agency.Huddle.App.Gateway;

/// <summary>A row of <c>gateway_devices</c> (Spec 7.1), minus the token hash.</summary>
/// <param name="DeviceId">The SHA-256 hex of the public key.</param>
/// <param name="PublicKey">The 32 key bytes.</param>
/// <param name="Name">1 to 60 characters; untrusted.</param>
/// <param name="Scopes">The approved scopes, sorted.</param>
/// <param name="HasToken">Whether a device token is current (false after revoke).</param>
/// <param name="PairedVia"><c>loopback</c>, <c>bootstrap</c>, <c>tailscale</c>, <c>approval</c> or <c>trusted-proxy</c>.</param>
/// <param name="TailscaleLogin">The Tailscale login at pairing, if any.</param>
/// <param name="PairedAt">When it was paired.</param>
/// <param name="LastSeenAt">The last handshake.</param>
/// <param name="LastSeenFrom">The address of the last handshake.</param>
/// <param name="UserAgent">The user agent of the last handshake.</param>
internal sealed record DeviceRecord(
    string DeviceId,
    byte[] PublicKey,
    string Name,
    IReadOnlyList<string> Scopes,
    bool HasToken,
    string PairedVia,
    string? TailscaleLogin,
    DateTimeOffset PairedAt,
    DateTimeOffset LastSeenAt,
    string LastSeenFrom,
    string? UserAgent);
