namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The last gateway middleware (Spec 6.8): after authentication, a request to a protected route without a session is redirected to
/// <c>/auth?return=…</c> when it is an HTML navigation (a GET or HEAD whose <c>Accept</c> names <c>text/html</c>) and answered 401 otherwise.
/// </summary>
/// <param name="next">The next middleware.</param>
internal sealed class GatewaySessionRequirement(RequestDelegate next)
{
    /// <summary>Runs the check for one request.</summary>
    /// <param name="http">The request.</param>
    public async Task InvokeAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (GatewayHttpItems.IsPublic(http) || http.User.Identity?.IsAuthenticated == true)
        {
            await next(http).ConfigureAwait(false);
            return;
        }

        if (IsHtmlNavigation(http.Request))
        {
            string target = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            http.Response.Redirect("/auth?return=" + Uri.EscapeDataString(target));
            return;
        }

        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    private static bool IsHtmlNavigation(HttpRequest request)
        => (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
}
