using System.Buffers.Text;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.7 and 7.2, C-9: bootstrap tokens are 32 bytes, bound to the first device that redeems them, good for 10 minutes, and burnable.</summary>
public sealed class BootstrapTokenStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A token is 32 random bytes as base64url, and two tokens differ.</summary>
    [Fact]
    public void Issue_Gives32ByteBase64UrlTokens()
    {
        BootstrapTokenStore store = new(new FakeTimeProvider(Start));

        string token = store.Issue(BootstrapTokenStore.Kind.Admin);

        Assert.Equal(32, Base64Url.DecodeFromChars(token).Length);
        Assert.NotEqual(token, store.Issue(BootstrapTokenStore.Kind.Admin));
    }

    /// <summary>An admin token redeems with admin scopes; a setup link with the limited ones.</summary>
    [Fact]
    public void Redeem_CarriesTheKindsScopes()
    {
        BootstrapTokenStore store = new(new FakeTimeProvider(Start));

        BootstrapTokenStore.Redemption admin = store.Redeem(store.Issue(BootstrapTokenStore.Kind.Admin), "dev-a");
        BootstrapTokenStore.Redemption setup = store.Redeem(store.Issue(BootstrapTokenStore.Kind.SetupLink), "dev-a");

        Assert.True(admin.Valid);
        Assert.Equal(BootstrapTokenStore.Kind.Admin, admin.TokenKind);
        Assert.True(GatewayScopes.Holds(admin.Scopes, GatewayScopes.Admin));
        Assert.True(setup.Valid);
        Assert.Equal(BootstrapTokenStore.Kind.SetupLink, setup.TokenKind);
        Assert.False(GatewayScopes.Holds(setup.Scopes, GatewayScopes.Admin));
    }

    /// <summary>The first device to redeem a token binds it: a second device is refused, the bound one may present it again.</summary>
    [Fact]
    public void Redeem_IsBoundToTheFirstDevice()
    {
        BootstrapTokenStore store = new(new FakeTimeProvider(Start));
        string token = store.Issue(BootstrapTokenStore.Kind.Admin);

        Assert.True(store.Redeem(token, "dev-a").Valid);

        Assert.False(store.Redeem(token, "dev-b").Valid);
        Assert.True(store.Redeem(token, "dev-a").Valid);
    }

    /// <summary>An unknown or empty token is not valid.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public void Redeem_UnknownToken_IsInvalid(string token)
    {
        BootstrapTokenStore store = new(new FakeTimeProvider(Start));

        BootstrapTokenStore.Redemption redemption = store.Redeem(token, "dev-a");

        Assert.False(redemption.Valid);
        Assert.Empty(redemption.Scopes);
    }

    /// <summary>A token lives 10 minutes: good at 9:59, dead at 10:01, bound device or not.</summary>
    [Fact]
    public void Redeem_Expires_After10Minutes()
    {
        FakeTimeProvider clock = new(Start);
        BootstrapTokenStore store = new(clock);
        string bound = store.Issue(BootstrapTokenStore.Kind.Admin);
        string unused = store.Issue(BootstrapTokenStore.Kind.Admin);
        Assert.True(store.Redeem(bound, "dev-a").Valid);

        clock.Advance(TimeSpan.FromSeconds(599));
        Assert.True(store.Redeem(unused, "dev-a").Valid);
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.False(store.Redeem(bound, "dev-a").Valid);
        Assert.False(store.Redeem(unused, "dev-a").Valid);
    }

    /// <summary>A burned token is unusable by everyone, including the device it was bound to.</summary>
    [Fact]
    public void Burn_MakesTheTokenUnusable()
    {
        BootstrapTokenStore store = new(new FakeTimeProvider(Start));
        string token = store.Issue(BootstrapTokenStore.Kind.Admin);
        Assert.True(store.Redeem(token, "dev-a").Valid);

        store.Burn(token);

        Assert.False(store.Redeem(token, "dev-a").Valid);
        Assert.False(store.Redeem(token, "dev-b").Valid);
    }
}
