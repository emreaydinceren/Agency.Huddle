namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The origin check of Spec 8.7 with corrections D-1 and D-9: an <c>Origin</c> is required only on a non-safe request to <c>/_blazor</c> or <c>/auth/*</c>
/// and on a WebSocket upgrade; when present it must pass the rules in order (public origin, strict parse, allowed origins, host-header fallback,
/// loopback ingress with a loopback origin, default same-origin).
/// </summary>
internal static class GatewayOrigin
{
    private const int HttpPort = 80;
    private const int HttpsPort = 443;

    /// <summary>The full origin check of Spec 8.7 with corrections D-1 and D-9.</summary>
    /// <param name="request">The request (method, path, <c>Host</c>, scheme and headers).</param>
    /// <param name="ingress">How it arrived.</param>
    /// <param name="options">The gateway options (<c>PublicOrigin</c>, <c>AllowedOrigins</c>, the host-header fallback flag).</param>
    public static bool IsAllowed(HttpRequest request, IngressResult ingress, GatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(options);

        string origin = request.Headers.Origin.ToString();
        if (origin.Length == 0)
        {
            return !MustCarryOrigin(request);
        }

        if (!TryParseStrict(origin, out Uri? uri))
        {
            return false;
        }

        string normalised = Normalise(uri);
        bool remote = ingress.Kind is IngressKind.Managed or IngressKind.TrustedProxy;
        if (remote && !string.IsNullOrWhiteSpace(options.PublicOrigin))
        {
            return string.Equals(normalised, NormaliseConfigured(options.PublicOrigin), StringComparison.Ordinal);
        }

        if (MatchesAllowedList(normalised, options))
        {
            return true;
        }

        if (options.DangerouslyAllowHostHeaderOriginFallback && string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ingress.Kind == IngressKind.Loopback && ConnectEndpoints.IsLoopbackName(uri.Host))
        {
            return true;
        }

        return IsDefaultSameOrigin(request, uri, remote);
    }

    /// <summary>D-1: a WebSocket upgrade, or a non-GET/HEAD/OPTIONS request to <c>/_blazor</c> or <c>/auth/*</c>, must carry an <c>Origin</c>.</summary>
    private static bool MustCarryOrigin(HttpRequest request)
    {
        if (IsWebSocketUpgrade(request))
        {
            return true;
        }

        bool safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        return !safe && (request.Path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase) || request.Path.StartsWithSegments("/auth", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWebSocketUpgrade(HttpRequest request) =>
        request.Headers.Connection.ToString().Contains("upgrade", StringComparison.OrdinalIgnoreCase)
        && string.Equals(request.Headers.Upgrade.ToString().Trim(), "websocket", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rule 2 (D-9): an absolute http(s) origin with no path beyond <c>/</c>, no query, fragment or credentials.</summary>
    private static bool TryParseStrict(string origin, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? uri)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && uri.UserInfo.Length == 0)
        {
            return true;
        }

        uri = null;
        return false;
    }

    private static string Normalise(Uri uri) => $"{uri.Scheme}://{uri.Authority}".ToLowerInvariant();

    private static string NormaliseConfigured(string configured)
    {
        string trimmed = configured.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ? Normalise(uri) : trimmed.TrimEnd('/').ToLowerInvariant();
    }

    /// <summary>Rule 3: <c>AllowedOrigins</c> (else <c>PublicOrigin</c>), an exact lower-cased match or <c>*</c>.</summary>
    private static bool MatchesAllowedList(string normalised, GatewayOptions options)
    {
        List<string> allowed = [.. options.AllowedOrigins];
        if (allowed.Count == 0 && !string.IsNullOrWhiteSpace(options.PublicOrigin))
        {
            allowed.Add(options.PublicOrigin);
        }

        foreach (string entry in allowed)
        {
            if (string.Equals(entry.Trim(), "*", StringComparison.Ordinal) || string.Equals(NormaliseConfigured(entry), normalised, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rule 5 (D-9): the origin's host and port equal the request's (default port by scheme) and the host is local or private. Behind a remote ingress the
    /// request's own scheme is the plain loopback hop to the proxy, so a Host without a port matches an origin on its scheme's default port.
    /// </summary>
    private static bool IsDefaultSameOrigin(HttpRequest request, Uri uri, bool remote)
    {
        if (!string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase) || !GatewayPlainHttp.IsLocalOrPrivateHost(request.Host.Host))
        {
            return false;
        }

        if (request.Host.Port is { } port)
        {
            return port == uri.Port;
        }

        if (remote)
        {
            return uri.IsDefaultPort;
        }

        return uri.Port == (string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? HttpsPort : HttpPort);
    }
}
