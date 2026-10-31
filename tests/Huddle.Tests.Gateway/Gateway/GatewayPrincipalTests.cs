using System.Security.Claims;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.8, C-4: the principal the <c>HuddleGateway</c> scheme builds from a session.</summary>
public sealed class GatewayPrincipalTests
{
    private static readonly GatewaySession Session = new("device-1", "ABCDEF", [GatewayScopes.Read, GatewayScopes.Admin], "Direct", "ada@example.com");

    /// <summary>The name identifier is the device id (antiforgery binds to it), the session claim is the hash, and the ingress claim is the ingress text.</summary>
    [Fact]
    public void Create_CarriesDeviceSessionAndIngressClaims()
    {
        ClaimsPrincipal principal = GatewayPrincipal.Create(Session);

        Assert.Equal("device-1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("ABCDEF", principal.FindFirst(GatewayClaims.Session)?.Value);
        Assert.Equal("Direct", principal.FindFirst(GatewayClaims.Ingress)?.Value);
        Assert.Equal("huddle:session", GatewayClaims.Session);
        Assert.Equal("huddle:scope", GatewayClaims.Scope);
        Assert.Equal("huddle:ingress", GatewayClaims.Ingress);
    }

    /// <summary>There is one <c>huddle:scope</c> claim per scope, and the identity is authenticated under the scheme name.</summary>
    [Fact]
    public void Create_AddsOneScopeClaimPerScopeAndIsAuthenticated()
    {
        ClaimsPrincipal principal = GatewayPrincipal.Create(Session);

        Assert.Equal([GatewayScopes.Read, GatewayScopes.Admin], principal.FindAll(GatewayClaims.Scope).Select(claim => claim.Value).ToArray());
        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("HuddleGateway", principal.Identity?.AuthenticationType);
    }
}
