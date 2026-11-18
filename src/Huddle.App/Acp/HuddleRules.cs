using Agency.Huddle.App.Acp.Tools;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The Huddle rules shipped inside the application (Team Playbooks spec §4.1, §4.2): enforced text the Human cannot edit. They name tools
/// through placeholders, filled here with the names the session offers, so the text itself never holds the Adapter's prefix.
/// </summary>
internal static class HuddleRules
{
    /// <summary>What a placeholder reads in place of a tool the session does not offer.</summary>
    private const string ToolUnavailable = "(not available here; tell the Human)";

    private static readonly (string Placeholder, string Tool)[] MemberTools =
    [
        ("{{addCommentTool}}", "add_comment"),
        ("{{addEvidenceTool}}", "add_evidence"),
        ("{{logChangeTool}}", "log_change"),
        ("{{createTaskTool}}", "create_task"),
        ("{{updateTaskTool}}", "update_task"),
    ];

    private static readonly (string Placeholder, string Tool)[] ManagerTools =
    [
        ("{{triageRequestTool}}", "triage_request"),
        ("{{closeTaskTool}}", "close_task"),
        ("{{createTaskTool}}", "create_task"),
        ("{{memoryTool}}", "memory"),
    ];

    /// <summary>The Member rules as shipped, LF line endings, placeholders unfilled.</summary>
    internal static string RawMember { get; } = Read("Playbooks/HuddleRules/member.md");

    /// <summary>The Manager rules as shipped, LF line endings, placeholders unfilled.</summary>
    internal static string RawManager { get; } = Read("Playbooks/HuddleRules/manager.md");

    /// <summary>Fills the Member rules' tool placeholders.</summary>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    /// <returns>The rules with each placeholder replaced by the offered name, or by the unavailable text.</returns>
    internal static string RenderMember(IReadOnlyList<string> toolNames) => Fill(RawMember, MemberTools, toolNames);

    /// <summary>Fills the Manager rules' tool placeholders.</summary>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    /// <returns>The rules with each placeholder replaced by the offered name, or by the unavailable text.</returns>
    internal static string RenderManager(IReadOnlyList<string> toolNames) => Fill(RawManager, ManagerTools, toolNames);

    private static string Fill(string raw, (string Placeholder, string Tool)[] tools, IReadOnlyList<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);

        string text = raw;
        foreach ((string placeholder, string tool) in tools)
        {
            string offered = toolNames.FirstOrDefault(name => AppToolNames.Matches(name, tool)) ?? ToolUnavailable;
            text = text.Replace(placeholder, offered, StringComparison.Ordinal);
        }

        return text;
    }

    private static string Read(string name) => EmbeddedResources.ReadText(typeof(HuddleRules).Assembly, name).Trim();
}
