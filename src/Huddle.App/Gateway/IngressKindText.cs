namespace Agency.Huddle.App.Gateway;

/// <summary>The text form of an <see cref="IngressKind"/>, as pairing requests and sessions carry it.</summary>
internal static class IngressKindText
{
    /// <summary>The name of <paramref name="kind"/>, without boxing it.</summary>
    /// <param name="kind">The ingress kind.</param>
    public static string Of(IngressKind kind) => kind switch
    {
        IngressKind.Loopback => nameof(IngressKind.Loopback),
        IngressKind.Direct => nameof(IngressKind.Direct),
        IngressKind.Managed => nameof(IngressKind.Managed),
        IngressKind.TrustedProxy => nameof(IngressKind.TrustedProxy),
        _ => nameof(IngressKind.Rejected),
    };
}
