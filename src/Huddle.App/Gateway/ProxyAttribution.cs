namespace Agency.Huddle.App.Gateway;

/// <summary>Switches on trusted-proxy attribution in the classifier (Spec 6.10, D-12): pass it only in <c>trusted-proxy</c> mode.</summary>
/// <param name="UserHeader">The header that must carry the proxy-authenticated user.</param>
internal sealed record ProxyAttribution(string UserHeader);
