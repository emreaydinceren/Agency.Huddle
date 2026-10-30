using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Http;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The harness seam of C-2: answers every request with one fixed, paired loopback admin session, so a hosted test that is not about the gateway
/// behaves as the loopback admin it always was. <c>TeamWebApplicationFactory</c> registers it unless <c>ConnectedAsAdmin</c> is false.
/// </summary>
internal sealed class LoopbackAdminSessionResolver : IGatewaySessionResolver
{
    /// <summary>The fixed device id every request carries.</summary>
    public const string DeviceId = "loopback-test-device";

    /// <summary>The fixed session hash every request carries.</summary>
    public const string SessionHash = "loopback-test-session";

    private static readonly GatewaySession Session = new(
        DeviceId,
        SessionHash,
        [GatewayScopes.Read, GatewayScopes.Write, GatewayScopes.Pairing, GatewayScopes.Admin],
        IngressKindText.Of(IngressKind.Loopback),
        null);

    /// <inheritdoc />
    public ValueTask<GatewaySession?> ResolveAsync(HttpContext http, IngressResult ingress, CancellationToken cancellationToken)
        => ValueTask.FromResult<GatewaySession?>(Session);
}
