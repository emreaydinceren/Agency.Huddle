using System.Text.RegularExpressions;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.9 and C-9: bootstrap URLs always open <c>/auth</c> with the token in the fragment; tokens last 10 minutes; the setup-link https rule.</summary>
public sealed partial class BootstrapLinksTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [GeneratedRegex(@"^(?<base>.+)/auth\?return=%2F#bootstrapToken=(?<token>[A-Za-z0-9_-]{43})$", RegexOptions.CultureInvariant)]
    private static partial Regex UrlShape();

    private static string TokenOf(string url)
    {
        Match match = UrlShape().Match(url);
        Assert.True(match.Success, url);
        return match.Groups["token"].Value;
    }

    /// <summary>The admin URL is <c>{base}/auth?return=%2F#bootstrapToken=...</c>, and the token in it redeems as admin.</summary>
    [Fact]
    public void AdminUrl_IsTheAuthUrlWithAFragmentToken()
    {
        BootstrapTokenStore store = new(new FakeTimeProvider(Start));
        BootstrapLinks links = new(store);

        string url = links.AdminUrl("http://127.0.0.1:5123");

        Assert.Equal("http://127.0.0.1:5123/auth?return=%2F#bootstrapToken=" + TokenOf(url), url);
        BootstrapTokenStore.Redemption redemption = store.Redeem(TokenOf(url), "dev-a");
        Assert.True(redemption.Valid);
        Assert.Equal(BootstrapTokenStore.Kind.Admin, redemption.TokenKind);
    }

    /// <summary>Each call mints a different token.</summary>
    [Fact]
    public void AdminUrl_MintsAFreshTokenEachTime()
    {
        BootstrapLinks links = new(new BootstrapTokenStore(new FakeTimeProvider(Start)));

        Assert.NotEqual(TokenOf(links.AdminUrl("http://127.0.0.1:5123")), TokenOf(links.AdminUrl("http://127.0.0.1:5123")));
    }

    /// <summary>An any-address, and a trailing slash, are normalised to a loopback origin.</summary>
    /// <param name="listen">The Kestrel address.</param>
    /// <param name="expected">The origin the URL starts with.</param>
    [Theory]
    [InlineData("http://[::]:5123", "http://127.0.0.1:5123")]
    [InlineData("http://0.0.0.0:5123", "http://127.0.0.1:5123")]
    [InlineData("http://+:5123", "http://127.0.0.1:5123")]
    [InlineData("http://*:5123", "http://127.0.0.1:5123")]
    [InlineData("http://127.0.0.1:5123/", "http://127.0.0.1:5123")]
    [InlineData("http://localhost:5100", "http://localhost:5100")]
    public void NormalizeListenAddress_MapsAnyAddressToLoopback(string listen, string expected)
    {
        Assert.Equal(expected, BootstrapLinks.NormalizeListenAddress(listen));

        BootstrapLinks links = new(new BootstrapTokenStore(new FakeTimeProvider(Start)));
        string url = links.AdminUrl(listen);
        Assert.Equal(expected + "/auth?return=%2F#bootstrapToken=" + TokenOf(url), url);
    }

    /// <summary>An admin token is valid for 10 minutes: accepted just before, refused at the mark.</summary>
    [Fact]
    public void AdminUrl_TokenExpiresAfterTenMinutes()
    {
        FakeTimeProvider clock = new(Start);
        BootstrapTokenStore store = new(clock);
        string token = TokenOf(new BootstrapLinks(store).AdminUrl("http://127.0.0.1:5123"));

        clock.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        Assert.True(store.Redeem(token, "dev-a").Valid);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.False(store.Redeem(token, "dev-a").Valid);
    }

    /// <summary>A setup link carries a limited token (never admin) that also lives 10 minutes.</summary>
    [Fact]
    public void SetupLink_MintsALimitedTokenWithTenMinutes()
    {
        FakeTimeProvider clock = new(Start);
        BootstrapTokenStore store = new(clock);
        BootstrapLinks links = new(store);

        BootstrapLinks.SetupLinkResult result = links.SetupLink("https://desktop-a.tail1234.ts.net:5101");

        Assert.True(result.Allowed);
        Assert.NotNull(result.Url);
        Assert.Equal("https://desktop-a.tail1234.ts.net:5101/auth?return=%2F#bootstrapToken=" + TokenOf(result.Url), result.Url);
        string token = TokenOf(result.Url);
        BootstrapTokenStore.Redemption redemption = store.Redeem(token, "phone");
        Assert.Equal(BootstrapTokenStore.Kind.SetupLink, redemption.TokenKind);
        Assert.Equal(GatewayScopes.Limited, redemption.Scopes);
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(store.Redeem(token, "phone").Valid);
    }

    /// <summary>The https rule: plain http to a non-loopback host is allowed only for a private IP literal, <c>.local</c> or <c>.ts.net</c>.</summary>
    /// <param name="origin">The origin.</param>
    /// <param name="allowed">Whether a setup link may point at it.</param>
    [Theory]
    [InlineData("https://desktop-a.tail1234.ts.net:5101", true)]
    [InlineData("https://example.com", true)]
    [InlineData("http://desktop-a.tail1234.ts.net:5101", true)]
    [InlineData("http://192.168.1.20:5101", true)]
    [InlineData("http://10.0.0.5:5101", true)]
    [InlineData("http://172.16.4.2:5101", true)]
    [InlineData("http://mypc.local:5101", true)]
    [InlineData("http://127.0.0.1:5101", true)]
    [InlineData("http://localhost:5101", true)]
    [InlineData("http://example.com", false)]
    [InlineData("http://203.0.113.9:5101", false)]
    [InlineData("http://172.32.0.1:5101", false)]
    [InlineData("http://notts.net.example.com", false)]
    [InlineData("not a url", false)]
    [InlineData("ftp://mypc.local", false)]
    public void IsSetupLinkOriginAllowed_FollowsTheHttpsRule(string origin, bool allowed)
    {
        Assert.Equal(allowed, BootstrapLinks.IsSetupLinkOriginAllowed(origin));
    }

    /// <summary>A refused origin yields no URL and a reason, and no usable link.</summary>
    [Fact]
    public void SetupLink_PlainHttpPublicHost_IsRefused()
    {
        BootstrapLinks links = new(new BootstrapTokenStore(new FakeTimeProvider(Start)));

        BootstrapLinks.SetupLinkResult result = links.SetupLink("http://example.com:5101");

        Assert.False(result.Allowed);
        Assert.Null(result.Url);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
}
