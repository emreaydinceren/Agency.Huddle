using Newtonsoft.Json;

namespace Agency.Huddle.Acp.DotAcp;

/// <summary>
/// The stable dotacp <c>ClientCapabilities</c> plus the <c>elicitation</c> member it has no property
/// for. Serialised by dotacp's own Newtonsoft settings, the wire object is exactly
/// <c>"elicitation":{"form":{}}</c>: form mode only. <c>url</c> is deliberately never advertised, because
/// the adapter would then start its MCP OAuth path, which this client cannot complete.
/// </summary>
internal sealed class ElicitationClientCapabilities : dotacp.protocol.ClientCapabilities
{
    /// <summary>Gets the <c>elicitation</c> capability object: a single empty <c>form</c> entry.</summary>
    [JsonProperty("elicitation")]
    public Dictionary<string, object> Elicitation { get; } = new(StringComparer.Ordinal)
    {
        ["form"] = new Dictionary<string, object>(StringComparer.Ordinal),
    };
}
