namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The first gateway middleware (Spec 6.8, C-1, C-3): classifies the ingress (403 <c>proxy_attribution_required</c> when it is rejected), stores it in
/// <see cref="GatewayHttpItems"/>, applies the origin check, lets <c>/health</c> and <c>/_desktop/*</c> through for loopback only, and counts the
/// unauthenticated connect routes against the pre-auth limiter (503 over the cap). Authentication and the session requirement run after it.
/// </summary>
/// <param name="next">The next middleware.</param>
internal sealed class GatewayGate(RequestDelegate next)
{
    private const string OriginNotAllowed = "origin_not_allowed";
    private const string LoopbackOnly = "loopback_only";

    /// <summary>Runs the gate for one request.</summary>
    /// <param name="http">The request.</param>
    /// <param name="whois">The Tailscale identity lookup.</param>
    /// <param name="limiter">The pre-auth connection limiter.</param>
    public async Task InvokeAsync(HttpContext http, IIngressWhois whois, PreAuthConnectionLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(http);

        IngressResult ingress = await ClassifyAsync(http, whois).ConfigureAwait(false);
        if (ingress.Kind == IngressKind.Rejected)
        {
            await RefuseAsync(http, StatusCodes.Status403Forbidden, ingress.Error ?? IngressClassifier.ProxyAttributionRequired).ConfigureAwait(false);
            return;
        }

        GatewayHttpItems.SetIngress(http, ingress);
        if (!GatewayOrigin.IsAllowed(http.Request, ingress))
        {
            await RefuseAsync(http, StatusCodes.Status403Forbidden, OriginNotAllowed).ConfigureAwait(false);
            return;
        }

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

    private static async ValueTask<IngressResult> ClassifyAsync(HttpContext http, IIngressWhois whois)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, Microsoft.Extensions.Primitives.StringValues value) in http.Request.Headers)
        {
            headers[name] = value.ToString();
        }

        IngressRequest request = new(IngressListenerKind.App, http.Connection.RemoteIpAddress, headers);
        return await IngressClassifier.ClassifyAsync(request, [], whois, http.RequestAborted).ConfigureAwait(false);
    }

    private sealed record ErrorBody(string Code);
}
