namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The origin check of Spec 8.7, the subset Stage C1 pins: no <c>Origin</c> passes; an unparsable one (<c>null</c>) fails; a loopback origin passes
/// only on a loopback ingress; any other origin must be the request's own host and port. The full table (allowed origins, Tailscale names) is Stage D.
/// </summary>
internal static class GatewayOrigin
{
    /// <summary>Whether the request's <c>Origin</c> is acceptable.</summary>
    /// <param name="request">The request.</param>
    /// <param name="ingress">How it arrived.</param>
    public static bool IsAllowed(HttpRequest request, IngressResult ingress)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ingress);

        string origin = request.Headers.Origin.ToString();
        if (origin.Length == 0)
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (ingress.Kind == IngressKind.Loopback && ConnectEndpoints.IsLoopbackName(uri.Host))
        {
            return true;
        }

        bool samePort = uri.IsDefaultPort ? request.Host.Port is null or 80 or 443 : request.Host.Port == uri.Port;
        return samePort && string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
    }
}
