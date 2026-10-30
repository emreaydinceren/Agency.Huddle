namespace Agency.Huddle.App.Gateway;

/// <summary>The body of <c>POST /auth/connect</c> (Spec 6.6); JSON names are camelCase.</summary>
/// <param name="DeviceId">Lowercase hex SHA-256 of the public key.</param>
/// <param name="PublicKey">The 32-byte Ed25519 public key, base64url without padding.</param>
/// <param name="Signature">The Ed25519 signature over the v3 payload, base64url without padding.</param>
/// <param name="SignedAt">The signing time, milliseconds since the Unix epoch.</param>
/// <param name="Nonce">The challenge nonce.</param>
/// <param name="Platform">The platform text.</param>
/// <param name="DeviceFamily">The device family text.</param>
/// <param name="Role">Always <c>operator</c>.</param>
/// <param name="Scopes">The scopes asked for.</param>
/// <param name="Auth">The credentials.</param>
internal sealed record ConnectRequest(
    string DeviceId,
    string PublicKey,
    string Signature,
    long SignedAt,
    string Nonce,
    string Platform,
    string DeviceFamily,
    string Role,
    IReadOnlyList<string> Scopes,
    ConnectAuth Auth);
