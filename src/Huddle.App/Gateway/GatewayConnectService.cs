using System.Buffers.Text;

namespace Agency.Huddle.App.Gateway;

/// <summary>The decision logic behind <c>POST /auth/connect</c> (Spec 8.3), with no HTTP in it.</summary>
internal sealed class GatewayConnectService
{
    private const int MaxNameLength = 60;
    private const long PasswordBusyRetryMs = 1000;

    private readonly PasswordVerificationGate passwordGate;
    private readonly GatewaySettings settings;
    private readonly AuthRateLimiter limiter;
    private readonly ChallengeNonceStore nonces;
    private readonly DeviceRegistry devices;
    private readonly PairingService pairing;
    private readonly BootstrapTokenStore bootstrap;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the service.</summary>
    /// <param name="settings">The gateway settings and secrets.</param>
    /// <param name="limiter">The rate limiter.</param>
    /// <param name="nonces">The challenge nonces.</param>
    /// <param name="devices">The paired devices.</param>
    /// <param name="pairing">The pairing requests.</param>
    /// <param name="bootstrap">The bootstrap tokens.</param>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="passwordGate">Caps concurrent password verifications; defaults to a gate of <see cref="PasswordVerificationGate.DefaultMaxConcurrent"/>.</param>
    public GatewayConnectService(
        GatewaySettings settings,
        AuthRateLimiter limiter,
        ChallengeNonceStore nonces,
        DeviceRegistry devices,
        PairingService pairing,
        BootstrapTokenStore bootstrap,
        TimeProvider? timeProvider = null,
        PasswordVerificationGate? passwordGate = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(nonces);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(pairing);
        ArgumentNullException.ThrowIfNull(bootstrap);

        this.settings = settings;
        this.limiter = limiter;
        this.nonces = nonces;
        this.devices = devices;
        this.pairing = pairing;
        this.bootstrap = bootstrap;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.passwordGate = passwordGate ?? new PasswordVerificationGate();
    }

    /// <summary>Runs the handshake: limiter, nonce and signature, gateway auth, then pairing.</summary>
    /// <param name="request">The body.</param>
    /// <param name="context">The request's ingress.</param>
    public ConnectOutcome Connect(ConnectRequest request, ConnectContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        string client = ClientOf(context.Ingress);
        string rateClient = AuthRateLimiter.ClientKey(context.Ingress.Client);
        bool loopback = context.Ingress.Kind == IngressKind.Loopback;
        string? credentialScope = CredentialScope(request.Auth);

        if (this.LockedOut(rateClient, credentialScope, loopback) is { } locked)
        {
            return locked;
        }

        if (this.CheckDevice(request, rateClient, loopback) is { } deviceFailure)
        {
            return deviceFailure;
        }

        Gate gate = this.Authenticate(request, context, rateClient, loopback);
        if (gate.Failure is not null)
        {
            return gate.Failure;
        }

        if (credentialScope is not null)
        {
            this.limiter.Reset(AuthRateLimiter.Key(credentialScope, rateClient));
        }

        return this.Admit(request, context, gate, client);
    }

    private static string? CredentialScope(ConnectAuth auth)
    {
        if (!string.IsNullOrEmpty(auth.DeviceToken))
        {
            return AuthRateLimitScopes.DeviceToken;
        }

        if (!string.IsNullOrEmpty(auth.Token) || !string.IsNullOrEmpty(auth.Password))
        {
            return AuthRateLimitScopes.SharedSecret;
        }

        return string.IsNullOrEmpty(auth.BootstrapToken) ? null : AuthRateLimitScopes.BootstrapToken;
    }

