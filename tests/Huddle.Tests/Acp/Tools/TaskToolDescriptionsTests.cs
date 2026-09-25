namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins Spec §11.1/§11.9's guardrail for the six Task tools (Task 10.8.t, R6): each tool's
/// <see cref="IAppTool.Description"/> names only argument identifiers actually declared on its own
/// <see cref="IAppTool.InputSchema"/>. R6 found <c>list_tasks</c>' description once advertising a
/// nonexistent <c>tags</c> filter - a model reading it would call <c>list_tasks</c> with an argument
/// the tool would then have to silently ignore. The check is scoped to <see cref="CompoundArgumentTokens"/>,
/// argument-shaped tokens distinctive enough that their presence in prose can only mean the argument
/// (a snake_case or distinctive camelCase name, plus the literal historical regression word
/// <c>tags</c>), not every plain field name that also reads as ordinary English (e.g. "title",
/// "status", "team"), since those legitimately appear in a tool's prose without promising an
/// argument by that name.
/// </summary>
public sealed class TaskToolDescriptionsTests
{
    /// <summary>Argument-shaped tokens, drawn from the union of all six Task tools' <c>InputSchema</c> properties, specific enough that mentioning one in prose can only mean the argument.</summary>
    private static readonly IReadOnlyList<string> CompoundArgumentTokens =
    [
        "taskId",
        "originRoomId",
        "blocked_by",
        "duplicate_of",
        "start_date",
        "due_date",
        "include_change_log",
        "tags",
    ];

    /// <summary>Each Task tool's <see cref="IAppTool.Description"/> mentions no <see cref="CompoundArgumentTokens"/> entry besides those declared on its own <c>InputSchema</c>.</summary>
    [Fact]
    public void EachTaskTool_DescriptionNamesOnlyItsOwnInputSchemaProperties()
    {
        using TaskToolHarness harness = new();
        FakePromptSource prompts = new();
        IReadOnlyList<IAppTool> tools =
        [
            new CreateTaskTool(harness.Service, harness.Triggers, harness.Directory, prompts, "caller-id"),
            new GetTaskTool(harness.Store, prompts),
            new ListTasksTool(harness.Store, harness.Directory, harness.Personas, prompts, "caller-id"),
            new UpdateTaskTool(harness.Service, harness.Store, harness.Triggers, harness.Directory, prompts, "caller-id"),
            new CloseTaskTool(harness.Service, harness.Store, harness.Triggers, harness.Directory, prompts, "caller-id"),
            new ReopenTaskTool(harness.Service, harness.Store, harness.Triggers, harness.Directory, prompts, "caller-id"),
        ];

        foreach (IAppTool tool in tools)
        {
            JsonObject properties = Assert.IsType<JsonObject>(tool.InputSchema["properties"]);
            HashSet<string> ownTokens = new(properties.Select(pair => pair.Key), StringComparer.Ordinal);

            foreach (string token in CompoundArgumentTokens)
            {
                if (ownTokens.Contains(token))
                {
                    continue;
                }

                Assert.DoesNotContain(token, tool.Description, StringComparison.Ordinal);
            }
        }
    }
}
