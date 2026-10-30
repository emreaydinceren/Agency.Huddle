namespace Agency.Huddle.App.Gateway;

/// <summary>The <see cref="HttpContext.Items"/> the gate fills and later middleware, the authentication handler and endpoints read (C-1, C-3).</summary>
internal static class GatewayHttpItems
{
    private const string IngressKey = "huddle:gateway:ingress";
    private const string PublicKey = "huddle:gateway:public";

    /// <summary>Records how the request arrived.</summary>
    /// <param name="http">The request.</param>
    /// <param name="ingress">The classifier's verdict.</param>
    public static void SetIngress(HttpContext http, IngressResult ingress)
    {
        ArgumentNullException.ThrowIfNull(http);

        http.Items[IngressKey] = ingress;
    }

    /// <summary>The classifier's verdict for the request, or <see langword="null"/> when the gate has not run.</summary>
    /// <param name="http">The request.</param>
    public static IngressResult? GetIngress(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return http.Items.TryGetValue(IngressKey, out object? value) ? value as IngressResult : null;
    }

    /// <summary>Marks a request whose route needs no session (the connect page, <c>/health</c>, <c>/_desktop/*</c>).</summary>
    /// <param name="http">The request.</param>
    public static void MarkPublic(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        http.Items[PublicKey] = true;
    }

    /// <summary>Whether the gate marked the route as needing no session.</summary>
    /// <param name="http">The request.</param>
    public static bool IsPublic(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return http.Items.ContainsKey(PublicKey);
    }
}
