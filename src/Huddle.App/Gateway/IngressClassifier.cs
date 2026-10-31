using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>Decides how a request reached Huddle (Spec 8.6).</summary>
internal static class IngressClassifier
{
    /// <summary>The 403 error code for missing or invalid forwarding attribution.</summary>
    public const string ProxyAttributionRequired = "proxy_attribution_required";

    private const string ForwardedFor = "X-Forwarded-For";
    private const string ForwardedProto = "X-Forwarded-Proto";
    private const string ForwardedHost = "X-Forwarded-Host";
    private const string TailscaleUserPrefix = "Tailscale-User-";

    /// <summary>Classifies <paramref name="request"/>.</summary>
    /// <param name="request">The request facts.</param>
    /// <param name="trustedProxies">The configured trusted proxy addresses.</param>
    /// <param name="whois">The Tailscale identity lookup.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public static ValueTask<IngressResult> ClassifyAsync(
        IngressRequest request,
        IReadOnlyCollection<IPAddress> trustedProxies,
        IIngressWhois whois,
        CancellationToken cancellationToken) => ClassifyAsync(request, trustedProxies, whois, null, cancellationToken);

    /// <summary>
    /// Classifies with trusted-proxy attribution (D-12): a trusted-proxy source needs a single non-loopback <c>X-Forwarded-For</c> and a non-empty
    /// <paramref name="attribution"/> user header, else <c>Rejected(proxy_attribution_required)</c>; the result carries <see cref="IngressResult.ProxyUser"/>.
    /// With <paramref name="attribution"/> <see langword="null"/> it behaves as the four-argument overload.
    /// </summary>
    /// <param name="request">The request facts.</param>
    /// <param name="trustedProxies">The configured trusted proxy addresses.</param>
    /// <param name="whois">The Tailscale identity lookup.</param>
    /// <param name="attribution">Set in <c>trusted-proxy</c> mode only.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public static async ValueTask<IngressResult> ClassifyAsync(
        IngressRequest request,
        IReadOnlyCollection<IPAddress> trustedProxies,
        IIngressWhois whois,
        ProxyAttribution? attribution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(trustedProxies);
        ArgumentNullException.ThrowIfNull(whois);

        if (request.Listener == IngressListenerKind.Managed)
        {
            return await ClassifyManagedAsync(request, whois, cancellationToken).ConfigureAwait(false);
        }

        IPAddress? socket = request.SocketAddress is null ? null : Normalise(request.SocketAddress);
        bool loopback = socket is null || IPAddress.IsLoopback(socket);
        bool trusted = socket is not null
            ? trustedProxies.Any(proxy => Normalise(proxy).Equals(socket))
            : trustedProxies.Any(IPAddress.IsLoopback);

        if (loopback && !HasForwardingHeader(request.Headers))
        {
            return new IngressResult(IngressKind.Loopback, socket ?? IPAddress.Loopback, null, false, null);
        }

        if (loopback && !trusted)
        {
            return Rejected();
        }

        if (trusted)
        {
            return ClassifyTrustedProxy(request, socket, attribution);
        }

        return new IngressResult(IngressKind.Direct, socket, null, false, null);
    }

    private static IngressResult ClassifyTrustedProxy(IngressRequest request, IPAddress? socket, ProxyAttribution? attribution)
    {
        if (!TryGetForwardedClient(request.Headers, out IPAddress? forwardedClient))
        {
            return attribution is null
                ? new IngressResult(IngressKind.TrustedProxy, socket ?? IPAddress.Loopback, null, false, null)
                : Rejected();
        }

        if (attribution is null)
        {
            return new IngressResult(IngressKind.TrustedProxy, forwardedClient, null, false, null);
        }

        string? user = Header(request.Headers, attribution.UserHeader)?.Trim();
        if (IPAddress.IsLoopback(forwardedClient) || string.IsNullOrEmpty(user))
        {
            return Rejected();
        }

        return new IngressResult(IngressKind.TrustedProxy, forwardedClient, null, false, null) { ProxyUser = user };
    }

    private static async ValueTask<IngressResult> ClassifyManagedAsync(IngressRequest request, IIngressWhois whois, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> headers = request.Headers;
        if (!TryGetForwardedClient(headers, out IPAddress? client)
            || IPAddress.IsLoopback(client)
            || string.IsNullOrWhiteSpace(Header(headers, ForwardedProto))
            || string.IsNullOrWhiteSpace(Header(headers, ForwardedHost)))
        {
            return Rejected();
        }

        bool funnel = string.Equals(Header(headers, "Tailscale-Funnel-Request"), "?1", StringComparison.Ordinal);
        string? login = Header(headers, "Tailscale-User-Login");
        string? identity = null;
        if (!funnel && !string.IsNullOrEmpty(login))
        {
            string? known = await whois.LookupLoginAsync(client, cancellationToken).ConfigureAwait(false);
            if (known is not null && string.Equals(known, login, StringComparison.OrdinalIgnoreCase))
            {
                identity = login;
            }
        }

        return new IngressResult(IngressKind.Managed, client, identity, funnel, null);
    }

    private static IngressResult Rejected() => new(IngressKind.Rejected, null, null, false, ProxyAttributionRequired);

    private static IPAddress Normalise(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static string? Header(IReadOnlyDictionary<string, string> headers, string name)
    {
        if (headers.TryGetValue(name, out string? value))
        {
            return value;
        }

        foreach ((string key, string candidate) in headers)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HasForwardingHeader(IReadOnlyDictionary<string, string> headers)
    {
        foreach (string key in headers.Keys)
        {
            if (string.Equals(key, ForwardedFor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Forwarded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "X-Real-IP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, ForwardedProto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, ForwardedHost, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(TailscaleUserPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads <c>X-Forwarded-For</c> as exactly one address; a list or a non-address is not a client.</summary>
    private static bool TryGetForwardedClient(IReadOnlyDictionary<string, string> headers, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IPAddress? client)
    {
        client = null;
        string? value = Header(headers, ForwardedFor);
        if (string.IsNullOrWhiteSpace(value) || value.Contains(','))
        {
            return false;
        }

        if (!IPAddress.TryParse(value.Trim(), out IPAddress? parsed))
        {
            return false;
        }

        client = Normalise(parsed);
        return true;
    }
}
