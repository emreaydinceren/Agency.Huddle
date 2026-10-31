namespace Agency.Huddle.App.Gateway;

/// <summary>Adds the gateway middlewares to the pipeline (Spec 6.8, C-3).</summary>
internal static class GatewayApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the gate: ingress classification, origin check, loopback-only routes and the pre-auth limit. Call it before <c>UseAuthentication</c>,
    /// which must run after it so the <c>HuddleGateway</c> handler can read the ingress.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    internal static IApplicationBuilder UseGatewayGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<GatewayGate>();
    }

    /// <summary>Adds the session requirement. Call it after <c>UseAuthentication</c> and <c>UseAuthorization</c>.</summary>
    /// <param name="app">The pipeline.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    internal static IApplicationBuilder UseGatewaySessionRequirement(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<GatewaySessionRequirement>();
    }
}
