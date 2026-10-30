namespace Agency.Huddle.App.Gateway;

/// <summary>A live session as the gate sees it (Spec 6.8, 8.5).</summary>
/// <param name="DeviceId">The paired device.</param>
/// <param name="Hash">The SHA-256 hex of the cookie value; the cookie itself is never kept.</param>
/// <param name="Scopes">The scopes the session was issued with.</param>
/// <param name="Ingress">The ingress kind it was issued over, as <see cref="IngressKindText"/> writes it.</param>
/// <param name="TailscaleLogin">The Tailscale login it is bound to, or <see langword="null"/>.</param>
internal sealed record GatewaySession(
    string DeviceId,
    string Hash,
    IReadOnlyList<string> Scopes,
    string Ingress,
    string? TailscaleLogin);
