using System.Buffers.Text;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.6 steps 2 to 4: the clock skew, the device id and key, and the Ed25519 signature, each with OpenClaw's code.</summary>
public sealed class DeviceAuthVerifierTests
{
    private static readonly long Now = new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static readonly TestDevice Device = new(Enumerable.Repeat((byte)1, 32).ToArray());

    private static ConnectRequest Signed(long signedAt, ConnectAuth? auth = null)
        => Device.Build("bm9uY2U", signedAt, [GatewayScopes.Read, GatewayScopes.Write], auth ?? new ConnectAuth(Token: "tok"));

    /// <summary>A correctly signed request signed now passes every check.</summary>
    [Fact]
    public void Verify_ValidRequest_ReturnsNull()
    {
        Assert.Null(DeviceAuth.Verify(Signed(Now), Now));
    }

    /// <summary>The skew window is plus or minus 60 seconds: 59 s either way passes.</summary>
    [Theory]
    [InlineData(-59_000)]
    [InlineData(59_000)]
    [InlineData(-60_000)]
    [InlineData(60_000)]
    public void Verify_WithinSkew_Passes(long offsetMs)
    {
        Assert.Null(DeviceAuth.Verify(Signed(Now + offsetMs), Now));
    }

    /// <summary>More than 60 seconds from the server clock, past or future, is <c>DEVICE_AUTH_SIGNATURE_EXPIRED</c>.</summary>
    [Theory]
    [InlineData(-61_000)]
    [InlineData(61_000)]
    [InlineData(-3_600_000)]
    public void Verify_OutsideSkew_IsSignatureExpired(long offsetMs)
    {
        Assert.Equal(DeviceAuth.SignatureExpired, DeviceAuth.Verify(Signed(Now + offsetMs), Now));
    }

    /// <summary>A device id that is not the hash of the key is <c>DEVICE_AUTH_DEVICE_ID_MISMATCH</c>.</summary>
    [Fact]
    public void Verify_WrongDeviceId_IsDeviceIdMismatch()
    {
        ConnectRequest request = Signed(Now) with { DeviceId = new string('a', 64) };

        Assert.Equal(DeviceAuth.DeviceIdMismatch, DeviceAuth.Verify(request, Now));
    }

    /// <summary>A key that is not 32 bytes (whose own hash is the device id) is <c>DEVICE_AUTH_PUBLIC_KEY_INVALID</c>.</summary>
    [Fact]
    public void Verify_KeyOfWrongLength_IsPublicKeyInvalid()
    {
        byte[] shortKey = new byte[31];
        ConnectRequest request = Signed(Now) with
        {
            PublicKey = Base64Url.EncodeToString(shortKey),
            DeviceId = DeviceAuth.DeviceIdOf(shortKey),
        };

        Assert.Equal(DeviceAuth.PublicKeyInvalid, DeviceAuth.Verify(request, Now));
    }

    /// <summary>A key that is not base64url at all is <c>DEVICE_AUTH_PUBLIC_KEY_INVALID</c>.</summary>
    [Fact]
    public void Verify_KeyNotBase64Url_IsPublicKeyInvalid()
    {
        ConnectRequest request = Signed(Now) with { PublicKey = "***not base64***" };

        Assert.Equal(DeviceAuth.PublicKeyInvalid, DeviceAuth.Verify(request, Now));
    }

    /// <summary>A signature from another key is <c>DEVICE_AUTH_SIGNATURE_INVALID</c>.</summary>
    [Fact]
    public void Verify_SignatureFromAnotherKey_IsSignatureInvalid()
    {
        TestDevice other = new(Enumerable.Repeat((byte)2, 32).ToArray());
        ConnectRequest request = Signed(Now) with { Signature = other.Build("bm9uY2U", Now, [GatewayScopes.Read, GatewayScopes.Write], new ConnectAuth(Token: "tok")).Signature };

        Assert.Equal(DeviceAuth.SignatureInvalid, DeviceAuth.Verify(request, Now));
    }

    /// <summary>Every signed field is covered: changing the scopes, the nonce, the credential or the platform after signing breaks the signature.</summary>
    [Fact]
    public void Verify_AnySignedFieldChangedAfterSigning_IsSignatureInvalid()
    {
        ConnectRequest signed = Signed(Now);

        Assert.Equal(DeviceAuth.SignatureInvalid, DeviceAuth.Verify(signed with { Scopes = [GatewayScopes.Admin] }, Now));
        Assert.Equal(DeviceAuth.SignatureInvalid, DeviceAuth.Verify(signed with { Nonce = "b3RoZXI" }, Now));
        Assert.Equal(DeviceAuth.SignatureInvalid, DeviceAuth.Verify(signed with { Auth = new ConnectAuth(Token: "other") }, Now));
        Assert.Equal(DeviceAuth.SignatureInvalid, DeviceAuth.Verify(signed with { Platform = "Linux" }, Now));
    }

    /// <summary>A signature that is not base64url, or the wrong length, is <c>DEVICE_AUTH_SIGNATURE_INVALID</c>, never an exception.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("***")]
    [InlineData("c2hvcnQ")]
    public void Verify_MalformedSignature_IsSignatureInvalid(string signature)
    {
        ConnectRequest request = Signed(Now) with { Signature = signature };

        Assert.Equal(DeviceAuth.SignatureInvalid, DeviceAuth.Verify(request, Now));
    }

    /// <summary>The checks run in order: an expired request with a wrong device id reports the expiry first, and a wrong id with a bad signature reports the id.</summary>
    [Fact]
    public void Verify_ChecksRunInSpecOrder()
    {
        ConnectRequest bothWrong = Signed(Now - 120_000) with { DeviceId = new string('b', 64) };
        ConnectRequest idAndSignature = Signed(Now) with { DeviceId = new string('b', 64), Signature = "c2hvcnQ" };

        Assert.Equal(DeviceAuth.SignatureExpired, DeviceAuth.Verify(bothWrong, Now));
        Assert.Equal(DeviceAuth.DeviceIdMismatch, DeviceAuth.Verify(idAndSignature, Now));
    }
}
