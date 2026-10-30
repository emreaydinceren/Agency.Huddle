namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The seam between a request and its session (C-2). The real resolver reads the cookie and runs the Spec 8.5 checks;
/// the hosted test factory replaces it with a fixed loopback admin session. No configuration key reaches the seam.
/// </summary>
internal interface IGatewaySessionResolver
{
    /// <summary>Returns the session this request carries, or <see langword="null"/> when it has none.</summary>
    /// <param name="http">The request.</param>
    /// <param name="ingress">How the request arrived, as the classifier decided (never <see cref="IngressKind.Rejected"/>).</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    ValueTask<GatewaySession?> ResolveAsync(HttpContext http, IngressResult ingress, CancellationToken cancellationToken);
}
