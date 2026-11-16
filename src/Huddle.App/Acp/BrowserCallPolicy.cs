using System.Collections.Frozen;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Decides which Playwright browser tool calls stop and ask the Human (issue #551, decision 4). A call that only looks at a page
/// runs freely; a call that acts on one, and any call this type cannot place, asks. It fails safe: an unknown tool name asks, so a
/// version bump that adds a tool never runs unasked.
/// </summary>
/// <remarks>
/// <para>
/// A read tool still asks when its arguments carry a <c>filename</c> (that writes a file into the Work Dir, which the Edit and Write
/// guards never see), when <c>browser_navigate</c> (or <c>browser_tabs</c> with a <c>url</c>) names anything but an <c>http</c> or
/// <c>https</c> address, and when its arguments are missing or are not a JSON object.
/// </para>
/// <para>
/// <c>browser_run_code_unsafe</c> is also denied outright by the session's deny rule (<c>DotAcpAgentHostFactory.DenyRules</c>); asking
/// here is the second line behind it. Calls of any other server return <see langword="false"/>: <see cref="ConnectorCallClassifier"/>
/// keeps its own rules for them.
/// </para>
/// </remarks>
internal static class BrowserCallPolicy
{
    /// <summary>The server name the Playwright MCP server is registered under.</summary>
    internal const string ServerName = BrowserServerConfig.ServerName;

    /// <summary>The prefix of every tool name of that server.</summary>
    internal const string ToolPrefix = "mcp__" + ServerName + "__";

    /// <summary>The tools that only look at a page and so run without asking, unless <see cref="AsksHuman"/> finds a reason to ask.</summary>
    private static readonly FrozenSet<string> ReadTools = new[]
    {
        "browser_snapshot", "browser_find", "browser_take_screenshot", "browser_console_messages", "browser_network_requests",
        "browser_network_request", "browser_navigate", "browser_navigate_back", "browser_tabs", "browser_wait_for", "browser_resize",
        "browser_close",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The tools that act on a page and always ask. Documentation and a pin: a tool not in <see cref="ReadTools"/> asks anyway.
    /// </summary>
    internal static IReadOnlyList<string> ActingTools { get; } =
    [
        "browser_click", "browser_type", "browser_fill_form", "browser_select_option", "browser_press_key", "browser_hover",
        "browser_drag", "browser_drop", "browser_file_upload", "browser_handle_dialog", "browser_evaluate", "browser_emulate_media",
        "browser_run_code_unsafe",
    ];

    /// <summary>The names of the tools that run without asking, for the test that pins them.</summary>
    internal static IReadOnlyCollection<string> ReadToolNames => ReadTools;

    /// <summary>Whether the call belongs to the Playwright server, by its server name or by its tool name.</summary>
    /// <param name="call">The tool call.</param>
    /// <returns><see langword="true"/> for a browser call.</returns>
    internal static bool Concerns(ToolCallInfo call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return string.Equals(call.McpServerName, ServerName, StringComparison.OrdinalIgnoreCase)
            || HasPrefix(call.ToolName)
            || (call.ToolName is null && HasPrefix(call.Title));
    }

    /// <summary>
    /// Gets the tool's own name without the server prefix: from the tool name, or from the title when the tool name is absent.
    /// </summary>
    /// <param name="call">The tool call.</param>
    /// <returns>The bare name, or <see langword="null"/> when the call does not carry one.</returns>
    internal static string? BareName(ToolCallInfo call)
    {
        ArgumentNullException.ThrowIfNull(call);
        string? full = call.ToolName ?? call.Title;
        return full is not null && HasPrefix(full) ? full[ToolPrefix.Length..] : null;
    }

    /// <summary>Whether the request must stop and ask the Human.</summary>
    /// <param name="call">The tool call.</param>
    /// <returns><see langword="false"/> for a call of another server or a browser call that only reads; <see langword="true"/> otherwise.</returns>
    internal static bool AsksHuman(ToolCallInfo call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (!Concerns(call))
        {
            return false;
        }

        string? name = BareName(call);
        if (name is null || !ReadTools.Contains(name))
        {
            return true;
        }

        return !ArgumentsAreSafe(name, call.RawInputJson);
    }

    private static bool HasPrefix(string? name) =>
        name is not null && name.StartsWith(ToolPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool ArgumentsAreSafe(string tool, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, "filename", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            bool hasUrl = root.TryGetProperty("url", out JsonElement url);
            return tool switch
            {
                "browser_navigate" => hasUrl && IsWebAddress(url),
                "browser_tabs" => !hasUrl || IsWebAddress(url),
                _ => true,
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsWebAddress(JsonElement url) =>
        url.ValueKind == JsonValueKind.String
        && Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
