namespace Agency.Huddle.App.Gateway;

/// <summary>The claim types of the <c>HuddleGateway</c> principal (Spec 6.8, C-4).</summary>
internal static class GatewayClaims
{
    /// <summary>The authentication type of the principal.</summary>
    public const string AuthenticationType = "HuddleGateway";

    /// <summary>The session hash (SHA-256 hex of the cookie), one per principal.</summary>
    public const string Session = "huddle:session";

    /// <summary>One claim per scope the session holds.</summary>
    public const string Scope = "huddle:scope";

    /// <summary>The ingress kind the session was issued over, as <see cref="IngressKindText"/> writes it.</summary>
    public const string Ingress = "huddle:ingress";

    /// <summary>Who is viewing; <see cref="ViewerHost"/> for the host principal of strict local mode.</summary>
    public const string Viewer = "huddle:viewer";

    /// <summary>The value of <see cref="Viewer"/> for the host.</summary>
    public const string ViewerHost = "host";
}
