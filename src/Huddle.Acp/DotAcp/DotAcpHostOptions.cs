namespace Agency.Huddle.Acp.DotAcp;

/// <summary>Options controlling how a <see cref="DotAcpAgentHost"/> connects to and identifies itself to an ACP agent.</summary>
/// <param name="ClientName">The client name sent in <c>initialize</c>.</param>
/// <param name="ClientVersion">The client version sent in <c>initialize</c>; null sends 0.1.0.</param>
/// <param name="TraceWire">Whether every JSON-RPC message is traced to the logger.</param>
/// <param name="AdvertiseElicitationForm">
/// Whether <c>initialize</c> advertises <c>clientCapabilities.elicitation.form</c> (form mode only,
/// never <c>url</c>) and the host rewrites an inbound <c>elicitation/create</c> request to the
/// extension method <c>_elicitation/create</c> stable dotacp can route. Off by default: a host that
/// has no way to answer must not advertise, because the adapter then holds its Turn open waiting for
/// a reply. The production factory is the only place that turns it on.
/// </param>
public sealed record DotAcpHostOptions(
    string ClientName = "Team.Console",
    string? ClientVersion = null,
    bool TraceWire = false,
    bool AdvertiseElicitationForm = false);