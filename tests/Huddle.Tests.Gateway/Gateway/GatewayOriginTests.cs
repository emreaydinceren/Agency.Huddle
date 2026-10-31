using System.Net;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Http;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The origin check of Spec 8.7, row by row, with corrections D-1 (Origin required only on non-GET and upgrades) and D-9 (host and port, ingress loopback).</summary>
public sealed class GatewayOriginTests
{
    private static readonly IngressResult Loopback = new(IngressKind.Loopback, IPAddress.Loopback, null, false, null);
    private static readonly IngressResult Managed = new(IngressKind.Managed, IPAddress.Parse("100.64.0.7"), null, false, null);
    private static readonly IngressResult TrustedProxy = new(IngressKind.TrustedProxy, IPAddress.Parse("198.51.100.7"), null, false, null);
    private static readonly IngressResult Direct = new(IngressKind.Direct, IPAddress.Parse("192.168.1.50"), null, false, null);

    private static bool Allowed(
        string method,
        string path,
        string host,
        string? origin,
        IngressResult ingress,
        GatewayOptions? options = null,
        string scheme = "http",
        bool webSocket = false)
    {
        DefaultHttpContext http = new();
        http.Request.Method = method;
        http.Request.Path = path;
        http.Request.Host = new HostString(host);
        http.Request.Scheme = scheme;
        if (origin is not null)
        {
            http.Request.Headers.Origin = origin;
        }

        if (webSocket)
        {
            http.Request.Headers.Connection = "Upgrade";
            http.Request.Headers.Upgrade = "websocket";
        }

        return GatewayOrigin.IsAllowed(http.Request, ingress, options ?? new GatewayOptions());
    }

    /// <summary>A same-origin GET carries no <c>Origin</c> in a browser, so an absent one passes on GET, HEAD and OPTIONS, including <c>/auth</c> and <c>/_blazor</c> (D-1).</summary>
    [Theory]
    [InlineData("GET", "/auth")]
    [InlineData("GET", "/auth/status")]
    [InlineData("GET", "/_blazor/negotiate")]
    [InlineData("HEAD", "/auth")]
    [InlineData("OPTIONS", "/_blazor")]
    [InlineData("GET", "/")]
    public void IsAllowed_SafeMethodWithoutOrigin_Passes(string method, string path)
    {
        Assert.True(Allowed(method, path, "localhost:5101", null, Loopback));
    }

    /// <summary>A non-GET to <c>/_blazor</c> or <c>/auth/*</c> must carry <c>Origin</c> (D-1).</summary>
    [Theory]
    [InlineData("POST", "/_blazor/negotiate")]
    [InlineData("POST", "/auth/connect")]
    [InlineData("PUT", "/_blazor")]
    [InlineData("DELETE", "/auth/session")]
    public void IsAllowed_UnsafeMethodOnBlazorOrAuthWithoutOrigin_Refused(string method, string path)
    {
        Assert.False(Allowed(method, path, "localhost:5101", null, Loopback));
    }

    /// <summary>A POST to another route without <c>Origin</c> passes the origin check (the antiforgery and session checks own it).</summary>
    [Fact]
    public void IsAllowed_UnsafeMethodOnOtherRouteWithoutOrigin_Passes()
    {
        Assert.True(Allowed("POST", "/api/rooms", "localhost:5101", null, Loopback));
    }

    /// <summary>A WebSocket upgrade without <c>Origin</c> is refused even on GET; with a same-origin one it passes (D-1).</summary>
    [Fact]
    public void IsAllowed_WebSocketUpgrade_NeedsAnOrigin()
    {
        Assert.False(Allowed("GET", "/_blazor", "localhost:5101", null, Loopback, webSocket: true));
        Assert.True(Allowed("GET", "/_blazor", "localhost:5101", "http://localhost:5101", Loopback, webSocket: true));
    }

    /// <summary>Rule 2: <c>null</c>, a path, a query, a fragment or credentials are refused; a bare origin and one with a lone trailing slash pass.</summary>
    [Theory]
    [InlineData("null", false)]
    [InlineData("http://localhost:5101/path", false)]
    [InlineData("http://localhost:5101/?x=1", false)]
    [InlineData("http://localhost:5101/#frag", false)]
    [InlineData("http://user:pw@localhost:5101", false)]
    [InlineData("not a url", false)]
    [InlineData("http://localhost:5101", true)]
    [InlineData("http://localhost:5101/", true)]
    public void IsAllowed_OriginParsedStrictly(string origin, bool expected)
    {
        Assert.Equal(expected, Allowed("POST", "/auth/connect", "localhost:5101", origin, Loopback));
    }

    /// <summary>Rule 1: behind remote ingress with <c>PublicOrigin</c> set, only an exact match passes, even when the Origin equals the Host.</summary>
    [Theory]
    [InlineData("managed")]
    [InlineData("trusted-proxy")]
    public void IsAllowed_RemoteIngressWithPublicOrigin_OnlyTheExactOriginPasses(string kind)
    {
        IngressResult ingress = kind == "managed" ? Managed : TrustedProxy;
        GatewayOptions options = new() { PublicOrigin = "https://desk.tail1.ts.net" };

        Assert.True(Allowed("POST", "/auth/connect", "desk.tail1.ts.net", "https://desk.tail1.ts.net", ingress, options, "https"));
        Assert.False(Allowed("POST", "/auth/connect", "other.tail1.ts.net", "https://other.tail1.ts.net", ingress, options, "https"));
    }

