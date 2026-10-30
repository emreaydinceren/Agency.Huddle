using System.Security.Claims;

namespace Agency.Huddle.App.Gateway;

/// <summary>Builds the principal the <c>HuddleGateway</c> scheme authenticates (Spec 6.8, C-4).</summary>
internal static class GatewayPrincipal
{
    /// <summary>
    /// The host principal strict local mode gives a loopback request, with no session: viewer <c>host</c>, the loopback ingress, every scope, and
    /// <see cref="ClaimTypes.NameIdentifier"/> = <c>local</c>.
    /// </summary>
    public static ClaimsPrincipal CreateHost()
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, "local"),
            new(GatewayClaims.Viewer, GatewayClaims.ViewerHost),
            new(GatewayClaims.Ingress, IngressKindText.Of(IngressKind.Loopback)),
        ];
        claims.AddRange(GatewayScopes.AdminSet.Select(scope => new Claim(GatewayClaims.Scope, scope)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, GatewayClaims.AuthenticationType));
    }

    /// <summary>
    /// Produces <see cref="ClaimTypes.NameIdentifier"/> = device id (antiforgery binds to it), <see cref="GatewayClaims.Session"/> = the hash,
    /// one <see cref="GatewayClaims.Scope"/> per scope and <see cref="GatewayClaims.Ingress"/>, under <see cref="GatewayClaims.AuthenticationType"/>.
    /// </summary>
    /// <param name="session">The session.</param>
    public static ClaimsPrincipal Create(GatewaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, session.DeviceId),
            new(GatewayClaims.Session, session.Hash),
            new(GatewayClaims.Ingress, session.Ingress),
        ];
        claims.AddRange(session.Scopes.Select(scope => new Claim(GatewayClaims.Scope, scope)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, GatewayClaims.AuthenticationType));
    }
}
