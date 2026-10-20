using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// The <c>_meta</c> that makes a Helper's session toolless on an Adapter that isolates user settings (the Claude
/// adapter), found by the HL-0 spike (see <c>acp-session-config.md</c>): the user-settings isolation every Teammate
/// gets, plus no built-in tools, no claude.ai connectors and no MCP servers beyond the (empty) configured set.
/// </summary>
internal static class HelperSessionMeta
{
    /// <summary>Builds the <c>_meta</c> for <paramref name="profile"/>, or <see langword="null"/> when the Adapter does not isolate user settings.</summary>
    /// <param name="profile">The Adapter the Helper runs on.</param>
    public static IReadOnlyDictionary<string, object>? For(AdapterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Dictionary<string, object>? meta = DotAcpAgentHostFactory.BuildIsolationMeta(profile);
        if (meta is null)
        {
            return null;
        }

        Dictionary<string, object> claudeCode = (Dictionary<string, object>)meta["claudeCode"];
        Dictionary<string, object> sessionOptions = (Dictionary<string, object>)claudeCode["options"];
        sessionOptions["tools"] = Array.Empty<string>();
        sessionOptions["env"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["ENABLE_CLAUDEAI_MCP_SERVERS"] = "false" };
        sessionOptions["extraArgs"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["strict-mcp-config"] = null };
        return meta;
    }
}
