using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The <c>HuddleGateway</c> scheme (Spec 6.8, C-3): reads the ingress the gate stored in <see cref="HttpContext.Items"/>, asks the
/// <see cref="IGatewaySessionResolver"/> for the request's session and, when there is one, authenticates its <see cref="GatewayPrincipal"/>.
/// Under strict local mode a loopback request is the host and gets <see cref="GatewayPrincipal.CreateHost"/> with no session.
/// Without a session it yields no result; the session requirement middleware turns that into 302 or 401.
/// </summary>
internal sealed class GatewayAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IGatewaySessionResolver resolver,
    GatewaySettings settings)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        IngressResult? ingress = GatewayHttpItems.GetIngress(this.Context);
        if (ingress is null || ingress.Kind == IngressKind.Rejected)
        {
            return AuthenticateResult.NoResult();
        }

        if (settings.Options.StrictLocal)
        {
            // Strict local: the gate admitted only loopback, which is the host, and there is no sign-in.
            return ingress.Kind == IngressKind.Loopback
                ? AuthenticateResult.Success(new AuthenticationTicket(GatewayPrincipal.CreateHost(), GatewayClaims.AuthenticationType))
                : AuthenticateResult.NoResult();
        }

        GatewaySession? session = await resolver.ResolveAsync(this.Context, ingress, this.Context.RequestAborted).ConfigureAwait(false);
        if (session is null)
        {
            return AuthenticateResult.NoResult();
        }

        AuthenticationTicket ticket = new(GatewayPrincipal.Create(session), GatewayClaims.AuthenticationType);
        return AuthenticateResult.Success(ticket);
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
