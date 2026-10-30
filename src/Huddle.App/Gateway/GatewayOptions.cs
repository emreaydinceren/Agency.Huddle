namespace Agency.Huddle.App.Gateway;

/// <summary><c>Team:Gateway</c> (Spec Appendix A): the keys Stage A needs, with the Appendix A defaults.</summary>
internal sealed class GatewayOptions
{
    /// <summary>The configuration section path.</summary>
    public const string SectionName = "Team:Gateway";

    /// <summary>The bind mode (<c>loopback</c>, <c>tailnet</c>, <c>lan</c>, <c>auto</c>, <c>custom</c>).</summary>
    public string Bind { get; set; } = "loopback";

    /// <summary>The gateway's own port, or <see langword="null"/> for the app's own fixed port.</summary>
    public int? Port { get; set; }

    /// <summary>Gateway auth.</summary>
    public GatewayAuthOptions Auth { get; set; } = new();

    /// <summary>Whether a loopback device pairs without approval.</summary>
    public bool AutoApproveLocal { get; set; } = true;

    /// <summary>The host to bind in <c>custom</c> mode (an IP literal), or <see langword="null"/>.</summary>
    public string? CustomBindHost { get; set; }

    /// <summary>The one origin the browser reaches the gateway at, or <see langword="null"/> (Spec 8.7 rule 1).</summary>
    public string? PublicOrigin { get; set; }

    /// <summary>The allowed origins (Spec 8.7 rule 3); empty means <see cref="PublicOrigin"/> only. <c>*</c> allows any.</summary>
    public List<string> AllowedOrigins { get; } = [];

    /// <summary>Whether an Origin whose host equals the <c>Host</c> header passes (Spec 8.7 rule 4).</summary>
    public bool DangerouslyAllowHostHeaderOriginFallback { get; set; }

    /// <summary>Extra host names the host filter and the plain-HTTP rule accept (Spec 6.4, D-10).</summary>
    public List<string> ExtraHosts { get; } = [];

    /// <summary>The source addresses of trusted proxies (Spec 6.10).</summary>
    public List<string> TrustedProxies { get; } = [];

    /// <summary>The trusted-proxy settings (<c>Team:Gateway:TrustedProxy</c>).</summary>
    public TrustedProxyOptions TrustedProxy { get; set; } = new();

    /// <summary>The Tailscale settings (<c>Team:Gateway:Tailscale</c>); Stage D2 extends them.</summary>
    public TailscaleOptions Tailscale { get; set; } = new();

    /// <summary><c>Team:Gateway:TrustedProxy</c>.</summary>
    internal sealed class TrustedProxyOptions
    {
        /// <summary>The header that carries the proxy-authenticated user, or <see langword="null"/>.</summary>
        public string? UserHeader { get; set; }
    }

    /// <summary><c>Team:Gateway:Tailscale</c>.</summary>
    internal sealed class TailscaleOptions
    {
        /// <summary><c>off</c>, <c>serve</c> or <c>funnel</c>.</summary>
        public string Mode { get; set; } = "off";

        /// <summary>The Tailscale HTTPS port, or <see langword="null"/> for the app's own port (Spec 8.1).</summary>
        public int? HttpsPort { get; set; }

        /// <summary>The <c>tailscale</c> executable to use when it is neither on <c>PATH</c> nor at the platform default, or <see langword="null"/>.</summary>
        public string? Command { get; set; }
    }
}