    /// <summary>Rule 3: an <c>AllowedOrigins</c> entry (lower-cased) passes, <c>*</c> passes anything, and unset defaults to <c>PublicOrigin</c>.</summary>
    [Fact]
    public void IsAllowed_AllowedOrigins_ExactStarAndPublicOriginDefault()
    {
        GatewayOptions listed = new() { AllowedOrigins = { "https://A.Example" } };
        GatewayOptions star = new() { AllowedOrigins = { "*" } };
        GatewayOptions publicOnly = new() { PublicOrigin = "https://desk.example.com" };

        Assert.True(Allowed("POST", "/auth/connect", "192.168.1.20:5101", "https://a.EXAMPLE", Direct, listed));
        Assert.False(Allowed("POST", "/auth/connect", "192.168.1.20:5101", "https://b.example", Direct, listed));
        Assert.True(Allowed("POST", "/auth/connect", "192.168.1.20:5101", "https://anything.example", Direct, star));
        Assert.True(Allowed("POST", "/auth/connect", "192.168.1.20:5101", "https://desk.example.com", Direct, publicOnly));
        Assert.False(Allowed("POST", "/auth/connect", "192.168.1.20:5101", "https://evil.example", Direct, publicOnly));
    }

    /// <summary>Rule 4: with the host-header fallback set, an Origin whose host equals the Host passes even for a bare machine name; unset, it does not.</summary>
    [Fact]
    public void IsAllowed_HostHeaderFallback_PassesABareNameOnlyWhenSet()
    {
        GatewayOptions on = new() { DangerouslyAllowHostHeaderOriginFallback = true };

        Assert.True(Allowed("POST", "/auth/connect", "desk:5101", "http://desk:5101", Direct, on));
        Assert.False(Allowed("POST", "/auth/connect", "desk:5101", "http://desk:5101", Direct));
    }

    /// <summary>Rule 5: the Origin's host and port equal the Host's (default port by scheme) and the host is loopback, private, <c>.local</c> or <c>.ts.net</c>.</summary>
    [Theory]
    [InlineData("192.168.1.20:5101", "http", "http://192.168.1.20:5101", true)]
    [InlineData("192.168.1.20:5101", "http", "http://192.168.1.20:5102", false)]
    [InlineData("192.168.1.20:5101", "http", "http://192.168.1.20", false)]
    [InlineData("192.168.1.20", "http", "http://192.168.1.20", true)]
    [InlineData("192.168.1.20:5101", "http", "http://192.168.1.21:5101", false)]
    [InlineData("desk.tail1.ts.net", "https", "https://desk.tail1.ts.net", true)]
    [InlineData("desk.tail1.ts.net", "https", "https://desk.tail1.ts.net:443", true)]
    [InlineData("desk.tail1.ts.net", "https", "http://desk.tail1.ts.net", false)]
    [InlineData("desk.tail1.ts.net:5101", "https", "https://desk.tail1.ts.net:5101", true)]
    [InlineData("desk.local:5101", "http", "http://desk.local:5101", true)]
    [InlineData("10.0.0.5:5101", "http", "http://10.0.0.5:5101", true)]
    [InlineData("172.16.0.9:5101", "http", "http://172.16.0.9:5101", true)]
    [InlineData("100.101.102.103:5101", "http", "http://100.101.102.103:5101", true)]
    [InlineData("[fd00::5]:5101", "http", "http://[fd00::5]:5101", true)]
    [InlineData("desk:5101", "http", "http://desk:5101", false)]
    [InlineData("example.com", "https", "https://example.com", false)]
    [InlineData("8.8.8.8:5101", "http", "http://8.8.8.8:5101", false)]
    [InlineData("xts.net", "https", "https://xts.net", false)]
    public void IsAllowed_DefaultSameOrigin_HostPortAndHostClass(string host, string scheme, string origin, bool expected)
    {
        Assert.Equal(expected, Allowed("POST", "/auth/connect", host, origin, Direct, null, scheme));
    }

    /// <summary>Rule 6 (D-9): a loopback ingress with a loopback Origin passes whatever the Host or port is.</summary>
    [Fact]
    public void IsAllowed_LoopbackIngressWithLoopbackOrigin_Passes()
    {
        Assert.True(Allowed("POST", "/auth/connect", "127.0.0.1:5101", "http://localhost:5101", Loopback));
        Assert.True(Allowed("POST", "/auth/connect", "localhost:5101", "http://[::1]:5999", Loopback));
        Assert.False(Allowed("POST", "/auth/connect", "localhost:5101", "http://evil.example", Loopback));
    }

    /// <summary>Rule 6 means the ingress is Loopback, not that the socket is: a managed-listener request (socket loopback) or a LAN client with a localhost Origin is refused.</summary>
    [Fact]
    public void IsAllowed_NonLoopbackIngressWithLoopbackOrigin_Refused()
    {
        Assert.False(Allowed("POST", "/auth/connect", "desk.tail1.ts.net", "http://localhost:5101", Managed, null, "https"));
        Assert.False(Allowed("POST", "/auth/connect", "192.168.1.20:5101", "http://localhost:5101", Direct));
    }
}
