using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.5, 6.6 and 8.3: the connect decision for every credential kind, the rate limiter's counting, and the pairing flow end to end.</summary>
public sealed class GatewayConnectServiceTests
{
    private static readonly string[] AdminApprover = [GatewayScopes.Admin];

    private static ConnectAuth Token(string token = GatewayConnectHarness.GatewayToken) => new(Token: token);

    private static GatewayConnectHarness Password() => new(config =>
    {
        config["Team:Gateway:Auth:Mode"] = "password";
        config["Team:Gateway:Auth:Password"] = GatewayConnectHarness.Password;
    });

    private static GatewayConnectHarness Mode(string mode) => new(config => config["Team:Gateway:Auth:Mode"] = mode);

    private static void AssertConnected(ConnectOutcome outcome, string via)
    {
        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
        Assert.Equal(200, outcome.HttpStatus);
        Assert.Null(outcome.Code);
        Assert.Equal(via, outcome.PairedVia);
    }

    private static void AssertFailed(ConnectOutcome outcome, int status, string code)
    {
        Assert.Equal(ConnectOutcome.Kind.Failed, outcome.Result);
        Assert.Equal(status, outcome.HttpStatus);
        Assert.Equal(code, outcome.Code);
        Assert.Null(outcome.DeviceToken);
    }

    /// <summary>The gateway token on a loopback connection pairs the device as admin, via loopback, and issues a device token.</summary>
    [Fact]
    public void Connect_GatewayTokenOnLoopback_PairsAsAdminAndIssuesToken()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();

        ConnectOutcome outcome = harness.Connect(device, Token(), GatewayConnectHarness.Loopback);

        AssertConnected(outcome, "loopback");
        Assert.Equal(device.DeviceId, outcome.DeviceId);
        Assert.True(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
        Assert.False(string.IsNullOrEmpty(outcome.DeviceToken));
        Assert.Equal(device.DeviceId, harness.Devices.FindByToken(outcome.DeviceToken)?.DeviceId);
    }

    /// <summary>The device token is issued once: the same device connecting again with the gateway token gets none.</summary>
    [Fact]
    public void Connect_PairedDeviceAgain_IssuesNoSecondToken()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = harness.Connect(device, Token(), GatewayConnectHarness.Loopback);

        ConnectOutcome again = harness.Connect(device, Token(), GatewayConnectHarness.Loopback);

