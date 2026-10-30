using System.Net;
using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Primitives;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// The <c>POST /_desktop/stop</c> endpoint that lets <c>huddle stop</c> shut a running desktop instance down.
/// It answers only loopback callers that present the per-instance stop token.
/// </summary>
internal static class DesktopStopEndpoint
{
    /// <summary>The route the endpoint is mapped at.</summary>
    internal const string Route = "/_desktop/stop";

    /// <summary>The request header that carries the stop token.</summary>
    internal const string TokenHeader = "X-Huddle-Token";

    /// <summary>
    /// Authenticates the request and, when it is allowed, asks the host to stop.
    /// </summary>
    /// <param name="context">The request. A null remote address (the in-memory test server) is allowed.</param>
    /// <param name="session">The session that holds the expected stop token.</param>
    /// <param name="shutdown">The seam that stops the host.</param>
    /// <returns>403 for a non-loopback caller, 401 for a missing, repeated or wrong token, otherwise 202.</returns>
    internal static IResult HandleAsync(HttpContext context, DesktopSession session, IDesktopShutdown shutdown)
    {
        if (!IsLoopbackCaller(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!context.Request.Headers.TryGetValue(TokenHeader, out StringValues values)
            || values.Count != 1
            || values[0] is not { } presented
            || !TokensMatch(presented, session.StopToken))
        {
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }

        shutdown.Stop();
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    /// <summary>
    /// Whether the caller is local (C-1): the ingress classifier's verdict the gate stored for the request is Loopback, which a forwarded or LAN
    /// request is not. A request that never went through the gate (a direct call) falls back to the socket address, where a null address is allowed.
    /// </summary>
    /// <param name="context">The request.</param>
    internal static bool IsLoopbackCaller(HttpContext context)
    {
        if (GatewayHttpItems.GetIngress(context) is { } ingress)
        {
            return ingress.Kind == IngressKind.Loopback;
        }

        IPAddress? remote = context.Connection.RemoteIpAddress;
        return remote is null || IPAddress.IsLoopback(remote);
    }

    /// <summary>
    /// Compares two tokens in constant time by hashing both to 32 bytes first, so the inputs are always the
    /// same length and no length-dependent early exit exists.
    /// </summary>
    /// <param name="presented">The token the caller sent.</param>
    /// <param name="expected">The instance's real token.</param>
    /// <returns><see langword="true"/> when the tokens are identical.</returns>
    internal static bool TokensMatch(string presented, string expected)
    {
        byte[] presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(presentedHash, expectedHash);
    }
}
