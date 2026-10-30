using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// <c>GET /auth/challenge</c>, <c>POST /auth/connect</c>, <c>GET /auth/pairing/{id}</c>, <c>GET /auth/status</c> (C-7) and
/// <c>POST /auth/bootstrap/burn</c> (C-9). Errors are JSON <c>{ "code", "retryAfterMs"? }</c>; a session is the cookie <c>huddle_session_{instanceId}</c>.
/// </summary>
internal static class ConnectEndpoints
{
    private const int MaxBodyBytes = 16 * 1024;
    private const int MaxUserAgentLength = 200;

    /// <summary>The session cookie name for an Instance id.</summary>
    /// <param name="instanceId">The Instance id.</param>
    public static string CookieName(string instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);

        return $"huddle_session_{instanceId}";
    }

    /// <summary>Maps the five endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet("/auth/challenge", Challenge).DisableAntiforgery();
        _ = endpoints.MapPost("/auth/connect", ConnectAsync).DisableAntiforgery();
        _ = endpoints.MapGet("/auth/pairing/{requestId}", PairingStatus).DisableAntiforgery();
        _ = endpoints.MapGet("/auth/status", StatusAsync).DisableAntiforgery();
        _ = endpoints.MapPost("/auth/bootstrap/burn", BurnAsync).DisableAntiforgery();
        return endpoints;
    }

    /// <summary>A <c>return</c> value is kept only when it is a local path: it starts with one slash, not two, and has no backslash or control character.</summary>
    /// <param name="requested">The <c>return</c> query value, or <see langword="null"/>.</param>
    /// <returns>The path with its query, or <c>/</c>.</returns>
    internal static string SafeReturn(string? requested)
    {
        if (string.IsNullOrEmpty(requested) || requested[0] != '/' || (requested.Length > 1 && requested[1] is '/' or '\\'))
        {
            return "/";
        }

        return requested.Any(char.IsControl) || requested.Contains('\\') ? "/" : requested;
    }

    private static IResult Challenge(HttpContext http, ChallengeNonceStore nonces)
    {
        NoStore(http);
        return Results.Json(nonces.Issue());
    }

    private static async Task<IResult> ConnectAsync(
        HttpContext http,
        GatewayConnectService service,
        SessionRegistry sessions,
        GatewaySettings settings,
        CancellationToken cancellationToken)
    {
        NoStore(http);
        ConnectRequest? body = await ReadBodyAsync<ConnectRequest>(http, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrEmpty(body.DeviceId) || string.IsNullOrEmpty(body.PublicKey) || string.IsNullOrEmpty(body.Signature))
        {
            return Results.Json(new ErrorBody("BAD_REQUEST", null), statusCode: StatusCodes.Status400BadRequest);
        }

        IngressResult ingress = await ClassifyAsync(http, cancellationToken).ConfigureAwait(false);
        if (ingress.Kind == IngressKind.Rejected)
        {
            return Results.Json(new ErrorBody(ingress.Error ?? "forbidden", null), statusCode: StatusCodes.Status403Forbidden);
        }

        ConnectContext context = new(ingress, HostAndOriginAreLoopback(http.Request), UserAgentOf(http.Request));
        ConnectOutcome outcome = service.Connect(Complete(body), context);
        if (outcome.Delay > TimeSpan.Zero)
        {
            await Task.Delay(outcome.Delay, cancellationToken).ConfigureAwait(false);
        }

        return outcome.Result switch
        {
            ConnectOutcome.Kind.Connected => Granted(http, outcome, ingress, sessions, settings),
            ConnectOutcome.Kind.PairingRequired => Results.Json(new PairingBody(outcome.Code, outcome.RequestId, outcome.DeviceId)),
            _ => Results.Json(new ErrorBody(outcome.Code, outcome.RetryAfterMs), statusCode: outcome.HttpStatus),
        };
    }

    private static IResult Granted(HttpContext http, ConnectOutcome outcome, IngressResult ingress, SessionRegistry sessions, GatewaySettings settings)
    {
        string cookie = sessions.Issue(outcome.DeviceId ?? string.Empty, outcome.Scopes, IngressKindText.Of(ingress.Kind), ingress.TailscaleLogin);
#pragma warning disable S2092 // Spec 7.4: Secure over HTTPS only; the same cookie must work on a plain-HTTP LAN or loopback page.
        CookieOptions options = new() { HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/", Secure = http.Request.IsHttps, IsEssential = true };
#pragma warning restore S2092
        http.Response.Cookies.Append(CookieName(settings.InstanceId), cookie, options);
        return Results.Json(new ConnectedBody(outcome.DeviceToken, outcome.Scopes, SafeReturn(http.Request.Query["return"].ToString())));
    }

    private static IResult PairingStatus(HttpContext http, string requestId, PairingService pairing)
    {
        NoStore(http);
        string status = pairing.StateOf(requestId) switch
        {
            PairingService.State.Pending => "pending",
            PairingService.State.Approved => "approved",
            PairingService.State.Rejected => "rejected",
            _ => "expired",
        };
        return Results.Json(new StatusBody(status));
    }

    /// <summary>
    /// <c>GET /auth/status</c>: whether the request's cookie names a live session (C-7), and what the connect page needs to draw itself:
    /// the auth <c>mode</c>, the verified <c>tailscaleLogin</c> (null unless a verified identity stands in for gateway auth), and the <c>host</c> and <c>port</c> asked for.
    /// </summary>
    private static async Task<IResult> StatusAsync(HttpContext http, SessionRegistry sessions, GatewaySettings settings, CancellationToken cancellationToken)
    {
        NoStore(http);
        IngressResult ingress = await ClassifyAsync(http, cancellationToken).ConfigureAwait(false);
        string? login = settings.Options.Auth.AllowTailscale && !ingress.IsFunnel ? ingress.TailscaleLogin : null;
        bool authenticated = sessions.TryGet(http.Request.Cookies[CookieName(settings.InstanceId)], ingress.TailscaleLogin) is not null;
        string mode = settings.Options.Auth.Mode switch
        {
            GatewayAuthMode.Password => "password",
            GatewayAuthMode.TrustedProxy => "trusted-proxy",
            GatewayAuthMode.None => "none",
            _ => "token",
        };
        return Results.Json(new SessionBody(authenticated, mode, login, Environment.MachineName, http.Request.Host.Port));
    }

    private static async Task<IResult> BurnAsync(HttpContext http, BootstrapTokenStore bootstrap, CancellationToken cancellationToken)
    {
        BurnRequest? body = await ReadBodyAsync<BurnRequest>(http, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrEmpty(body.BootstrapToken))
        {
            return Results.Json(new ErrorBody("BAD_REQUEST", null), statusCode: StatusCodes.Status400BadRequest);
        }

        bootstrap.Burn(body.BootstrapToken);
        return Results.NoContent();
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpContext http, CancellationToken cancellationToken)
        where T : class
    {
        if (!http.Request.HasJsonContentType())
        {
            return null;
        }

        IHttpMaxRequestBodySizeFeature? limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = MaxBodyBytes;
        }

        try
        {
            return await http.Request.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Malformed JSON is the caller's mistake and is answered 400 by the caller; nothing to log or rethrow.
            return null;
        }
        catch (BadHttpRequestException)
        {
            // A body over the limit is reported the same way as junk.
            return null;
        }
    }

    /// <summary>Fills what a JSON body may leave out, so the service never sees a null.</summary>
    /// <param name="body">The parsed body.</param>
    private static ConnectRequest Complete(ConnectRequest body) => body with
    {
        Nonce = string.IsNullOrEmpty(body.Nonce) ? string.Empty : body.Nonce,
        Platform = string.IsNullOrEmpty(body.Platform) ? string.Empty : body.Platform,
        DeviceFamily = string.IsNullOrEmpty(body.DeviceFamily) ? string.Empty : body.DeviceFamily,
        Role = string.IsNullOrEmpty(body.Role) ? DeviceAuth.Role : body.Role,
        Scopes = body.Scopes ?? [],
        Auth = body.Auth ?? new ConnectAuth(),
    };

    private static async ValueTask<IngressResult> ClassifyAsync(HttpContext http, CancellationToken cancellationToken)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, Microsoft.Extensions.Primitives.StringValues value) in http.Request.Headers)
        {
            headers[name] = value.ToString();
        }

        IngressRequest request = new(IngressListenerKind.App, http.Connection.RemoteIpAddress, headers);
        IIngressWhois whois = http.RequestServices.GetRequiredService<IIngressWhois>();
        return await IngressClassifier.ClassifyAsync(request, [], whois, cancellationToken).ConfigureAwait(false);
    }

    private static bool HostAndOriginAreLoopback(HttpRequest request)
    {
        if (!IsLoopbackName(request.Host.Host))
        {
            return false;
        }

        string origin = request.Headers.Origin.ToString();
        return origin.Length == 0 || (Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) && IsLoopbackName(uri.Host));
    }

    internal static bool IsLoopbackName(string host)
    {
        string name = host.Trim('[', ']');
        return string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(name, out IPAddress? address) && IPAddress.IsLoopback(address));
    }

    private static string? UserAgentOf(HttpRequest request)
    {
        string agent = request.Headers.UserAgent.ToString();
        return agent.Length == 0 ? null : agent[..Math.Min(agent.Length, MaxUserAgentLength)];
    }

    private static void NoStore(HttpContext http) => http.Response.Headers.CacheControl = "no-store";

    private sealed record ErrorBody(
        string? Code,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? RetryAfterMs);

    private sealed record PairingBody(string? Code, string? RequestId, string? DeviceId);

    private sealed record ConnectedBody(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeviceToken,
        IReadOnlyList<string> Scopes,
        string Return);

    private sealed record StatusBody(string Status);

    private sealed record SessionBody(bool Authenticated, string Mode, string? TailscaleLogin, string Host, int? Port);

    private sealed record BurnRequest(string? BootstrapToken);
}