    private static string ClientOf(IngressResult ingress)
    {
        if (ingress.Client is not { } address)
        {
            return "unknown";
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    /// <summary>The shared secret the client sent: in password mode the password, else the token; the other kind is the fallback.</summary>
    private static string? Presented(ConnectAuth auth, bool passwordMode)
    {
        string? first = passwordMode ? auth.Password : auth.Token;
        string? second = passwordMode ? auth.Token : auth.Password;
        return string.IsNullOrEmpty(first) ? second : first;
    }

    private static ConnectOutcome Failed(int status, string code)
        => new(ConnectOutcome.Kind.Failed, status, code, null, null, null, null, [], null);

    private static ConnectOutcome Connected(DeviceRecord device, string? deviceToken)
        => new(ConnectOutcome.Kind.Connected, 200, null, null, null, device.DeviceId, deviceToken, device.Scopes, device.PairedVia);

    private static string DeviceName(ConnectRequest request)
    {
        string name = $"{request.Platform} {request.DeviceFamily}".Trim();
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    private ConnectOutcome? LockedOut(string client, string? credentialScope, bool loopback)
    {
        AuthRateLimitDecision signature = this.limiter.Check(AuthRateLimiter.Key(AuthRateLimitScopes.DeviceSignature, client), loopback);
        AuthRateLimitDecision credential = credentialScope is null
            ? new AuthRateLimitDecision(true, 0)
            : this.limiter.Check(AuthRateLimiter.Key(credentialScope, client), loopback);
        if (signature.Allowed && credential.Allowed)
        {
            return null;
        }

        return Failed(429, AuthRateLimiter.ErrorCode) with { RetryAfterMs = Math.Max(signature.RetryAfterMs, credential.RetryAfterMs) };
    }

    private ConnectOutcome Fail(string scope, string client, bool loopback, string code)
    {
        AuthRateLimitFailure failure = this.limiter.RecordFailure(AuthRateLimiter.Key(scope, client), loopback);
        return Failed(401, code) with { Delay = failure.Delay };
    }

    /// <summary>Spec 6.6 steps 1 to 4: the nonce, then the device signature. Each failure is counted.</summary>
    private ConnectOutcome? CheckDevice(ConnectRequest request, string client, bool loopback)
    {
        string? code = this.nonces.Consume(request.Nonce) switch
        {
            ChallengeNonceStore.Check.Required => DeviceAuth.NonceRequired,
            ChallengeNonceStore.Check.Mismatch => DeviceAuth.NonceMismatch,
            _ => DeviceAuth.Verify(request, this.timeProvider.GetUtcNow().ToUnixTimeMilliseconds()),
        };
        return code is null ? null : this.Fail(AuthRateLimitScopes.DeviceSignature, client, loopback, code);
    }

    /// <summary>Spec 6.6 step 5: gateway auth. A device token, a bootstrap token, a Tailscale identity, a trusted proxy or the shared secret.</summary>
    private Gate Authenticate(ConnectRequest request, ConnectContext context, string client, bool loopback)
    {
        GatewayAuthMode mode = this.settings.Options.Auth.Mode;
        IngressResult ingress = context.Ingress;
        ConnectAuth auth = request.Auth;
        bool launcher = ingress.Kind == IngressKind.Loopback && PresentsOnlyATokenOfItsOwn(auth);
        bool refused = ingress.Kind == IngressKind.Rejected
            || (mode == GatewayAuthMode.None && ingress.Kind != IngressKind.Loopback)
            || (mode == GatewayAuthMode.TrustedProxy && ingress.Kind != IngressKind.TrustedProxy && !launcher);
        if (refused)
        {
            return Gate.Refused(Failed(401, ConnectOutcome.AuthRequired));
        }

        if (ingress.IsFunnel)
        {
            Gate password = this.CheckSharedSecret(auth, GatewayAuthMode.Password, client, loopback);
            if (password.Failure is not null || (string.IsNullOrEmpty(auth.DeviceToken) && string.IsNullOrEmpty(auth.BootstrapToken)))
            {
                return password;
            }
        }

        if (!string.IsNullOrEmpty(auth.DeviceToken))
        {
            bool known = this.devices.FindByToken(auth.DeviceToken) is { } owner
                && string.Equals(owner.DeviceId, request.DeviceId, StringComparison.Ordinal);
            return known
                ? Gate.Passed(null)
                : Gate.Refused(this.Fail(AuthRateLimitScopes.DeviceToken, client, loopback, ConnectOutcome.DeviceTokenMismatch));
        }

        if (string.IsNullOrEmpty(auth.Token) && string.IsNullOrEmpty(auth.Password) && !string.IsNullOrEmpty(auth.BootstrapToken))
        {
            BootstrapTokenStore.Redemption redemption = this.bootstrap.Redeem(auth.BootstrapToken, request.DeviceId);
            return redemption.Valid
                ? Gate.Passed(redemption.TokenKind)
                : Gate.Refused(this.Fail(AuthRateLimitScopes.BootstrapToken, client, loopback, ConnectOutcome.BootstrapTokenInvalid));
        }

        bool identity = ingress.Kind == IngressKind.Managed && !ingress.IsFunnel && !string.IsNullOrEmpty(ingress.TailscaleLogin)
            && this.settings.Options.Auth.AllowTailscale;
        if (identity || mode is GatewayAuthMode.None or GatewayAuthMode.TrustedProxy)
        {
            return Gate.Passed(null);
        }

        return this.CheckSharedSecret(auth, mode, client, loopback);
    }

    /// <summary>D-14: a device token, or a bootstrap token alone, as the launcher presents one; no shared secret rides with it.</summary>
    private static bool PresentsOnlyATokenOfItsOwn(ConnectAuth auth) =>
        !string.IsNullOrEmpty(auth.DeviceToken)
        || (string.IsNullOrEmpty(auth.Token) && string.IsNullOrEmpty(auth.Password) && !string.IsNullOrEmpty(auth.BootstrapToken));

    private Gate CheckSharedSecret(ConnectAuth auth, GatewayAuthMode mode, string client, bool loopback)
    {
        bool passwordMode = mode == GatewayAuthMode.Password;
        string? presented = Presented(auth, passwordMode);
        if (string.IsNullOrEmpty(presented))
        {
            string missing = passwordMode ? ConnectOutcome.PasswordMissing : ConnectOutcome.TokenMissing;
            return Gate.Refused(this.Fail(AuthRateLimitScopes.SharedSecret, client, loopback, missing));
        }

        bool matches;
        if (passwordMode)
        {
            using IDisposable? lease = this.passwordGate.TryEnter();
            if (lease is null)
            {
                return Gate.Refused(Failed(429, AuthRateLimiter.ErrorCode) with { RetryAfterMs = PasswordBusyRetryMs });
            }

            matches = this.settings.VerifyPassword(presented);
        }
        else
        {
            matches = this.settings.VerifyToken(presented);
        }

        if (matches)
        {
            return Gate.Passed(null);
        }

        string mismatch = passwordMode ? ConnectOutcome.PasswordMismatch : ConnectOutcome.TokenMismatch;
        return Gate.Refused(this.Fail(AuthRateLimitScopes.SharedSecret, client, loopback, mismatch));
    }

    /// <summary>Spec 6.6 step 6: paired, auto-approved, or a request.</summary>
    private ConnectOutcome Admit(ConnectRequest request, ConnectContext context, Gate gate, string client)
    {
        IReadOnlyList<string> requested = GatewayScopes.Known(request.Scopes);
        if (requested.Count == 0)
        {
            requested = GatewayScopes.Limited;
        }

        DeviceRecord? device = this.devices.Find(request.DeviceId);
        if (device is { HasToken: true } && GatewayScopes.HoldsAll(device.Scopes, requested))
        {
            return this.Admitted(device, null, client, context);
        }

        PairingPolicy.Decision decision = this.Decide(request, context, gate, requested);
        if (!decision.AutoApprove)
        {
            return this.RequestPairing(request, context, decision.Scopes, client);
        }

        IReadOnlyList<string> grant = device is { HasToken: true } ? GatewayScopes.Normalize([.. device.Scopes, .. decision.Scopes]) : decision.Scopes;
        string? token = this.devices.Pair(new DeviceRegistry.Registration(
            request.DeviceId,
            Base64Url.DecodeFromChars(request.PublicKey),
            DeviceName(request),
            grant,
            decision.PairedVia ?? "approval",
            context.Ingress.TailscaleLogin,
            client,
            context.UserAgent));
        DeviceRecord? paired = this.devices.Find(request.DeviceId);
        return paired is null ? Failed(401, ConnectOutcome.AuthRequired) : Connected(paired, token);
    }

    private PairingPolicy.Decision Decide(ConnectRequest request, ConnectContext context, Gate gate, IReadOnlyList<string> requested)
    {
        GatewayOptions.DeviceAutoApproveOptions auto = this.settings.Options.TrustedProxy.DeviceAutoApprove;
        IReadOnlyList<string> cap = auto.Scopes.Count == 0 ? GatewayOptions.DeviceAutoApproveOptions.DefaultScopes : auto.Scopes;
        PairingPolicy.TrustedProxyRule? rule = auto.Enabled ? new PairingPolicy.TrustedProxyRule(true, cap) : null;
        return PairingPolicy.Decide(new PairingPolicy.Input(
            context.Ingress.Kind,
            context.HostAndOriginLoopback,
            this.settings.Options.AutoApproveLocal,
            GatewayAuthPassed: true,
            context.Ingress.TailscaleLogin,
            this.settings.Options.Auth.AllowTailscale,
            context.Ingress.IsFunnel,
            gate.Bootstrap,
            rule,
            requested,
            Revoked: this.devices.IsRevoked(request.DeviceId)));
    }

    private ConnectOutcome Admitted(DeviceRecord device, string? issuedToken, string client, ConnectContext context)
    {
        this.devices.Touch(device.DeviceId, client, context.UserAgent);
        return Connected(device, issuedToken ?? this.pairing.TakePendingToken(device.DeviceId));
    }

    private ConnectOutcome RequestPairing(ConnectRequest request, ConnectContext context, IReadOnlyList<string> scopes, string client)
    {
        PairingRequest? opened = this.pairing.Open(new PairingService.Candidate(
            request.DeviceId,
            Base64Url.DecodeFromChars(request.PublicKey),
            scopes,
            DeviceName(request),
            client,
            context.UserAgent,
            IngressKindText.Of(context.Ingress.Kind),
            context.Ingress.TailscaleLogin));
        if (opened is null)
        {
            return Failed(503, ConnectOutcome.PairingQueueFull);
        }

        return new ConnectOutcome(
            ConnectOutcome.Kind.PairingRequired, 200, ConnectOutcome.PairingRequired, null, opened.RequestId, request.DeviceId, null, [], null);
    }

    /// <summary>What gateway auth decided.</summary>
    private sealed record Gate(ConnectOutcome? Failure, BootstrapTokenStore.Kind? Bootstrap)
    {
        public static Gate Passed(BootstrapTokenStore.Kind? bootstrap) => new(null, bootstrap);

        public static Gate Refused(ConnectOutcome failure) => new(failure, null);
    }
}