        AssertConnected(again, "loopback");
        Assert.Null(again.DeviceToken);
    }

    /// <summary>A wrong gateway token is a 401 <c>AUTH_TOKEN_MISMATCH</c> and creates no device and no request.</summary>
    [Fact]
    public void Connect_WrongToken_IsUnauthorized()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();

        ConnectOutcome outcome = harness.Connect(device, Token("wrong"), GatewayConnectHarness.Direct());

        AssertFailed(outcome, 401, ConnectOutcome.TokenMismatch);
        Assert.Null(harness.Devices.Find(device.DeviceId));
        Assert.Empty(harness.Pairing.Pending());
    }

    /// <summary>No credential in token mode away from loopback is a 401 <c>AUTH_TOKEN_MISSING</c>.</summary>
    [Fact]
    public void Connect_NoCredentialInTokenMode_IsTokenMissing()
    {
        using GatewayConnectHarness harness = new();

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), GatewayConnectHarness.Direct());

        AssertFailed(outcome, 401, ConnectOutcome.TokenMissing);
    }

    /// <summary>Password mode accepts the gateway password and refuses the gateway token and a wrong password.</summary>
    [Fact]
    public void Connect_PasswordMode_AcceptsOnlyThePassword()
    {
        using GatewayConnectHarness harness = Password();

        ConnectOutcome good = harness.Connect(new TestDevice(), new ConnectAuth(Password: GatewayConnectHarness.Password), GatewayConnectHarness.Loopback);
        ConnectOutcome wrong = harness.Connect(new TestDevice(), new ConnectAuth(Password: "nope"), GatewayConnectHarness.Loopback);
        ConnectOutcome token = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Loopback);
        ConnectOutcome none = harness.Connect(new TestDevice(), new ConnectAuth(), GatewayConnectHarness.Loopback);

        AssertConnected(good, "loopback");
        AssertFailed(wrong, 401, ConnectOutcome.PasswordMismatch);
        AssertFailed(token, 401, ConnectOutcome.PasswordMismatch);
        AssertFailed(none, 401, ConnectOutcome.PasswordMissing);
    }

    /// <summary>A device token alone is a credential: a paired device connects with it and is issued no new token.</summary>
    [Fact]
    public void Connect_DeviceToken_ConnectsAPairedDevice()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        string token = harness.Connect(device, Token(), GatewayConnectHarness.Loopback).DeviceToken ?? string.Empty;

        ConnectOutcome outcome = harness.Connect(device, new ConnectAuth(DeviceToken: token), GatewayConnectHarness.Direct());

        AssertConnected(outcome, "loopback");
        Assert.Null(outcome.DeviceToken);
        Assert.True(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
    }

    /// <summary>A wrong device token, another device's token, and a revoked token are each 401 <c>AUTH_DEVICE_TOKEN_MISMATCH</c>.</summary>
    [Fact]
    public void Connect_BadDeviceToken_IsUnauthorized()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        TestDevice other = new();
        string token = harness.Connect(device, Token(), GatewayConnectHarness.Loopback).DeviceToken ?? string.Empty;
        _ = harness.Connect(other, Token(), GatewayConnectHarness.Loopback);

        ConnectOutcome wrong = harness.Connect(device, new ConnectAuth(DeviceToken: "wrong"), GatewayConnectHarness.Direct());
        ConnectOutcome foreign = harness.Connect(other, new ConnectAuth(DeviceToken: token), GatewayConnectHarness.Direct());
        _ = harness.Devices.Revoke(device.DeviceId);
        ConnectOutcome revoked = harness.Connect(device, new ConnectAuth(DeviceToken: token), GatewayConnectHarness.Direct());

        AssertFailed(wrong, 401, ConnectOutcome.DeviceTokenMismatch);
        AssertFailed(foreign, 401, ConnectOutcome.DeviceTokenMismatch);
        AssertFailed(revoked, 401, ConnectOutcome.DeviceTokenMismatch);
    }

    /// <summary>A paired device whose scopes cover the request connects with the gateway token from anywhere, without a new request.</summary>
    [Fact]
    public void Connect_PairedDeviceWithGatewayToken_ConnectsWithoutPairing()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        _ = harness.Devices.Pair(new DeviceRegistry.Registration(device.DeviceId, device.PublicKey, "Laptop", GatewayScopes.Limited, "approval", null, "203.0.113.9", null));

        ConnectOutcome outcome = harness.Connect(device, Token(), GatewayConnectHarness.Direct());

        AssertConnected(outcome, "approval");
        Assert.Empty(harness.Pairing.Pending());
    }

    /// <summary>An admin bootstrap token pairs the device as admin, via bootstrap, from a non-loopback ingress with no gateway token.</summary>
    [Fact]
    public void Connect_AdminBootstrap_PairsAsAdmin()
    {
        using GatewayConnectHarness harness = new();
        string bootstrap = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: bootstrap), GatewayConnectHarness.Direct());

        AssertConnected(outcome, "bootstrap");
        Assert.True(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
        Assert.False(string.IsNullOrEmpty(outcome.DeviceToken));
    }

    /// <summary>A bootstrap token is single use and bound to the first device: a second device is refused, and so is anyone after it expires.</summary>
    [Fact]
    public void Connect_Bootstrap_IsBoundToTheFirstDeviceAndExpires()
    {
        using GatewayConnectHarness harness = new();
        string bound = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);
        string stale = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);
        Assert.Equal(ConnectOutcome.Kind.Connected, harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: bound), GatewayConnectHarness.Direct()).Result);

        ConnectOutcome second = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: bound), GatewayConnectHarness.Direct());
        harness.Clock.Advance(TimeSpan.FromMinutes(11));
        ConnectOutcome expired = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: stale), GatewayConnectHarness.Direct());

        AssertFailed(second, 401, ConnectOutcome.BootstrapTokenInvalid);
        AssertFailed(expired, 401, ConnectOutcome.BootstrapTokenInvalid);
    }

    /// <summary>A setup-link token is never auto-approved: it opens a pairing request.</summary>
    [Fact]
    public void Connect_SetupLink_RequiresApproval()
    {
        using GatewayConnectHarness harness = new();
        string link = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.SetupLink);

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: link), GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, outcome.Result);
        Assert.Single(harness.Pairing.Pending());
    }

    /// <summary>A verified Tailscale identity stands in for gateway auth: no credential is needed and the device pairs limited, via tailscale.</summary>
    [Fact]
    public void Connect_TailscaleIdentity_PairsLimitedWithoutACredential()
    {
        using GatewayConnectHarness harness = new();

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), GatewayConnectHarness.Managed("ada@example.com"));

        AssertConnected(outcome, "tailscale");
        Assert.False(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
        Assert.True(GatewayScopes.HoldsAll(outcome.Scopes, [GatewayScopes.Read, GatewayScopes.Write]));
        Assert.Equal("ada@example.com", harness.Devices.Find(outcome.DeviceId!)?.TailscaleLogin);
    }

    /// <summary>With <c>AllowTailscale</c> off, a Tailscale identity is no credential.</summary>
    [Fact]
    public void Connect_TailscaleIdentityWithAllowTailscaleOff_IsRefused()
    {
        using GatewayConnectHarness harness = new(config => config["Team:Gateway:Auth:AllowTailscale"] = "false");

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), GatewayConnectHarness.Managed());

        AssertFailed(outcome, 401, ConnectOutcome.TokenMissing);
    }

    /// <summary>Funnel traffic has no identity and always needs the password: with it the device needs approval, without it the handshake is refused.</summary>
    [Fact]
    public void Connect_Funnel_NeedsThePasswordAndApproval()
    {
        using GatewayConnectHarness harness = Password();

        ConnectOutcome none = harness.Connect(new TestDevice(), new ConnectAuth(), GatewayConnectHarness.Funnel);
        ConnectOutcome with = harness.Connect(new TestDevice(), new ConnectAuth(Password: GatewayConnectHarness.Password), GatewayConnectHarness.Funnel);

        AssertFailed(none, 401, ConnectOutcome.PasswordMissing);
        Assert.Equal(ConnectOutcome.Kind.PairingRequired, with.Result);
    }

    /// <summary>Mode none lets a loopback client in with no credential, still pairing it as admin; device identity stays required.</summary>
    [Fact]
    public void Connect_ModeNoneOnLoopback_NeedsNoCredential()
    {
        using GatewayConnectHarness harness = Mode("none");

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(), GatewayConnectHarness.Loopback);

        AssertConnected(outcome, "loopback");
        Assert.True(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
    }

    /// <summary>Mode none is loopback only: any other ingress is refused with 401 <c>AUTH_REQUIRED</c>, even with the gateway token.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Connect_ModeNoneAwayFromLoopback_IsRefused(bool withToken)
    {
        using GatewayConnectHarness harness = Mode("none");

        ConnectOutcome outcome = harness.Connect(new TestDevice(), withToken ? Token() : new ConnectAuth(), GatewayConnectHarness.Direct());

        AssertFailed(outcome, 401, ConnectOutcome.AuthRequired);
    }

    /// <summary>Mode none does not waive the device signature: a bad signature on loopback is still <c>DEVICE_AUTH_SIGNATURE_INVALID</c>.</summary>
    [Fact]
    public void Connect_ModeNoneWithBadSignature_IsStillRefused()
    {
        using GatewayConnectHarness harness = Mode("none");
        ChallengeNonceStore.Challenge challenge = harness.Nonces.Issue();
        ConnectRequest request = new TestDevice().Build(challenge.Nonce, challenge.Ts, [GatewayScopes.Read], new ConnectAuth()) with { Signature = "c2hvcnQ" };

        ConnectOutcome outcome = harness.Service.Connect(request, new ConnectContext(GatewayConnectHarness.Loopback, true, null));

        AssertFailed(outcome, 401, DeviceAuth.SignatureInvalid);
    }

    /// <summary>A nonce can be used once: replaying a handshake is <c>DEVICE_AUTH_NONCE_MISMATCH</c>, and a missing nonce is <c>DEVICE_AUTH_NONCE_REQUIRED</c>.</summary>
    [Fact]
    public void Connect_NonceReplayedOrMissing_IsRefused()
    {
        using GatewayConnectHarness harness = new();
        ChallengeNonceStore.Challenge challenge = harness.Nonces.Issue();
        ConnectRequest request = new TestDevice().Build(challenge.Nonce, challenge.Ts, [GatewayScopes.Read], Token());
        ConnectContext context = new(GatewayConnectHarness.Loopback, true, null);

        Assert.Equal(ConnectOutcome.Kind.Connected, harness.Service.Connect(request, context).Result);

        AssertFailed(harness.Service.Connect(request, context), 401, DeviceAuth.NonceMismatch);
        AssertFailed(harness.Service.Connect(request with { Nonce = string.Empty }, context), 401, DeviceAuth.NonceRequired);
    }

    /// <summary>A handshake signed more than a minute ago is <c>DEVICE_AUTH_SIGNATURE_EXPIRED</c>.</summary>
    [Fact]
    public void Connect_StaleSignature_IsExpired()
    {
        using GatewayConnectHarness harness = new();
        ChallengeNonceStore.Challenge challenge = harness.Nonces.Issue();
        long signedAt = harness.Clock.GetUtcNow().ToUnixTimeMilliseconds() - 90_000;
        ConnectRequest request = new TestDevice().Build(challenge.Nonce, signedAt, [GatewayScopes.Read], Token());

        ConnectOutcome outcome = harness.Service.Connect(request, new ConnectContext(GatewayConnectHarness.Loopback, true, null));

        AssertFailed(outcome, 401, DeviceAuth.SignatureExpired);
    }

    /// <summary>Ten wrong tokens from one address lock it out: the tenth already answers the lockout, and the next handshake, even with the right token, is 429 <c>AUTH_RATE_LIMITED</c> with a retry time.</summary>
    [Fact]
    public void Connect_TenWrongTokens_LockTheAddressOut()
    {
        using GatewayConnectHarness harness = new();
        for (int attempt = 0; attempt < 9; attempt++)
        {
            AssertFailed(harness.Connect(new TestDevice(), Token("wrong"), GatewayConnectHarness.Direct()), 401, ConnectOutcome.TokenMismatch);
        }

        AssertFailed(harness.Connect(new TestDevice(), Token("wrong"), GatewayConnectHarness.Direct()), 429, AuthRateLimiter.ErrorCode);

        ConnectOutcome locked = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Direct());
        ConnectOutcome elsewhere = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Direct("203.0.113.77"));

        AssertFailed(locked, 429, AuthRateLimiter.ErrorCode);
        Assert.True(locked.RetryAfterMs > 0);
        Assert.Equal(ConnectOutcome.Kind.PairingRequired, elsewhere.Result);
    }

    /// <summary>The lockout ends: after five minutes the right token works again.</summary>
    [Fact]
    public void Connect_AfterTheLockout_WorksAgain()
    {
        using GatewayConnectHarness harness = new();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            _ = harness.Connect(new TestDevice(), Token("wrong"), GatewayConnectHarness.Direct());
        }

        harness.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Direct()).Result);
    }

    /// <summary>Loopback is exempt from lockout: any number of wrong tokens stay 401 and the right one still connects.</summary>
    [Fact]
    public void Connect_WrongTokensOnLoopback_NeverLockOut()
    {
        using GatewayConnectHarness harness = new();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            AssertFailed(harness.Connect(new TestDevice(), Token("wrong"), GatewayConnectHarness.Loopback), 401, ConnectOutcome.TokenMismatch);
        }

        Assert.Equal(ConnectOutcome.Kind.Connected, harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Loopback).Result);
    }

    /// <summary>Wrong device tokens are counted too: ten from one address lock the device-token scope out.</summary>
    [Fact]
    public void Connect_TenWrongDeviceTokens_LockTheAddressOut()
    {
        using GatewayConnectHarness harness = new();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            _ = harness.Connect(new TestDevice(), new ConnectAuth(DeviceToken: "wrong"), GatewayConnectHarness.Direct());
        }

        ConnectOutcome locked = harness.Connect(new TestDevice(), new ConnectAuth(DeviceToken: "wrong"), GatewayConnectHarness.Direct());

        AssertFailed(locked, 429, AuthRateLimiter.ErrorCode);
    }

    /// <summary>Bad signatures are counted: ten from one address lock it out, and a valid handshake is then refused with 429.</summary>
    [Fact]
    public void Connect_TenBadSignatures_LockTheAddressOut()
    {
        using GatewayConnectHarness harness = new();
        ConnectContext context = new(GatewayConnectHarness.Direct(), false, null);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            ChallengeNonceStore.Challenge challenge = harness.Nonces.Issue();
            ConnectRequest forged = new TestDevice().Build(challenge.Nonce, challenge.Ts, [GatewayScopes.Read], Token()) with { Signature = "c2hvcnQ" };
            bool locking = attempt == 9;
            AssertFailed(harness.Service.Connect(forged, context), locking ? 429 : 401, locking ? AuthRateLimiter.ErrorCode : DeviceAuth.SignatureInvalid);
        }

        ConnectOutcome valid = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Direct());

        AssertFailed(valid, 429, AuthRateLimiter.ErrorCode);
    }

    /// <summary>An unpaired device on a LAN address with the right token opens a request: 200-shaped <c>PAIRING_REQUIRED</c> with a request id and the device id, and no session.</summary>
    [Fact]
    public void Connect_UnpairedDeviceOnLan_RequiresPairing()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();

        ConnectOutcome outcome = harness.Connect(device, Token(), GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, outcome.Result);
        Assert.Equal(200, outcome.HttpStatus);
        Assert.Equal(ConnectOutcome.PairingRequired, outcome.Code);
        Assert.Equal(device.DeviceId, outcome.DeviceId);
        Assert.Equal(harness.Pairing.Pending().Single().RequestId, outcome.RequestId);
        Assert.Null(outcome.DeviceToken);
        Assert.Null(harness.Devices.Find(device.DeviceId));
    }

    /// <summary>Approval alone grants nothing: after it the device must handshake again, and that handshake delivers the device token exactly once.</summary>
    [Fact]
    public void Connect_AfterApproval_NeedsAnotherHandshakeAndDeliversTheTokenOnce()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        ConnectOutcome pending = harness.Connect(device, Token(), GatewayConnectHarness.Direct());
        _ = harness.Pairing.Approve(pending.RequestId!, AdminApprover, [GatewayScopes.Read, GatewayScopes.Write]);
        Assert.Equal(PairingService.State.Approved, harness.Pairing.StateOf(pending.RequestId!));

        ConnectOutcome connected = harness.Connect(device, Token(), GatewayConnectHarness.Direct());
        ConnectOutcome later = harness.Connect(device, Token(), GatewayConnectHarness.Direct());

        AssertConnected(connected, "approval");
        Assert.False(string.IsNullOrEmpty(connected.DeviceToken));
        Assert.Equal(device.DeviceId, harness.Devices.FindByToken(connected.DeviceToken)?.DeviceId);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], connected.Scopes);
        AssertConnected(later, "approval");
        Assert.Null(later.DeviceToken);
    }

    /// <summary>A rejected request grants nothing, and the next handshake opens a new request.</summary>
    [Fact]
    public void Connect_AfterRejection_OpensANewRequest()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        ConnectOutcome first = harness.Connect(device, Token(), GatewayConnectHarness.Direct());
        _ = harness.Pairing.Reject(first.RequestId!, AdminApprover);

        ConnectOutcome second = harness.Connect(device, Token(), GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, second.Result);
        Assert.NotEqual(first.RequestId, second.RequestId);
    }

    /// <summary>A new handshake with different scopes supersedes the pending request: a new id, the old one expired, one pending.</summary>
    [Fact]
    public void Connect_ChangedScopes_SupersedesThePendingRequest()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        ConnectOutcome first = harness.Connect(device, Token(), GatewayConnectHarness.Direct());

        ConnectOutcome second = harness.Connect(device, Token(), GatewayConnectHarness.Direct(), [GatewayScopes.Admin]);

        Assert.NotEqual(first.RequestId, second.RequestId);
        Assert.Equal(PairingService.State.Expired, harness.Pairing.StateOf(first.RequestId!));
        Assert.Single(harness.Pairing.Pending());
    }

    /// <summary>Request expiry shows at the endpoint: after five minutes the id reads as expired and a new handshake opens another.</summary>
    [Fact]
    public void Connect_RequestLapses_AfterFiveMinutes()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        ConnectOutcome first = harness.Connect(device, Token(), GatewayConnectHarness.Direct());

        harness.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.Equal(PairingService.State.Expired, harness.Pairing.StateOf(first.RequestId!));
        Assert.NotEqual(first.RequestId, harness.Connect(device, Token(), GatewayConnectHarness.Direct()).RequestId);
    }

    /// <summary>Ten pending requests fill the queue: the eleventh device gets 503 <c>PAIRING_QUEUE_FULL</c>.</summary>
    [Fact]
    public void Connect_EleventhPendingRequest_IsQueueFull()
    {
        using GatewayConnectHarness harness = new();
        for (int index = 0; index < 10; index++)
        {
            Assert.Equal(ConnectOutcome.Kind.PairingRequired, harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Direct()).Result);
        }

        ConnectOutcome eleventh = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Direct());

        AssertFailed(eleventh, 503, ConnectOutcome.PairingQueueFull);
    }

    /// <summary>With <c>AutoApproveLocal</c> off a loopback device needs approval like any other.</summary>
    [Fact]
    public void Connect_LoopbackWithAutoApproveLocalOff_RequiresPairing()
    {
        using GatewayConnectHarness harness = new(config => config["Team:Gateway:AutoApproveLocal"] = "false");

        ConnectOutcome outcome = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Loopback);

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, outcome.Result);
    }

    /// <summary>A loopback socket reached through a non-loopback <c>Host</c> or <c>Origin</c> is not auto-approved.</summary>
    [Fact]
    public void Connect_LoopbackWithForeignHostOrOrigin_RequiresPairing()
    {
        using GatewayConnectHarness harness = new();

        ConnectOutcome outcome = harness.Connect(new TestDevice(), Token(), GatewayConnectHarness.Loopback, hostAndOriginLoopback: false);

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, outcome.Result);
    }

    /// <summary>A paired device asking for more scopes than it holds is widened when the auto-approve rule allows it (loopback grants admin), keeping its token.</summary>
    [Fact]
    public void Connect_PairedDeviceAsksMoreOnLoopback_IsWidened()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        string token = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed()).DeviceToken ?? string.Empty;

        ConnectOutcome outcome = harness.Connect(device, Token(), GatewayConnectHarness.Loopback, [GatewayScopes.Admin]);

        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
        Assert.True(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
        Assert.Equal(device.DeviceId, harness.Devices.FindByToken(token)?.DeviceId);
    }

    /// <summary>Request admin: a Tailscale-paired limited device asking for admin gets a request that only an admin approves; after that it connects as admin.</summary>
    [Fact]
    public void Connect_RequestAdmin_NeedsAnAdminApprover()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        AssertConnected(harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed()), "tailscale");

        ConnectOutcome request = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed(), [GatewayScopes.Admin]);
        PairingService.ApprovalResult refused = harness.Pairing.Approve(request.RequestId!, [GatewayScopes.Pairing, GatewayScopes.Read, GatewayScopes.Write]);
        PairingService.ApprovalResult approved = harness.Pairing.Approve(request.RequestId!, AdminApprover);
        ConnectOutcome admin = harness.Connect(device, new ConnectAuth(), GatewayConnectHarness.Managed(), [GatewayScopes.Admin]);

        Assert.Equal(ConnectOutcome.Kind.PairingRequired, request.Result);
        Assert.Empty(harness.Pairing.Pending());
        Assert.Equal(PairingService.ApprovalOutcome.AdminRequired, refused.Outcome);
        Assert.Equal(PairingService.ApprovalOutcome.Approved, approved.Outcome);
        Assert.Equal(ConnectOutcome.Kind.Connected, admin.Result);
        Assert.True(GatewayScopes.Holds(admin.Scopes, GatewayScopes.Admin));
    }

    /// <summary>After revoke a device can no longer present its token; after remove it is unknown and must pair again; after rotate only the new token works.</summary>
    [Fact]
    public void Connect_AfterRevokeRemoveRotate_FollowsTheDeviceTable()
    {
        using GatewayConnectHarness harness = new();
        TestDevice device = new();
        string first = harness.Connect(device, Token(), GatewayConnectHarness.Loopback).DeviceToken ?? string.Empty;

        string second = harness.Devices.Rotate(device.DeviceId) ?? string.Empty;
        AssertFailed(harness.Connect(device, new ConnectAuth(DeviceToken: first), GatewayConnectHarness.Direct()), 401, ConnectOutcome.DeviceTokenMismatch);
        AssertConnected(harness.Connect(device, new ConnectAuth(DeviceToken: second), GatewayConnectHarness.Direct()), "loopback");

        _ = harness.Devices.Revoke(device.DeviceId);
        AssertFailed(harness.Connect(device, new ConnectAuth(DeviceToken: second), GatewayConnectHarness.Direct()), 401, ConnectOutcome.DeviceTokenMismatch);

        _ = harness.Devices.Remove(device.DeviceId);
        Assert.Equal(ConnectOutcome.Kind.PairingRequired, harness.Connect(device, Token(), GatewayConnectHarness.Direct()).Result);
    }
}
