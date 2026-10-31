using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.6: the v3 signed payload, the device id, and the scope helpers the payload and the device table share.</summary>
public sealed class DeviceAuthPayloadTests
{
    private const string DeviceId = "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925";

    /// <summary>The payload is the v3 format byte for byte, with the scopes sorted and joined by commas.</summary>
    [Fact]
    public void BuildPayloadV3_FixedVector_MatchesExactBytes()
    {
        string payload = DeviceAuth.BuildPayloadV3(
            DeviceId,
            [GatewayScopes.Write, GatewayScopes.Read],
            1_761_815_400_000,
            "tok-123",
            "bm9uY2U",
            "windows",
            "desktop");

        Assert.Equal(
            "v3|66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925|huddle-web|webchat|operator|operator.read,operator.write|1761815400000|tok-123|bm9uY2U|windows|desktop",
            payload);
    }

    /// <summary>A null or empty credential leaves the credential field empty, keeping both pipes.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildPayloadV3_NoCredential_LeavesTheFieldEmpty(string? credential)
    {
        string payload = DeviceAuth.BuildPayloadV3(DeviceId, [GatewayScopes.Read], 5, credential, "n1", "windows", "desktop");

        Assert.Equal($"v3|{DeviceId}|huddle-web|webchat|operator|operator.read|5||n1|windows|desktop", payload);
    }

    /// <summary>Platform and device family are trimmed and lower-cased before they are signed.</summary>
    [Fact]
    public void BuildPayloadV3_PlatformAndFamily_AreTrimmedAndLowerCased()
    {
        string payload = DeviceAuth.BuildPayloadV3(DeviceId, [GatewayScopes.Read], 5, null, "n1", "  Windows ", "\tDesktop  ");

        Assert.Equal($"v3|{DeviceId}|huddle-web|webchat|operator|operator.read|5||n1|windows|desktop", payload);
    }

    /// <summary>The scopes are sorted whatever order they arrive in, so client and server agree on the bytes.</summary>
    [Fact]
    public void BuildPayloadV3_ScopesInAnyOrder_AreSorted()
    {
        string payload = DeviceAuth.BuildPayloadV3(
            DeviceId,
            [GatewayScopes.Write, GatewayScopes.Admin, GatewayScopes.Read, GatewayScopes.Pairing],
            5,
            null,
            "n1",
            "w",
            "d");

        Assert.Equal($"v3|{DeviceId}|huddle-web|webchat|operator|operator.admin,operator.pairing,operator.read,operator.write|5||n1|w|d", payload);
    }

    /// <summary>The device id is the lowercase hex SHA-256 of the raw 32-byte public key (SHA-256 of 32 zero bytes is a known value).</summary>
    [Fact]
    public void DeviceIdOf_ThirtyTwoZeroBytes_IsTheKnownSha256Hex()
    {
        string id = DeviceAuth.DeviceIdOf(new byte[32]);

        Assert.Equal(DeviceId, id);
    }

    /// <summary>The device id of a real key matches the test device's independently computed id, and is 64 lowercase hex characters.</summary>
    [Fact]
    public void DeviceIdOf_RealKey_Is64LowercaseHex()
    {
        TestDevice device = new(Enumerable.Repeat((byte)7, 32).ToArray());

        string id = DeviceAuth.DeviceIdOf(device.PublicKey);

        Assert.Equal(device.DeviceId, id);
        Assert.Equal(64, id.Length);
        Assert.Equal(id.ToLowerInvariant(), id);
    }

    /// <summary>The credential a signature covers follows OpenClaw's order: device token, else token or password, else bootstrap token, else nothing.</summary>
    [Fact]
    public void SignedCredential_FollowsClientOrder()
    {
        Assert.Equal("dev", new ConnectAuth(Token: "tok", DeviceToken: "dev", BootstrapToken: "boot").SignedCredential);
        Assert.Equal("tok", new ConnectAuth(Token: "tok", BootstrapToken: "boot").SignedCredential);
        Assert.Equal("pw", new ConnectAuth(Password: "pw", BootstrapToken: "boot").SignedCredential);
        Assert.Equal("boot", new ConnectAuth(BootstrapToken: "boot").SignedCredential);
        Assert.Equal(string.Empty, new ConnectAuth().SignedCredential);
    }

    /// <summary>Normalising scopes sorts them, drops duplicates, and adds read to write because V1 has no read-only mode.</summary>
    [Fact]
    public void Normalize_SortsDeduplicatesAndPairsReadWithWrite()
    {
        IReadOnlyList<string> scopes = GatewayScopes.Normalize([GatewayScopes.Write, GatewayScopes.Write]);

        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], scopes);
    }

    /// <summary>Admin holds every scope; a limited set holds neither admin nor pairing.</summary>
    [Fact]
    public void Holds_AdminImpliesEveryScope()
    {
        Assert.True(GatewayScopes.Holds([GatewayScopes.Admin], GatewayScopes.Pairing));
        Assert.True(GatewayScopes.HoldsAll([GatewayScopes.Admin], [GatewayScopes.Read, GatewayScopes.Write]));
        Assert.False(GatewayScopes.Holds(GatewayScopes.Limited, GatewayScopes.Admin));
        Assert.False(GatewayScopes.Holds(GatewayScopes.Limited, GatewayScopes.Pairing));
        Assert.True(GatewayScopes.HoldsAll(GatewayScopes.Limited, [GatewayScopes.Read]));
    }

    /// <summary>The stored scope text is sorted and comma-joined.</summary>
    [Fact]
    public void Join_SortsAndCommaJoins()
    {
        Assert.Equal("operator.read,operator.write", GatewayScopes.Join([GatewayScopes.Write, GatewayScopes.Read]));
    }
}
