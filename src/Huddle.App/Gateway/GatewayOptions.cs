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
}
