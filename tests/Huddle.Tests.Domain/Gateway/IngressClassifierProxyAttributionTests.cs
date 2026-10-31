using System.Net;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Trusted-proxy attribution in the classifier (Spec 6.10, correction D-12): a trusted-proxy source in <c>trusted-proxy</c> mode needs a single non-loopback <c>X-Forwarded-For</c> and the user header.</summary>
public sealed class IngressClassifierProxyAttributionTests
{
    private static readonly IPAddress Proxy = IPAddress.Parse("203.0.113.5");
    private static readonly ProxyAttribution Attribution = new("X-Auth-User");

    private static Dictionary<string, string> Headers(params (string Name, string Value)[] pairs)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            headers[name] = value;
        }

        return headers;
    }

    private static ValueTask<IngressResult> Classify(IPAddress? socket, Dictionary<string, string> headers, ProxyAttribution? attribution, params IPAddress[] trusted) =>
        IngressClassifier.ClassifyAsync(
            new IngressRequest(IngressListenerKind.App, socket, headers),
            trusted,
            new FakeIngressWhois(),
            attribution,
            TestContext.Current.CancellationToken);

    /// <summary>With one non-loopback forwarded address and the user header, the request is TrustedProxy, its client the forwarded address and its user the header value.</summary>
    [Fact]
    public async Task TrustedProxy_FullAttribution_CarriesClientAndProxyUser()
    {
        IngressResult result = await Classify(Proxy, Headers(("X-Forwarded-For", "198.51.100.7"), ("x-auth-user", "ada@example.com")), Attribution, Proxy);

        Assert.Equal(IngressKind.TrustedProxy, result.Kind);
        Assert.Equal(IPAddress.Parse("198.51.100.7"), result.Client);
        Assert.Equal("ada@example.com", result.ProxyUser);
    }

    /// <summary>A missing, loopback or multi-address <c>X-Forwarded-For</c> from a trusted proxy is rejected with <c>proxy_attribution_required</c>.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("127.0.0.1")]
    [InlineData("198.51.100.7, 198.51.100.8")]
    [InlineData("not-an-address")]
    public async Task TrustedProxy_BadForwardedFor_IsRejected(string? forwarded)
    {
        Dictionary<string, string> headers = Headers(("X-Auth-User", "ada@example.com"));
        if (forwarded is not null)
        {
            headers["X-Forwarded-For"] = forwarded;
        }

        IngressResult result = await Classify(Proxy, headers, Attribution, Proxy);

        Assert.Equal(IngressKind.Rejected, result.Kind);
        Assert.Equal(IngressClassifier.ProxyAttributionRequired, result.Error);
    }

    /// <summary>A missing or empty user header from a trusted proxy is rejected with <c>proxy_attribution_required</c>.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TrustedProxy_NoUserHeader_IsRejected(string? user)
    {
        Dictionary<string, string> headers = Headers(("X-Forwarded-For", "198.51.100.7"));
        if (user is not null)
        {
            headers["X-Auth-User"] = user;
        }

        IngressResult result = await Classify(Proxy, headers, Attribution, Proxy);

        Assert.Equal(IngressKind.Rejected, result.Kind);
        Assert.Equal(IngressClassifier.ProxyAttributionRequired, result.Error);
    }

    /// <summary>A same-host proxy listed as <c>127.0.0.1</c> must still supply the attribution; with it, it is TrustedProxy.</summary>
    [Fact]
    public async Task TrustedProxy_LoopbackProxy_NeedsAttributionToo()
    {
        IngressResult bare = await Classify(IPAddress.Loopback, Headers(("X-Forwarded-For", "198.51.100.7")), Attribution, IPAddress.Loopback);
        IngressResult full = await Classify(
            IPAddress.Loopback,
            Headers(("X-Forwarded-For", "198.51.100.7"), ("X-Auth-User", "ada@example.com")),
            Attribution,
            IPAddress.Loopback);

        Assert.Equal(IngressKind.Rejected, bare.Kind);
        Assert.Equal(IngressKind.TrustedProxy, full.Kind);
        Assert.Equal("ada@example.com", full.ProxyUser);
    }

    /// <summary>Without attribution (any other auth mode) the socket fallback stays: a trusted proxy with no forwarded address is TrustedProxy with the socket as client and no user.</summary>
    [Fact]
    public async Task TrustedProxy_NoAttributionRequested_KeepsTheSocketFallback()
    {
        IngressResult result = await Classify(Proxy, Headers(), null, Proxy);

        Assert.Equal(IngressKind.TrustedProxy, result.Kind);
        Assert.Equal(Proxy, result.Client);
        Assert.Null(result.ProxyUser);
    }

    /// <summary>A request from a source that is not a trusted proxy is not made a proxy request by the attribution rule: it stays Direct.</summary>
    [Fact]
    public async Task Direct_NotATrustedProxy_StaysDirect()
    {
        IngressResult result = await Classify(IPAddress.Parse("192.168.1.50"), Headers(), Attribution, Proxy);

        Assert.Equal(IngressKind.Direct, result.Kind);
    }
}
