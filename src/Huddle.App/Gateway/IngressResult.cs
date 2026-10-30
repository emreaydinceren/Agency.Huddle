using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>How a request reached Huddle.</summary>
/// <param name="Kind">The classification.</param>
/// <param name="Client">The client address the limiter and registry use, normalised; loopback for a null socket; null when rejected.</param>
/// <param name="TailscaleLogin">The verified Tailscale login, or <see langword="null"/>.</param>
/// <param name="IsFunnel">Whether it came through Tailscale Funnel.</param>
/// <param name="Error">The 403 error code when <see cref="Kind"/> is <see cref="IngressKind.Rejected"/>; otherwise <see langword="null"/>.</param>
internal sealed record IngressResult(
    IngressKind Kind,
    IPAddress? Client,
    string? TailscaleLogin,
    bool IsFunnel,
    string? Error)
{
    /// <summary>The user the trusted proxy authenticated (the <c>UserHeader</c> value), or <see langword="null"/> (D-12).</summary>
    public string? ProxyUser { get; init; }
}
