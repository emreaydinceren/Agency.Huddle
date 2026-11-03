using Newtonsoft.Json;

namespace Agency.Huddle.Acp.DotAcp;

/// <summary>
/// The stable dotacp <c>ClientSessionCapabilities</c> plus the <c>notices</c> member it has no property
/// for (ACP schema 1.25 and later; dotacp is built on 1.19). Serialised by dotacp's own Newtonsoft
/// settings, the wire object is exactly <c>"session":{"notices":{}}</c>: an empty object is the whole
/// capability.
/// </summary>
internal sealed class NoticeSessionCapabilities : dotacp.protocol.ClientSessionCapabilities
{
    /// <summary>Gets the <c>notices</c> capability object, which carries no settings.</summary>
    [JsonProperty("notices")]
    public Dictionary<string, object> Notices { get; } = new(StringComparer.Ordinal);
}
