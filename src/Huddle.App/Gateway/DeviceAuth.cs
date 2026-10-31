using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Agency.Huddle.App.Gateway;

/// <summary>The device-signature half of the handshake (Spec 6.6): the v3 payload, the device id, and checks 2 to 4.</summary>
internal static class DeviceAuth
{
    /// <summary>The client id in the signed payload.</summary>
    public const string ClientId = "huddle-web";

    /// <summary>The client mode in the signed payload.</summary>
    public const string ClientMode = "webchat";

    /// <summary>The role in the signed payload.</summary>
    public const string Role = "operator";

    /// <summary>The allowed distance between <c>signedAt</c> and the server clock, in milliseconds.</summary>
    public const long SkewMilliseconds = 60_000;

    /// <summary>No nonce was sent.</summary>
    public const string NonceRequired = "DEVICE_AUTH_NONCE_REQUIRED";

    /// <summary>The nonce was not issued here, was used, or is over 60 s old.</summary>
    public const string NonceMismatch = "DEVICE_AUTH_NONCE_MISMATCH";

    /// <summary><c>signedAt</c> is more than 60 s from the server clock.</summary>
    public const string SignatureExpired = "DEVICE_AUTH_SIGNATURE_EXPIRED";

    /// <summary>The device id is not the SHA-256 hex of the public key.</summary>
    public const string DeviceIdMismatch = "DEVICE_AUTH_DEVICE_ID_MISMATCH";

    /// <summary>The public key is not 32 base64url bytes.</summary>
    public const string PublicKeyInvalid = "DEVICE_AUTH_PUBLIC_KEY_INVALID";

    /// <summary>The signature does not verify.</summary>
    public const string SignatureInvalid = "DEVICE_AUTH_SIGNATURE_INVALID";

    private const int PublicKeyLength = 32;
    private const int SignatureLength = 64;

    /// <summary>
    /// Builds the v3 payload: <c>v3|{deviceId}|huddle-web|webchat|operator|{scopes sorted, comma-joined}|{signedAtMs}|{credential or empty}|{nonce}|{platform}|{deviceFamily}</c>,
    /// with <paramref name="platform"/> and <paramref name="deviceFamily"/> trimmed and lower-cased.
    /// </summary>
    /// <param name="deviceId">The device id.</param>
    /// <param name="scopes">The requested scopes.</param>
    /// <param name="signedAtMs">The signing time in Unix milliseconds.</param>
    /// <param name="credential">The credential presented, or <see langword="null"/> for none.</param>
    /// <param name="nonce">The challenge nonce.</param>
    /// <param name="platform">The platform text.</param>
    /// <param name="deviceFamily">The device family text.</param>
    public static string BuildPayloadV3(
        string deviceId,
        IEnumerable<string> scopes,
        long signedAtMs,
        string? credential,
        string nonce,
        string platform,
        string deviceFamily)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(deviceFamily);

        return string.Join(
            '|',
            "v3",
            deviceId,
            ClientId,
            ClientMode,
            Role,
            GatewayScopes.Join(scopes),
            signedAtMs.ToString(CultureInfo.InvariantCulture),
            credential ?? string.Empty,
            nonce,
            platform.Trim().ToLowerInvariant(),
            deviceFamily.Trim().ToLowerInvariant());
    }

    /// <summary>The device id of a public key: the lowercase hex SHA-256 of its bytes.</summary>
    /// <param name="publicKey">The key bytes.</param>
    public static string DeviceIdOf(ReadOnlySpan<byte> publicKey) => Convert.ToHexStringLower(SHA256.HashData(publicKey));

    /// <summary>
    /// Checks 2 to 4 of Spec 6.6 in order: the clock skew, the device id and key, then the Ed25519 signature over the payload
    /// rebuilt from <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The handshake.</param>
    /// <param name="nowMs">The server clock in Unix milliseconds.</param>
    /// <returns>The first failing <c>DEVICE_AUTH_*</c> code, or <see langword="null"/> when all pass.</returns>
    public static string? Verify(ConnectRequest request, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SignedAt < nowMs - SkewMilliseconds || request.SignedAt > nowMs + SkewMilliseconds)
        {
            return SignatureExpired;
        }

        if (!TryDecode(request.PublicKey, PublicKeyLength, out byte[] publicKey))
        {
            return PublicKeyInvalid;
        }

        if (!string.Equals(request.DeviceId, DeviceIdOf(publicKey), StringComparison.Ordinal))
        {
            return DeviceIdMismatch;
        }

        return SignatureMatches(request, publicKey) ? null : SignatureInvalid;
    }

    private static bool SignatureMatches(ConnectRequest request, byte[] publicKey)
    {
        if (!TryDecode(request.Signature, SignatureLength, out byte[] signature))
        {
            return false;
        }

        string payload = BuildPayloadV3(
            request.DeviceId,
            request.Scopes,
            request.SignedAt,
            request.Auth.SignedCredential,
            request.Nonce,
            request.Platform,
            request.DeviceFamily);
        byte[] message = Encoding.UTF8.GetBytes(payload);

        Ed25519Signer verifier = new();
        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey));
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }

    private static bool TryDecode(string text, int expectedLength, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || text.Length > expectedLength * 2 || !Base64Url.IsValid(text))
        {
            return false;
        }

        byte[] buffer = new byte[expectedLength + 3];
        if (!Base64Url.TryDecodeFromChars(text, buffer, out int written) || written != expectedLength)
        {
            return false;
        }

        bytes = buffer[..written];
        return true;
    }
}
