using System.Net;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 8.6: every branch of the ingress classifier, over a small input record and a whois seam.</summary>
public sealed class IngressClassifierTests
{
    private static readonly IPAddress Phone = IPAddress.Parse("100.64.1.2");
    private static readonly IPAddress LanHost = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress ProxyHost = IPAddress.Parse("10.0.0.5");

    /// <summary>Builds a case-insensitive header map.</summary>
    private static Dictionary<string, string> Headers(params (string Name, string Value)[] pairs)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            headers[name] = value;
        }

        return headers;
    }

    /// <summary>The three attribution headers a managed-listener request must carry.</summary>
    private static Dictionary<string, string> ManagedHeaders(params (string Name, string Value)[] extra)
    {
        Dictionary<string, string> headers = Headers(
            ("X-Forwarded-For", "100.64.1.2"),
            ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "desktop-a.tail1234.ts.net:5101"));
        foreach ((string name, string value) in extra)
        {
            headers[name] = value;
        }

        return headers;
    }

    private static ValueTask<IngressResult> Classify(
        IngressListenerKind listener,
        IPAddress? socket,
        Dictionary<string, string> headers,
        FakeIngressWhois? whois = null,
        params IPAddress[] trusted) =>
        IngressClassifier.ClassifyAsync(new IngressRequest(listener, socket, headers), trusted, whois ?? new FakeIngressWhois(), TestContext.Current.CancellationToken);

    /// <summary>A managed-listener request with all three attribution headers is Managed, and its client is the forwarded address.</summary>
    [Fact]
    public async Task Managed_FullHeaders_IsManagedWithForwardedClient()
    {
        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, ManagedHeaders());

        Assert.Equal(IngressKind.Managed, result.Kind);
        Assert.Equal(Phone, result.Client);
        Assert.Null(result.TailscaleLogin);
        Assert.False(result.IsFunnel);
        Assert.Null(result.Error);
    }

    /// <summary>A managed-listener request missing any one of X-Forwarded-For, -Proto or -Host is refused with 403 proxy_attribution_required.</summary>
    [Theory]
    [InlineData("X-Forwarded-For")]
    [InlineData("X-Forwarded-Proto")]
    [InlineData("X-Forwarded-Host")]
    public async Task Managed_MissingAttributionHeader_IsRejected(string missing)
    {
        Dictionary<string, string> headers = ManagedHeaders();
        headers.Remove(missing);

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers);

        Assert.Equal(IngressKind.Rejected, result.Kind);
        Assert.Equal("proxy_attribution_required", result.Error);
        Assert.Null(result.Client);
    }

    /// <summary>The forwarded address must be exactly one non-loopback address: a loopback, a list and a non-address are all refused.</summary>
    [Theory]
    [InlineData("loopback-v4", "127.0.0.1")]
    [InlineData("loopback-v6", "::1")]
    [InlineData("a-list", "100.64.1.2, 100.64.1.3")]
    [InlineData("not-an-address", "not-an-address")]
    public async Task Managed_BadForwardedFor_IsRejected(string label, string forwardedFor)
    {
        Assert.NotEmpty(label);
        Dictionary<string, string> headers = ManagedHeaders(("X-Forwarded-For", forwardedFor));

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers);

        Assert.Equal(IngressKind.Rejected, result.Kind);
        Assert.Equal("proxy_attribution_required", result.Error);
    }

    /// <summary><c>Tailscale-Funnel-Request: ?1</c> marks Funnel traffic, which never has an identity and never asks whois.</summary>
    [Fact]
    public async Task Managed_FunnelHeader_IsFunnelWithNoIdentity()
    {
        FakeIngressWhois whois = new FakeIngressWhois().With(Phone, "ada@example.com");
        Dictionary<string, string> headers = ManagedHeaders(("Tailscale-Funnel-Request", "?1"), ("Tailscale-User-Login", "ada@example.com"));

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers, whois);

        Assert.Equal(IngressKind.Managed, result.Kind);
        Assert.True(result.IsFunnel);
        Assert.Null(result.TailscaleLogin);
        Assert.Empty(whois.Lookups);
    }

    /// <summary>Only the exact value <c>?1</c> means Funnel: <c>?0</c> is an ordinary Serve request.</summary>
    [Fact]
    public async Task Managed_FunnelHeaderWithOtherValue_IsNotFunnel()
    {
        FakeIngressWhois whois = new FakeIngressWhois().With(Phone, "ada@example.com");
        Dictionary<string, string> headers = ManagedHeaders(("Tailscale-Funnel-Request", "?0"), ("Tailscale-User-Login", "ada@example.com"));

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers, whois);

        Assert.False(result.IsFunnel);
        Assert.Equal("ada@example.com", result.TailscaleLogin);
    }

    /// <summary>A Tailscale-User-Login that whois confirms for the forwarded address becomes the identity.</summary>
    [Fact]
    public async Task Managed_LoginConfirmedByWhois_HasIdentity()
    {
        FakeIngressWhois whois = new FakeIngressWhois().With(Phone, "ada@example.com");
        Dictionary<string, string> headers = ManagedHeaders(("Tailscale-User-Login", "ada@example.com"));

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers, whois);

        Assert.Equal(IngressKind.Managed, result.Kind);
        Assert.Equal("ada@example.com", result.TailscaleLogin);
        Assert.Equal([Phone], whois.Lookups);
    }

    /// <summary>A Tailscale-User-Login that whois contradicts gives no identity, but the request is still Managed.</summary>
    [Fact]
    public async Task Managed_LoginMismatchesWhois_HasNoIdentity()
    {
        FakeIngressWhois whois = new FakeIngressWhois().With(Phone, "bob@example.com");
        Dictionary<string, string> headers = ManagedHeaders(("Tailscale-User-Login", "ada@example.com"));

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers, whois);

        Assert.Equal(IngressKind.Managed, result.Kind);
        Assert.Null(result.TailscaleLogin);
    }

    /// <summary>A login header for an address whois does not know gives no identity.</summary>
    [Fact]
    public async Task Managed_WhoisUnknown_HasNoIdentity()
    {
        Dictionary<string, string> headers = ManagedHeaders(("Tailscale-User-Login", "ada@example.com"));

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, headers);

        Assert.Null(result.TailscaleLogin);
    }

    /// <summary>With no Tailscale-User-Login header there is no identity and whois is not asked.</summary>
    [Fact]
    public async Task Managed_NoLoginHeader_DoesNotAskWhois()
    {
        FakeIngressWhois whois = new FakeIngressWhois().With(Phone, "ada@example.com");

        IngressResult result = await Classify(IngressListenerKind.Managed, IPAddress.Loopback, ManagedHeaders(), whois);

        Assert.Null(result.TailscaleLogin);
        Assert.Empty(whois.Lookups);
    }

    /// <summary>A loopback socket on the app listener with no forwarding headers is Loopback.</summary>
    [Theory]
    [InlineData("v4", "127.0.0.1")]
    [InlineData("v6", "::1")]
    public async Task App_LoopbackSocketNoHeaders_IsLoopback(string family, string address)
    {
        Assert.NotEmpty(family);
        IPAddress socket = IPAddress.Parse(address);

        IngressResult result = await Classify(IngressListenerKind.App, socket, Headers());

        Assert.Equal(IngressKind.Loopback, result.Kind);
        Assert.Equal(socket, result.Client);
        Assert.Null(result.Error);
    }

    /// <summary>An IPv4-mapped loopback address is normalised and is Loopback.</summary>
    [Fact]
    public async Task App_Ipv4MappedLoopback_IsLoopbackWithNormalisedClient()
    {
        IPAddress mapped = IPAddress.Parse("127.0.0.1").MapToIPv6();

        IngressResult result = await Classify(IngressListenerKind.App, mapped, Headers());

        Assert.Equal(IngressKind.Loopback, result.Kind);
        Assert.Equal(IPAddress.Loopback, result.Client);
    }

    /// <summary>No socket address (the in-memory test server, a named pipe, a Unix socket) is treated as loopback.</summary>
    [Fact]
    public async Task App_NullSocket_IsLoopback()
    {
        IngressResult result = await Classify(IngressListenerKind.App, null, Headers());

        Assert.Equal(IngressKind.Loopback, result.Kind);
        Assert.Equal(IPAddress.Loopback, result.Client);
    }

    /// <summary>A loopback socket carrying any forwarding header is a proxy that was not configured: refused with 403 proxy_attribution_required.</summary>
    [Theory]
    [InlineData("X-Forwarded-For")]
    [InlineData("Forwarded")]
    [InlineData("X-Real-IP")]
    [InlineData("Tailscale-User-Login")]
    [InlineData("Tailscale-User-Name")]
    public async Task App_LoopbackWithForwardingHeader_IsRejected(string header)
    {
        IngressResult result = await Classify(IngressListenerKind.App, IPAddress.Loopback, Headers((header, "198.51.100.7")));

        Assert.Equal(IngressKind.Rejected, result.Kind);
        Assert.Equal("proxy_attribution_required", result.Error);
    }

    /// <summary>The same rule holds for a null socket address.</summary>
    [Fact]
    public async Task App_NullSocketWithForwardedFor_IsRejected()
    {
        IngressResult result = await Classify(IngressListenerKind.App, null, Headers(("X-Forwarded-For", "198.51.100.7")));

        Assert.Equal(IngressKind.Rejected, result.Kind);
        Assert.Equal("proxy_attribution_required", result.Error);
    }

    /// <summary>A loopback socket that is in TrustedProxies may forward: the request is TrustedProxy and the client is the forwarded address.</summary>
    [Fact]
    public async Task App_LoopbackSocketInTrustedProxies_WithForwardedFor_IsTrustedProxy()
    {
        IngressResult result = await Classify(
            IngressListenerKind.App,
            IPAddress.Loopback,
            Headers(("X-Forwarded-For", "198.51.100.7")),
            null,
            IPAddress.Loopback);

        Assert.Equal(IngressKind.TrustedProxy, result.Kind);
        Assert.Equal(IPAddress.Parse("198.51.100.7"), result.Client);
    }

    /// <summary>A non-loopback socket in TrustedProxies is TrustedProxy even with no headers, and the client is then the socket.</summary>
    [Fact]
    public async Task App_NonLoopbackSocketInTrustedProxies_IsTrustedProxy()
    {
        IngressResult result = await Classify(IngressListenerKind.App, ProxyHost, Headers(), null, ProxyHost);

        Assert.Equal(IngressKind.TrustedProxy, result.Kind);
        Assert.Equal(ProxyHost, result.Client);
    }

    /// <summary>A trusted proxy's forwarded address is the client.</summary>
    [Fact]
    public async Task App_NonLoopbackTrustedProxy_WithForwardedFor_ClientIsForwarded()
    {
        IngressResult result = await Classify(
            IngressListenerKind.App,
            ProxyHost,
            Headers(("X-Forwarded-For", "198.51.100.7")),
            null,
            ProxyHost);

        Assert.Equal(IngressKind.TrustedProxy, result.Kind);
        Assert.Equal(IPAddress.Parse("198.51.100.7"), result.Client);
    }

    /// <summary>Any other non-loopback socket is Direct and its own address is the client.</summary>
    [Fact]
    public async Task App_UnlistedSocket_IsDirect()
    {
        IngressResult result = await Classify(IngressListenerKind.App, LanHost, Headers(), null, ProxyHost);

        Assert.Equal(IngressKind.Direct, result.Kind);
        Assert.Equal(LanHost, result.Client);
        Assert.Null(result.Error);
    }

    /// <summary>A direct client's forwarding headers are ignored: they cannot spoof the address, and are not an error.</summary>
    [Fact]
    public async Task App_DirectWithForwardedFor_IgnoresTheHeader()
    {
        IngressResult result = await Classify(IngressListenerKind.App, LanHost, Headers(("X-Forwarded-For", "198.51.100.7")));

        Assert.Equal(IngressKind.Direct, result.Kind);
        Assert.Equal(LanHost, result.Client);
    }

    /// <summary>An IPv4-mapped LAN address is normalised before it becomes the client.</summary>
    [Fact]
    public async Task App_Ipv4MappedDirect_ClientIsNormalised()
    {
        IngressResult result = await Classify(IngressListenerKind.App, LanHost.MapToIPv6(), Headers());

        Assert.Equal(IngressKind.Direct, result.Kind);
        Assert.Equal(LanHost, result.Client);
    }

    /// <summary>Header names are matched without regard to case.</summary>
    [Fact]
    public async Task App_LoopbackWithLowercaseForwardingHeader_IsRejected()
    {
        IngressResult result = await Classify(IngressListenerKind.App, IPAddress.Loopback, Headers(("x-forwarded-for", "198.51.100.7")));

        Assert.Equal(IngressKind.Rejected, result.Kind);
    }
}
