using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Primitives;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The <c>/_gateway/*</c> endpoints the <c>devices</c> CLI verbs call (Spec 6.13, C-13): loopback only, <c>Authorization: Bearer {gateway token}</c>,
/// acting as a host-admin principal. A wrong bearer is a failure counted under <see cref="AuthRateLimitScopes.SharedSecret"/>.
/// </summary>
internal static class GatewayAdminEndpoints
{
    /// <summary>The route prefix.</summary>
    public const string Prefix = "/_gateway";

    /// <summary>Maps the endpoints.</summary>
    /// <param name="app">The application.</param>
    public static WebApplication MapGatewayAdminEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup(Prefix).DisableAntiforgery();
        _ = group.AddEndpointFilter(RequireBearerAsync);
        _ = group.MapGet("/devices", List);
        _ = group.MapPost("/devices/requests/{requestId}/approve", ApproveAsync);
        _ = group.MapPost("/devices/requests/{requestId}/reject", Reject);
        _ = group.MapPost("/devices/{deviceId}/revoke", (string deviceId, DeviceRegistry devices) => Done(devices.Revoke(deviceId)));
        _ = group.MapPost("/devices/{deviceId}/remove", (string deviceId, DeviceRegistry devices) => Done(devices.Remove(deviceId)));
        _ = group.MapPost("/devices/{deviceId}/rotate", (string deviceId, PairingService pairing) => Done(pairing.Rotate(deviceId) is not null));
        return app;
    }

    /// <summary>The scopes the admin routes act with: the host's own, which is every scope (C-13).</summary>
    private static IReadOnlyList<string> HostAdminScopes => GatewayScopes.AdminSet;

    private static IResult Done(bool found) => found ? Results.Json(new Okay(true)) : Results.NotFound();

    private static async ValueTask<object?> RequireBearerAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        HttpContext http = invocation.HttpContext;
        GatewaySettings settings = http.RequestServices.GetRequiredService<GatewaySettings>();
        AuthRateLimiter limiter = http.RequestServices.GetRequiredService<AuthRateLimiter>();
        string client = (http.Connection.RemoteIpAddress ?? IPAddress.Loopback).ToString();
        string key = AuthRateLimiter.Key(AuthRateLimitScopes.SharedSecret, client);
        bool loopback = GatewayHttpItems.GetIngress(http) is { } ingress
            ? ingress.Kind == IngressKind.Loopback
            : http.Connection.RemoteIpAddress is null || IPAddress.IsLoopback(http.Connection.RemoteIpAddress);

        if (!loopback)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        AuthRateLimitDecision decision = limiter.Check(key, loopback);
        if (!decision.Allowed)
        {
            http.Response.Headers.RetryAfter = ((decision.RetryAfterMs + 999) / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (BearerOf(http) is { } presented && settings.VerifyToken(presented))
        {
            limiter.Reset(key);
            return await next(invocation).ConfigureAwait(false);
        }

        AuthRateLimitFailure failure = limiter.RecordFailure(key, loopback);
        if (failure.Delay > TimeSpan.Zero)
        {
            await Task.Delay(failure.Delay, http.RequestAborted).ConfigureAwait(false);
        }

        return Results.StatusCode(StatusCodes.Status401Unauthorized);
    }

    private static string? BearerOf(HttpContext http)
    {
        return http.Request.Headers.TryGetValue("Authorization", out StringValues values)
            && values.Count == 1
            && AuthenticationHeaderValue.TryParse(values[0], out AuthenticationHeaderValue? header)
            && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(header.Parameter)
                ? header.Parameter
                : null;
    }

    private static IResult List(DeviceRegistry devices, PairingService pairing)
    {
        return Results.Json(new Listing(
            [.. devices.List().Select(device => new DeviceView(
                device.DeviceId, device.Name, device.Scopes, device.HasToken, device.PairedVia, device.PairedAt, device.LastSeenAt, device.LastSeenFrom, device.UserAgent, !device.HasToken))],
            [.. pairing.Pending().Select(request => new RequestView(
                request.RequestId, request.DeviceId, request.Name, request.Scopes, request.Address, request.Ingress, request.UserAgent, request.CreatedAt, request.ExpiresAt))]));
    }

    private static async Task<IResult> ApproveAsync(string requestId, HttpContext http, PairingService pairing)
    {
        IReadOnlyList<string>? scopes = null;
        if (http.Request.ContentLength is > 0 && http.Request.HasJsonContentType())
        {
            ApproveBody? body = await http.Request.ReadFromJsonAsync<ApproveBody>(http.RequestAborted).ConfigureAwait(false);
            scopes = body?.Scope;
        }

        PairingService.ApprovalResult result = pairing.Approve(requestId, HostAdminScopes, scopes);
        return result.Outcome switch
        {
            PairingService.ApprovalOutcome.Approved => Results.Json(new Okay(true)),
            PairingService.ApprovalOutcome.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status403Forbidden),
        };
    }

    private static IResult Reject(string requestId, PairingService pairing) => Done(pairing.Reject(requestId, HostAdminScopes));

    private sealed record Okay(bool Ok);

    private sealed record ApproveBody(IReadOnlyList<string>? Scope);

    private sealed record DeviceView(
        string DeviceId,
        string Name,
        IReadOnlyList<string> Scopes,
        bool HasToken,
        string PairedVia,
        DateTimeOffset PairedAt,
        DateTimeOffset LastSeenAt,
        string LastSeenFrom,
        string? UserAgent,
        bool Revoked);

    private sealed record RequestView(
        string RequestId,
        string DeviceId,
        string Name,
        IReadOnlyList<string> Scopes,
        string Address,
        string Ingress,
        string? UserAgent,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);

    private sealed record Listing(IReadOnlyList<DeviceView> Devices, IReadOnlyList<RequestView> Requests);
}
