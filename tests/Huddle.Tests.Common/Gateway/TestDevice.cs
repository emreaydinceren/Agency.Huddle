using System.Buffers.Text;
using System.Security.Cryptography;
using Agency.Huddle.App.Gateway;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>A browser's device identity in a test: an Ed25519 key pair from a known seed, signing exactly as <c>auth.js</c> will.</summary>
internal sealed class TestDevice
{
    private readonly Ed25519PrivateKeyParameters privateKey;

    /// <summary>Creates a device from a 32-byte seed; a random seed when <paramref name="seed"/> is <see langword="null"/>.</summary>
    /// <param name="seed">The private key seed.</param>
    public TestDevice(byte[]? seed = null)
    {
        this.Seed = seed ?? RandomNumberGenerator.GetBytes(32);
        this.privateKey = new Ed25519PrivateKeyParameters(this.Seed);
        this.PublicKey = this.privateKey.GeneratePublicKey().GetEncoded();
        this.DeviceId = Convert.ToHexStringLower(SHA256.HashData(this.PublicKey));
    }

    /// <summary>The seed.</summary>
    public byte[] Seed { get; }

    /// <summary>The 32 public key bytes.</summary>
    public byte[] PublicKey { get; }

    /// <summary>The lowercase hex SHA-256 of <see cref="PublicKey"/>.</summary>
    public string DeviceId { get; }

    /// <summary>The public key as base64url without padding.</summary>
    public string PublicKeyText => Base64Url.EncodeToString(this.PublicKey);

    /// <summary>Signs <paramref name="payload"/> (UTF-8) and returns the signature as base64url without padding.</summary>
    /// <param name="payload">The payload text.</param>
    public string Sign(string payload)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        Ed25519Signer signer = new();
        signer.Init(true, this.privateKey);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        return Base64Url.EncodeToString(signer.GenerateSignature());
    }

    /// <summary>Builds a signed handshake body.</summary>
    /// <param name="nonce">The challenge nonce.</param>
    /// <param name="signedAtMs">The signing time in Unix milliseconds.</param>
    /// <param name="scopes">The scopes asked for.</param>
    /// <param name="auth">The credentials; the signature covers the device token, else token or password, else bootstrap token.</param>
    /// <param name="platform">The platform text.</param>
    /// <param name="deviceFamily">The device family text.</param>
    public ConnectRequest Build(
        string nonce,
        long signedAtMs,
        IReadOnlyList<string> scopes,
        ConnectAuth auth,
        string platform = "Windows",
        string deviceFamily = "Desktop")
    {
        string credential = auth.DeviceToken ?? auth.Token ?? auth.Password ?? auth.BootstrapToken ?? string.Empty;
        string payload = DeviceAuth.BuildPayloadV3(this.DeviceId, scopes, signedAtMs, credential, nonce, platform, deviceFamily);
        return new ConnectRequest(
            this.DeviceId,
            this.PublicKeyText,
            this.Sign(payload),
            signedAtMs,
            nonce,
            platform,
            deviceFamily,
            DeviceAuth.Role,
            scopes,
            auth);
    }
}
