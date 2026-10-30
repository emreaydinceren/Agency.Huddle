using System.Net;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Review fixes R-5, R-6 and R-12: what counts as configured, nonce and challenge flooding, and the forwarding headers on loopback.</summary>
public sealed class GatewayReviewFixInfraTests
{
    private static readonly IPEndPoint[] LanEndpoint = [new IPEndPoint(IPAddress.Any, 5101)];
    private static readonly IPEndPoint[] LoopbackEndpoint = [new IPEndPoint(IPAddress.Loopback, 5101)];

    private static RemoteAccessState State(Action<GatewayOptions>? configure = null, IReadOnlyList<IPEndPoint>? endpoints = null)
    {
        GatewayOptions options = new();
        configure?.Invoke(options);
        return new RemoteAccessState(options, () => endpoints ?? []);
    }

    private static ValueTask<IngressResult> Classify(params (string Name, string Value)[] headers)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in headers)
        {
            map[name] = value;
        }

        return IngressClassifier.ClassifyAsync(new IngressRequest(IngressListenerKind.App, IPAddress.Loopback, map), [], new FakeIngressWhois(), CancellationToken.None);
    }

    /// <summary>R-5: nothing exposed means not configured.</summary>
    [Fact]
    public void IsConfigured_LoopbackOnly_IsFalse()
    {
        Assert.False(State(endpoints: LoopbackEndpoint).IsConfigured);
    }

    /// <summary>R-5: trusted-proxy auth mode counts as remote exposure.</summary>
    [Fact]
    public void IsConfigured_TrustedProxyMode_IsTrue()
    {
        Assert.True(State(options => options.Auth.ModeText = "trusted-proxy").IsConfigured);
    }

    /// <summary>R-5: a configured trusted proxy counts as remote exposure.</summary>
    [Fact]
    public void IsConfigured_TrustedProxiesListed_IsTrue()
    {
        Assert.True(State(options => options.TrustedProxies.Add("10.0.0.5")).IsConfigured);
    }

    /// <summary>R-5: a bound app endpoint that is not loopback counts as remote exposure.</summary>
    [Fact]
    public void IsConfigured_NonLoopbackAppEndpoint_IsTrue()
    {
        Assert.True(State(endpoints: LanEndpoint).IsConfigured);
    }

    /// <summary>R-6: at the cap the store evicts the oldest nonces, so recently issued ones stay valid.</summary>
    [Fact]
    public void Issue_AtCap_EvictsOldestAndKeepsRecent()
    {
        FakeTimeProvider clock = new(GatewayConnectHarness.Start);
        ChallengeNonceStore store = new(clock);
        string oldest = store.Issue().Nonce;
        List<string> recent = [];
        for (int index = 0; index < ChallengeNonceStore.MaxOutstanding + 5; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            string nonce = store.Issue().Nonce;
            if (index >= ChallengeNonceStore.MaxOutstanding - 50)
            {
                recent.Add(nonce);
            }
        }

        Assert.Equal(ChallengeNonceStore.Check.Mismatch, store.Consume(oldest));
        Assert.All(recent, nonce => Assert.Equal(ChallengeNonceStore.Check.Valid, store.Consume(nonce)));
    }

    /// <summary>R-6: a client address may fetch 60 challenges a minute; the 61st is refused, others are unaffected, and the window slides.</summary>
    [Fact]
    public void ChallengeRateLimiter_SixtyPerMinutePerAddress()
    {
        FakeTimeProvider clock = new(GatewayConnectHarness.Start);
        ChallengeRateLimiter limiter = new(clock);
        for (int index = 0; index < ChallengeRateLimiter.PerMinute; index++)
        {
            Assert.True(limiter.TryAcquire("203.0.113.9"));
        }

        bool over = limiter.TryAcquire("203.0.113.9");
        bool other = limiter.TryAcquire("203.0.113.10");
        clock.Advance(TimeSpan.FromSeconds(61));
        bool later = limiter.TryAcquire("203.0.113.9");

        Assert.False(over);
        Assert.True(other);
        Assert.True(later);
    }

    /// <summary>R-12: <c>X-Forwarded-Proto</c> alone on a loopback socket is forwarding, so it is rejected.</summary>
    [Fact]
    public async Task Classify_LoopbackWithForwardedProto_IsRejected()
    {
        IngressResult result = await Classify(("X-Forwarded-Proto", "https"));

        Assert.Equal(IngressKind.Rejected, result.Kind);
    }

    /// <summary>R-12: <c>X-Forwarded-Host</c> alone on a loopback socket is forwarding, so it is rejected.</summary>
    [Fact]
    public async Task Classify_LoopbackWithForwardedHost_IsRejected()
    {
        IngressResult result = await Classify(("X-Forwarded-Host", "evil.example"));

        Assert.Equal(IngressKind.Rejected, result.Kind);
    }

    /// <summary>R-12: a loopback request with no forwarding header is still loopback.</summary>
    [Fact]
    public async Task Classify_LoopbackPlain_IsLoopback()
    {
        IngressResult result = await Classify(("Accept", "text/html"));

        Assert.Equal(IngressKind.Loopback, result.Kind);
    }
}
