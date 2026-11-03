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
/// <param name="AdvertiseSessionNotices">
/// Whether <c>initialize</c> advertises <c>clientCapabilities.session.notices</c> and the host rewrites an
/// inbound <c>session/update</c> whose <c>update.sessionUpdate</c> is <c>notice</c> to the extension
/// notification <c>_session/notice</c>, published as an <see cref="Abstractions.AgentNotice"/>. Stable
/// dotacp predates the <c>notice</c> update and fails to deserialise one, so the rewrite is what makes
/// advertising safe. Off by default: an agent that was not told writes the notice into the reply text,
/// which is what every host without a place to show one should get.
/// </param>
public sealed record DotAcpHostOptions(
    string ClientName = "Team.Console",
    string? ClientVersion = null,
    bool TraceWire = false,
    bool AdvertiseElicitationForm = false,
    bool AdvertiseSessionNotices = false);