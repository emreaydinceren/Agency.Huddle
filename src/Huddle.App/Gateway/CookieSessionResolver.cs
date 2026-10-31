namespace Agency.Huddle.App.Gateway;

/// <summary>The real <see cref="IGatewaySessionResolver"/>: the session cookie, looked up in the <see cref="SessionRegistry"/> with the Spec 8.5 checks.</summary>
/// <param name="sessions">The sessions.</param>
/// <param name="settings">The gateway settings, whose Instance id names the cookie.</param>
internal sealed class CookieSessionResolver(SessionRegistry sessions, GatewaySettings settings) : IGatewaySessionResolver, ISessionLiveness
{
    /// <inheritdoc />
    public ValueTask<GatewaySession?> ResolveAsync(HttpContext http, IngressResult ingress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(ingress);

        string? cookie = http.Request.Cookies[ConnectEndpoints.CookieName(settings.InstanceId)];
        return ValueTask.FromResult(sessions.TryGet(cookie, ingress.TailscaleLogin));
    }

    /// <inheritdoc />
    public bool IsLive(string sessionHash) => sessions.TryGetByHash(sessionHash) is not null;
}
