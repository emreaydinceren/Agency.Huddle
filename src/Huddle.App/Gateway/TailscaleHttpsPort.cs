namespace Agency.Huddle.App.Gateway;

/// <summary>The Tailscale HTTPS port rules (Spec 8.1).</summary>
internal static class TailscaleHttpsPort
{
    /// <summary>Tailscale's own default HTTPS port.</summary>
    public const int Default = 443;

    /// <summary>Stored port, else the app's fixed port, else <see cref="Default"/>.</summary>
    /// <param name="stored"><c>Team:Gateway:Tailscale:HttpsPort</c>.</param>
    /// <param name="appPort">The app's fixed port, or <see langword="null"/>.</param>
    public static int Resolve(int? stored, int? appPort) => stored ?? appPort ?? Default;

    /// <summary>Whether Funnel accepts <paramref name="port"/> (443, 8443 and 10000).</summary>
    /// <param name="port">The port.</param>
    public static bool FunnelAllows(int port) => port is 443 or 8443 or 10000;
}
