using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Primitives;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// <c>POST /_desktop/bootstrap</c> (Spec 6.9): a loopback caller with the instance token gets <c>{ "url": ... }</c>, an admin bootstrap URL
/// built from the request's own origin.
/// </summary>
internal static class DesktopBootstrapEndpoint
{
    /// <summary>The route.</summary>
    internal const string Route = "/_desktop/bootstrap";

    /// <summary>Authenticates and mints.</summary>
    /// <param name="context">The request.</param>
    /// <param name="session">Holds the instance token.</param>
    /// <param name="links">Mints the URL.</param>
    /// <returns>403 for a non-loopback caller, 401 for a missing, repeated or wrong token, otherwise 200 with the URL.</returns>
    internal static IResult HandleAsync(HttpContext context, DesktopSession session, BootstrapLinks links)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(links);

        if (!DesktopStopEndpoint.IsLoopbackCaller(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!context.Request.Headers.TryGetValue(DesktopStopEndpoint.TokenHeader, out StringValues values)
            || values.Count != 1
            || values[0] is not { } presented
            || !GatewaySecrets.SecretsEqual(presented, session.StopToken))
        {
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }

        string origin = $"{context.Request.Scheme}://{context.Request.Host}";
        return Results.Json(new BootstrapAnswer(links.AdminUrl(origin)));
    }

    private sealed record BootstrapAnswer(string Url);
}
