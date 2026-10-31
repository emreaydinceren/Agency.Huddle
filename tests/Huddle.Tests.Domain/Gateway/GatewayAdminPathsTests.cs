using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Http;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.13: <c>/_gateway/*</c> passes the gate without a session, loopback only, like <c>/_desktop/*</c>; its own bearer check follows.</summary>
public sealed class GatewayAdminPathsTests
{
    /// <summary>The admin routes are loopback-only, not protected and not public.</summary>
    /// <param name="path">The request path.</param>
    [Theory]
    [InlineData("/_gateway/devices")]
    [InlineData("/_gateway/devices/requests/r1/approve")]
    [InlineData("/_gateway/devices/d1/revoke")]
    [InlineData("/_desktop/bootstrap")]
    public void Of_AdminRoutes_AreLoopbackOnly(string path)
    {
        Assert.Equal(GatewayPaths.Kind.LoopbackOnly, GatewayPaths.Of(new PathString(path)));
    }

    /// <summary>A look-alike prefix is not an admin route.</summary>
    [Fact]
    public void Of_GatewayLookAlike_IsProtected()
    {
        Assert.Equal(GatewayPaths.Kind.Protected, GatewayPaths.Of(new PathString("/_gatewayish")));
    }
}
