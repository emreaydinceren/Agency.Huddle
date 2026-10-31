namespace Agency.Huddle.App.Gateway;

/// <summary>What the connect endpoint knows about the request besides its body.</summary>
/// <param name="Ingress">The classified ingress.</param>
/// <param name="HostAndOriginLoopback">Whether <c>Host</c> and <c>Origin</c> are loopback names.</param>
/// <param name="UserAgent">The user agent.</param>
internal sealed record ConnectContext(IngressResult Ingress, bool HostAndOriginLoopback, string? UserAgent);
