namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The first gateway middleware (Spec 6.8, C-1, C-3): classifies the ingress (403 <c>proxy_attribution_required</c> when it is rejected), stores it in
/// <see cref="GatewayHttpItems"/>, applies the origin check, lets <c>/health</c> and <c>/_desktop/*</c> through for loopback only, and counts the
/// unauthenticated connect routes against the pre-auth limiter (503 over the cap). Under strict local mode every ingress but loopback is refused
/// with 403 <c>strict_local</c> before the origin check. Authentication and the session requirement run after it.
/// </summary>
/// <param name="next">The next middleware.</param>
internal sealed class GatewayGate(RequestDelegate next)
{
    private const string OriginNotAllowed = "origin_not_allowed";
    private const string LoopbackOnly = "loopback_only";
    private const string StrictLocal = "strict_local";

    /// <summary>Runs the gate for one request.</summary>
    /// <param name="http">The request.</param>
    /// <param name="whois">The Tailscale identity lookup.</param>
    /// <param name="limiter">The pre-auth connection limiter.</param>
    /// <param name="listener">Tells which listener the connection arrived on.</param>
    /// <param name="settings">The gateway settings (trusted proxies, origin and plain-HTTP rules).</param>
    /// <param name="bareNames">Records the admission of a bare-name <c>Host</c> (R-13).</param>
    public async Task InvokeAsync(
        HttpContext http,
        IIngressWhois whois,
        PreAuthConnectionLimiter limiter,
        GatewayListener listener,
        GatewaySettings settings,
        BareNameHostMonitor bareNames)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(bareNames);

        IngressResult ingress = await ClassifyAsync(http, whois, listener, settings.Options).ConfigureAwait(false);
        if (ingress.Kind == IngressKind.Rejected)
        {
            await RefuseAsync(http, StatusCodes.Status403Forbidden, ingress.Error ?? IngressClassifier.ProxyAttributionRequired).ConfigureAwait(false);
            return;
        }

        GatewayHttpItems.SetIngress(http, ingress);
        if (settings.Options.StrictLocal && ingress.Kind != IngressKind.Loopback)
        {
            await RefuseStrictLocalAsync(http).ConfigureAwait(false);
            return;
        }

        ApplyEffectiveScheme(http, ingress);
        if (!GatewayPlainHttp.IsAllowed(http.Request.Scheme, http.Request.Host.Host, settings.Options))
        {
            await RefusePlainHttpAsync(http).ConfigureAwait(false);
            return;
        }

        if (!GatewayOrigin.IsAllowed(http.Request, ingress, settings.Options))
        {
            await RefuseAsync(http, StatusCodes.Status403Forbidden, OriginNotAllowed).ConfigureAwait(false);
            return;
        }

        bareNames.Note(http.Request.Host.Host);
        switch (GatewayPaths.Of(http.Request.Path))
        {
            case GatewayPaths.Kind.LoopbackOnly when ingress.Kind != IngressKind.Loopback:
                await RefuseAsync(http, StatusCodes.Status403Forbidden, LoopbackOnly).ConfigureAwait(false);
                return;
            case GatewayPaths.Kind.LoopbackOnly:
                GatewayHttpItems.MarkPublic(http);
                break;
            case GatewayPaths.Kind.Public:
                GatewayHttpItems.MarkPublic(http);
                using (IDisposable? lease = limiter.TryAcquire((ingress.Client ?? System.Net.IPAddress.Loopback).ToString()))
                {
                    if (lease is null)
                    {
                        await RefuseAsync(http, StatusCodes.Status503ServiceUnavailable, "too_many_connections").ConfigureAwait(false);
                        return;
                    }

                    await next(http).ConfigureAwait(false);
                }

                return;
            default:
                break;
        }

        await next(http).ConfigureAwait(false);
    }

    private static async Task RefuseAsync(HttpContext http, int status, string code)
    {
        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ErrorBody(code), http.RequestAborted).ConfigureAwait(false);
    }

    private static async Task RefuseStrictLocalAsync(HttpContext http)
    {
        if (!StrictLocalPage.Suits(http.Request))
        {
            await RefuseAsync(http, StatusCodes.Status403Forbidden, StrictLocal).ConfigureAwait(false);
            return;
        }

        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        if (!HttpMethods.IsHead(http.Request.Method))
        {
            await http.Response.WriteAsync(StrictLocalPage.Html, http.RequestAborted).ConfigureAwait(false);
        }
    }

    private static async Task RefusePlainHttpAsync(HttpContext http)
    {
        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync(PlainHttpPage, http.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>D-8: behind the managed listener or a trusted proxy the forwarded scheme is the one the browser used (so the cookie is Secure and redirects are https).</summary>
    private static void ApplyEffectiveScheme(HttpContext http, IngressResult ingress)
    {
        if (ingress.Kind is not (IngressKind.Managed or IngressKind.TrustedProxy))
        {
            return;
        }

        string proto = http.Request.Headers["X-Forwarded-Proto"].ToString().Trim();
        if (string.Equals(proto, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.Equals(proto, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            http.Request.Scheme = proto.ToLowerInvariant();
        }
    }

    /// <summary>The single classification of the request (D-2): the listener it arrived on and the configured trusted proxies decide.</summary>
    private static async ValueTask<IngressResult> ClassifyAsync(HttpContext http, IIngressWhois whois, GatewayListener listener, GatewayOptions options)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, Microsoft.Extensions.Primitives.StringValues value) in http.Request.Headers)
        {
            headers[name] = value.ToString();
        }

        List<System.Net.IPAddress> trusted = [];
        foreach (string text in options.TrustedProxies)
        {
            if (System.Net.IPAddress.TryParse(text, out System.Net.IPAddress? address))
            {
                trusted.Add(address);
            }
        }

        ProxyAttribution? attribution = options.Auth.Mode == GatewayAuthMode.TrustedProxy ? new ProxyAttribution(options.TrustedProxy.UserHeader ?? string.Empty) : null;
        IngressRequest request = new(listener.KindOf(http.Connection), http.Connection.RemoteIpAddress, headers);
        return await IngressClassifier.ClassifyAsync(request, trusted, whois, attribution, http.RequestAborted).ConfigureAwait(false);
    }

    private const string PlainHttpPage = """
        <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Plain HTTP is not allowed here</title></head>
        <body><h1>Plain HTTP is not allowed for this address</h1>
        <p>Huddle serves plain HTTP only on this computer, on private network addresses and on .local or .ts.net names. Use https, or ask the administrator to list this host name in Team:Gateway:ExtraHosts.</p></body></html>
        """;

    private sealed record ErrorBody(string Code);
}
