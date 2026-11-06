using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Decides whether a permission request is for a claude.ai account connector tool, which must ask the Human (Agent Connectors
/// spec 8.4). It fails safe: a request it cannot place asks. The settled scope is the account connectors only; a Team's own
/// <c>.mcp.json</c> server and <c>mcp__team__</c> do not ask, because user-level servers do not load in Agents.
/// </summary>
internal static class ConnectorCallClassifier
{
    /// <summary>The marker every claude.ai connector server and tool name carries (<c>mcp__claude_ai_&lt;server&gt;__&lt;tool&gt;</c>).</summary>
    private const string Marker = "claude_ai_";

    /// <summary>
    /// The titles of kind <c>other</c> the Adapter writes for built-in tools. Source: <c>reporters/interaction.js</c> (<c>SkillReporter</c>
    /// "Load skill", <c>AskUserQuestionReporter</c> "Asking for your input", the agent control tools in <c>AGENT_CONTROL_TOOLS</c>:
    /// SendMessage, TaskStop, ListAgents, Monitor, whose generic title is the tool name) and <c>reporters/file-edit.js</c>
    /// (<c>NotebookEditReporter</c> "NotebookEdit"). Not on it, so they ask: "Unknown Tool" and every other generic tool name.
    /// </summary>
    private static readonly HashSet<string> BuiltInTitles = new(StringComparer.Ordinal)
    {
        "Load skill", "NotebookEdit", "Asking for your input", "SendMessage", "TaskStop", "ListAgents", "Monitor",
    };

    private static readonly HashSet<string> ResourceTools = new(StringComparer.Ordinal) { "ReadMcpResourceTool", "ListMcpResourcesTool" };

    /// <summary>The start of the Adapter's title for a Skill call, followed by the skill's name.</summary>
    private const string SkillTitlePrefix = "Load skill: ";

    /// <summary>Whether the request must stop and ask the Human.</summary>
    /// <param name="call">The tool call.</param>
    /// <returns><see langword="true"/> when the call is, or may be, a connector call.</returns>
    internal static bool AsksHuman(ToolCallInfo call)
    {
        ArgumentNullException.ThrowIfNull(call);

        // The Adapter's own word for where the server came from ("claudeai") is the strongest signal: it holds whatever the tool is called.
        if (Names(call.ToolName) || Names(call.McpServerName) || IsClaudeAiSource(call.McpServerSource))
        {
            return true;
        }

        // The MCP resource tools are built in, so their name carries no marker; the server they read is in their arguments.
        if (ResourceTools.Contains(call.ToolName ?? string.Empty))
        {
            return Names(call.RawInputJson);
        }

        if (call.ToolName is not null || call.McpServerName is not null)
        {
            // The Adapter named a tool or server and it is not a connector: Bash, mcp__team__, a Work Dir server.
            return false;
        }

        // No name at all. A kind the Adapter could not classify may be anything, so it asks unless the title is one the Adapter
        // itself writes for a built-in tool; a known kind (Bash is Execute) asks only when its title is an MCP tool name, never
        // because a command merely quotes the marker.
        if (call.Kind == ToolKind.Other)
        {
            return !IsKnownBuiltInTitle(call.Title);
        }

        return call.Title is { } title && title.StartsWith("mcp__", StringComparison.OrdinalIgnoreCase) && Names(title);
    }

    /// <summary>
    /// Whether <paramref name="title"/> is one the Adapter writes for a built-in tool of kind <c>other</c> that carries no <c>_meta</c>
    /// (claude-agent-acp 0.100.0, the version <c>tools/acp/package.json</c> pins; checked against <c>dist/tool-calls/reporters</c>. A single-question
    /// AskUserQuestion is titled with the question itself, which is not on the list and so asks). A generic tool's title is its own name, so a connector arrives as an
    /// <c>mcp__</c> title and never matches. The list is exact on purpose: a look-alike asks.
    /// </summary>
    private static bool IsKnownBuiltInTitle(string? title) =>
        title is not null
        && (BuiltInTitles.Contains(title) || title.StartsWith(SkillTitlePrefix, StringComparison.Ordinal));

    private static bool IsClaudeAiSource(string? source) =>
        source is not null && source.Replace("_", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal).Equals("claudeai", StringComparison.OrdinalIgnoreCase);

    private static bool Names(string? text) => text is not null && text.Contains(Marker, StringComparison.OrdinalIgnoreCase);
}
